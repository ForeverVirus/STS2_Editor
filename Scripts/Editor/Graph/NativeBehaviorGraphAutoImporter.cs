using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2_Editor.Scripts.Editor.Core.Models;

namespace STS2_Editor.Scripts.Editor.Graph;

public sealed class NativeBehaviorGraphAutoImportResult
{
    public bool IsSupported { get; set; }

    public bool IsPartial { get; set; }

    public BehaviorGraphDefinition? Graph { get; set; }

    public string Summary { get; set; } = string.Empty;

    public List<string> RecognizedCalls { get; } = new();

    public List<string> UnsupportedCalls { get; } = new();

    public List<string> Warnings { get; } = new();
}

internal sealed class NativeCallSite
{
    public required MethodBase Method { get; init; }

    public int? Int32ArgumentHint { get; init; }
}

internal sealed class PendingAttackBuilderState
{
    public Dictionary<string, string> DamageParameters { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, string>? RepeatParameters { get; set; }

    public bool HasRepeatBuilder { get; set; }

    public string? MultiplierKeyHint { get; set; }

    public int? LiteralHitCountHint { get; set; }
}

public sealed class NativeBehaviorGraphAutoImporter
{
    private static readonly IReadOnlyDictionary<short, OpCode> SingleByteOpCodes;
    private static readonly IReadOnlyDictionary<short, OpCode> MultiByteOpCodes;

    private readonly NativeBehaviorGraphTranslator _translator = new();

    static NativeBehaviorGraphAutoImporter()
    {
        var single = new Dictionary<short, OpCode>();
        var multi = new Dictionary<short, OpCode>();
        foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.GetValue(null) is not OpCode opcode)
            {
                continue;
            }

            var value = unchecked((short)opcode.Value);
            if ((value & 0xff00) == 0xfe00)
            {
                multi[(short)(value & 0x00ff)] = opcode;
            }
            else
            {
                single[value] = opcode;
            }
        }

        SingleByteOpCodes = single;
        MultiByteOpCodes = multi;
    }

    public bool TryCreateGraph(ModStudioEntityKind kind, string entityId, out NativeBehaviorGraphAutoImportResult result)
    {
        result = new NativeBehaviorGraphAutoImportResult();
        if (string.IsNullOrWhiteSpace(entityId))
        {
            result.Summary = "Native auto-import requires a valid entity id.";
            return false;
        }

        return kind switch
        {
            ModStudioEntityKind.Card => TryCreateCardGraph(entityId, result),
            ModStudioEntityKind.Potion => TryCreatePotionGraph(entityId, result),
            ModStudioEntityKind.Relic => TryCreateRelicGraph(entityId, result),
            _ => SetUnsupported(result, $"Native auto-import is not implemented for {kind} in Phase 1.")
        };
    }

    private bool TryCreateCardGraph(string entityId, NativeBehaviorGraphAutoImportResult result)
    {
        var card = ResolveCardModel(entityId);
        if (card == null)
        {
            return SetUnsupported(result, $"Could not resolve runtime card '{entityId}'.");
        }

        if (TryCreateCardSpecialCaseGraph(card, result))
        {
            return true;
        }

        var method = card.GetType().GetMethod(
            "OnPlay",
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(PlayerChoiceContext), typeof(CardPlay)],
            modifiers: null);
        if (method == null)
        {
            return SetUnsupported(result, $"Card '{entityId}' does not expose an overrideable OnPlay method.");
        }

        var steps = ExtractStepsFromMethod(
            card,
            method,
            "card.on_play",
            ResolveDefaultTargetSelector(card.TargetType),
            ResolveDefaultSelfSelector(card.TargetType),
            result);
        return FinalizeTranslation(ModStudioEntityKind.Card, entityId, ResolveModelTitle(card), steps, "card.on_play", result);
    }

    private bool TryCreateCardSpecificGraph(CardModel card, NativeBehaviorGraphAutoImportResult result)
    {
        if (string.Equals(card.Id.Entry, "OMNISLICE", StringComparison.OrdinalIgnoreCase))
        {
            var firstHit = new NativeBehaviorStep
            {
                Kind = "combat.damage",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["target"] = "current_target",
                    ["props"] = ResolveDamageProps(card)
                }
            };
            PopulateDynamicAmountParameters(firstHit.Parameters, card, "Damage", "0");

            var mirroredHit = new NativeBehaviorStep
            {
                Kind = "combat.damage",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["target"] = "other_enemies",
                    ["props"] = "Unpowered, Move",
                    ["amount"] = "$state.last_damage_total_plus_overkill",
                    ["amount_source_kind"] = DynamicValueSourceKind.Literal.ToString(),
                    ["amount_template"] = "上一段实际伤害"
                }
            };

            var translation = _translator.Translate(new NativeBehaviorGraphSource
            {
                EntityKind = ModStudioEntityKind.Card,
                GraphId = $"native_auto_card_{card.Id.Entry.ToLowerInvariant()}",
                Name = string.IsNullOrWhiteSpace(card.Title) ? card.Id.Entry : $"{card.Title} Native Import",
                Description = string.Empty,
                TriggerId = "card.on_play",
                Steps = new List<NativeBehaviorStep> { firstHit, mirroredHit }
            });

            result.Graph = translation.Graph;
            result.IsSupported = translation.AppliedStepKinds.Count > 0;
            result.IsPartial = translation.IsPartial;
            result.RecognizedCalls.Add("CreatureCmd.Damage(current_target)");
            result.RecognizedCalls.Add("CreatureCmd.Damage(other_enemies, last_damage_total_plus_overkill)");
            result.Warnings.Add("Imported mirror-damage pattern as a stateful follow-up damage node targeting other enemies.");
            result.Summary = string.Join(
                Environment.NewLine,
                new[]
                {
                    "Native import: specialized",
                    "Trigger: card.on_play",
                    "Applied steps: combat.damage, combat.damage",
                    "Unsupported calls: -",
                    $"Warnings: {(result.Warnings.Count == 0 ? "-" : string.Join(" | ", result.Warnings.Distinct(StringComparer.OrdinalIgnoreCase)))}"
                });
            return result.IsSupported;
        }

        return false;
    }

    private bool TryCreateCardSpecialCaseGraph(CardModel card, NativeBehaviorGraphAutoImportResult result)
    {
        var normalizedCardEntry = (card.Id.Entry ?? string.Empty).Replace("_", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        if (normalizedCardEntry == "UPPERCUT")
        {
            var powerFallback = ResolveDynamicAmount(card, "Power", "1");

            var damage = new NativeBehaviorStep
            {
                Kind = "combat.damage",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["target"] = "current_target",
                    ["props"] = ResolveDamageProps(card)
                }
            };
            PopulateDynamicAmountParameters(damage.Parameters, card, "Damage", "0");

            var weak = new NativeBehaviorStep
            {
                Kind = "combat.apply_power",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["power_id"] = "WEAK_POWER",
                    ["target"] = "current_target"
                }
            };
            PopulateDynamicAmountParameters(weak.Parameters, card, "Power", powerFallback);

            var vulnerable = new NativeBehaviorStep
            {
                Kind = "combat.apply_power",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["power_id"] = "VULNERABLE_POWER",
                    ["target"] = "current_target"
                }
            };
            PopulateDynamicAmountParameters(vulnerable.Parameters, card, "Power", powerFallback);

            var title = ResolveModelTitle(card);
            var graphEntry = string.IsNullOrWhiteSpace(card.Id?.Entry) ? card.GetType().Name : card.Id.Entry;
            var translation = _translator.Translate(new NativeBehaviorGraphSource
            {
                EntityKind = ModStudioEntityKind.Card,
                GraphId = $"native_auto_card_{graphEntry.ToLowerInvariant()}",
                Name = string.IsNullOrWhiteSpace(title) ? graphEntry : $"{title} Native Import",
                Description = string.Empty,
                TriggerId = "card.on_play",
                Steps = new List<NativeBehaviorStep> { damage, weak, vulnerable }
            });

            result.Graph = translation.Graph;
            result.IsSupported = translation.AppliedStepKinds.Count > 0;
            result.IsPartial = translation.IsPartial;
            result.RecognizedCalls.Add("DamageCmd.Attack(current_target)");
            result.RecognizedCalls.Add("PowerCmd.Apply(WeakPower)");
            result.RecognizedCalls.Add("PowerCmd.Apply(VulnerablePower)");
            result.Warnings.Add("Imported multi-debuff attack as damage + weak + vulnerable using shared Power dynamic value.");
            result.Summary = string.Join(
                Environment.NewLine,
                new[]
                {
                    "Native import: specialized",
                    "Trigger: card.on_play",
                    "Applied steps: combat.damage, combat.apply_power, combat.apply_power",
                    "Unsupported calls: -",
                    $"Warnings: {(result.Warnings.Count == 0 ? "-" : string.Join(" | ", result.Warnings.Distinct(StringComparer.OrdinalIgnoreCase)))}"
                });
            return result.IsSupported;
        }

        if (normalizedCardEntry == "ALLFORONE")
        {
            var damage = new NativeBehaviorStep
            {
                Kind = "combat.damage",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["target"] = "current_target",
                    ["props"] = ResolveDamageProps(card)
                }
            };
            PopulateDynamicAmountParameters(damage.Parameters, card, "Damage", "0");

            var moveCards = new NativeBehaviorStep
            {
                Kind = "cardpile.move_cards",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["source_pile"] = PileType.Discard.ToString(),
                    ["target_pile"] = PileType.Hand.ToString(),
                    ["count"] = "0",
                    ["exact_energy_cost"] = "0",
                    ["include_x_cost"] = bool.FalseString,
                    ["card_type_scope"] = "attack_skill_power"
                }
            };

            var title = ResolveModelTitle(card);
            var graphEntry = string.IsNullOrWhiteSpace(card.Id?.Entry) ? card.GetType().Name : card.Id.Entry;
            var translation = _translator.Translate(new NativeBehaviorGraphSource
            {
                EntityKind = ModStudioEntityKind.Card,
                GraphId = $"native_auto_card_{graphEntry.ToLowerInvariant()}",
                Name = string.IsNullOrWhiteSpace(title) ? graphEntry : $"{title} Native Import",
                Description = string.Empty,
                TriggerId = "card.on_play",
                Steps = new List<NativeBehaviorStep> { damage, moveCards }
            });

            result.Graph = translation.Graph;
            result.IsSupported = translation.AppliedStepKinds.Count > 0;
            result.IsPartial = translation.IsPartial;
            result.RecognizedCalls.Add("DamageCmd.Attack(current_target)");
            result.RecognizedCalls.Add("CardPileCmd.Add(filtered discard cards -> hand)");
            result.Warnings.Add("Imported discard-to-hand recovery as a filtered move-cards node (cost 0, non-X, attack/skill/power).");
            result.Summary = string.Join(
                Environment.NewLine,
                new[]
                {
                    "Native import: specialized",
                    "Trigger: card.on_play",
                    "Applied steps: combat.damage, cardpile.move_cards",
                    "Unsupported calls: -",
                    $"Warnings: {(result.Warnings.Count == 0 ? "-" : string.Join(" | ", result.Warnings.Distinct(StringComparer.OrdinalIgnoreCase)))}"
                });
            return result.IsSupported;
        }

        if (normalizedCardEntry == "SEVENSTARS")
        {
            var repeat = new NativeBehaviorStep
            {
                Kind = "combat.repeat",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["count"] = ResolveDynamicAmount(card, "Repeat", "1")
                }
            };
            PopulateDynamicValueParameters(repeat.Parameters, "count", card, "Repeat", ResolveDynamicAmount(card, "Repeat", "1"));

            var damage = new NativeBehaviorStep
            {
                Kind = "combat.damage",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["target"] = "all_enemies",
                    ["props"] = ResolveDamageProps(card)
                }
            };
            PopulateDynamicAmountParameters(damage.Parameters, card, "Damage", "0");

            var title = ResolveModelTitle(card);
            var graphEntry = string.IsNullOrWhiteSpace(card.Id?.Entry) ? card.GetType().Name : card.Id.Entry;
            var translation = _translator.Translate(new NativeBehaviorGraphSource
            {
                EntityKind = ModStudioEntityKind.Card,
                GraphId = $"native_auto_card_{graphEntry.ToLowerInvariant()}",
                Name = string.IsNullOrWhiteSpace(title) ? graphEntry : $"{title} Native Import",
                Description = string.Empty,
                TriggerId = "card.on_play",
                Steps = new List<NativeBehaviorStep> { repeat, damage }
            });

            result.Graph = translation.Graph;
            result.IsSupported = translation.AppliedStepKinds.Count > 0;
            result.IsPartial = translation.IsPartial;
            result.RecognizedCalls.Add("DamageCmd.Attack.WithHitCount.Repeat");
            result.RecognizedCalls.Add("AttackCommand.TargetingAllOpponents");
            result.Warnings.Add("Imported multi-hit AOE attack as Repeat + Damage(all_enemies).");
            result.Summary = string.Join(
                Environment.NewLine,
                new[]
                {
                    "Native import: specialized",
                    "Trigger: card.on_play",
                    "Applied steps: combat.repeat, combat.damage",
                    "Unsupported calls: -",
                    $"Warnings: {(result.Warnings.Count == 0 ? "-" : string.Join(" | ", result.Warnings.Distinct(StringComparer.OrdinalIgnoreCase)))}"
                });
            return result.IsSupported;
        }

        if (normalizedCardEntry != "OMNISLICE")
        {
            return TryCreateCardSpecificGraph(card, result);
        }

        var firstHit = new NativeBehaviorStep
        {
            Kind = "combat.damage",
            Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["target"] = "current_target",
                ["props"] = ResolveDamageProps(card)
            }
        };
        PopulateDynamicAmountParameters(firstHit.Parameters, card, "Damage", "0");

        var mirroredHit = new NativeBehaviorStep
        {
            Kind = "combat.damage",
            Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["target"] = "other_enemies",
                ["props"] = "Unpowered, Move",
                ["amount"] = "$state.last_damage_total_plus_overkill",
                ["amount_source_kind"] = DynamicValueSourceKind.Literal.ToString(),
                ["amount_template"] = "previous_dealt_damage"
            }
        };

        var omnisliceTitle = ResolveModelTitle(card);
        var omnisliceEntry = string.IsNullOrWhiteSpace(card.Id?.Entry) ? card.GetType().Name : card.Id.Entry;
        var omnisliceTranslation = _translator.Translate(new NativeBehaviorGraphSource
        {
            EntityKind = ModStudioEntityKind.Card,
            GraphId = $"native_auto_card_{omnisliceEntry.ToLowerInvariant()}",
            Name = string.IsNullOrWhiteSpace(omnisliceTitle) ? omnisliceEntry : $"{omnisliceTitle} Native Import",
            Description = string.Empty,
            TriggerId = "card.on_play",
            Steps = new List<NativeBehaviorStep> { firstHit, mirroredHit }
        });

        result.Graph = omnisliceTranslation.Graph;
        result.IsSupported = omnisliceTranslation.AppliedStepKinds.Count > 0;
        result.IsPartial = omnisliceTranslation.IsPartial;
        result.RecognizedCalls.Add("CreatureCmd.Damage(current_target)");
        result.RecognizedCalls.Add("CreatureCmd.Damage(other_enemies, last_damage_total_plus_overkill)");
        result.Warnings.Add("Imported mirror-damage pattern as a stateful follow-up damage node targeting other enemies.");
        result.Summary = string.Join(
            Environment.NewLine,
            new[]
            {
                "Native import: specialized",
                "Trigger: card.on_play",
                "Applied steps: combat.damage, combat.damage",
                "Unsupported calls: -",
                $"Warnings: {(result.Warnings.Count == 0 ? "-" : string.Join(" | ", result.Warnings.Distinct(StringComparer.OrdinalIgnoreCase)))}"
            });
        return result.IsSupported;
    }

    private bool TryCreatePotionGraph(string entityId, NativeBehaviorGraphAutoImportResult result)
    {
        var potion = ResolvePotionModel(entityId);
        if (potion == null)
        {
            return SetUnsupported(result, $"Could not resolve runtime potion '{entityId}'.");
        }

        var method = potion.GetType().GetMethod(
            "OnUse",
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(PlayerChoiceContext), typeof(Creature)],
            modifiers: null);
        if (method == null)
        {
            return SetUnsupported(result, $"Potion '{entityId}' does not expose an overrideable OnUse method.");
        }

        var targetSelector = potion.TargetType == TargetType.Self ? "self" : "current_target";
        var steps = ExtractStepsFromMethod(
            potion,
            method,
            "potion.on_use",
            targetSelector,
            "self",
            result);
        return FinalizeTranslation(ModStudioEntityKind.Potion, entityId, ResolveModelTitle(potion), steps, "potion.on_use", result);
    }

    private bool TryCreateRelicGraph(string entityId, NativeBehaviorGraphAutoImportResult result)
    {
        var relic = ResolveRelicModel(entityId);
        if (relic == null)
        {
            return SetUnsupported(result, $"Could not resolve runtime relic '{entityId}'.");
        }

        var hookCandidates = new (string MethodName, Type[] Args, string TriggerId)[]
        {
            ("BeforeCardPlayed", [typeof(CardPlay)], "relic.before_card_played"),
            ("AfterCardPlayed", [typeof(PlayerChoiceContext), typeof(CardPlay)], "relic.after_card_played"),
            ("AfterCardPlayedLate", [typeof(PlayerChoiceContext), typeof(CardPlay)], "relic.after_card_played_late"),
            ("BeforePotionUsed", [typeof(PotionModel), typeof(Creature)], "relic.before_potion_used"),
            ("AfterPotionUsed", [typeof(PotionModel), typeof(Creature)], "relic.after_potion_used")
        };

        foreach (var candidate in hookCandidates)
        {
            var method = relic.GetType().GetMethod(candidate.MethodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, candidate.Args, null);
            if (method == null || method.DeclaringType == typeof(AbstractModel))
            {
                continue;
            }

            var steps = ExtractStepsFromMethod(relic, method, candidate.TriggerId, "current_target", "self", result);
            if (steps.Count == 0)
            {
                continue;
            }

            return FinalizeTranslation(ModStudioEntityKind.Relic, entityId, ResolveModelTitle(relic), steps, candidate.TriggerId, result);
        }

        return SetUnsupported(result, $"Relic '{entityId}' does not currently map to a supported graph hook trigger.");
    }

    private static string ResolveModelTitle(AbstractModel model)
    {
        try
        {
            return model switch
            {
                CardModel card => string.IsNullOrWhiteSpace(card.Title) ? card.Id.Entry : card.Title,
                PotionModel potion => string.IsNullOrWhiteSpace(potion.Title.GetRawText()) ? potion.Id.Entry : potion.Title.GetRawText(),
                RelicModel relic => string.IsNullOrWhiteSpace(relic.Title.GetRawText()) ? relic.Id.Entry : relic.Title.GetRawText(),
                _ => model.Id.Entry
            };
        }
        catch
        {
            return model.Id.Entry;
        }
    }

    private static CardModel? ResolveCardModel(string entityId)
    {
        try
        {
            return ModelDb.AllCards.FirstOrDefault(candidate => string.Equals(candidate.Id.Entry, entityId, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return CreateModelByTypeName<CardModel>(entityId);
        }
    }

    private static PotionModel? ResolvePotionModel(string entityId)
    {
        try
        {
            return ModelDb.AllPotions.FirstOrDefault(candidate => string.Equals(candidate.Id.Entry, entityId, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return CreateModelByTypeName<PotionModel>(entityId);
        }
    }

    private static RelicModel? ResolveRelicModel(string entityId)
    {
        try
        {
            return ModelDb.AllRelics.FirstOrDefault(candidate => string.Equals(candidate.Id.Entry, entityId, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return CreateModelByTypeName<RelicModel>(entityId);
        }
    }

    private static TModel? CreateModelByTypeName<TModel>(string entityId) where TModel : AbstractModel
    {
        var normalizedId = NormalizeTypeLookup(entityId);
        var targetType = typeof(TModel).Assembly
            .GetTypes()
            .FirstOrDefault(type =>
                typeof(TModel).IsAssignableFrom(type) &&
                !type.IsAbstract &&
                type.GetConstructor(Type.EmptyTypes) != null &&
                string.Equals(NormalizeTypeLookup(type.Name), normalizedId, StringComparison.OrdinalIgnoreCase));

        return targetType == null ? null : Activator.CreateInstance(targetType) as TModel;
    }

    private static string NormalizeTypeLookup(string rawValue)
    {
        return new string(rawValue.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
    }

    private bool FinalizeTranslation(
        ModStudioEntityKind kind,
        string entityId,
        string title,
        IReadOnlyList<NativeBehaviorStep> steps,
        string triggerId,
        NativeBehaviorGraphAutoImportResult result)
    {
        if (steps.Count == 0)
        {
            return SetUnsupported(result, string.IsNullOrWhiteSpace(result.Summary)
                ? $"No supported native steps were discovered for '{entityId}'."
                : result.Summary);
        }

        var source = new NativeBehaviorGraphSource
        {
            EntityKind = kind,
            GraphId = $"native_auto_{kind.ToString().ToLowerInvariant()}_{entityId.ToLowerInvariant()}",
            Name = string.IsNullOrWhiteSpace(title) ? entityId : $"{title} Native Import",
            Description = string.Empty,
            TriggerId = triggerId,
            Steps = steps.ToList()
        };

        var translation = _translator.Translate(source);
        result.Graph = translation.Graph;
        result.IsSupported = translation.AppliedStepKinds.Count > 0;
        result.IsPartial = translation.IsPartial || result.UnsupportedCalls.Count > 0;
        result.Warnings.AddRange(translation.Warnings);

        result.Summary = string.Join(
            Environment.NewLine,
            new[]
            {
                $"Native import: {(result.IsPartial ? "partial" : "supported")}",
                $"Trigger: {triggerId}",
                $"Applied steps: {(translation.AppliedStepKinds.Count == 0 ? "-" : string.Join(", ", translation.AppliedStepKinds))}",
                $"Unsupported calls: {(result.UnsupportedCalls.Count == 0 ? "-" : string.Join(", ", result.UnsupportedCalls.Distinct(StringComparer.OrdinalIgnoreCase)))}",
                $"Warnings: {(result.Warnings.Count == 0 ? "-" : string.Join(" | ", result.Warnings.Distinct(StringComparer.OrdinalIgnoreCase)))}"
            });
        return result.IsSupported;
    }

    private List<NativeBehaviorStep> ExtractStepsFromMethod(
        AbstractModel model,
        MethodInfo method,
        string triggerId,
        string defaultTargetSelector,
        string defaultSelfSelector,
        NativeBehaviorGraphAutoImportResult result)
    {
        var steps = new List<NativeBehaviorStep>();
        string? pendingCreatedCardId = null;
        PendingAttackBuilderState? pendingAttack = null;
        foreach (var callSite in EnumerateCallSites(method))
        {
            var calledMethod = callSite.Method;
            if (TryResolveCreatedCardIdFromCall(calledMethod, out var createdCardId))
            {
                pendingCreatedCardId = createdCardId;
                continue;
            }

            if (TryHandleAttackBuilderCall(model, calledMethod, callSite.Int32ArgumentHint, defaultTargetSelector, result, ref pendingAttack))
            {
                continue;
            }

            if (pendingAttack != null && !IsAttackBuilderMethod(calledMethod) && IsCoreGameplayCall(calledMethod))
            {
                FlushPendingAttackBuilder(steps, pendingAttack, result);
                pendingAttack = null;
            }

            if (TryTranslateRecognizedCall(model, calledMethod, defaultTargetSelector, defaultSelfSelector, pendingCreatedCardId, result, out var step, out var callLabel))
            {
                if (string.Equals(step.Kind, "combat.create_card", StringComparison.OrdinalIgnoreCase) &&
                    (!step.Parameters.TryGetValue("card_id", out var createCardId) || string.IsNullOrWhiteSpace(createCardId)))
                {
                    result.Warnings.Add($"The native importer could not resolve a concrete card id for '{callLabel}'. The generated combat.create_card node will need manual completion.");
                }

                if (steps.Count > 0 && AreEquivalentSteps(steps[^1], step))
                {
                    result.Warnings.Add($"Collapsed duplicate native call '{callLabel}'.");
                    continue;
                }

                steps.Add(step);
                result.RecognizedCalls.Add(callLabel);
                continue;
            }

            if (ShouldIgnoreCall(calledMethod))
            {
                continue;
            }

            if (IsCoreGameplayCall(calledMethod))
            {
                result.UnsupportedCalls.Add($"{calledMethod.DeclaringType?.Name}.{calledMethod.Name}");
            }
        }

        if (pendingAttack != null)
        {
            FlushPendingAttackBuilder(steps, pendingAttack, result);
        }

        if (steps.Count == 0 && result.UnsupportedCalls.Count > 0)
        {
            result.Summary = $"Trigger '{triggerId}' only contained unsupported native gameplay calls.";
        }

        return steps;
    }

    private static bool TryHandleAttackBuilderCall(
        AbstractModel model,
        MethodBase method,
        int? int32ArgumentHint,
        string defaultTargetSelector,
        NativeBehaviorGraphAutoImportResult result,
        ref PendingAttackBuilderState? pendingAttack)
    {
        var typeName = method.DeclaringType?.Name ?? string.Empty;
        var methodName = method.Name;
        if (!IsAttackBuilderMethod(method))
        {
            if (methodName is "ResolveEnergyXValue" or "ResolveStarXValue" or "ResolveHandXValue")
            {
                if (pendingAttack == null)
                {
                    return false;
                }

                pendingAttack.MultiplierKeyHint = methodName switch
                {
                    "ResolveEnergyXValue" => "energy",
                    "ResolveStarXValue" => "stars",
                    "ResolveHandXValue" => "hand_count",
                    _ => pendingAttack.MultiplierKeyHint
                };
                return true;
            }

            return false;
        }

        if (typeName is "DamageCmd" && string.Equals(methodName, "Attack", StringComparison.Ordinal))
        {
            pendingAttack = new PendingAttackBuilderState();
            pendingAttack.DamageParameters["target"] = defaultTargetSelector;
            pendingAttack.DamageParameters["props"] = ResolveDamageProps(model);
            PopulateDynamicAmountParameters(pendingAttack.DamageParameters, model, ResolveDamageVarName(model), "0");
            return true;
        }

        if (pendingAttack == null)
        {
            return true;
        }

        switch (methodName)
        {
            case "TargetingAllOpponents":
                pendingAttack.DamageParameters["target"] = "all_enemies";
                return true;
            case "Targeting":
                pendingAttack.DamageParameters["target"] = defaultTargetSelector;
                return true;
            case "WithHitCount":
                pendingAttack.HasRepeatBuilder = true;
                if (int32ArgumentHint.HasValue && int32ArgumentHint.Value > 0)
                {
                    pendingAttack.LiteralHitCountHint = int32ArgumentHint.Value;
                }

                pendingAttack.RepeatParameters = BuildAttackRepeatParameters(model, pendingAttack, result);
                return true;
            case "Execute":
                return true;
            default:
                return true;
        }
    }

    private static Dictionary<string, string> BuildAttackRepeatParameters(
        AbstractModel model,
        PendingAttackBuilderState pendingAttack,
        NativeBehaviorGraphAutoImportResult result)
    {
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["count"] = "1"
        };

        if (pendingAttack.LiteralHitCountHint.HasValue && pendingAttack.LiteralHitCountHint.Value > 0)
        {
            parameters["count"] = pendingAttack.LiteralHitCountHint.Value.ToString(CultureInfo.InvariantCulture);
            return parameters;
        }

        if (TryResolveAttackRepeatVarName(model, out var varName))
        {
            PopulateDynamicValueParameters(parameters, "count", model, varName, ResolveDynamicAmount(model, varName, "1"));
            return parameters;
        }

        if (!string.IsNullOrWhiteSpace(pendingAttack.MultiplierKeyHint))
        {
            PopulateFormulaMultiplierParameters(parameters, "count", pendingAttack.MultiplierKeyHint, "0", "1");
            result.Warnings.Add($"Approximated hit-count builder for '{model.Id.Entry}' as context-driven repeat count using '{pendingAttack.MultiplierKeyHint}'.");
            return parameters;
        }

        result.Warnings.Add($"Could not resolve WithHitCount source for '{model.Id.Entry}'. Imported repeat count as 1 and left it for manual adjustment.");
        return parameters;
    }

    private static void FlushPendingAttackBuilder(
        List<NativeBehaviorStep>? steps,
        PendingAttackBuilderState pendingAttack,
        NativeBehaviorGraphAutoImportResult result)
    {
        if (steps == null)
        {
            return;
        }

        if (pendingAttack.HasRepeatBuilder)
        {
            steps.Add(new NativeBehaviorStep
            {
                Kind = "combat.repeat",
                Parameters = pendingAttack.RepeatParameters ?? new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["count"] = "1"
                }
            });
            result.RecognizedCalls.Add("AttackCommand.WithHitCount");
        }

        steps.Add(new NativeBehaviorStep
        {
            Kind = "combat.damage",
            Parameters = new Dictionary<string, string>(pendingAttack.DamageParameters, StringComparer.Ordinal)
        });
        result.RecognizedCalls.Add("DamageCmd.Attack");
    }

    private static bool IsAttackBuilderMethod(MethodBase method)
    {
        var typeName = method.DeclaringType?.Name ?? string.Empty;
        var methodName = method.Name;
        if (typeName is "DamageCmd" && string.Equals(methodName, "Attack", StringComparison.Ordinal))
        {
            return true;
        }

        return typeName is "AttackCommand" &&
               methodName is "FromCard" or "FromOsty" or "Targeting" or "TargetingAllOpponents" or "WithHitFx" or "WithHitCount" or "SpawningHitVfxOnEachCreature" or "WithNoAttackerAnim" or "WithAttackerAnim" or "Execute";
    }

    private static bool TryResolveAttackRepeatVarName(AbstractModel model, out string varName)
    {
        foreach (var candidate in new[] { "Repeat", "CalculatedHits", "Hits", "Cards", "Amount" })
        {
            if (TryGetDynamicVar(model, candidate, out _))
            {
                varName = candidate;
                return true;
            }
        }

        varName = string.Empty;
        return false;
    }

    private static bool AreEquivalentSteps(NativeBehaviorStep left, NativeBehaviorStep right)
    {
        if (!string.Equals(left.Kind, right.Kind, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (left.Parameters.Count != right.Parameters.Count)
        {
            return false;
        }

        foreach (var pair in left.Parameters)
        {
            if (!right.Parameters.TryGetValue(pair.Key, out var value) ||
                !string.Equals(pair.Value, value, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private bool TryTranslateRecognizedCall(
        AbstractModel model,
        MethodBase method,
        string defaultTargetSelector,
        string defaultSelfSelector,
        string? pendingCreatedCardId,
        NativeBehaviorGraphAutoImportResult result,
        out NativeBehaviorStep step,
        out string callLabel)
    {
        step = new NativeBehaviorStep();
        callLabel = $"{method.DeclaringType?.Name}.{method.Name}";

        if (method.DeclaringType?.Name == "DamageCmd" && string.Equals(method.Name, "Attack", StringComparison.Ordinal))
        {
            var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["target"] = defaultTargetSelector,
                ["props"] = ResolveDamageProps(model)
            };
            PopulateDynamicAmountParameters(parameters, model, "Damage", "0");
            step = new NativeBehaviorStep
            {
                Kind = "combat.damage",
                Parameters = parameters
            };
            return true;
        }

        if (method.DeclaringType?.Name == "CreatureCmd" && string.Equals(method.Name, "Damage", StringComparison.Ordinal))
        {
            var damageVarName = ResolveDamageVarName(model);
            var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["target"] = defaultTargetSelector,
                ["props"] = ResolveDamageProps(model)
            };
            PopulateDynamicAmountParameters(parameters, model, damageVarName, damageVarName.Equals("HpLoss", StringComparison.OrdinalIgnoreCase) ? "1" : "0");
            step = new NativeBehaviorStep
            {
                Kind = "combat.damage",
                Parameters = parameters
            };
            if (model is RelicModel or PotionModel && string.Equals(defaultTargetSelector, "current_target", StringComparison.OrdinalIgnoreCase))
            {
                result.Warnings.Add($"Mapped CreatureCmd.Damage for '{model.Id.Entry}' to combat.damage with target '{defaultTargetSelector}'. Review target if the original effect was AoE or self-damage.");
            }

            return true;
        }

        if (method.DeclaringType?.Name == "CreatureCmd" && string.Equals(method.Name, "GainBlock", StringComparison.Ordinal))
        {
            var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["target"] = defaultSelfSelector,
                ["props"] = "none"
            };
            PopulateDynamicAmountParameters(parameters, model, "Block", "0");
            step = new NativeBehaviorStep
            {
                Kind = "combat.gain_block",
                Parameters = parameters
            };
            return true;
        }

        if (method.DeclaringType?.Name == "CreatureCmd" && string.Equals(method.Name, "Heal", StringComparison.Ordinal))
        {
            var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["target"] = defaultSelfSelector
            };
            PopulateDynamicAmountParameters(parameters, model, "Heal", "0");
            step = new NativeBehaviorStep
            {
                Kind = "combat.heal",
                Parameters = parameters
            };
            return true;
        }

        if (method.DeclaringType?.Name == "CardPileCmd" && string.Equals(method.Name, "Draw", StringComparison.Ordinal))
        {
            var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
            PopulateDynamicAmountParameters(parameters, model, "Cards", "1");
            step = new NativeBehaviorStep
            {
                Kind = "combat.draw_cards",
                Parameters = parameters
            };
            return true;
        }

        if (method.DeclaringType?.Name == "PlayerCmd" && string.Equals(method.Name, "GainEnergy", StringComparison.Ordinal))
        {
            var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
            PopulateDynamicAmountParameters(parameters, model, "Energy", "1");
            step = new NativeBehaviorStep
            {
                Kind = "player.gain_energy",
                Parameters = parameters
            };
            return true;
        }

        if (method.DeclaringType?.Name == "PlayerCmd" && string.Equals(method.Name, "GainGold", StringComparison.Ordinal))
        {
            var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
            PopulateDynamicAmountParameters(parameters, model, "Gold", "1");
            step = new NativeBehaviorStep
            {
                Kind = "player.gain_gold",
                Parameters = parameters
            };
            return true;
        }

        if (method.DeclaringType?.Name == "PlayerCmd" && string.Equals(method.Name, "GainStars", StringComparison.Ordinal))
        {
            var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
            PopulateDynamicAmountParameters(parameters, model, "Stars", "1");
            step = new NativeBehaviorStep
            {
                Kind = "player.gain_stars",
                Parameters = parameters
            };
            return true;
        }

        if (method.DeclaringType?.Name == "PowerCmd" && string.Equals(method.Name, "Apply", StringComparison.Ordinal) &&
            TryResolvePowerApplication(method, model, out var powerId, out var amountVarName, out var amount))
        {
            var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["power_id"] = powerId,
                ["target"] = defaultTargetSelector
            };
            PopulateDynamicAmountParameters(parameters, model, amountVarName, amount);
            step = new NativeBehaviorStep
            {
                Kind = "combat.apply_power",
                Parameters = parameters
            };
            return true;
        }

        if (method.DeclaringType?.Name == "PowerCmd" && string.Equals(method.Name, "Remove", StringComparison.Ordinal) &&
            TryResolvePowerId(method, model, out powerId, out _))
        {
            step = new NativeBehaviorStep
            {
                Kind = "power.remove",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["power_id"] = powerId,
                    ["target"] = defaultTargetSelector
                }
            };
            return true;
        }

        if (method.DeclaringType?.Name == "PowerCmd" && string.Equals(method.Name, "ModifyAmount", StringComparison.Ordinal) &&
            TryResolvePowerApplication(method, model, out powerId, out amountVarName, out amount))
        {
            step = new NativeBehaviorStep
            {
                Kind = "power.modify_amount",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["power_id"] = powerId,
                    ["target"] = defaultTargetSelector,
                    ["amount"] = ResolveDynamicAmount(model, amountVarName, amount),
                    ["silent"] = bool.FalseString
                }
            };
            return true;
        }

        if (method.DeclaringType?.Name == "CardSelectCmd")
        {
            var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["state_key"] = "selected_cards",
                ["selection_mode"] = ResolveSelectionMode(method.Name),
                ["source_pile"] = ResolveSelectionSourcePile(method.Name),
                ["count"] = ResolveSelectionCount(model),
                ["prompt_kind"] = ResolvePromptKind(method.Name),
                ["allow_cancel"] = bool.FalseString
            };
            if (string.Equals(method.Name, "FromDeckForEnchantment", StringComparison.Ordinal) &&
                TryResolveEnchantmentId(model, out var enchantmentId))
            {
                parameters["enchantment_id"] = enchantmentId;
            }

            step = new NativeBehaviorStep
            {
                Kind = "card.select_cards",
                Parameters = parameters
            };
            return true;
        }

        if (method.DeclaringType?.Name == "CardCmd" && string.Equals(method.Name, "Discard", StringComparison.Ordinal))
        {
            step = new NativeBehaviorStep
            {
                Kind = "combat.discard_cards",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["card_state_key"] = "selected_cards",
                    ["target"] = PileType.Hand.ToString()
                }
            };
            return true;
        }

        if (method.DeclaringType?.Name == "CardCmd" && string.Equals(method.Name, "DiscardAndDraw", StringComparison.Ordinal))
        {
            step = new NativeBehaviorStep
            {
                Kind = "card.discard_and_draw",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["card_state_key"] = "selected_cards",
                    ["draw_count"] = ResolveDynamicAmount(model, "Cards", "1")
                }
            };
            return true;
        }

        if (method.DeclaringType?.Name == "CardCmd" && string.Equals(method.Name, "Exhaust", StringComparison.Ordinal))
        {
            step = new NativeBehaviorStep
            {
                Kind = "combat.exhaust_cards",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["card_state_key"] = "selected_cards",
                    ["target"] = PileType.Hand.ToString()
                }
            };
            return true;
        }

        if (method.DeclaringType?.Name == "CardCmd" && string.Equals(method.Name, "Transform", StringComparison.Ordinal))
        {
            step = new NativeBehaviorStep
            {
                Kind = "combat.transform_card",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["card_state_key"] = "selected_cards"
                }
            };
            return true;
        }

        if (method.DeclaringType?.Name == "CardCmd" && string.Equals(method.Name, "TransformTo", StringComparison.Ordinal))
        {
            var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["card_state_key"] = "selected_cards"
            };
            if (TryResolveCardIdFromMethod(method, out var replacementCardId))
            {
                parameters["replacement_card_id"] = replacementCardId;
            }

            step = new NativeBehaviorStep
            {
                Kind = "combat.transform_card",
                Parameters = parameters
            };
            return true;
        }

        if (method.DeclaringType?.Name == "CardCmd" && string.Equals(method.Name, "TransformToRandom", StringComparison.Ordinal))
        {
            step = new NativeBehaviorStep
            {
                Kind = "combat.transform_card",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["card_state_key"] = "selected_cards",
                    ["random_replacement"] = bool.TrueString
                }
            };
            return true;
        }

        if (method.DeclaringType?.Name == "CardCmd" && string.Equals(method.Name, "Upgrade", StringComparison.Ordinal))
        {
            step = new NativeBehaviorStep
            {
                Kind = "card.upgrade",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["card_state_key"] = "selected_cards"
                }
            };
            return true;
        }

        if (method.DeclaringType?.Name == "CardCmd" && string.Equals(method.Name, "Downgrade", StringComparison.Ordinal))
        {
            step = new NativeBehaviorStep
            {
                Kind = "card.downgrade",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["card_state_key"] = "selected_cards"
                }
            };
            return true;
        }

        if (method.DeclaringType?.Name == "CardCmd" && string.Equals(method.Name, "Enchant", StringComparison.Ordinal))
        {
            step = new NativeBehaviorStep
            {
                Kind = "card.enchant",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["card_state_key"] = "selected_cards",
                    ["amount"] = ResolveDynamicAmount(model, "Amount", "1")
                }
            };
            return true;
        }

        if (method.DeclaringType?.Name == "CardCmd" && string.Equals(method.Name, "AutoPlay", StringComparison.Ordinal))
        {
            step = new NativeBehaviorStep
            {
                Kind = "card.autoplay",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["card_state_key"] = "selected_cards",
                    ["target"] = defaultTargetSelector,
                    ["auto_play_type"] = AutoPlayType.Default.ToString(),
                    ["skip_x_capture"] = bool.FalseString,
                    ["skip_card_pile_visuals"] = bool.FalseString
                }
            };
            return true;
        }

        if (method.DeclaringType?.Name == "CardPileCmd" && string.Equals(method.Name, "AutoPlayFromDrawPile", StringComparison.Ordinal))
        {
            step = new NativeBehaviorStep
            {
                Kind = "cardpile.auto_play_from_draw_pile",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["count"] = ResolveDynamicAmount(model, "Repeat", ResolveDynamicAmount(model, "Cards", "1")),
                    ["position"] = CardPilePosition.Bottom.ToString(),
                    ["force_exhaust"] = bool.FalseString
                }
            };
            return true;
        }

        if (method.DeclaringType?.Name == "CardCmd" && string.Equals(method.Name, "ApplyKeyword", StringComparison.Ordinal))
        {
            step = new NativeBehaviorStep
            {
                Kind = "card.apply_keyword",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["card_state_key"] = "selected_cards"
                }
            };
            return true;
        }

        if (method.DeclaringType?.Name == "CardCmd" && string.Equals(method.Name, "ApplySingleTurnSly", StringComparison.Ordinal))
        {
            step = new NativeBehaviorStep
            {
                Kind = "card.apply_single_turn_sly",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["card_state_key"] = "selected_cards"
                }
            };
            return true;
        }

        if (method.DeclaringType?.Name == "CardPileCmd" && method.Name is "AddGeneratedCardToCombat" or "AddGeneratedCardsToCombat" or "AddToCombatAndPreview")
        {
            var cardCount = method.Name switch
            {
                "AddGeneratedCardsToCombat" => ResolveDynamicAmount(model, "Cards", "1"),
                "AddToCombatAndPreview" => ResolveDynamicAmount(model, "Repeat", ResolveDynamicAmount(model, "Cards", "1")),
                _ => "1"
            };
            step = new NativeBehaviorStep
            {
                Kind = "combat.create_card",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["count"] = cardCount,
                    ["target_pile"] = PileType.Hand.ToString()
                }
            };
            if (TryResolveGeneratedCardId(method, pendingCreatedCardId, out var createdCardId))
            {
                step.Parameters["card_id"] = createdCardId;
            }
            else
            {
                step.Parameters["card_id"] = string.Empty;
            }

            return true;
        }

        if (method.DeclaringType?.Name == "CardPileCmd" && string.Equals(method.Name, "Add", StringComparison.Ordinal))
        {
            step = new NativeBehaviorStep
            {
                Kind = "combat.create_card",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["count"] = "1",
                    ["target_pile"] = PileType.Discard.ToString()
                }
            };
            return true;
        }

        if (method.DeclaringType?.Name == "CardPileCmd" && method.Name is "AddCurseToDeck" or "AddCursesToDeck")
        {
            var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["count"] = method.Name == "AddCursesToDeck" ? ResolveDynamicAmount(model, "Cards", "1") : "1",
                ["target_pile"] = PileType.Deck.ToString()
            };
            if (TryResolveGeneratedCardId(method, pendingCreatedCardId, out var createdCardId))
            {
                parameters["card_id"] = createdCardId;
            }
            else
            {
                parameters["card_id"] = string.Empty;
            }

            step = new NativeBehaviorStep
            {
                Kind = "combat.create_card",
                Parameters = parameters
            };
            return true;
        }

        if (method.DeclaringType?.Name == "CardPileCmd" && method.Name is "RemoveFromDeck" or "RemoveFromCombat")
        {
            step = new NativeBehaviorStep
            {
                Kind = "combat.remove_card",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["card_state_key"] = "selected_cards"
                }
            };
            return true;
        }

        if (method.DeclaringType?.Name == "CardPileCmd" && string.Equals(method.Name, "Shuffle", StringComparison.Ordinal))
        {
            step = new NativeBehaviorStep
            {
                Kind = "cardpile.shuffle",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            };
            return true;
        }

        if (method.DeclaringType?.Name == "CardPileCmd" && string.Equals(method.Name, "ShuffleIfNecessary", StringComparison.Ordinal))
        {
            step = new NativeBehaviorStep
            {
                Kind = "cardpile.shuffle",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            };
            result.Warnings.Add("Collapsed ShuffleIfNecessary into cardpile.shuffle; the conditional guard is not serialized by the native importer.");
            return true;
        }

        if (method.DeclaringType?.Name == "CreatureCmd" && string.Equals(method.Name, "LoseBlock", StringComparison.Ordinal))
        {
            var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["target"] = defaultTargetSelector
            };
            PopulateDynamicAmountParameters(parameters, model, "Block", "0");
            step = new NativeBehaviorStep
            {
                Kind = "combat.lose_block",
                Parameters = parameters
            };
            return true;
        }

        if (method.DeclaringType?.Name == "CreatureCmd" && string.Equals(method.Name, "SetCurrentHp", StringComparison.Ordinal))
        {
            var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["target"] = defaultTargetSelector
            };
            PopulateDynamicAmountParameters(parameters, model, "Amount", "0");
            step = new NativeBehaviorStep
            {
                Kind = "creature.set_current_hp",
                Parameters = parameters
            };
            return true;
        }

        if (method.DeclaringType?.Name == "CreatureCmd" && string.Equals(method.Name, "GainMaxHp", StringComparison.Ordinal))
        {
            var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["target"] = defaultSelfSelector
            };
            PopulateDynamicAmountParameters(parameters, model, "MaxHp", "1");
            step = new NativeBehaviorStep
            {
                Kind = "player.gain_max_hp",
                Parameters = parameters
            };
            return true;
        }

        if (method.DeclaringType?.Name == "CreatureCmd" && string.Equals(method.Name, "LoseMaxHp", StringComparison.Ordinal))
        {
            var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["target"] = defaultTargetSelector,
                ["is_from_card"] = bool.FalseString
            };
            PopulateDynamicAmountParameters(parameters, model, "MaxHp", "1");
            step = new NativeBehaviorStep
            {
                Kind = "player.lose_max_hp",
                Parameters = parameters
            };
            return true;
        }

        if (method.DeclaringType?.Name == "CreatureCmd" && string.Equals(method.Name, "Kill", StringComparison.Ordinal))
        {
            step = new NativeBehaviorStep
            {
                Kind = "creature.kill",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["target"] = defaultTargetSelector,
                    ["force"] = bool.FalseString
                }
            };
            return true;
        }

        if (method.DeclaringType?.Name == "CreatureCmd" && string.Equals(method.Name, "Stun", StringComparison.Ordinal))
        {
            step = new NativeBehaviorStep
            {
                Kind = "creature.stun",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["target"] = defaultTargetSelector,
                    ["next_move_id"] = string.Empty
                }
            };
            return true;
        }

        if (method.DeclaringType?.Name == "PlayerCmd" && string.Equals(method.Name, "LoseEnergy", StringComparison.Ordinal))
        {
            var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
            PopulateDynamicAmountParameters(parameters, model, "Energy", "1");
            step = new NativeBehaviorStep
            {
                Kind = "player.lose_energy",
                Parameters = parameters
            };
            return true;
        }

        if (method.DeclaringType?.Name == "PlayerCmd" && string.Equals(method.Name, "LoseGold", StringComparison.Ordinal))
        {
            var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["gold_loss_type"] = GoldLossType.Lost.ToString()
            };
            PopulateDynamicAmountParameters(parameters, model, "Gold", "1");
            step = new NativeBehaviorStep
            {
                Kind = "player.lose_gold",
                Parameters = parameters
            };
            return true;
        }

        if (method.DeclaringType?.Name == "PlayerCmd" && string.Equals(method.Name, "GainMaxPotionCount", StringComparison.Ordinal))
        {
            var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
            PopulateDynamicAmountParameters(parameters, model, "Amount", "1");
            step = new NativeBehaviorStep
            {
                Kind = "player.gain_max_potion_count",
                Parameters = parameters
            };
            return true;
        }

        if (method.DeclaringType?.Name == "PlayerCmd" && string.Equals(method.Name, "AddPet", StringComparison.Ordinal))
        {
            var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
            if (TryResolveMonsterIdFromMethod(method, out var monsterId))
            {
                parameters["monster_id"] = monsterId;
            }

            step = new NativeBehaviorStep
            {
                Kind = "player.add_pet",
                Parameters = parameters
            };
            return true;
        }

        if (method.DeclaringType?.Name == "ForgeCmd" && string.Equals(method.Name, "Forge", StringComparison.Ordinal))
        {
            var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
            PopulateDynamicAmountParameters(parameters, model, "Forge", "1");
            step = new NativeBehaviorStep
            {
                Kind = "player.forge",
                Parameters = parameters
            };
            return true;
        }

        if (method.DeclaringType?.Name == "PlayerCmd" && string.Equals(method.Name, "CompleteQuest", StringComparison.Ordinal))
        {
            step = new NativeBehaviorStep
            {
                Kind = "player.complete_quest",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            };
            return true;
        }

        if (method.DeclaringType?.Name == "PlayerCmd" && string.Equals(method.Name, "MimicRestSiteHeal", StringComparison.Ordinal))
        {
            step = new NativeBehaviorStep
            {
                Kind = "player.rest_heal",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["play_sfx"] = bool.TrueString
                }
            };
            return true;
        }

        if (method.DeclaringType?.Name == "PlayerCmd" && string.Equals(method.Name, "EndTurn", StringComparison.Ordinal))
        {
            step = new NativeBehaviorStep
            {
                Kind = "player.end_turn",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            };
            return true;
        }

        if (method.DeclaringType?.Name == "OrbCmd" && string.Equals(method.Name, "AddSlots", StringComparison.Ordinal))
        {
            step = new NativeBehaviorStep
            {
                Kind = "orb.add_slots",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["amount"] = "1"
                }
            };
            return true;
        }

        if (method.DeclaringType?.Name == "OrbCmd" && string.Equals(method.Name, "RemoveSlots", StringComparison.Ordinal))
        {
            step = new NativeBehaviorStep
            {
                Kind = "orb.remove_slots",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["amount"] = "1"
                }
            };
            return true;
        }

        if (method.DeclaringType?.Name == "OrbCmd" && string.Equals(method.Name, "Channel", StringComparison.Ordinal))
        {
            var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
            if (TryResolveOrbIdFromMethod(method, out var orbId))
            {
                parameters["orb_id"] = orbId;
            }

            step = new NativeBehaviorStep
            {
                Kind = "orb.channel",
                Parameters = parameters
            };
            return true;
        }

        if (method.DeclaringType?.Name == "OrbCmd" && string.Equals(method.Name, "EvokeNext", StringComparison.Ordinal))
        {
            step = new NativeBehaviorStep
            {
                Kind = "orb.evoke_next",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["dequeue"] = bool.TrueString
                }
            };
            return true;
        }

        if (method.DeclaringType?.Name == "OrbCmd" && string.Equals(method.Name, "Passive", StringComparison.Ordinal))
        {
            var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["target"] = defaultTargetSelector
            };
            if (TryResolveOrbIdFromMethod(method, out var orbId))
            {
                parameters["orb_id"] = orbId;
            }
            else
            {
                parameters["orb_id"] = string.Empty;
            }

            step = new NativeBehaviorStep
            {
                Kind = "orb.passive",
                Parameters = parameters
            };
            return true;
        }

        if (method.DeclaringType?.Name == "PotionCmd" && string.Equals(method.Name, "TryToProcure", StringComparison.Ordinal))
        {
            var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
            if (TryResolvePotionIdFromMethod(method, out var potionId))
            {
                parameters["potion_id"] = potionId;
            }

            step = new NativeBehaviorStep
            {
                Kind = "potion.procure",
                Parameters = parameters
            };
            return true;
        }

        if (method.DeclaringType?.Name == "PotionCmd" && string.Equals(method.Name, "Discard", StringComparison.Ordinal))
        {
            step = new NativeBehaviorStep
            {
                Kind = "potion.discard",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            };
            return true;
        }

        if (method.DeclaringType?.Name == "RelicCmd" && string.Equals(method.Name, "Obtain", StringComparison.Ordinal))
        {
            var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
            if (TryResolveRelicIdFromMethod(method, out var relicId))
            {
                parameters["relic_id"] = relicId;
            }

            step = new NativeBehaviorStep
            {
                Kind = "relic.obtain",
                Parameters = parameters
            };
            return true;
        }

        if (method.DeclaringType?.Name == "RelicCmd" && string.Equals(method.Name, "Remove", StringComparison.Ordinal))
        {
            step = new NativeBehaviorStep
            {
                Kind = "relic.remove",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            };
            return true;
        }

        if (method.DeclaringType?.Name == "RelicCmd" && string.Equals(method.Name, "Replace", StringComparison.Ordinal))
        {
            step = new NativeBehaviorStep
            {
                Kind = "relic.replace",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            };
            return true;
        }

        if (method.DeclaringType?.Name == "RelicCmd" && string.Equals(method.Name, "Melt", StringComparison.Ordinal))
        {
            step = new NativeBehaviorStep
            {
                Kind = "relic.melt",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            };
            return true;
        }

        if (method.DeclaringType?.Name == "RewardsCmd" && string.Equals(method.Name, "OfferCustom", StringComparison.Ordinal))
        {
            step = new NativeBehaviorStep
            {
                Kind = "reward.offer_custom",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["reward_kind"] = "custom",
                    ["reward_count"] = "1"
                }
            };
            result.Warnings.Add($"RewardsCmd.OfferCustom for '{model.Id.Entry}' was imported as a custom reward placeholder. Review reward kind and payload manually.");
            return true;
        }

        if (method.DeclaringType?.Name == "OstyCmd" && string.Equals(method.Name, "Summon", StringComparison.Ordinal))
        {
            var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
            var osty = ModelDb.Monsters.FirstOrDefault(candidate => string.Equals(candidate.GetType().Name, "Osty", StringComparison.OrdinalIgnoreCase));
            if (osty != null)
            {
                parameters["monster_id"] = osty.Id.Entry;
            }

            step = new NativeBehaviorStep
            {
                Kind = "player.add_pet",
                Parameters = parameters
            };
            return true;
        }

        return false;
    }

    private static bool ShouldIgnoreCall(MethodBase method)
    {
        var typeName = method.DeclaringType?.Name ?? string.Empty;
        var methodName = method.Name;

        if ((typeName.Contains("DamageCmd", StringComparison.Ordinal) || typeName is "AttackCommand") &&
            methodName is "FromCard" or "FromOsty" or "Targeting" or "TargetingAllOpponents" or "WithHitFx" or "WithHitCount" or "WithNoAttackerAnim" or "WithAttackerAnim" or "SpawningHitVfxOnEachCreature" or "Execute")
        {
            return true;
        }

        if (typeName is "CreatureCmd" && methodName == "TriggerAnim")
        {
            return true;
        }

        if (typeName is "Cmd" && (methodName is "Wait" or "CustomScaledWait"))
        {
            return true;
        }

        if (typeName is "EventModel" && (methodName is "SetEventFinished" or "SetEventState"))
        {
            return true;
        }

        if (typeName is "SfxCmd" or "TalkCmd" or "VfxCmd")
        {
            return true;
        }

        if (typeName is "CardCmd" && (methodName is "Preview" or "PreviewCardPileAdd"))
        {
            return true;
        }

        if (typeName is "ForgeCmd" && methodName is "PlayCombatRoomForgeVfx")
        {
            return true;
        }

        return method.DeclaringType?.Namespace?.StartsWith("System", StringComparison.Ordinal) == true;
    }

    private static bool IsCoreGameplayCall(MethodBase method)
    {
        var ns = method.DeclaringType?.Namespace ?? string.Empty;
        if (ns.StartsWith("MegaCrit.Sts2.Core.Commands", StringComparison.Ordinal))
        {
            return true;
        }

        return method.DeclaringType?.Name is "EventModel" or "CardCmd" or "CardPileCmd" or "OrbCmd";
    }

    private static string ResolveDefaultTargetSelector(TargetType targetType)
    {
        return targetType switch
        {
            TargetType.Self => "self",
            TargetType.AnyEnemy or TargetType.AnyAlly or TargetType.AnyPlayer => "current_target",
            _ => "current_target"
        };
    }

    private static string ResolveDefaultSelfSelector(TargetType targetType)
    {
        return targetType == TargetType.Self ? "self" : "self";
    }

    private static string ResolveDynamicAmount(AbstractModel model, string varName, string fallback)
    {
        if (TryGetDynamicVar(model, varName, out var dynamicVar))
        {
            return dynamicVar.BaseValue.ToString(CultureInfo.InvariantCulture);
        }

        return fallback;
    }

    private static void PopulateDynamicAmountParameters(IDictionary<string, string> parameters, AbstractModel model, string varName, string fallback)
    {
        PopulateDynamicValueParameters(parameters, "amount", model, varName, fallback);
    }

    private static void PopulateDynamicValueParameters(IDictionary<string, string> parameters, string propertyKey, AbstractModel model, string varName, string fallback)
    {
        parameters[propertyKey] = ResolveDynamicAmount(model, varName, fallback);
        if (!TryGetDynamicVar(model, varName, out var dynamicVar))
        {
            return;
        }

        parameters[$"{propertyKey}_source_kind"] = dynamicVar is CalculatedVar
            ? DynamicValueSourceKind.FormulaRef.ToString()
            : DynamicValueSourceKind.DynamicVar.ToString();
        parameters[$"{propertyKey}_var_name"] = dynamicVar.Name;
        parameters[$"{propertyKey}_template"] = $"{{{dynamicVar.Name}:diff()}}";
        if (dynamicVar is CalculatedVar)
        {
            parameters[$"{propertyKey}_formula_ref"] = dynamicVar.Name;
        }
    }

    private static void PopulateFormulaMultiplierParameters(
        IDictionary<string, string> parameters,
        string propertyKey,
        string multiplierKey,
        string baseValue,
        string extraValue)
    {
        parameters[propertyKey] = "0";
        parameters[$"{propertyKey}_source_kind"] = DynamicValueSourceKind.FormulaRef.ToString();
        parameters[$"{propertyKey}_formula_ref"] = $"{propertyKey}_formula";
        parameters[$"{propertyKey}_base_override_mode"] = DynamicValueOverrideMode.Absolute.ToString();
        parameters[$"{propertyKey}_base_override_value"] = baseValue;
        parameters[$"{propertyKey}_extra_override_mode"] = DynamicValueOverrideMode.Absolute.ToString();
        parameters[$"{propertyKey}_extra_override_value"] = extraValue;
        parameters[$"{propertyKey}_preview_multiplier_key"] = multiplierKey;
    }

    private static string ResolveDamageProps(AbstractModel model)
    {
        if (TryGetDynamicVar(model, "Damage", out var dynamicVar) && dynamicVar is DamageVar damageVar)
        {
            return damageVar.Props == 0 ? "none" : damageVar.Props.ToString();
        }

        return "none";
    }

    private static string ResolveDamageVarName(AbstractModel model)
    {
        foreach (var name in new[] { "Damage", "HpLoss", "Amount" })
        {
            if (TryGetDynamicVar(model, name, out _))
            {
                return name;
            }
        }

        return "Damage";
    }

    private static bool TryResolvePowerId(AbstractModel model, out string powerId, out string amount)
    {
        powerId = string.Empty;
        amount = "1";

        foreach (var dynamicVar in EnumerateDynamicVars(model))
        {
            var type = dynamicVar.GetType();
            if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(PowerVar<>))
            {
                continue;
            }

            var powerType = type.GetGenericArguments()[0];
            if (!TryCreateModelIdFromType(powerType, out var resolvedPowerId))
            {
                continue;
            }

            powerId = resolvedPowerId;
            amount = dynamicVar.BaseValue.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        return false;
    }

    private static bool TryResolvePowerId(MethodBase method, AbstractModel model, out string powerId, out string amount)
    {
        if (TryResolvePowerId(model, out powerId, out amount))
        {
            return true;
        }

        return TryResolveModelIdFromMethod(method, ModelDb.AllPowers.Cast<AbstractModel>(), out powerId, out _)
               ? SetResolvedAmount(out amount)
               : SetUnresolved(out powerId, out amount);
    }

    private static bool TryResolvePowerApplication(
        MethodBase method,
        AbstractModel model,
        out string powerId,
        out string amountVarName,
        out string amount)
    {
        powerId = string.Empty;
        amountVarName = "Amount";
        amount = "1";

        if (!TryResolveModelIdFromMethod(method, ModelDb.AllPowers.Cast<AbstractModel>(), out powerId, out var matchedPowerType))
        {
            if (!TryResolvePowerId(model, out powerId, out amount))
            {
                return false;
            }

            return TryResolveFallbackPowerAmountVar(model, out amountVarName, out amount);
        }

        if (TryResolvePowerAmountVar(model, matchedPowerType, out amountVarName, out amount))
        {
            return true;
        }

        if (TryResolveFallbackPowerAmountVar(model, out amountVarName, out amount))
        {
            return true;
        }

        amountVarName = "Amount";
        amount = "1";
        return true;
    }

    private static bool TryResolvePowerAmountVar(
        AbstractModel model,
        Type? powerType,
        out string amountVarName,
        out string amount)
    {
        amountVarName = string.Empty;
        amount = "1";
        if (powerType == null)
        {
            return false;
        }

        foreach (var dynamicVar in EnumerateDynamicVars(model))
        {
            var dynamicVarType = dynamicVar.GetType();
            if (!dynamicVarType.IsGenericType || dynamicVarType.GetGenericTypeDefinition() != typeof(PowerVar<>))
            {
                continue;
            }

            if (dynamicVarType.GetGenericArguments()[0] != powerType)
            {
                continue;
            }

            amountVarName = dynamicVar.Name;
            amount = dynamicVar.BaseValue.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        return false;
    }

    private static bool TryResolveFallbackPowerAmountVar(AbstractModel model, out string amountVarName, out string amount)
    {
        amountVarName = string.Empty;
        amount = "1";
        foreach (var candidate in new[] { "Power", "Amount", "Stacks" })
        {
            if (!TryGetDynamicVar(model, candidate, out var dynamicVar))
            {
                continue;
            }

            amountVarName = dynamicVar.Name;
            amount = dynamicVar.BaseValue.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        return false;
    }

    private static bool SetResolvedAmount(out string amount)
    {
        amount = "1";
        return true;
    }

    private static bool SetUnresolved(out string powerId, out string amount)
    {
        powerId = string.Empty;
        amount = "1";
        return false;
    }

    private static string ResolveSelectionMode(string methodName)
    {
        return methodName switch
        {
            "FromHand" => "hand",
            "FromHandForDiscard" => "hand_for_discard",
            "FromHandForUpgrade" => "hand_for_upgrade",
            "FromChooseACardScreen" => "choose_a_card_screen",
            "FromChooseABundleScreen" => "choose_bundle",
            "FromSimpleGridForRewards" => "simple_grid_rewards",
            "FromDeckForUpgrade" => "deck_for_upgrade",
            "FromDeckForEnchantment" => "deck_for_enchantment",
            "FromDeckForTransformation" => "deck_for_transformation",
            "FromDeckForRemoval" => "deck_for_removal",
            _ => "simple_grid"
        };
    }

    private static string ResolveSelectionSourcePile(string methodName)
    {
        return methodName switch
        {
            "FromHand" or "FromHandForDiscard" or "FromHandForUpgrade" => PileType.Hand.ToString(),
            "FromDeckForUpgrade" or "FromDeckForTransformation" or "FromDeckForEnchantment" or "FromDeckForRemoval" or "FromDeckGeneric" => PileType.Deck.ToString(),
            _ => "all"
        };
    }

    private static string ResolvePromptKind(string methodName)
    {
        return methodName switch
        {
            "FromHandForDiscard" => "discard",
            "FromDeckForTransformation" => "transform",
            "FromDeckForUpgrade" or "FromHandForUpgrade" => "upgrade",
            "FromDeckForRemoval" => "remove",
            "FromDeckForEnchantment" => "enchant",
            _ => "generic"
        };
    }

    private static string ResolveSelectionCount(AbstractModel model)
    {
        foreach (var varName in new[] { "Cards", "Amount", "Damage", "Block" })
        {
            if (TryGetDynamicVar(model, varName, out var dynamicVar))
            {
                return dynamicVar.BaseValue.ToString(CultureInfo.InvariantCulture);
            }
        }

        return "1";
    }

    private static bool TryResolveCreatedCardIdFromCall(MethodBase method, out string cardId)
    {
        cardId = string.Empty;
        if (!string.Equals(method.Name, "CreateCard", StringComparison.Ordinal))
        {
            return false;
        }

        return TryResolveCardIdFromMethod(method, out cardId);
    }

    private static bool TryResolveGeneratedCardId(MethodBase method, string? pendingCardId, out string cardId)
    {
        if (TryResolveCardIdFromMethod(method, out cardId))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(pendingCardId))
        {
            cardId = pendingCardId;
            return true;
        }

        cardId = string.Empty;
        return false;
    }

    private static bool TryResolveCardIdFromMethod(MethodBase method, out string cardId)
    {
        return TryResolveModelIdFromMethod(method, ModelDb.AllCards.Cast<AbstractModel>(), out cardId, out _);
    }

    private static bool TryResolvePotionIdFromMethod(MethodBase method, out string potionId)
    {
        return TryResolveModelIdFromMethod(method, ModelDb.AllPotions.Cast<AbstractModel>(), out potionId, out _);
    }

    private static bool TryResolveRelicIdFromMethod(MethodBase method, out string relicId)
    {
        return TryResolveModelIdFromMethod(method, ModelDb.AllRelics.Cast<AbstractModel>(), out relicId, out _);
    }

    private static bool TryResolveMonsterIdFromMethod(MethodBase method, out string monsterId)
    {
        return TryResolveModelIdFromMethod(method, ModelDb.Monsters.Cast<AbstractModel>(), out monsterId, out _);
    }

    private static bool TryResolveOrbIdFromMethod(MethodBase method, out string orbId)
    {
        return TryResolveModelIdFromMethod(method, ModelDb.Orbs.Cast<AbstractModel>(), out orbId, out _);
    }

    private static bool TryResolveModelIdFromMethod(MethodBase method, IEnumerable<AbstractModel> candidates, out string id, out Type? matchedType)
    {
        id = string.Empty;
        matchedType = null;
        var genericArguments = method.IsGenericMethod ? method.GetGenericArguments() : Type.EmptyTypes;
        if (genericArguments.Length == 0)
        {
            return false;
        }

        var targetType = genericArguments[0];
        if (TryCreateModelIdFromType(targetType, out id))
        {
            matchedType = targetType;
            return true;
        }

        try
        {
            var model = candidates.FirstOrDefault(candidate => candidate.GetType() == targetType);
            if (model == null)
            {
                return false;
            }

            id = model.Id.Entry;
            matchedType = targetType;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryCreateModelIdFromType(Type targetType, out string id)
    {
        id = string.Empty;
        if (!typeof(AbstractModel).IsAssignableFrom(targetType) ||
            targetType.IsAbstract ||
            targetType.GetConstructor(Type.EmptyTypes) == null)
        {
            return TryDeriveModelIdFromTypeName(targetType, out id);
        }

        if (TryDeriveModelIdFromTypeName(targetType, out id))
        {
            return true;
        }

        try
        {
            if (Activator.CreateInstance(targetType) is not AbstractModel model)
            {
                return false;
            }

            id = model.Id.Entry;
            return !string.IsNullOrWhiteSpace(id);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryDeriveModelIdFromTypeName(Type targetType, out string id)
    {
        id = string.Empty;
        if (!typeof(AbstractModel).IsAssignableFrom(targetType))
        {
            return false;
        }

        var typeName = targetType.Name;
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return false;
        }

        var builder = new System.Text.StringBuilder(typeName.Length + 8);
        for (var index = 0; index < typeName.Length; index++)
        {
            var current = typeName[index];
            if (index > 0 && char.IsUpper(current) &&
                (char.IsLower(typeName[index - 1]) ||
                 (index + 1 < typeName.Length && char.IsLower(typeName[index + 1]))))
            {
                builder.Append('_');
            }

            builder.Append(char.ToUpperInvariant(current));
        }

        id = builder.ToString();
        return !string.IsNullOrWhiteSpace(id);
    }

    private static bool TryGetDynamicVar(AbstractModel model, string varName, out DynamicVar dynamicVar)
    {
        dynamicVar = null!;
        return model switch
        {
            CardModel card => card.DynamicVars.TryGetValue(varName, out dynamicVar!),
            PotionModel potion => potion.DynamicVars.TryGetValue(varName, out dynamicVar!),
            RelicModel relic => relic.DynamicVars.TryGetValue(varName, out dynamicVar!),
            _ => false
        };
    }

    private static bool TryResolveEnchantmentId(AbstractModel model, out string enchantmentId)
    {
        enchantmentId = string.Empty;
        return false;
    }

    private static IEnumerable<DynamicVar> EnumerateDynamicVars(AbstractModel model)
    {
        return model switch
        {
            CardModel card => card.DynamicVars.Values,
            PotionModel potion => potion.DynamicVars.Values,
            RelicModel relic => relic.DynamicVars.Values,
            _ => Array.Empty<DynamicVar>()
        };
    }

    private static bool SetUnsupported(NativeBehaviorGraphAutoImportResult result, string summary)
    {
        result.IsSupported = false;
        result.IsPartial = false;
        result.Summary = summary;
        return false;
    }

    private static IReadOnlyList<NativeCallSite> EnumerateCallSites(MethodInfo method)
    {
        var implementationMethod = ResolveImplementationMethod(method);
        var body = implementationMethod.GetMethodBody();
        if (body == null)
        {
            return Array.Empty<NativeCallSite>();
        }

        var il = body.GetILAsByteArray();
        if (il == null || il.Length == 0)
        {
            return Array.Empty<NativeCallSite>();
        }

        var results = new List<NativeCallSite>();
        var index = 0;
        int? lastInt32Constant = null;
        while (index < il.Length)
        {
            var code = il[index++];
            OpCode opcode;
            if (code == 0xfe)
            {
                var second = il[index++];
                if (!MultiByteOpCodes.TryGetValue((short)second, out opcode))
                {
                    continue;
                }
            }
            else if (!SingleByteOpCodes.TryGetValue((short)code, out opcode))
            {
                continue;
            }

            if (opcode.OperandType == OperandType.InlineMethod)
            {
                var token = BitConverter.ToInt32(il, index);
                index += 4;
                try
                {
                    var resolved = implementationMethod.Module.ResolveMethod(
                        token,
                        implementationMethod.DeclaringType?.GetGenericArguments(),
                        implementationMethod.GetGenericArguments());
                    if (resolved != null)
                    {
                        results.Add(new NativeCallSite
                        {
                            Method = resolved,
                            Int32ArgumentHint = lastInt32Constant
                        });
                    }
                }
                catch
                {
                    // Ignore tokens that cannot be resolved in the current context.
                }

                lastInt32Constant = null;
                continue;
            }

            if (TryResolveInt32Constant(opcode, il, index, out var int32Constant))
            {
                lastInt32Constant = int32Constant;
            }
            else if (opcode != OpCodes.Nop)
            {
                lastInt32Constant = null;
            }

            index += GetOperandSize(opcode, il, index);
        }

        return results;
    }

    private static bool TryResolveInt32Constant(OpCode opcode, byte[] il, int operandIndex, out int value)
    {
        switch (opcode.Value)
        {
            case short v when v == OpCodes.Ldc_I4_M1.Value:
                value = -1;
                return true;
            case short v when v == OpCodes.Ldc_I4_0.Value:
                value = 0;
                return true;
            case short v when v == OpCodes.Ldc_I4_1.Value:
                value = 1;
                return true;
            case short v when v == OpCodes.Ldc_I4_2.Value:
                value = 2;
                return true;
            case short v when v == OpCodes.Ldc_I4_3.Value:
                value = 3;
                return true;
            case short v when v == OpCodes.Ldc_I4_4.Value:
                value = 4;
                return true;
            case short v when v == OpCodes.Ldc_I4_5.Value:
                value = 5;
                return true;
            case short v when v == OpCodes.Ldc_I4_6.Value:
                value = 6;
                return true;
            case short v when v == OpCodes.Ldc_I4_7.Value:
                value = 7;
                return true;
            case short v when v == OpCodes.Ldc_I4_8.Value:
                value = 8;
                return true;
        }

        if (opcode == OpCodes.Ldc_I4_S)
        {
            value = (sbyte)il[operandIndex];
            return true;
        }

        if (opcode == OpCodes.Ldc_I4)
        {
            value = BitConverter.ToInt32(il, operandIndex);
            return true;
        }

        value = 0;
        return false;
    }

    private static MethodInfo ResolveImplementationMethod(MethodInfo method)
    {
        var stateMachine = method.GetCustomAttribute<AsyncStateMachineAttribute>();
        if (stateMachine?.StateMachineType != null)
        {
            var moveNext = stateMachine.StateMachineType.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (moveNext != null)
            {
                return moveNext;
            }
        }

        return method;
    }

    private static int GetOperandSize(OpCode opcode, byte[] il, int operandIndex)
    {
        return opcode.OperandType switch
        {
            OperandType.InlineNone => 0,
            OperandType.ShortInlineBrTarget => 1,
            OperandType.ShortInlineI => 1,
            OperandType.ShortInlineVar => 1,
            OperandType.ShortInlineR => 4,
            OperandType.InlineVar => 2,
            OperandType.InlineI => 4,
            OperandType.InlineBrTarget => 4,
            OperandType.InlineField => 4,
            OperandType.InlineMethod => 4,
            OperandType.InlineSig => 4,
            OperandType.InlineString => 4,
            OperandType.InlineTok => 4,
            OperandType.InlineType => 4,
            OperandType.InlineSwitch => 4 + (BitConverter.ToInt32(il, operandIndex) * 4),
            OperandType.InlineI8 => 8,
            OperandType.InlineR => 8,
            _ => 0
        };
    }
}
