using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Enchantments;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
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

    public string? StringArgumentHint { get; init; }
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
            ModStudioEntityKind.Enchantment => TryCreateEnchantmentGraph(entityId, result),
            _ => SetUnsupported(result, $"Native auto-import is not implemented for {kind} in Phase 1.")
        };
    }

    public bool TryCreateGraphFromMethod(
        ModStudioEntityKind kind,
        string entityId,
        string title,
        AbstractModel model,
        MethodInfo method,
        string triggerId,
        string defaultTargetSelector,
        string defaultSelfSelector,
        out NativeBehaviorGraphAutoImportResult result)
    {
        result = new NativeBehaviorGraphAutoImportResult();
        var steps = ExtractStepsFromMethod(model, method, triggerId, defaultTargetSelector, defaultSelfSelector, result);
        if (steps.Count == 0)
        {
            if (result.UnsupportedCalls.Count == 0)
            {
                var fallbackGraph = BehaviorGraphTemplateFactory.CreateDefaultScaffold(
                    $"native_auto_{entityId.ToLowerInvariant()}",
                    kind,
                    title,
                    ResolveModelTitle(model),
                    triggerId);
                result.Graph = fallbackGraph;
                result.IsSupported = true;
                result.IsPartial = result.Warnings.Count > 0;
                result.Summary = string.Join(
                    Environment.NewLine,
                    new[]
                    {
                        "Native import: cosmetic/no-op",
                        $"Trigger: {triggerId}",
                        $"Warnings: {(result.Warnings.Count == 0 ? "-" : string.Join(" | ", result.Warnings.Distinct(StringComparer.OrdinalIgnoreCase)))}"
                    });
                return true;
            }

            return SetUnsupported(result, $"No supported native steps were found for {method.DeclaringType?.Name}.{method.Name}.");
        }

        var graph = TranslateTriggerGraph(kind, entityId, title, steps, triggerId, result);
        if (graph == null)
        {
            return SetUnsupported(result, $"Could not translate supported native steps for {method.DeclaringType?.Name}.{method.Name}.");
        }

        result.Graph = graph;
        result.IsSupported = true;
        result.IsPartial = result.UnsupportedCalls.Count > 0 || result.Warnings.Count > 0;
        result.Summary = string.Join(
            Environment.NewLine,
            new[]
            {
                $"Native import: {(result.IsPartial ? "partial" : "supported")}",
                $"Trigger: {triggerId}",
                $"Applied steps: {(result.Graph.Nodes.Count == 0 ? "-" : string.Join(", ", result.Graph.Nodes.Select(node => node.NodeType).Where(nodeType => nodeType is not "flow.entry" and not "flow.exit").Distinct(StringComparer.OrdinalIgnoreCase)))}",
                $"Unsupported calls: {(result.UnsupportedCalls.Count == 0 ? "-" : string.Join(", ", result.UnsupportedCalls.Distinct(StringComparer.OrdinalIgnoreCase)))}",
                $"Warnings: {(result.Warnings.Count == 0 ? "-" : string.Join(" | ", result.Warnings.Distinct(StringComparer.OrdinalIgnoreCase)))}"
            });
        return true;
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

        var title = ResolveModelTitle(card);
        var translatedGraphs = new List<(string TriggerId, BehaviorGraphDefinition Graph)>();
        var methodCandidates = new (string MethodName, BindingFlags BindingFlags, Type[] Args, string TriggerId, Type? SkipDeclaringType)[]
        {
            ("OnPlay", BindingFlags.Instance | BindingFlags.NonPublic, [typeof(PlayerChoiceContext), typeof(CardPlay)], "card.on_play", typeof(CardModel)),
            ("OnTurnEndInHand", BindingFlags.Instance | BindingFlags.Public, [typeof(PlayerChoiceContext)], "card.on_turn_end_in_hand", typeof(CardModel)),
            ("AfterCardDrawn", BindingFlags.Instance | BindingFlags.Public, [typeof(PlayerChoiceContext), typeof(CardModel), typeof(bool)], "after_card_drawn", typeof(AbstractModel)),
            ("BeforeTurnEnd", BindingFlags.Instance | BindingFlags.Public, [typeof(PlayerChoiceContext), typeof(CombatSide)], "before_turn_end", typeof(AbstractModel)),
            ("AfterCombatEnd", BindingFlags.Instance | BindingFlags.Public, [typeof(MegaCrit.Sts2.Core.Rooms.CombatRoom)], "after_combat_end", typeof(AbstractModel)),
            ("TryModifyRestSiteOptions", BindingFlags.Instance | BindingFlags.Public, [typeof(MegaCrit.Sts2.Core.Entities.Players.Player), typeof(ICollection<MegaCrit.Sts2.Core.Entities.RestSite.RestSiteOption>)], "modify_rest_site_options", typeof(AbstractModel)),
            ("ModifyGeneratedMap", BindingFlags.Instance | BindingFlags.Public, [typeof(MegaCrit.Sts2.Core.Runs.IRunState), typeof(MegaCrit.Sts2.Core.Map.ActMap), typeof(int)], "modify_generated_map", typeof(AbstractModel)),
            ("ModifyGeneratedMapLate", BindingFlags.Instance | BindingFlags.Public, [typeof(MegaCrit.Sts2.Core.Runs.IRunState), typeof(MegaCrit.Sts2.Core.Map.ActMap), typeof(int)], "modify_generated_map_late", typeof(AbstractModel)),
            ("AfterMapGenerated", BindingFlags.Instance | BindingFlags.Public, [typeof(MegaCrit.Sts2.Core.Map.ActMap), typeof(int)], "after_map_generated", typeof(AbstractModel)),
            ("BeforeCardRemoved", BindingFlags.Instance | BindingFlags.Public, [typeof(CardModel)], "before_card_removed", typeof(AbstractModel)),
            ("OnChosen", BindingFlags.Instance | BindingFlags.Public, Type.EmptyTypes, "card.on_chosen", null)
        };

        foreach (var candidate in methodCandidates)
        {
            var method = card.GetType().GetMethod(
                candidate.MethodName,
                candidate.BindingFlags,
                binder: null,
                types: candidate.Args,
                modifiers: null);
            if (method == null)
            {
                continue;
            }

            if (candidate.SkipDeclaringType != null && method.DeclaringType == candidate.SkipDeclaringType)
            {
                continue;
            }

            var steps = ExtractStepsFromMethod(
                card,
                method,
                candidate.TriggerId,
                ResolveDefaultTargetSelector(card.TargetType),
                ResolveDefaultSelfSelector(card.TargetType),
                result);
            if (steps.Count == 0)
            {
                continue;
            }

            var graph = TranslateTriggerGraph(ModStudioEntityKind.Card, entityId, title, steps, candidate.TriggerId, result);
            if (graph != null)
            {
                translatedGraphs.Add((candidate.TriggerId, graph));
            }
        }

        if (translatedGraphs.Count == 0)
        {
            return SetUnsupported(result, $"Card '{entityId}' does not currently map to a supported graph trigger.");
        }

        result.Graph = MergeTriggerGraphs(
            $"native_auto_card_{entityId.ToLowerInvariant()}",
            ModStudioEntityKind.Card,
            string.IsNullOrWhiteSpace(title) ? entityId : $"{title} Native Import",
            translatedGraphs);
        result.IsSupported = true;
        result.IsPartial = result.UnsupportedCalls.Count > 0 || result.Warnings.Count > 0;
        result.Summary = string.Join(
            Environment.NewLine,
            new[]
            {
                $"Native import: {(result.IsPartial ? "partial" : "supported")}",
                $"Triggers: {string.Join(", ", translatedGraphs.Select(item => item.TriggerId))}",
                $"Unsupported calls: {(result.UnsupportedCalls.Count == 0 ? "-" : string.Join(", ", result.UnsupportedCalls.Distinct(StringComparer.OrdinalIgnoreCase)))}",
                $"Warnings: {(result.Warnings.Count == 0 ? "-" : string.Join(" | ", result.Warnings.Distinct(StringComparer.OrdinalIgnoreCase)))}"
            });
        return true;
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
            ("AfterObtained", Type.EmptyTypes, "relic.after_obtained"),
            ("AfterActEntered", Type.EmptyTypes, "relic.after_act_entered"),
            ("AfterBlockCleared", [typeof(Creature)], "relic.after_block_cleared"),
            ("AfterCardEnteredCombat", [typeof(CardModel)], "relic.after_card_entered_combat"),
            ("AfterCardDiscarded", [typeof(PlayerChoiceContext), typeof(CardModel)], "relic.after_card_discarded"),
            ("AfterDamageGiven", [typeof(PlayerChoiceContext), typeof(Creature), typeof(DamageResult), typeof(ValueProp), typeof(Creature), typeof(CardModel)], "relic.after_damage_given"),
            ("AfterDeath", [typeof(PlayerChoiceContext), typeof(Creature), typeof(bool), typeof(float)], "relic.after_death"),
            ("AfterGoldGained", [typeof(MegaCrit.Sts2.Core.Entities.Players.Player)], "relic.after_gold_gained"),
            ("AfterHandEmptied", [typeof(PlayerChoiceContext), typeof(MegaCrit.Sts2.Core.Entities.Players.Player)], "relic.after_hand_emptied"),
            ("AfterDiedToDoom", [typeof(PlayerChoiceContext), typeof(IReadOnlyList<Creature>)], "relic.after_died_to_doom"),
            ("AfterCurrentHpChanged", [typeof(Creature), typeof(decimal)], "relic.after_current_hp_changed"),
            ("AfterOrbChanneled", [typeof(PlayerChoiceContext), typeof(MegaCrit.Sts2.Core.Entities.Players.Player), typeof(OrbModel)], "relic.after_orb_channeled"),
            ("AfterPotionDiscarded", [typeof(PotionModel)], "relic.after_potion_discarded"),
            ("AfterPotionProcured", [typeof(PotionModel)], "relic.after_potion_procured"),
            ("AfterPreventingBlockClear", [typeof(AbstractModel), typeof(Creature)], "relic.after_preventing_block_clear"),
            ("AfterPreventingDeath", [typeof(Creature)], "relic.after_preventing_death"),
            ("AfterRestSiteHeal", [typeof(MegaCrit.Sts2.Core.Entities.Players.Player), typeof(bool)], "relic.after_rest_site_heal"),
            ("AfterShuffle", [typeof(PlayerChoiceContext), typeof(MegaCrit.Sts2.Core.Entities.Players.Player)], "relic.after_shuffle"),
            ("AfterStarsSpent", [typeof(int), typeof(MegaCrit.Sts2.Core.Entities.Players.Player)], "relic.after_stars_spent"),
            ("TryModifyRewards", [typeof(MegaCrit.Sts2.Core.Entities.Players.Player), typeof(List<MegaCrit.Sts2.Core.Rewards.Reward>), typeof(AbstractRoom)], "relic.modify_rewards"),
            ("TryModifyRewardsLate", [typeof(MegaCrit.Sts2.Core.Entities.Players.Player), typeof(List<MegaCrit.Sts2.Core.Rewards.Reward>), typeof(AbstractRoom)], "relic.modify_rewards_late"),
            ("TryModifyRestSiteHealRewards", [typeof(MegaCrit.Sts2.Core.Entities.Players.Player), typeof(List<MegaCrit.Sts2.Core.Rewards.Reward>), typeof(bool)], "relic.modify_rest_site_heal_rewards"),
            ("TryModifyCardRewardOptions", [typeof(MegaCrit.Sts2.Core.Entities.Players.Player), typeof(List<CardCreationResult>), typeof(MegaCrit.Sts2.Core.Runs.CardCreationOptions)], "relic.modify_card_reward_options"),
            ("TryModifyCardRewardOptionsLate", [typeof(MegaCrit.Sts2.Core.Entities.Players.Player), typeof(List<CardCreationResult>), typeof(MegaCrit.Sts2.Core.Runs.CardCreationOptions)], "relic.modify_card_reward_options_late"),
            ("ModifyDamageAdditive", [typeof(Creature), typeof(decimal), typeof(ValueProp), typeof(Creature), typeof(CardModel)], "relic.modify_damage_additive"),
            ("ModifyDamageMultiplicative", [typeof(Creature), typeof(decimal), typeof(ValueProp), typeof(Creature), typeof(CardModel)], "relic.modify_damage_multiplicative"),
            ("ModifyBlockMultiplicative", [typeof(Creature), typeof(decimal), typeof(ValueProp), typeof(CardModel), typeof(CardPlay)], "relic.modify_block_multiplicative"),
            ("ModifyHpLostBeforeOsty", [typeof(Creature), typeof(decimal), typeof(ValueProp), typeof(Creature), typeof(CardModel)], "relic.modify_hp_lost_before_osty"),
            ("ModifyHpLostAfterOsty", [typeof(Creature), typeof(decimal), typeof(ValueProp), typeof(Creature), typeof(CardModel)], "relic.modify_hp_lost_after_osty"),
            ("ModifyHandDraw", [typeof(MegaCrit.Sts2.Core.Entities.Players.Player), typeof(decimal)], "relic.modify_hand_draw"),
            ("ModifyHandDrawLate", [typeof(MegaCrit.Sts2.Core.Entities.Players.Player), typeof(decimal)], "relic.modify_hand_draw_late"),
            ("ModifyMaxEnergy", [typeof(MegaCrit.Sts2.Core.Entities.Players.Player), typeof(decimal)], "relic.modify_max_energy"),
            ("ModifyMerchantPrice", [typeof(MegaCrit.Sts2.Core.Entities.Players.Player), typeof(MegaCrit.Sts2.Core.Entities.Merchant.MerchantEntry), typeof(decimal)], "relic.modify_merchant_price"),
            ("ModifyUnknownMapPointRoomTypes", [typeof(IReadOnlySet<RoomType>)], "relic.modify_unknown_map_point_room_types"),
            ("ModifyXValue", [typeof(CardModel), typeof(int)], "relic.modify_x_value"),
            ("ShouldDisableRemainingRestSiteOptions", [typeof(MegaCrit.Sts2.Core.Entities.Players.Player)], "relic.should_disable_remaining_rest_site_options"),
            ("ShouldFlush", [typeof(MegaCrit.Sts2.Core.Entities.Players.Player)], "relic.should_flush"),
            ("ShouldForcePotionReward", [typeof(MegaCrit.Sts2.Core.Entities.Players.Player), typeof(RoomType)], "relic.should_force_potion_reward"),
            ("ShouldGainGold", [typeof(decimal), typeof(MegaCrit.Sts2.Core.Entities.Players.Player)], "relic.should_gain_gold"),
            ("ShouldPlayerResetEnergy", [typeof(MegaCrit.Sts2.Core.Entities.Players.Player)], "relic.should_player_reset_energy"),
            ("ShouldProcurePotion", [typeof(PotionModel), typeof(MegaCrit.Sts2.Core.Entities.Players.Player)], "relic.should_procure_potion"),
            ("ShouldRefillMerchantEntry", [typeof(MegaCrit.Sts2.Core.Entities.Merchant.MerchantEntry), typeof(MegaCrit.Sts2.Core.Entities.Players.Player)], "relic.should_refill_merchant_entry"),
            ("ModifyGeneratedMap", [typeof(MegaCrit.Sts2.Core.Runs.IRunState), typeof(MegaCrit.Sts2.Core.Map.ActMap), typeof(int)], "relic.modify_generated_map"),
            ("ModifyGeneratedMapLate", [typeof(MegaCrit.Sts2.Core.Runs.IRunState), typeof(MegaCrit.Sts2.Core.Map.ActMap), typeof(int)], "relic.modify_generated_map_late"),
            ("BeforeCombatStart", Type.EmptyTypes, "relic.before_combat_start"),
            ("BeforeCombatStartLate", Type.EmptyTypes, "relic.before_combat_start_late"),
            ("BeforeAttack", [typeof(AttackCommand)], "relic.before_attack"),
            ("AfterAttack", [typeof(AttackCommand)], "relic.after_attack"),
            ("BeforeCardPlayed", [typeof(CardPlay)], "relic.before_card_played"),
            ("AfterCardPlayed", [typeof(PlayerChoiceContext), typeof(CardPlay)], "relic.after_card_played"),
            ("AfterCardPlayedLate", [typeof(PlayerChoiceContext), typeof(CardPlay)], "relic.after_card_played_late"),
            ("AfterCardExhausted", [typeof(PlayerChoiceContext), typeof(CardModel), typeof(bool)], "relic.after_card_exhausted"),
            ("AfterDamageReceived", [typeof(PlayerChoiceContext), typeof(Creature), typeof(DamageResult), typeof(ValueProp), typeof(Creature), typeof(CardModel)], "relic.after_damage_received"),
            ("AfterDamageReceivedLate", [typeof(PlayerChoiceContext), typeof(Creature), typeof(DamageResult), typeof(ValueProp), typeof(Creature), typeof(CardModel)], "relic.after_damage_received_late"),
            ("AfterRoomEntered", [typeof(AbstractRoom)], "relic.after_room_entered"),
            ("BeforeHandDraw", [typeof(MegaCrit.Sts2.Core.Entities.Players.Player), typeof(PlayerChoiceContext), typeof(CombatState)], "relic.before_hand_draw"),
            ("BeforeHandDrawLate", [typeof(MegaCrit.Sts2.Core.Entities.Players.Player), typeof(PlayerChoiceContext), typeof(CombatState)], "relic.before_hand_draw_late"),
            ("BeforePlayPhaseStart", [typeof(PlayerChoiceContext), typeof(MegaCrit.Sts2.Core.Entities.Players.Player)], "relic.before_play_phase_start"),
            ("BeforeSideTurnStart", [typeof(PlayerChoiceContext), typeof(CombatSide), typeof(CombatState)], "relic.before_side_turn_start"),
            ("BeforeTurnEndVeryEarly", [typeof(PlayerChoiceContext), typeof(CombatSide)], "relic.before_turn_end_very_early"),
            ("BeforeTurnEndEarly", [typeof(PlayerChoiceContext), typeof(CombatSide)], "relic.before_turn_end_early"),
            ("BeforeTurnEnd", [typeof(PlayerChoiceContext), typeof(CombatSide)], "relic.before_turn_end"),
            ("AfterCardDrawn", [typeof(PlayerChoiceContext), typeof(CardModel), typeof(bool)], "relic.after_card_drawn"),
            ("AfterPlayerTurnStart", [typeof(PlayerChoiceContext), typeof(MegaCrit.Sts2.Core.Entities.Players.Player)], "relic.after_player_turn_start"),
            ("AfterPlayerTurnStartEarly", [typeof(PlayerChoiceContext), typeof(MegaCrit.Sts2.Core.Entities.Players.Player)], "relic.after_player_turn_start_early"),
            ("AfterPlayerTurnStartLate", [typeof(PlayerChoiceContext), typeof(MegaCrit.Sts2.Core.Entities.Players.Player)], "relic.after_player_turn_start_late"),
            ("AfterEnergyReset", [typeof(MegaCrit.Sts2.Core.Entities.Players.Player)], "relic.after_energy_reset"),
            ("AfterEnergyResetLate", [typeof(MegaCrit.Sts2.Core.Entities.Players.Player)], "relic.after_energy_reset_late"),
            ("AfterSideTurnStart", [typeof(CombatSide), typeof(CombatState)], "relic.after_side_turn_start"),
            ("AfterTurnEnd", [typeof(PlayerChoiceContext), typeof(CombatSide)], "relic.after_turn_end"),
            ("AfterTurnEndLate", [typeof(PlayerChoiceContext), typeof(CombatSide)], "relic.after_turn_end_late"),
            ("AfterCardChangedPiles", [typeof(CardModel), typeof(PileType), typeof(AbstractModel)], "relic.after_card_changed_piles"),
            ("AfterCardChangedPilesLate", [typeof(CardModel), typeof(PileType), typeof(AbstractModel)], "relic.after_card_changed_piles_late"),
            ("AfterCombatEnd", [typeof(MegaCrit.Sts2.Core.Rooms.CombatRoom)], "relic.after_combat_end"),
            ("AfterCombatVictoryEarly", [typeof(MegaCrit.Sts2.Core.Rooms.CombatRoom)], "relic.after_combat_victory_early"),
            ("AfterCombatVictory", [typeof(MegaCrit.Sts2.Core.Rooms.CombatRoom)], "relic.after_combat_victory"),
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

            var steps = TryBuildSyntheticRelicSteps(relic, method, candidate.TriggerId, result)
                ?? ExtractStepsFromMethod(relic, method, candidate.TriggerId, "current_target", "self", result);
            if (steps.Count == 0)
            {
                continue;
            }

            return FinalizeTranslation(ModStudioEntityKind.Relic, entityId, ResolveModelTitle(relic), steps, candidate.TriggerId, result);
        }

        return SetUnsupported(result, $"Relic '{entityId}' does not currently map to a supported graph hook trigger.");
    }

    private bool TryCreateEnchantmentGraph(string entityId, NativeBehaviorGraphAutoImportResult result)
    {
        var enchantment = ResolveEnchantmentModel(entityId);
        if (enchantment == null)
        {
            return SetUnsupported(result, $"Could not resolve runtime enchantment '{entityId}'.");
        }

        var title = ResolveModelTitle(enchantment);
        var translatedGraphs = new List<(string TriggerId, BehaviorGraphDefinition Graph)>();

        var methodCandidates = new (string MethodName, Type[] Args, string TriggerId)[]
        {
            ("OnPlay", [typeof(PlayerChoiceContext), typeof(CardPlay)], "enchantment.on_play"),
            ("OnEnchant", Type.EmptyTypes, "enchantment.on_enchant"),
            ("AfterCardPlayed", [typeof(PlayerChoiceContext), typeof(CardPlay)], "enchantment.after_card_played"),
            ("AfterCardDrawn", [typeof(PlayerChoiceContext), typeof(CardModel), typeof(bool)], "enchantment.after_card_drawn"),
            ("AfterPlayerTurnStart", [typeof(PlayerChoiceContext), typeof(MegaCrit.Sts2.Core.Entities.Players.Player)], "enchantment.after_player_turn_start"),
            ("BeforeFlush", [typeof(PlayerChoiceContext), typeof(MegaCrit.Sts2.Core.Entities.Players.Player)], "enchantment.before_flush"),
            ("EnchantDamageAdditive", [typeof(decimal), typeof(ValueProp)], "enchantment.modify_damage_additive"),
            ("EnchantDamageMultiplicative", [typeof(decimal), typeof(ValueProp)], "enchantment.modify_damage_multiplicative"),
            ("EnchantBlockAdditive", [typeof(decimal), typeof(ValueProp)], "enchantment.modify_block_additive"),
            ("EnchantBlockMultiplicative", [typeof(decimal), typeof(ValueProp)], "enchantment.modify_block_multiplicative"),
            ("EnchantPlayCount", [typeof(int)], "enchantment.modify_play_count")
        };

        foreach (var candidate in methodCandidates)
        {
            var method = enchantment.GetType().GetMethod(
                candidate.MethodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                types: candidate.Args,
                modifiers: null);
            if (method == null)
            {
                continue;
            }

            var declaringType = candidate.MethodName == "OnEnchant" ? typeof(EnchantmentModel) : typeof(AbstractModel);
            if (method.DeclaringType == declaringType)
            {
                continue;
            }

            var steps = TryBuildSyntheticEnchantmentSteps(enchantment, method, candidate.TriggerId, result)
                ?? ExtractStepsFromMethod(
                    enchantment,
                    method,
                    candidate.TriggerId,
                    "current_target",
                    "self",
                    result);
            if (steps.Count == 0)
            {
                continue;
            }

            var graph = TranslateTriggerGraph(ModStudioEntityKind.Enchantment, entityId, title, steps, candidate.TriggerId, result);
            if (graph != null)
            {
                translatedGraphs.Add((candidate.TriggerId, graph));
            }
        }

        if (translatedGraphs.Count == 0)
        {
            return SetUnsupported(result, $"Enchantment '{entityId}' does not currently map to a supported graph trigger.");
        }

        result.Graph = MergeTriggerGraphs(
            $"native_auto_enchantment_{entityId.ToLowerInvariant()}",
            ModStudioEntityKind.Enchantment,
            string.IsNullOrWhiteSpace(title) ? entityId : $"{title} Native Import",
            translatedGraphs);
        result.IsSupported = true;
        result.IsPartial = result.UnsupportedCalls.Count > 0 || result.Warnings.Count > 0;
        result.Summary = string.Join(
            Environment.NewLine,
            new[]
            {
                $"Native import: {(result.IsPartial ? "partial" : "supported")}",
                $"Triggers: {string.Join(", ", translatedGraphs.Select(item => item.TriggerId))}",
                $"Unsupported calls: {(result.UnsupportedCalls.Count == 0 ? "-" : string.Join(", ", result.UnsupportedCalls.Distinct(StringComparer.OrdinalIgnoreCase)))}",
                $"Warnings: {(result.Warnings.Count == 0 ? "-" : string.Join(" | ", result.Warnings.Distinct(StringComparer.OrdinalIgnoreCase)))}"
            });
        return true;
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
                EnchantmentModel enchantment => NativeLocalizationTableFallback.TryGetText(enchantment.Title) is var enchantmentTitle && !string.IsNullOrWhiteSpace(enchantmentTitle) ? enchantmentTitle : enchantment.Id.Entry,
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

    private static EnchantmentModel? ResolveEnchantmentModel(string entityId)
    {
        try
        {
            return ModelDb.DebugEnchantments.FirstOrDefault(candidate => string.Equals(candidate.Id.Entry, entityId, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return CreateModelByTypeName<EnchantmentModel>(entityId);
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

    private BehaviorGraphDefinition? TranslateTriggerGraph(
        ModStudioEntityKind kind,
        string entityId,
        string title,
        IReadOnlyList<NativeBehaviorStep> steps,
        string triggerId,
        NativeBehaviorGraphAutoImportResult result)
    {
        if (steps.Count == 0)
        {
            return null;
        }

        var source = new NativeBehaviorGraphSource
        {
            EntityKind = kind,
            GraphId = $"native_auto_{kind.ToString().ToLowerInvariant()}_{entityId.ToLowerInvariant()}_{triggerId.Replace('.', '_')}",
            Name = string.IsNullOrWhiteSpace(title) ? entityId : $"{title} Native Import",
            Description = string.Empty,
            TriggerId = triggerId,
            Steps = steps.ToList()
        };

        var translation = _translator.Translate(source);
        result.Warnings.AddRange(translation.Warnings);
        return translation.AppliedStepKinds.Count > 0 ? translation.Graph : null;
    }

    private static BehaviorGraphDefinition MergeTriggerGraphs(
        string graphId,
        ModStudioEntityKind kind,
        string name,
        IReadOnlyList<(string TriggerId, BehaviorGraphDefinition Graph)> translatedGraphs)
    {
        var merged = new BehaviorGraphDefinition
        {
            GraphId = graphId,
            Name = name,
            EntityKind = kind,
            Description = string.Empty,
            EntryNodeId = translatedGraphs[0].Graph.EntryNodeId,
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        };

        for (var index = 0; index < translatedGraphs.Count; index++)
        {
            var (triggerId, graph) = translatedGraphs[index];
            var suffix = $"__{index}";
            var nodeIdMap = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var node in graph.Nodes)
            {
                var mappedNodeId = node.NodeId + suffix;
                nodeIdMap[node.NodeId] = mappedNodeId;
                merged.Nodes.Add(new BehaviorGraphNodeDefinition
                {
                    NodeId = mappedNodeId,
                    NodeType = node.NodeType,
                    DisplayName = node.DisplayName,
                    Description = node.Description,
                    Properties = new Dictionary<string, string>(node.Properties, StringComparer.Ordinal),
                    DynamicValues = node.DynamicValues.ToDictionary(pair => pair.Key, pair => pair.Value.Clone(), StringComparer.Ordinal)
                });
            }

            foreach (var connection in graph.Connections)
            {
                if (!nodeIdMap.TryGetValue(connection.FromNodeId, out var mappedFrom) ||
                    !nodeIdMap.TryGetValue(connection.ToNodeId, out var mappedTo))
                {
                    continue;
                }

                merged.Connections.Add(new BehaviorGraphConnectionDefinition
                {
                    FromNodeId = mappedFrom,
                    FromPortId = connection.FromPortId,
                    ToNodeId = mappedTo,
                    ToPortId = connection.ToPortId
                });
            }

            if (index == 0 && nodeIdMap.TryGetValue(graph.EntryNodeId, out var mappedEntry))
            {
                merged.EntryNodeId = mappedEntry;
            }

            if (nodeIdMap.TryGetValue(graph.EntryNodeId, out var entryNodeId))
            {
                merged.Metadata[$"trigger.{triggerId}"] = entryNodeId;
                if (index == 0)
                {
                    merged.Metadata["trigger.default"] = entryNodeId;
                }
            }
        }

        return merged;
    }

    private List<NativeBehaviorStep> ExtractStepsFromMethod(
        AbstractModel model,
        MethodInfo method,
        string triggerId,
        string defaultTargetSelector,
        string defaultSelfSelector,
        NativeBehaviorGraphAutoImportResult result)
    {
        return ExtractStepsFromMethod(
            model,
            method,
            triggerId,
            defaultTargetSelector,
            defaultSelfSelector,
            result,
            new HashSet<MethodInfo>());
    }

    private List<NativeBehaviorStep> ExtractStepsFromMethod(
        AbstractModel model,
        MethodInfo method,
        string triggerId,
        string defaultTargetSelector,
        string defaultSelfSelector,
        NativeBehaviorGraphAutoImportResult result,
        ISet<MethodInfo> visitedMethods)
    {
        method = ResolveImplementationMethod(method);
        if (!visitedMethods.Add(method))
        {
            return new List<NativeBehaviorStep>();
        }

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

            if (TryTranslateRecognizedCall(model, calledMethod, callSite.Int32ArgumentHint, callSite.StringArgumentHint, defaultTargetSelector, defaultSelfSelector, pendingCreatedCardId, result, out var step, out var callLabel))
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

            if (TryInlineHelperMethod(model, method, calledMethod, triggerId, defaultTargetSelector, defaultSelfSelector, result, visitedMethods, out var helperSteps))
            {
                steps.AddRange(helperSteps);
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
        int? int32ArgumentHint,
        string? stringArgumentHint,
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

        if (method.DeclaringType?.Name == "CardCmd" && string.Equals(method.Name, "RemoveKeyword", StringComparison.Ordinal))
        {
            step = new NativeBehaviorStep
            {
                Kind = "card.remove_keyword",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["card_state_key"] = "selected_cards",
                    ["keyword"] = ResolveCardKeywordText(int32ArgumentHint)
                }
            };
            return true;
        }

        if (method.DeclaringType?.Name == "CardModel" && string.Equals(method.Name, "AddKeyword", StringComparison.Ordinal))
        {
            step = new NativeBehaviorStep
            {
                Kind = "card.apply_keyword",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["card_state_key"] = "selected_cards",
                    ["keyword"] = ResolveCardKeywordText(int32ArgumentHint)
                }
            };
            return true;
        }

        if (method.DeclaringType?.Name == "CardEnergyCost" && string.Equals(method.Name, "UpgradeBy", StringComparison.Ordinal))
        {
            step = new NativeBehaviorStep
            {
                Kind = "card.set_cost_delta",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["card_state_key"] = "selected_cards",
                    ["amount"] = (int32ArgumentHint ?? -1).ToString(CultureInfo.InvariantCulture)
                }
            };
            return true;
        }

        if (method.DeclaringType?.Name == "CardEnergyCost" && string.Equals(method.Name, "SetCustomBaseCost", StringComparison.Ordinal))
        {
            step = new NativeBehaviorStep
            {
                Kind = "card.set_cost_absolute",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["card_state_key"] = "selected_cards",
                    ["amount"] = (int32ArgumentHint ?? 0).ToString(CultureInfo.InvariantCulture)
                }
            };
            return true;
        }

        if (method.DeclaringType?.Name == "CardEnergyCost" && string.Equals(method.Name, "SetThisCombat", StringComparison.Ordinal))
        {
            step = new NativeBehaviorStep
            {
                Kind = "card.set_cost_this_combat",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["card_state_key"] = "selected_cards",
                    ["amount"] = (int32ArgumentHint ?? 0).ToString(CultureInfo.InvariantCulture)
                }
            };
            return true;
        }

        if (method.DeclaringType?.Name == "CardEnergyCost" && string.Equals(method.Name, "AddUntilPlayed", StringComparison.Ordinal))
        {
            step = new NativeBehaviorStep
            {
                Kind = "card.add_cost_until_played",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["card_state_key"] = "selected_cards",
                    ["amount"] = (int32ArgumentHint ?? -1).ToString(CultureInfo.InvariantCulture)
                }
            };
            return true;
        }

        if (method.DeclaringType?.Name == "EnchantmentModel" && string.Equals(method.Name, "set_Status", StringComparison.Ordinal))
        {
            step = new NativeBehaviorStep
            {
                Kind = "enchantment.set_status",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["status"] = ResolveEnchantmentStatusText(int32ArgumentHint)
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

        if (method.DeclaringType?.Name == "CreatureCmd" && string.Equals(method.Name, "TriggerAnim", StringComparison.Ordinal))
        {
            step = new NativeBehaviorStep
            {
                Kind = "monster.animate",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["animation_id"] = string.IsNullOrWhiteSpace(stringArgumentHint) ? "Attack" : stringArgumentHint,
                    ["wait_duration"] = "0"
                }
            };
            return true;
        }

        if (method.DeclaringType?.Name == "CreatureCmd" && string.Equals(method.Name, "Escape", StringComparison.Ordinal))
        {
            step = new NativeBehaviorStep
            {
                Kind = "monster.escape",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            };
            return true;
        }

        if (method.DeclaringType?.Name == "SfxCmd" && string.Equals(method.Name, "Play", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(stringArgumentHint))
        {
            step = new NativeBehaviorStep
            {
                Kind = "monster.play_sfx",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["sfx_path"] = stringArgumentHint
                }
            };
            return true;
        }

        if (method.DeclaringType?.IsSubclassOf(typeof(MonsterModel)) == true &&
            method.Name.StartsWith("set_", StringComparison.Ordinal) &&
            int32ArgumentHint.HasValue)
        {
            var parameterType = method.GetParameters().FirstOrDefault()?.ParameterType;
            var valueText = parameterType == typeof(bool)
                ? (int32ArgumentHint.Value != 0 ? bool.TrueString : bool.FalseString)
                : int32ArgumentHint.Value.ToString(CultureInfo.InvariantCulture);
            step = new NativeBehaviorStep
            {
                Kind = "monster.set_state",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["variable_name"] = method.Name["set_".Length..],
                    ["value"] = valueText
                }
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

        if (method.DeclaringType?.Name == "CreatureCmd" && string.Equals(method.Name, "Add", StringComparison.Ordinal))
        {
            if (method is MethodInfo addMethod && addMethod.IsGenericMethod)
            {
                var genericMonsterType = addMethod.GetGenericArguments().FirstOrDefault(type => type.IsSubclassOf(typeof(MonsterModel)));
                if (genericMonsterType != null)
                {
                    step = new NativeBehaviorStep
                    {
                        Kind = "monster.summon",
                        Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["monster_id"] = ModelDb.GetId(genericMonsterType).Entry
                        }
                    };
                    return true;
                }
            }
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

    private bool TryInlineHelperMethod(
        AbstractModel model,
        MethodInfo ownerMethod,
        MethodBase calledMethod,
        string triggerId,
        string defaultTargetSelector,
        string defaultSelfSelector,
        NativeBehaviorGraphAutoImportResult result,
        ISet<MethodInfo> visitedMethods,
        out IReadOnlyList<NativeBehaviorStep> helperSteps)
    {
        helperSteps = Array.Empty<NativeBehaviorStep>();
        if (calledMethod is not MethodInfo helperMethod)
        {
            return false;
        }

        helperMethod = ResolveImplementationMethod(helperMethod);
        if (helperMethod == ownerMethod ||
            helperMethod.IsSpecialName ||
            helperMethod.DeclaringType != ownerMethod.DeclaringType ||
            helperMethod.ReturnType != typeof(Task))
        {
            return false;
        }

        var inlined = ExtractStepsFromMethod(model, helperMethod, triggerId, defaultTargetSelector, defaultSelfSelector, result, visitedMethods);
        if (inlined.Count == 0)
        {
            return false;
        }

        helperSteps = inlined;
        result.RecognizedCalls.Add($"inline:{helperMethod.DeclaringType?.Name}.{helperMethod.Name}");
        return true;
    }

    private static bool ShouldIgnoreCall(MethodBase method)
    {
        var typeName = method.DeclaringType?.Name ?? string.Empty;
        var methodName = method.Name;

        if ((typeName.Contains("DamageCmd", StringComparison.Ordinal) || typeName is "AttackCommand") &&
            methodName is "FromCard" or "FromOsty" or "Targeting" or "TargetingAllOpponents" or "TargetingRandomOpponents" or "WithHitFx" or "WithHitCount" or "WithNoAttackerAnim" or "WithAttackerAnim" or "WithAttackerFx" or "SpawningHitVfxOnEachCreature" or "Execute" or "WithHitVfxNode" or "WithHitVfxSpawnedAtBase" or "BeforeDamage" or "OnlyPlayAnimOnce" or "CreateContextAsync" or "AddHit" or "DisposeAsync")
        {
            return true;
        }

        if (typeName is "AttackCommand" && methodName.StartsWith("get_", StringComparison.Ordinal))
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
        foreach (var candidate in new[] { "Power", "Amount", "Stacks", "Cards", "Repeat" })
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

    private static string ResolveCardKeywordText(int? int32ArgumentHint)
    {
        if (int32ArgumentHint.HasValue && Enum.IsDefined(typeof(CardKeyword), int32ArgumentHint.Value))
        {
            return ((CardKeyword)int32ArgumentHint.Value).ToString();
        }

        return string.Empty;
    }

    private static string ResolveEnchantmentStatusText(int? int32ArgumentHint)
    {
        if (int32ArgumentHint.HasValue && Enum.IsDefined(typeof(EnchantmentStatus), int32ArgumentHint.Value))
        {
            return ((EnchantmentStatus)int32ArgumentHint.Value).ToString();
        }

        return EnchantmentStatus.Disabled.ToString();
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
                EnchantmentModel enchantment when string.Equals(varName, "Amount", StringComparison.OrdinalIgnoreCase) => TryCreateSyntheticEnchantmentAmountVar(enchantment, out dynamicVar!),
                EnchantmentModel enchantment => enchantment.DynamicVars.TryGetValue(varName, out dynamicVar!),
                _ => false
            };
    }

    private static bool TryCreateSyntheticEnchantmentAmountVar(EnchantmentModel enchantment, out DynamicVar dynamicVar)
    {
        dynamicVar = new IntVar("Amount", enchantment.Amount);
        return true;
    }

    private List<NativeBehaviorStep>? TryBuildSyntheticEnchantmentSteps(
        EnchantmentModel enchantment,
        MethodInfo method,
        string triggerId,
        NativeBehaviorGraphAutoImportResult result)
    {
        var methodName = method.Name;
        Dictionary<string, string>? parameters = null;
        string? kind = null;

        switch (methodName)
        {
            case "EnchantDamageAdditive":
                kind = "modifier.damage_additive";
                parameters = BuildEnchantmentModifierParameters(enchantment, "Amount", "0");
                break;
            case "EnchantBlockAdditive":
                kind = "modifier.block_additive";
                parameters = BuildEnchantmentModifierParameters(enchantment, "Amount", "0");
                break;
            case "EnchantDamageMultiplicative":
                kind = "modifier.damage_multiplicative";
                parameters = string.Equals(enchantment.Id.Entry, "FAVORED", StringComparison.OrdinalIgnoreCase)
                    ? new Dictionary<string, string>(StringComparer.Ordinal) { ["amount"] = "2" }
                    : BuildEnchantmentModifierParameters(enchantment, "Amount", "1");
                break;
            case "EnchantBlockMultiplicative":
                kind = "modifier.block_multiplicative";
                parameters = BuildEnchantmentModifierParameters(enchantment, "Amount", "1");
                break;
            case "EnchantPlayCount":
                kind = "modifier.play_count";
                parameters = BuildEnchantmentModifierParameters(enchantment, "Times", "1");
                parameters["mode"] = "delta";
                break;
        }

        if (string.IsNullOrWhiteSpace(kind) || parameters == null)
        {
            return null;
        }

        result.RecognizedCalls.Add($"{methodName} -> {kind}");
        return
        [
            new NativeBehaviorStep
            {
                Kind = kind,
                Parameters = parameters
            }
        ];
    }

    private List<NativeBehaviorStep>? TryBuildSyntheticRelicSteps(
        RelicModel relic,
        MethodInfo method,
        string triggerId,
        NativeBehaviorGraphAutoImportResult result)
    {
        _ = triggerId;
        Dictionary<string, string>? parameters = null;
        string? kind = null;

        switch (method.Name)
        {
            case "AfterObtained":
                return TryBuildSyntheticRelicAfterObtainedSteps(relic, result);
            case "AfterBlockCleared":
                return TryBuildSyntheticRelicAfterBlockClearedSteps(relic, result);
            case "AfterCardExhausted":
                return TryBuildSyntheticRelicAfterCardExhaustedSteps(relic, result);
            case "AfterCardEnteredCombat":
                return TryBuildSyntheticRelicAfterCardEnteredCombatSteps(relic, result);
            case "AfterCardDiscarded":
                return TryBuildSyntheticRelicAfterCardDiscardedSteps(relic, result);
            case "AfterDamageGiven":
                return TryBuildSyntheticRelicAfterDamageGivenSteps(relic, result);
            case "AfterDeath":
                return TryBuildSyntheticRelicAfterDeathSteps(relic, result);
            case "AfterDiedToDoom":
                return TryBuildSyntheticRelicAfterDiedToDoomSteps(relic, result);
            case "AfterCurrentHpChanged":
                return TryBuildSyntheticRelicAfterCurrentHpChangedSteps(relic, result);
            case "AfterHandEmptied":
                return TryBuildSyntheticRelicAfterHandEmptiedSteps(relic, result);
            case "AfterOrbChanneled":
                return TryBuildSyntheticRelicAfterOrbChanneledSteps(relic, result);
            case "AfterPotionDiscarded":
                return TryBuildSyntheticRelicAfterPotionDiscardedSteps(relic, result);
            case "AfterPotionProcured":
                return TryBuildSyntheticRelicAfterPotionProcuredSteps(relic, result);
            case "AfterPreventingBlockClear":
                return TryBuildSyntheticRelicAfterPreventingBlockClearSteps(relic, result);
            case "AfterPreventingDeath":
                return TryBuildSyntheticRelicAfterPreventingDeathSteps(relic, result);
            case "AfterRestSiteHeal":
                return TryBuildSyntheticRelicAfterRestSiteHealSteps(relic, result);
            case "AfterShuffle":
                return TryBuildSyntheticRelicAfterShuffleSteps(relic, result);
            case "AfterStarsSpent":
                return TryBuildSyntheticRelicAfterStarsSpentSteps(relic, result);
            case "BeforeTurnEnd":
                return TryBuildSyntheticRelicBeforeTurnEndSteps(relic, result);
            case "BeforeHandDraw":
                return TryBuildSyntheticRelicBeforeHandDrawSteps(relic, result);
            case "BeforeCombatStart":
                return TryBuildSyntheticRelicBeforeCombatStartSteps(relic, result);
            case "TryModifyCardRewardOptions":
            case "TryModifyCardRewardOptionsLate":
                return TryBuildSyntheticRelicCardRewardOptionSteps(relic, method.Name, result);
            case "TryModifyRewards":
                return TryBuildSyntheticRelicRewardSteps(relic, method.Name, result);
            case "TryModifyRewardsLate":
                return TryBuildSyntheticRelicRewardSteps(relic, method.Name, result);
            case "TryModifyRestSiteHealRewards":
                return TryBuildSyntheticRelicRewardSteps(relic, method.Name, result);
            case "ModifyDamageAdditive":
                return TryBuildSyntheticRelicDamageAdditiveSteps(relic, result);
            case "ModifyDamageMultiplicative":
                return TryBuildSyntheticRelicDamageMultiplicativeSteps(relic, result);
            case "ModifyBlockMultiplicative":
                return TryBuildSyntheticRelicBlockMultiplicativeSteps(relic, result);
            case "ModifyHpLostBeforeOsty":
                return TryBuildSyntheticRelicHpLostBeforeOstySteps(relic, result);
            case "ModifyHpLostAfterOsty":
                return TryBuildSyntheticRelicHpLostAfterOstySteps(relic, result);
            case "ModifyMerchantPrice":
                return TryBuildSyntheticRelicMerchantPriceSteps(relic, result);
            case "ModifyUnknownMapPointRoomTypes":
                return TryBuildSyntheticRelicUnknownRoomTypeSteps(relic, result);
            case "ShouldDisableRemainingRestSiteOptions":
                return TryBuildSyntheticRelicBoolSteps(relic, method.Name, result);
            case "ShouldGainGold":
                return TryBuildSyntheticRelicBoolSteps(relic, method.Name, result);
            case "ShouldPlayerResetEnergy":
                return TryBuildSyntheticRelicBoolSteps(relic, method.Name, result);
            case "ShouldFlush":
                return TryBuildSyntheticRelicBoolSteps(relic, method.Name, result);
            case "ShouldForcePotionReward":
                return TryBuildSyntheticRelicBoolSteps(relic, method.Name, result);
            case "ShouldRefillMerchantEntry":
                return TryBuildSyntheticRelicBoolSteps(relic, method.Name, result);
            case "ShouldProcurePotion":
                return TryBuildSyntheticRelicBoolSteps(relic, method.Name, result);
            case "ModifyGeneratedMap":
            case "ModifyGeneratedMapLate":
                return TryBuildSyntheticRelicMapSteps(relic, method.Name, result);
            case "ModifyHandDraw":
            case "ModifyHandDrawLate":
                kind = "modifier.hand_draw";
                parameters = BuildRelicNumericModifierParameters(
                    relic,
                    preferredVarName: "Cards",
                    fallbackLiteral: "0",
                    isNegativeDelta: string.Equals(relic.Id.Entry, "BIG_MUSHROOM", StringComparison.OrdinalIgnoreCase));
                parameters["mode"] = "delta";
                break;
            case "ModifyXValue":
                kind = "modifier.x_value";
                parameters = BuildRelicNumericModifierParameters(relic, preferredVarName: "Increase", fallbackLiteral: "0");
                parameters["mode"] = "delta";
                break;
            case "ModifyMaxEnergy":
                kind = "modifier.max_energy";
                parameters = BuildRelicNumericModifierParameters(relic, preferredVarName: "Energy", fallbackLiteral: "0");
                parameters["mode"] = "delta";
                break;
        }

        if (string.IsNullOrWhiteSpace(kind) || parameters == null)
        {
            return null;
        }

        result.RecognizedCalls.Add($"{method.Name} -> {kind}");
        return
        [
            new NativeBehaviorStep
            {
                Kind = kind,
                Parameters = parameters
            }
        ];
    }

    private List<NativeBehaviorStep>? TryBuildSyntheticRelicRewardSteps(
        RelicModel relic,
        string methodName,
        NativeBehaviorGraphAutoImportResult result)
    {
        List<NativeBehaviorStep>? steps = relic.Id.Entry switch
        {
            "BLACK_STAR" when methodName == "TryModifyRewards" => BuildRoomConditionalRewardSteps(
                "Elite",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["reward_kind"] = "relic",
                    ["reward_count"] = "1"
                }),
            "PRAYER_WHEEL" when methodName == "TryModifyRewards" => BuildRoomConditionalRewardSteps(
                "Monster",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["reward_kind"] = "card_reward",
                    ["card_count"] = "3",
                    ["reward_room_type"] = "Monster"
                }),
            "WHITE_STAR" when methodName == "TryModifyRewards" => BuildRoomConditionalRewardSteps(
                "Elite",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["reward_kind"] = "card_reward",
                    ["card_count"] = "3",
                    ["reward_room_type"] = "Boss"
                }),
            "DREAM_CATCHER" when methodName == "TryModifyRestSiteHealRewards" =>
            [
                new NativeBehaviorStep
                {
                    Kind = "reward.offer_custom",
                    Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["reward_kind"] = "card_reward",
                        ["card_count"] = "3",
                        ["reward_room_type"] = "Monster"
                    }
                }
            ],
            "TINY_MAILBOX" when methodName == "TryModifyRestSiteHealRewards" =>
            [
                new NativeBehaviorStep
                {
                    Kind = "reward.offer_custom",
                    Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["reward_kind"] = "potion",
                        ["reward_count"] = "1"
                    }
                }
            ],
            "DRIFTWOOD" when methodName == "TryModifyRewardsLate" =>
            [
                new NativeBehaviorStep
                {
                    Kind = "reward.mark_card_rewards_rerollable",
                    Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                }
            ],
            "AMETHYST_AUBERGINE" when methodName == "TryModifyRewards" => BuildAmethystAubergineRewardSteps(relic),
            "LAVA_ROCK" when methodName == "TryModifyRewards" => BuildLavaRockRewardSteps(relic),
            "WONGOS_MYSTERY_TICKET" when methodName == "TryModifyRewards" => BuildWongosMysteryTicketRewardSteps(relic),
            _ => null
        };

        if (steps != null)
        {
            result.RecognizedCalls.Add($"{methodName} -> synthetic reward graph");
        }

        return steps;
    }

    private List<NativeBehaviorStep>? TryBuildSyntheticRelicAfterObtainedSteps(
        RelicModel relic,
        NativeBehaviorGraphAutoImportResult result)
    {
        List<NativeBehaviorStep>? steps = relic.Id.Entry switch
        {
            "BELT_BUCKLE" => BuildBeltBuckleAfterObtainedSteps(relic),
            "PAELS_GROWTH" => BuildDeckEnchantAfterObtainedSteps("CLONE", "4", selectAll: false),
            "ROYAL_STAMP" => BuildDeckEnchantAfterObtainedSteps("ROYALLY_APPROVED", "1", selectAll: false),
            "PAELS_CLAW" => BuildDeckEnchantAfterObtainedSteps("GOOPY", "1", selectAll: true),
            "PAELS_LEGION" => BuildAddPetAfterObtainedSteps("PaelsLegion"),
            _ => null
        };

        if (steps != null)
        {
            result.RecognizedCalls.Add("AfterObtained -> synthetic obtained graph");
        }

        return steps;
    }

    private List<NativeBehaviorStep>? TryBuildSyntheticRelicAfterCardExhaustedSteps(
        RelicModel relic,
        NativeBehaviorGraphAutoImportResult result)
    {
        if (relic.Id.Entry != "JOSS_PAPER")
        {
            return null;
        }

        result.RecognizedCalls.Add("AfterCardExhausted -> synthetic exhaust counter graph");
        return
        [
            BuildCompareStep("$state.CardOwnerIsOwner", "eq", bool.TrueString, "joss_owner_card"),
            BuildBranchStep(
                "joss_owner_card",
                new[]
                {
                    BuildCompareStep("$state.ExhaustedCausedByEthereal", "eq", bool.TrueString, "joss_ethereal_exhaust"),
                    BuildBranchStep(
                        "joss_ethereal_exhaust",
                        new[]
                        {
                            new NativeBehaviorStep
                            {
                                Kind = "value.add",
                                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                                {
                                    ["key"] = "EtherealCount",
                                    ["delta"] = "1"
                                }
                            }
                        },
                        new[]
                        {
                            new NativeBehaviorStep
                            {
                                Kind = "value.add",
                                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                                {
                                    ["key"] = "CardsExhausted",
                                    ["delta"] = "1"
                                }
                            },
                            BuildCompareStep("$state.CardsExhausted", "gte", "$state.ExhaustAmount", "joss_threshold_met"),
                            BuildBranchStep(
                                "joss_threshold_met",
                                new[]
                                {
                                    new NativeBehaviorStep
                                    {
                                        Kind = "combat.draw_cards",
                                        Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                                        {
                                            ["amount"] = "1"
                                        }
                                    },
                                    new NativeBehaviorStep
                                    {
                                        Kind = "value.set",
                                        Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                                        {
                                            ["key"] = "CardsExhausted",
                                            ["value"] = "0"
                                        }
                                    }
                                })
                        })
                })
        ];
    }

    private List<NativeBehaviorStep>? TryBuildSyntheticRelicAfterBlockClearedSteps(
        RelicModel relic,
        NativeBehaviorGraphAutoImportResult result)
    {
        List<NativeBehaviorStep>? steps = relic.Id.Entry switch
        {
            "CAPTAINS_WHEEL" => BuildRoundConditionalRelicSteps(
                roundNumber: 3,
                BuildRelicAmountAction(relic, "combat.gain_block", "Block", "18", "self")),
            "HORN_CLEAT" => BuildRoundConditionalRelicSteps(
                roundNumber: 2,
                BuildRelicAmountAction(relic, "combat.gain_block", "Block", "14", "self")),
            "SPARKLING_ROUGE" => BuildRoundConditionalRelicSteps(
                roundNumber: 3,
                BuildSparklingRougePowerSteps(relic).ToArray()),
            _ => null
        };

        if (steps != null)
        {
            result.RecognizedCalls.Add("AfterBlockCleared -> synthetic round conditional graph");
        }

        return steps;
    }

    private List<NativeBehaviorStep>? TryBuildSyntheticRelicAfterCardEnteredCombatSteps(
        RelicModel relic,
        NativeBehaviorGraphAutoImportResult result)
    {
        if (relic.Id.Entry != "REGALITE")
        {
            return null;
        }

        result.RecognizedCalls.Add("AfterCardEnteredCombat -> synthetic colorless card graph");
        return
        [
            BuildCompareStep("$state.EnteredCardOwnerIsOwner", "eq", bool.TrueString, "entered_card_owned"),
            BuildBranchStep(
                "entered_card_owned",
                new[]
                {
                    BuildCompareStep("$state.EnteredCardIsColorless", "eq", bool.TrueString, "entered_card_colorless"),
                    BuildBranchStep(
                        "entered_card_colorless",
                        new[]
                        {
                            BuildRelicAmountAction(relic, "combat.gain_block", "Block", "2", "self")
                        })
                })
        ];
    }

    private List<NativeBehaviorStep>? TryBuildSyntheticRelicAfterDamageGivenSteps(
        RelicModel relic,
        NativeBehaviorGraphAutoImportResult result)
    {
        if (relic.Id.Entry != "HAND_DRILL")
        {
            return null;
        }

        result.RecognizedCalls.Add("AfterDamageGiven -> synthetic block-break graph");
        return
        [
            BuildCompareStep("$state.DealerIsOwnerOrPetOwner", "eq", bool.TrueString, "dealer_matches"),
            BuildBranchStep(
                "dealer_matches",
                new[]
                {
                    BuildCompareStep("$state.TargetIsPlayer", "eq", bool.FalseString, "target_is_enemy"),
                    BuildBranchStep(
                        "target_is_enemy",
                        new[]
                        {
                            BuildCompareStep("$state.BlockWasBroken", "eq", bool.TrueString, "block_broken"),
                            BuildBranchStep(
                                "block_broken",
                                new[]
                                {
                                    BuildRelicPowerAction(relic, "Vulnerable", "2", "current_target")
                                })
                        })
                })
        ];
    }

    private List<NativeBehaviorStep>? TryBuildSyntheticRelicAfterDeathSteps(
        RelicModel relic,
        NativeBehaviorGraphAutoImportResult result)
    {
        if (relic.Id.Entry != "GREMLIN_HORN")
        {
            return null;
        }

        result.RecognizedCalls.Add("AfterDeath -> synthetic enemy death graph");
        return
        [
            BuildCompareStep("$state.DeathTargetIsEnemy", "eq", bool.TrueString, "enemy_died"),
            BuildBranchStep(
                "enemy_died",
                new[]
                {
                    BuildRelicAmountAction(relic, "player.gain_energy", "Energy", "1", "self"),
                    new NativeBehaviorStep
                    {
                        Kind = "combat.draw_cards",
                        Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["amount"] = "1"
                        }
                    }
                })
        ];
    }

    private List<NativeBehaviorStep>? TryBuildSyntheticRelicAfterPreventingBlockClearSteps(
        RelicModel relic,
        NativeBehaviorGraphAutoImportResult result)
    {
        if (relic.Id.Entry != "STURDY_CLAMP")
        {
            return null;
        }

        result.RecognizedCalls.Add("AfterPreventingBlockClear -> synthetic retained block graph");
        return
        [
            BuildCompareStep("$state.PreventerIsSelf", "eq", bool.TrueString, "preventer_matches"),
            BuildBranchStep(
                "preventer_matches",
                new[]
                {
                    BuildCompareStep("$state.CurrentBlock", "gt", "10", "block_above_cap"),
                    BuildBranchStep(
                        "block_above_cap",
                        new[]
                        {
                            new NativeBehaviorStep
                            {
                                Kind = "combat.lose_block",
                                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                                {
                                    ["amount"] = "$state.BlockAboveCap",
                                    ["target"] = "self",
                                    ["props"] = "none"
                                }
                            }
                        })
                })
        ];
    }

    private List<NativeBehaviorStep>? TryBuildSyntheticRelicAfterCurrentHpChangedSteps(
        RelicModel relic,
        NativeBehaviorGraphAutoImportResult result)
    {
        if (relic.Id.Entry != "RED_SKULL")
        {
            return null;
        }

        result.RecognizedCalls.Add("AfterCurrentHpChanged -> synthetic hp-threshold power graph");
        return
        [
            BuildCompareStep("$state.IsInCombat", "eq", bool.TrueString, "red_skull_in_combat"),
            BuildBranchStep(
                "red_skull_in_combat",
                new[]
                {
                    BuildCompareStep("$state.CurrentHpAboveThreshold", "eq", bool.TrueString, "red_skull_above_threshold"),
                    BuildBranchStep(
                        "red_skull_above_threshold",
                        new[]
                        {
                            BuildCompareStep("$state.StrengthApplied", "eq", bool.TrueString, "red_skull_remove_strength"),
                            BuildBranchStep(
                                "red_skull_remove_strength",
                                new[]
                                {
                                    BuildRelicPowerAction(relic, "Strength", "-3", "self"),
                                    new NativeBehaviorStep
                                    {
                                        Kind = "value.set",
                                        Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                                        {
                                            ["key"] = "StrengthApplied",
                                            ["value"] = bool.FalseString
                                        }
                                    }
                                })
                        },
                        new[]
                        {
                            BuildCompareStep("$state.StrengthApplied", "eq", bool.FalseString, "red_skull_add_strength"),
                            BuildBranchStep(
                                "red_skull_add_strength",
                                new[]
                                {
                                    BuildRelicPowerAction(relic, "Strength", "3", "self"),
                                    new NativeBehaviorStep
                                    {
                                        Kind = "value.set",
                                        Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                                        {
                                            ["key"] = "StrengthApplied",
                                            ["value"] = bool.TrueString
                                        }
                                    }
                                })
                        })
                })
        ];
    }

    private List<NativeBehaviorStep>? TryBuildSyntheticRelicAfterOrbChanneledSteps(
        RelicModel relic,
        NativeBehaviorGraphAutoImportResult result)
    {
        if (relic.Id.Entry != "METRONOME")
        {
            return null;
        }

        result.RecognizedCalls.Add("AfterOrbChanneled -> synthetic orb counter graph");
        return
        [
            BuildCompareStep("$state.HookPlayerIsOwner", "eq", bool.TrueString, "metronome_owner_orb"),
            BuildBranchStep(
                "metronome_owner_orb",
                new[]
                {
                    new NativeBehaviorStep
                    {
                        Kind = "value.add",
                        Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["key"] = "OrbsChanneled",
                            ["delta"] = "1"
                        }
                    },
                    BuildCompareStep("$state.OrbsChanneled", "eq", "$state.OrbCount", "metronome_threshold"),
                    BuildBranchStep(
                        "metronome_threshold",
                        new[]
                        {
                            BuildRelicAmountAction(relic, "combat.damage", "Damage", "30", "all_enemies")
                        })
                })
        ];
    }

    private List<NativeBehaviorStep>? TryBuildSyntheticRelicAfterPotionProcuredSteps(
        RelicModel relic,
        NativeBehaviorGraphAutoImportResult result)
    {
        if (relic.Id.Entry != "BELT_BUCKLE")
        {
            return null;
        }

        result.RecognizedCalls.Add("AfterPotionProcured -> synthetic dexterity removal graph");
        return
        [
            BuildCompareStep("$state.IsInCombat", "eq", bool.TrueString, "belt_buckle_in_combat"),
            BuildBranchStep(
                "belt_buckle_in_combat",
                new[]
                {
                    BuildCompareStep("$state.DexterityApplied", "eq", bool.TrueString, "belt_buckle_remove"),
                    BuildBranchStep(
                        "belt_buckle_remove",
                        new[]
                        {
                            BuildRelicPowerAction(relic, "Dexterity", "-2", "self"),
                            new NativeBehaviorStep
                            {
                                Kind = "value.set",
                                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                                {
                                    ["key"] = "DexterityApplied",
                                    ["value"] = bool.FalseString
                                }
                            }
                        })
                })
        ];
    }

    private List<NativeBehaviorStep>? TryBuildSyntheticRelicAfterPotionDiscardedSteps(
        RelicModel relic,
        NativeBehaviorGraphAutoImportResult result)
    {
        if (relic.Id.Entry != "BELT_BUCKLE")
        {
            return null;
        }

        result.RecognizedCalls.Add("AfterPotionDiscarded -> synthetic dexterity apply graph");
        return
        [
            BuildCompareStep("$state.IsInCombat", "eq", bool.TrueString, "belt_buckle_in_combat"),
            BuildBranchStep(
                "belt_buckle_in_combat",
                new[]
                {
                    BuildCompareStep("$state.OwnerPotionCount", "eq", "0", "belt_buckle_no_potions"),
                    BuildBranchStep(
                        "belt_buckle_no_potions",
                        new[]
                        {
                            BuildCompareStep("$state.DexterityApplied", "eq", bool.FalseString, "belt_buckle_apply"),
                            BuildBranchStep(
                                "belt_buckle_apply",
                                new[]
                                {
                                    BuildRelicPowerAction(relic, "Dexterity", "2", "self"),
                                    new NativeBehaviorStep
                                    {
                                        Kind = "value.set",
                                        Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                                        {
                                            ["key"] = "DexterityApplied",
                                            ["value"] = bool.TrueString
                                        }
                                    }
                                })
                        })
                })
        ];
    }

    private List<NativeBehaviorStep>? TryBuildSyntheticRelicAfterPreventingDeathSteps(
        RelicModel relic,
        NativeBehaviorGraphAutoImportResult result)
    {
        if (relic.Id.Entry != "LIZARD_TAIL")
        {
            return null;
        }

        result.RecognizedCalls.Add("AfterPreventingDeath -> synthetic revive heal graph");
        return
        [
            new NativeBehaviorStep
            {
                Kind = "value.set",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["key"] = "WasUsed",
                    ["value"] = bool.TrueString
                }
            },
            new NativeBehaviorStep
            {
                Kind = "combat.heal",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["target"] = "self",
                    ["amount"] = "$state.ReviveHealAmount"
                }
            }
        ];
    }

    private List<NativeBehaviorStep>? TryBuildSyntheticRelicAfterStarsSpentSteps(
        RelicModel relic,
        NativeBehaviorGraphAutoImportResult result)
    {
        if (relic.Id.Entry != "MINI_REGENT")
        {
            return null;
        }

        result.RecognizedCalls.Add("AfterStarsSpent -> synthetic once-per-turn power graph");
        return
        [
            BuildCompareStep("$state.HookPlayerIsOwner", "eq", bool.TrueString, "owner_spent_stars"),
            BuildBranchStep(
                "owner_spent_stars",
                new[]
                {
                    BuildCompareStep("$state.UsedThisTurn", "eq", bool.FalseString, "mini_regent_ready"),
                    BuildBranchStep(
                        "mini_regent_ready",
                        new[]
                        {
                            BuildRelicPowerAction(relic, "Strength", "1", "self")
                        })
                })
        ];
    }

    private List<NativeBehaviorStep>? TryBuildSyntheticRelicBeforeHandDrawSteps(
        RelicModel relic,
        NativeBehaviorGraphAutoImportResult result)
    {
        if (relic.Id.Entry != "NINJA_SCROLL")
        {
            return null;
        }

        result.RecognizedCalls.Add("BeforeHandDraw -> synthetic first-turn shiv graph");
        return
        [
            BuildCompareStep("$state.HookPlayerIsOwner", "eq", bool.TrueString, "owner_draw_phase"),
            BuildBranchStep(
                "owner_draw_phase",
                new[]
                {
                    BuildCompareStep("$state.CombatRound", "lte", "1", "first_round_draw"),
                    BuildBranchStep(
                        "first_round_draw",
                        new[]
                        {
                            new NativeBehaviorStep
                            {
                                Kind = "combat.create_card",
                                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                                {
                                    ["card_id"] = "SHIV",
                                    ["count"] = "3",
                                    ["target_pile"] = "Hand"
                                }
                            }
                        })
                })
        ];
    }

    private List<NativeBehaviorStep>? TryBuildSyntheticRelicBeforeCombatStartSteps(
        RelicModel relic,
        NativeBehaviorGraphAutoImportResult result)
    {
        List<NativeBehaviorStep>? steps = relic.Id.Entry switch
        {
            "BOUND_PHYLACTERY" => BuildBoundPhylacteryBeforeCombatStartSteps(relic),
            "FAKE_SNECKO_EYE" => BuildFakeSneckoEyeBeforeCombatStartSteps(),
            _ => null
        };

        if (steps != null)
        {
            result.RecognizedCalls.Add("BeforeCombatStart -> synthetic combat-start graph");
        }

        return steps;
    }

    private List<NativeBehaviorStep>? TryBuildSyntheticRelicAfterDiedToDoomSteps(
        RelicModel relic,
        NativeBehaviorGraphAutoImportResult result)
    {
        if (relic.Id.Entry != "BOOK_REPAIR_KNIFE")
        {
            return null;
        }

        result.RecognizedCalls.Add("AfterDiedToDoom -> combat.heal(state)");
        return
        [
            new NativeBehaviorStep
            {
                Kind = "combat.heal",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["target"] = "self",
                    ["amount"] = "$state.DoomHealAmount"
                }
            }
        ];
    }

    private List<NativeBehaviorStep>? TryBuildSyntheticRelicAfterRestSiteHealSteps(
        RelicModel relic,
        NativeBehaviorGraphAutoImportResult result)
    {
        if (relic.Id.Entry == "STONE_HUMIDIFIER")
        {
            result.RecognizedCalls.Add("AfterRestSiteHeal -> player.gain_max_hp");
            return
            [
                BuildRelicAmountAction(relic, "player.gain_max_hp", "MaxHp", "5", "self")
            ];
        }

        if (relic.Id.Entry != "REGAL_PILLOW")
        {
            return null;
        }

        result.RecognizedCalls.Add("AfterRestSiteHeal -> value.set(Status)");
        return
        [
            BuildCompareStep("$state.HookPlayerIsOwner", "eq", bool.TrueString, "rest_heal_owner"),
            BuildBranchStep(
                "rest_heal_owner",
                new[]
                {
                    new NativeBehaviorStep
                    {
                        Kind = "value.set",
                        Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["key"] = "Status",
                            ["value"] = "Normal"
                        }
                    }
                })
        ];
    }

    private List<NativeBehaviorStep>? TryBuildSyntheticRelicBeforeTurnEndSteps(
        RelicModel relic,
        NativeBehaviorGraphAutoImportResult result)
    {
        List<NativeBehaviorStep>? steps = relic.Id.Entry switch
        {
            "CLOAK_CLASP" => BuildCloakClaspBeforeTurnEndSteps(),
            "DIAMOND_DIADEM" => BuildDiamondDiademBeforeTurnEndSteps(),
            "FAKE_ORICHALCUM" => BuildOrichalcumBeforeTurnEndSteps(relic, "3"),
            "ORICHALCUM" => BuildOrichalcumBeforeTurnEndSteps(relic, "6"),
            "RIPPLE_BASIN" => BuildRippleBasinBeforeTurnEndSteps(relic),
            "SCREAMING_FLAGON" => BuildScreamingFlagonBeforeTurnEndSteps(relic),
            "STONE_CALENDAR" => BuildStoneCalendarBeforeTurnEndSteps(relic),
            _ => null
        };

        if (steps != null)
        {
            result.RecognizedCalls.Add("BeforeTurnEnd -> synthetic end-turn graph");
        }

        return steps;
    }

    private List<NativeBehaviorStep>? TryBuildSyntheticRelicAfterCardDiscardedSteps(
        RelicModel relic,
        NativeBehaviorGraphAutoImportResult result)
    {
        List<NativeBehaviorStep>? steps = relic.Id.Entry switch
        {
            "TINGSHA" => BuildTingshaAfterDiscardSteps(relic),
            "TOUGH_BANDAGES" => BuildToughBandagesAfterDiscardSteps(relic),
            _ => null
        };

        if (steps != null)
        {
            result.RecognizedCalls.Add("AfterCardDiscarded -> synthetic discard graph");
        }

        return steps;
    }

    private List<NativeBehaviorStep>? TryBuildSyntheticRelicAfterShuffleSteps(
        RelicModel relic,
        NativeBehaviorGraphAutoImportResult result)
    {
        List<NativeBehaviorStep>? steps = relic.Id.Entry switch
        {
            "PENDULUM" => BuildPendulumAfterShuffleSteps(),
            "THE_ABACUS" => BuildTheAbacusAfterShuffleSteps(relic),
            _ => null
        };

        if (steps != null)
        {
            result.RecognizedCalls.Add("AfterShuffle -> synthetic shuffle graph");
        }

        return steps;
    }

    private List<NativeBehaviorStep>? TryBuildSyntheticRelicAfterHandEmptiedSteps(
        RelicModel relic,
        NativeBehaviorGraphAutoImportResult result)
    {
        if (relic.Id.Entry != "UNCEASING_TOP")
        {
            return null;
        }

        result.RecognizedCalls.Add("AfterHandEmptied -> synthetic draw graph");
        return
        [
            BuildCompareStep("$state.HookPlayerIsOwner", "eq", bool.TrueString, "owner_hand_emptied"),
            BuildBranchStep(
                "owner_hand_emptied",
                new[]
                {
                    BuildCompareStep("$state.IsPlayPhase", "eq", bool.TrueString, "play_phase_active"),
                    BuildBranchStep(
                        "play_phase_active",
                        new[]
                        {
                            new NativeBehaviorStep
                            {
                                Kind = "combat.draw_cards",
                                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                                {
                                    ["amount"] = "1"
                                }
                            }
                        })
                })
        ];
    }

    private List<NativeBehaviorStep>? TryBuildSyntheticRelicCardRewardOptionSteps(
        RelicModel relic,
        string methodName,
        NativeBehaviorGraphAutoImportResult result)
    {
        List<NativeBehaviorStep>? steps = relic.Id.Entry switch
        {
            "FROZEN_EGG" when methodName == "TryModifyCardRewardOptionsLate" => BuildCardRewardUpgradeSteps("power", requireHookUpgradesEnabled: true),
            "MOLTEN_EGG" when methodName == "TryModifyCardRewardOptionsLate" => BuildCardRewardUpgradeSteps("attack", requireHookUpgradesEnabled: true),
            "TOXIC_EGG" when methodName == "TryModifyCardRewardOptionsLate" => BuildCardRewardUpgradeSteps("skill", requireHookUpgradesEnabled: true),
            "GLITTER" when methodName == "TryModifyCardRewardOptionsLate" => BuildCardRewardEnchantSteps(relic, "GLAM", "1", selection: "all"),
            "FRESNEL_LENS" when methodName == "TryModifyCardRewardOptionsLate" => BuildCardRewardEnchantSteps(relic, "NIMBLE", "NimbleAmount", selection: "all"),
            "WING_CHARM" when methodName == "TryModifyCardRewardOptionsLate" => BuildCardRewardEnchantSteps(relic, "SWIFT", "SwiftAmount", selection: "random_one"),
            "SILVER_CRUCIBLE" when methodName == "TryModifyCardRewardOptionsLate" => BuildSilverCrucibleRewardSteps(),
            "LAVA_LAMP" when methodName == "TryModifyCardRewardOptionsLate" => BuildLavaLampRewardSteps(),
            _ => null
        };

        if (steps != null)
        {
            result.RecognizedCalls.Add($"{methodName} -> synthetic card reward option graph");
        }

        return steps;
    }

    private List<NativeBehaviorStep>? TryBuildSyntheticRelicDamageAdditiveSteps(
        RelicModel relic,
        NativeBehaviorGraphAutoImportResult result)
    {
        List<NativeBehaviorStep>? steps = relic.Id.Entry switch
        {
            "STRIKE_DUMMY" => BuildStrikeDummyDamageAdditiveSteps(relic, fallbackAmount: "3", requireUpgraded: false),
            "FAKE_STRIKE_DUMMY" => BuildStrikeDummyDamageAdditiveSteps(relic, fallbackAmount: "1", requireUpgraded: false),
            "MINIATURE_CANNON" => BuildStrikeDummyDamageAdditiveSteps(relic, fallbackAmount: "3", requireUpgraded: true),
            "MYSTIC_LIGHTER" => BuildMysticLighterDamageAdditiveSteps(relic),
            _ => null
        };

        if (steps != null)
        {
            result.RecognizedCalls.Add("ModifyDamageAdditive -> synthetic damage additive graph");
        }

        return steps;
    }

    private List<NativeBehaviorStep>? TryBuildSyntheticRelicDamageMultiplicativeSteps(
        RelicModel relic,
        NativeBehaviorGraphAutoImportResult result)
    {
        List<NativeBehaviorStep>? steps = relic.Id.Entry switch
        {
            "PEN_NIB" => BuildPenNibDamageMultiplicativeSteps(),
            "UNDYING_SIGIL" => BuildUndyingSigilDamageMultiplicativeSteps(relic),
            "VITRUVIAN_MINION" => BuildVitruvianMinionDamageMultiplicativeSteps(),
            _ => null
        };

        if (steps != null)
        {
            result.RecognizedCalls.Add("ModifyDamageMultiplicative -> synthetic damage multiplier graph");
        }

        return steps;
    }

    private List<NativeBehaviorStep>? TryBuildSyntheticRelicBlockMultiplicativeSteps(
        RelicModel relic,
        NativeBehaviorGraphAutoImportResult result)
    {
        if (relic.Id.Entry != "VITRUVIAN_MINION")
        {
            return null;
        }

        result.RecognizedCalls.Add("ModifyBlockMultiplicative -> synthetic block multiplier graph");
        return
        [
            BuildCompareStep("$state.CardOwnerIsOwner", "eq", bool.TrueString, "minion_owner_card"),
            BuildBranchStep(
                "minion_owner_card",
                new[]
                {
                    BuildCompareStep("$state.CardHasMinionTag", "eq", bool.TrueString, "minion_tag"),
                    BuildBranchStep(
                        "minion_tag",
                        new[]
                        {
                            new NativeBehaviorStep
                            {
                                Kind = "modifier.block_multiplicative",
                                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                                {
                                    ["amount"] = "2"
                                }
                            }
                        })
                })
        ];
    }

    private List<NativeBehaviorStep>? TryBuildSyntheticRelicHpLostBeforeOstySteps(
        RelicModel relic,
        NativeBehaviorGraphAutoImportResult result)
    {
        if (relic.Id.Entry != "THE_BOOT")
        {
            return null;
        }

        result.RecognizedCalls.Add("ModifyHpLostBeforeOsty -> synthetic hp-loss-before graph");
        return
        [
            BuildCompareStep("$state.DealerIsOwnerCreature", "eq", bool.TrueString, "boot_owner_dealer"),
            BuildBranchStep(
                "boot_owner_dealer",
                new[]
                {
                    BuildCompareStep("$state.DamagePropsPoweredAttack", "eq", bool.TrueString, "boot_powered_attack"),
                    BuildBranchStep(
                        "boot_powered_attack",
                        new[]
                        {
                            BuildCompareStep("$state.modifier_base", "gte", "1", "boot_positive_damage"),
                            BuildBranchStep(
                                "boot_positive_damage",
                                new[]
                                {
                                    BuildCompareStep("$state.modifier_base", "lt", "$state.DamageMinimum", "boot_under_minimum"),
                                    BuildBranchStep(
                                        "boot_under_minimum",
                                        new[]
                                        {
                                            new NativeBehaviorStep
                                            {
                                                Kind = "value.set",
                                                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                                                {
                                                    ["key"] = "modifier_result",
                                                    ["value"] = "$state.DamageMinimum"
                                                }
                                            }
                                        })
                                })
                        })
                })
        ];
    }

    private List<NativeBehaviorStep>? TryBuildSyntheticRelicHpLostAfterOstySteps(
        RelicModel relic,
        NativeBehaviorGraphAutoImportResult result)
    {
        List<NativeBehaviorStep>? steps = relic.Id.Entry switch
        {
            "TUNGSTEN_ROD" => BuildTungstenRodHpLostAfterSteps(relic),
            "BEATING_REMNANT" => BuildBeatingRemnantHpLostAfterSteps(),
            _ => null
        };

        if (steps != null)
        {
            result.RecognizedCalls.Add("ModifyHpLostAfterOsty -> synthetic hp-loss-after graph");
        }

        return steps;
    }

    private List<NativeBehaviorStep>? TryBuildSyntheticRelicMerchantPriceSteps(
        RelicModel relic,
        NativeBehaviorGraphAutoImportResult result)
    {
        List<NativeBehaviorStep>? steps = relic.Id.Entry switch
        {
            "MEMBERSHIP_CARD" => BuildMerchantPriceSteps("0.5", requireLocalOwner: true),
            _ => null
        };

        if (steps != null)
        {
            result.RecognizedCalls.Add("ModifyMerchantPrice -> synthetic merchant price graph");
        }

        return steps;
    }

    private List<NativeBehaviorStep>? TryBuildSyntheticRelicUnknownRoomTypeSteps(
        RelicModel relic,
        NativeBehaviorGraphAutoImportResult result)
    {
        if (relic.Id.Entry != "JUZU_BRACELET")
        {
            return null;
        }

        result.RecognizedCalls.Add("ModifyUnknownMapPointRoomTypes -> synthetic room-type filter graph");
        return
        [
            new NativeBehaviorStep
            {
                Kind = "map.remove_unknown_room_type",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["room_type"] = RoomType.Monster.ToString()
                }
            }
        ];
    }

    private List<NativeBehaviorStep>? TryBuildSyntheticRelicBoolSteps(
        RelicModel relic,
        string methodName,
        NativeBehaviorGraphAutoImportResult result)
    {
        var shouldBlock = relic.Id.Entry switch
        {
            "ECTOPLASM" when methodName == "ShouldGainGold" => true,
            "ICE_CREAM" when methodName == "ShouldPlayerResetEnergy" => true,
            "RUNIC_PYRAMID" when methodName == "ShouldFlush" => true,
            "RINGING_TRIANGLE" when methodName == "ShouldFlush" => true,
            "SOZU" when methodName == "ShouldProcurePotion" => true,
            "WHITE_BEAST_STATUE" when methodName == "ShouldForcePotionReward" => true,
            "THE_COURIER" when methodName == "ShouldRefillMerchantEntry" => true,
            "MINIATURE_TENT" when methodName == "ShouldDisableRemainingRestSiteOptions" => true,
            _ => false
        };

        if (!shouldBlock)
        {
            return null;
        }

        result.RecognizedCalls.Add($"{methodName} -> value.set(hook_result)");
        return methodName switch
        {
            "ShouldPlayerResetEnergy" => BuildOwnerBoolResultSteps(bool.FalseString),
            "ShouldFlush" when relic.Id.Entry == "RUNIC_PYRAMID" => BuildOwnerBoolResultSteps(bool.FalseString),
            "ShouldFlush" when relic.Id.Entry == "RINGING_TRIANGLE" => BuildRingingTriangleShouldFlushSteps(),
            "ShouldRefillMerchantEntry" => BuildOwnerBoolResultSteps(bool.TrueString),
            "ShouldForcePotionReward" => BuildOwnerCombatRoomBoolResultSteps(),
            "ShouldDisableRemainingRestSiteOptions" => BuildOwnerBoolResultSteps(bool.FalseString),
            _ =>
            [
                new NativeBehaviorStep
                {
                    Kind = "value.set",
                    Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["key"] = "hook_result",
                        ["value"] = bool.FalseString
                    }
                }
            ]
        };
    }

    private List<NativeBehaviorStep>? TryBuildSyntheticRelicMapSteps(
        RelicModel relic,
        string methodName,
        NativeBehaviorGraphAutoImportResult result)
    {
        if (relic.Id.Entry != "GOLDEN_COMPASS" || methodName != "ModifyGeneratedMap")
        {
            return null;
        }

        result.RecognizedCalls.Add($"{methodName} -> map.replace_generated");
        return
        [
            BuildCompareStep("$state.GoldenPathAct", "eq", "$state.ActIndex", "golden_path_enabled"),
            BuildBranchStep(
                "golden_path_enabled",
                new[]
                {
                    new NativeBehaviorStep
                    {
                        Kind = "map.replace_generated",
                        Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["map_kind"] = "golden_path"
                        }
                    }
                })
        ];
    }

    private static List<NativeBehaviorStep> BuildRoomConditionalRewardSteps(string roomType, Dictionary<string, string> rewardParameters)
    {
        return
        [
            BuildCompareStep("$state.RoomType", "eq", roomType, "room_matches"),
            BuildBranchStep(
                "room_matches",
                new[]
                {
                    new NativeBehaviorStep
                    {
                        Kind = "reward.offer_custom",
                        Parameters = rewardParameters
                    }
                })
        ];
    }

    private List<NativeBehaviorStep> BuildAmethystAubergineRewardSteps(RelicModel relic)
    {
        var rewardParameters = BuildRelicDynamicValueParameters(relic, "amount", "Gold", "10");
        rewardParameters["reward_kind"] = "gold";
        rewardParameters["reward_count"] = "1";

        var rewardStep = new NativeBehaviorStep
        {
            Kind = "reward.offer_custom",
            Parameters = rewardParameters
        };

        return
        [
            BuildCompareStep("$state.RoomIsCombat", "eq", bool.TrueString, "room_is_combat"),
            BuildBranchStep(
                "room_is_combat",
                new[]
                {
                    BuildCompareStep("$state.RoomType", "eq", "Boss", "room_is_boss"),
                    BuildBranchStep(
                        "room_is_boss",
                        new[]
                        {
                            BuildCompareStep("$state.IsFinalAct", "eq", bool.TrueString, "is_final_boss_act"),
                            BuildBranchStep("is_final_boss_act", Array.Empty<NativeBehaviorStep>(), new[] { rewardStep })
                        },
                        new[] { rewardStep })
                })
        ];
    }

    private List<NativeBehaviorStep> BuildLavaRockRewardSteps(RelicModel relic)
    {
        var rewardParameters = BuildRelicDynamicValueParameters(relic, "reward_count", "Relics", "2");
        rewardParameters["reward_kind"] = "relic";

        return
        [
            BuildCompareStep("$state.RoomType", "eq", "Boss", "room_is_boss"),
            BuildBranchStep(
                "room_is_boss",
                new[]
                {
                    BuildCompareStep("$state.CurrentActIndex", "eq", "0", "first_act"),
                    BuildBranchStep(
                        "first_act",
                        new[]
                        {
                            BuildCompareStep("$state.HasTriggered", "eq", bool.FalseString, "lava_rock_ready"),
                            BuildBranchStep(
                                "lava_rock_ready",
                                new[]
                                {
                                    new NativeBehaviorStep
                                    {
                                        Kind = "reward.offer_custom",
                                        Parameters = rewardParameters
                                    }
                                })
                        })
                })
        ];
    }

    private List<NativeBehaviorStep> BuildWongosMysteryTicketRewardSteps(RelicModel relic)
    {
        var rewardParameters = BuildRelicDynamicValueParameters(relic, "reward_count", "Repeat", "3");
        rewardParameters["reward_kind"] = "relic";

        return
        [
            BuildCompareStep("$state.RoomIsCombat", "eq", bool.TrueString, "room_is_combat"),
            BuildBranchStep(
                "room_is_combat",
                new[]
                {
                    BuildCompareStep("$state.GaveRelic", "eq", bool.FalseString, "ticket_unused"),
                    BuildBranchStep(
                        "ticket_unused",
                        new[]
                        {
                            BuildCompareStep("$state.RemainingCombats", "lte", "0", "ticket_ready"),
                            BuildBranchStep(
                                "ticket_ready",
                                new[]
                                {
                                    new NativeBehaviorStep
                                    {
                                        Kind = "reward.offer_custom",
                                        Parameters = rewardParameters
                                    }
                                })
                        })
                })
        ];
    }

    private static NativeBehaviorStep BuildCompareStep(string left, string comparisonOperator, string right, string resultKey)
    {
        return new NativeBehaviorStep
        {
            Kind = "value.compare",
            Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["left"] = left,
                ["right"] = right,
                ["operator"] = comparisonOperator,
                ["result_key"] = resultKey
            }
        };
    }

    private static NativeBehaviorStep BuildBranchStep(
        string conditionKey,
        IReadOnlyList<NativeBehaviorStep> trueBranch,
        IReadOnlyList<NativeBehaviorStep>? falseBranch = null)
    {
        return new NativeBehaviorStep
        {
            Kind = "flow.branch",
            Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["condition_key"] = conditionKey
            },
            TrueBranch = trueBranch.ToList(),
            FalseBranch = falseBranch?.ToList() ?? new List<NativeBehaviorStep>()
        };
    }

    private Dictionary<string, string> BuildRelicDynamicValueParameters(
        RelicModel relic,
        string propertyKey,
        string preferredVarName,
        string fallbackLiteral)
    {
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [propertyKey] = fallbackLiteral
        };

        if (TryGetDynamicVar(relic, preferredVarName, out _))
        {
            PopulateDynamicValueParameters(parameters, propertyKey, relic, preferredVarName, ResolveDynamicAmount(relic, preferredVarName, fallbackLiteral));
        }
        else if (TryGetDynamicVar(relic, "Amount", out _))
        {
            PopulateDynamicValueParameters(parameters, propertyKey, relic, "Amount", ResolveDynamicAmount(relic, "Amount", fallbackLiteral));
        }

        return parameters;
    }

    private static List<NativeBehaviorStep> BuildCardRewardUpgradeSteps(string cardTypeScope, bool requireHookUpgradesEnabled)
    {
        return
        [
            new NativeBehaviorStep
            {
                Kind = "reward.card_options_upgrade",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["card_type_scope"] = cardTypeScope,
                    ["require_hook_upgrades_enabled"] = requireHookUpgradesEnabled.ToString()
                }
            }
        ];
    }

    private List<NativeBehaviorStep> BuildCardRewardEnchantSteps(
        RelicModel relic,
        string enchantmentId,
        string amountVarOrLiteral,
        string selection)
    {
        var parameters = char.IsLetter(amountVarOrLiteral[0])
            ? BuildRelicDynamicValueParameters(relic, "amount", amountVarOrLiteral, "1")
            : new Dictionary<string, string>(StringComparer.Ordinal) { ["amount"] = amountVarOrLiteral };
        parameters["enchantment_id"] = enchantmentId;
        parameters["selection"] = selection;

        return
        [
            new NativeBehaviorStep
            {
                Kind = "reward.card_options_enchant",
                Parameters = parameters
            }
        ];
    }

    private static List<NativeBehaviorStep> BuildSilverCrucibleRewardSteps()
    {
        return
        [
            BuildCompareStep("$state.TimesUsed", "lt", "$state.Cards", "silver_crucible_has_uses"),
            BuildBranchStep(
                "silver_crucible_has_uses",
                new[]
                {
                    new NativeBehaviorStep
                    {
                        Kind = "reward.card_options_upgrade",
                        Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["card_type_scope"] = "any",
                            ["require_hook_upgrades_enabled"] = bool.FalseString
                        }
                    }
                })
        ];
    }

    private static List<NativeBehaviorStep> BuildLavaLampRewardSteps()
    {
        return
        [
            BuildCompareStep("$state.CurrentRoomIsCombat", "eq", bool.TrueString, "lava_lamp_in_combat_room"),
            BuildBranchStep(
                "lava_lamp_in_combat_room",
                new[]
                {
                    BuildCompareStep("$state.TookDamageThisCombat", "eq", bool.FalseString, "lava_lamp_safe_combat"),
                    BuildBranchStep(
                        "lava_lamp_safe_combat",
                        new[]
                        {
                            new NativeBehaviorStep
                            {
                                Kind = "reward.card_options_upgrade",
                                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                                {
                                    ["card_type_scope"] = "any",
                                    ["require_hook_upgrades_enabled"] = bool.FalseString
                                }
                            }
                        })
                })
        ];
    }

    private static List<NativeBehaviorStep> BuildCloakClaspBeforeTurnEndSteps()
    {
        return
        [
            BuildCompareStep("$state.IsOwnerTurnSide", "eq", bool.TrueString, "owner_turn_end"),
            BuildBranchStep(
                "owner_turn_end",
                new[]
                {
                    BuildCompareStep("$state.HandCount", "gt", "0", "hand_not_empty"),
                    BuildBranchStep(
                        "hand_not_empty",
                        new[]
                        {
                            new NativeBehaviorStep
                            {
                                Kind = "combat.gain_block",
                                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                                {
                                    ["amount"] = "$state.HandCount",
                                    ["target"] = "self",
                                    ["props"] = "Unpowered"
                                }
                            }
                        })
                })
        ];
    }

    private static List<NativeBehaviorStep> BuildDiamondDiademBeforeTurnEndSteps()
    {
        return
        [
            BuildCompareStep("$state.IsOwnerTurnSide", "eq", bool.TrueString, "owner_turn_end"),
            BuildBranchStep(
                "owner_turn_end",
                new[]
                {
                    BuildCompareStep("$state.DisplayAmount", "lte", "$state.CardThreshold", "diamond_diadem_ready"),
                    BuildBranchStep(
                        "diamond_diadem_ready",
                        new[]
                        {
                            new NativeBehaviorStep
                            {
                                Kind = "combat.apply_power",
                                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                                {
                                    ["power_id"] = "DiamondDiademPower",
                                    ["amount"] = "1",
                                    ["target"] = "self"
                                }
                            }
                        })
                })
        ];
    }

    private List<NativeBehaviorStep> BuildOrichalcumBeforeTurnEndSteps(RelicModel relic, string fallbackAmount)
    {
        return
        [
            BuildCompareStep("$state.ShouldTrigger", "eq", bool.TrueString, "orichalcum_should_trigger"),
            BuildBranchStep(
                "orichalcum_should_trigger",
                new[]
                {
                    BuildRelicAmountAction(relic, "combat.gain_block", "Block", fallbackAmount, "self")
                })
        ];
    }

    private List<NativeBehaviorStep> BuildRippleBasinBeforeTurnEndSteps(RelicModel relic)
    {
        return
        [
            BuildCompareStep("$state.IsOwnerTurnSide", "eq", bool.TrueString, "owner_turn_end"),
            BuildBranchStep(
                "owner_turn_end",
                new[]
                {
                    BuildCompareStep("$state.PlayedAttackThisTurn", "eq", bool.FalseString, "no_attack_played"),
                    BuildBranchStep(
                        "no_attack_played",
                        new[]
                        {
                            BuildRelicAmountAction(relic, "combat.gain_block", "Block", "4", "self")
                        })
                })
        ];
    }

    private List<NativeBehaviorStep> BuildScreamingFlagonBeforeTurnEndSteps(RelicModel relic)
    {
        return
        [
            BuildCompareStep("$state.TurnSide", "eq", "Player", "player_turn_end"),
            BuildBranchStep(
                "player_turn_end",
                new[]
                {
                    BuildCompareStep("$state.HandCount", "eq", "0", "hand_empty"),
                    BuildBranchStep(
                        "hand_empty",
                        new[]
                        {
                            BuildRelicAmountAction(relic, "combat.damage", "Damage", "20", "all_enemies")
                        })
                })
        ];
    }

    private List<NativeBehaviorStep> BuildStoneCalendarBeforeTurnEndSteps(RelicModel relic)
    {
        return
        [
            BuildCompareStep("$state.IsOwnerTurnSide", "eq", bool.TrueString, "owner_turn_end"),
            BuildBranchStep(
                "owner_turn_end",
                new[]
                {
                    BuildCompareStep("$state.CombatRound", "eq", "$state.DamageTurn", "stone_calendar_ready"),
                    BuildBranchStep(
                        "stone_calendar_ready",
                        new[]
                        {
                            BuildRelicAmountAction(relic, "combat.damage", "Damage", "52", "all_enemies")
                        })
                })
        ];
    }

    private List<NativeBehaviorStep> BuildTingshaAfterDiscardSteps(RelicModel relic)
    {
        return
        [
            BuildCompareStep("$state.DiscardingOwnerIsOwner", "eq", bool.TrueString, "owner_discarded"),
            BuildBranchStep(
                "owner_discarded",
                new[]
                {
                    BuildCompareStep("$state.IsOwnerCurrentSide", "eq", bool.TrueString, "owner_side_active"),
                    BuildBranchStep(
                        "owner_side_active",
                        new[]
                        {
                            BuildRelicAmountAction(relic, "combat.damage", "Damage", "3", "current_target")
                        })
                })
        ];
    }

    private List<NativeBehaviorStep> BuildToughBandagesAfterDiscardSteps(RelicModel relic)
    {
        return
        [
            BuildCompareStep("$state.DiscardingOwnerIsOwner", "eq", bool.TrueString, "owner_discarded"),
            BuildBranchStep(
                "owner_discarded",
                new[]
                {
                    BuildCompareStep("$state.IsOwnerCurrentSide", "eq", bool.TrueString, "owner_side_active"),
                    BuildBranchStep(
                        "owner_side_active",
                        new[]
                        {
                            BuildRelicAmountAction(relic, "combat.gain_block", "Block", "3", "self")
                        })
                })
        ];
    }

    private static List<NativeBehaviorStep> BuildPendulumAfterShuffleSteps()
    {
        return
        [
            BuildCompareStep("$state.HookPlayerIsOwner", "eq", bool.TrueString, "owner_shuffled"),
            BuildBranchStep(
                "owner_shuffled",
                new[]
                {
                    new NativeBehaviorStep
                    {
                        Kind = "combat.draw_cards",
                        Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["amount"] = "1"
                        }
                    }
                })
        ];
    }

    private static List<NativeBehaviorStep> BuildOwnerBoolResultSteps(string boolLiteral)
    {
        return
        [
            new NativeBehaviorStep
            {
                Kind = "value.set",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["key"] = "hook_result",
                    ["value"] = boolLiteral
                }
            }
        ];
    }

    private static List<NativeBehaviorStep> BuildOwnerCombatRoomBoolResultSteps()
    {
        return
        [
            BuildCompareStep("$state.CurrentRoomIsCombat", "eq", bool.TrueString, "combat_room"),
            BuildBranchStep(
                "combat_room",
                new[]
                {
                    new NativeBehaviorStep
                    {
                        Kind = "value.set",
                        Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["key"] = "hook_result",
                            ["value"] = bool.TrueString
                        }
                    }
                },
                new[]
                {
                    new NativeBehaviorStep
                    {
                        Kind = "value.set",
                        Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["key"] = "hook_result",
                            ["value"] = bool.FalseString
                        }
                    }
                })
        ];
    }

    private List<NativeBehaviorStep> BuildStrikeDummyDamageAdditiveSteps(RelicModel relic, string fallbackAmount, bool requireUpgraded)
    {
        var amountStep = BuildRelicAmountAction(relic, "modifier.damage_additive", "ExtraDamage", fallbackAmount, "self");
        return
        [
            BuildCompareStep("$state.DamagePropsPoweredAttack", "eq", bool.TrueString, "powered_attack"),
            BuildBranchStep(
                "powered_attack",
                new[]
                {
                    BuildCompareStep("$state.CardHasStrike", "eq", bool.TrueString, "strike_card"),
                    BuildBranchStep(
                        "strike_card",
                        requireUpgraded
                            ? new[]
                            {
                                BuildCompareStep("$state.CardIsUpgraded", "eq", bool.TrueString, "upgraded_card"),
                                BuildBranchStep(
                                    "upgraded_card",
                                    new[]
                                    {
                                        BuildCompareStep("$state.DealerIsOwnerOrCardOwner", "eq", bool.TrueString, "owner_source"),
                                        BuildBranchStep("owner_source", new[] { amountStep })
                                    })
                            }
                            : new[]
                            {
                                BuildCompareStep("$state.DealerIsOwnerOrCardOwner", "eq", bool.TrueString, "owner_source"),
                                BuildBranchStep("owner_source", new[] { amountStep })
                            })
                })
        ];
    }

    private static List<NativeBehaviorStep> BuildPenNibDamageMultiplicativeSteps()
    {
        return
        [
            BuildCompareStep("$state.DamagePropsPoweredAttack", "eq", bool.TrueString, "pen_nib_attack"),
            BuildBranchStep(
                "pen_nib_attack",
                new[]
                {
                    BuildCompareStep("$state.DealerIsOwnerOrOsty", "eq", bool.TrueString, "pen_nib_owner"),
                    BuildBranchStep(
                        "pen_nib_owner",
                        new[]
                        {
                            BuildCompareStep("$state.ShouldDoubleDamage", "eq", bool.TrueString, "pen_nib_double"),
                            BuildBranchStep(
                                "pen_nib_double",
                                new[]
                                {
                                    new NativeBehaviorStep
                                    {
                                        Kind = "modifier.damage_multiplicative",
                                        Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                                        {
                                            ["amount"] = "2"
                                        }
                                    }
                                })
                        })
                })
        ];
    }

    private List<NativeBehaviorStep> BuildUndyingSigilDamageMultiplicativeSteps(RelicModel relic)
    {
        return
        [
            BuildCompareStep("$state.DealerExists", "eq", bool.TrueString, "sigil_dealer_exists"),
            BuildBranchStep(
                "sigil_dealer_exists",
                new[]
                {
                    BuildCompareStep("$state.DamagePropsPoweredAttack", "eq", bool.TrueString, "sigil_attack"),
                    BuildBranchStep(
                        "sigil_attack",
                        new[]
                        {
                            BuildCompareStep("$state.TargetIsOwner", "eq", bool.TrueString, "sigil_owner_target"),
                            BuildBranchStep(
                                "sigil_owner_target",
                                new[]
                                {
                                    BuildCompareStep("$state.DealerIsOwnerCreature", "eq", bool.FalseString, "sigil_not_self"),
                                    BuildBranchStep(
                                        "sigil_not_self",
                                        new[]
                                        {
                                            BuildCompareStep("$state.DealerCurrentHpAtOrBelowDoom", "eq", bool.TrueString, "sigil_doom_threshold"),
                                            BuildBranchStep(
                                                "sigil_doom_threshold",
                                                new[]
                                                {
                                                    BuildRelicAmountAction(relic, "modifier.damage_multiplicative", "DamageDecrease", "0.5", "self")
                                                })
                                        })
                                })
                        })
                })
        ];
    }

    private static List<NativeBehaviorStep> BuildVitruvianMinionDamageMultiplicativeSteps()
    {
        return
        [
            BuildCompareStep("$state.CardOwnerIsOwner", "eq", bool.TrueString, "minion_owner_card"),
            BuildBranchStep(
                "minion_owner_card",
                new[]
                {
                    BuildCompareStep("$state.CardHasMinionTag", "eq", bool.TrueString, "minion_tag"),
                    BuildBranchStep(
                        "minion_tag",
                        new[]
                        {
                            new NativeBehaviorStep
                            {
                                Kind = "modifier.damage_multiplicative",
                                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                                {
                                    ["amount"] = "2"
                                }
                            }
                        })
                })
        ];
    }

    private List<NativeBehaviorStep> BuildMysticLighterDamageAdditiveSteps(RelicModel relic)
    {
        return
        [
            BuildCompareStep("$state.DamagePropsPoweredAttack", "eq", bool.TrueString, "powered_attack"),
            BuildBranchStep(
                "powered_attack",
                new[]
                {
                    BuildCompareStep("$state.CardHasEnchantment", "eq", bool.TrueString, "enchanted_card"),
                    BuildBranchStep(
                        "enchanted_card",
                        new[]
                        {
                            BuildCompareStep("$state.CardOwnerIsOwner", "eq", bool.TrueString, "owner_card"),
                            BuildBranchStep("owner_card", new[] { BuildRelicAmountAction(relic, "modifier.damage_additive", "Damage", "9", "self") })
                        })
                })
        ];
    }

    private List<NativeBehaviorStep> BuildTungstenRodHpLostAfterSteps(RelicModel relic)
    {
        return
        [
            BuildCompareStep("$state.TargetIsOwner", "eq", bool.TrueString, "tungsten_target_owner"),
            BuildBranchStep(
                "tungsten_target_owner",
                new[]
                {
                    new NativeBehaviorStep
                    {
                        Kind = "value.set",
                        Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["key"] = "modifier_result",
                            ["value"] = "$state.modifier_base"
                        }
                    },
                    new NativeBehaviorStep
                    {
                        Kind = "value.add",
                        Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["key"] = "modifier_result",
                            ["delta"] = "$state.HpLossReductionNegated"
                        }
                    }
                })
        ];
    }

    private static List<NativeBehaviorStep> BuildBeatingRemnantHpLostAfterSteps()
    {
        return
        [
            BuildCompareStep("$state.TargetIsOwner", "eq", bool.TrueString, "beating_target_owner"),
            BuildBranchStep(
                "beating_target_owner",
                new[]
                {
                    BuildCompareStep("$state.modifier_base", "gt", "$state.RemainingMaxHpLoss", "beating_cap_hit"),
                    BuildBranchStep(
                        "beating_cap_hit",
                        new[]
                        {
                            new NativeBehaviorStep
                            {
                                Kind = "value.set",
                                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                                {
                                    ["key"] = "modifier_result",
                                    ["value"] = "$state.RemainingMaxHpLoss"
                                }
                            }
                        })
                })
        ];
    }

    private static List<NativeBehaviorStep> BuildMerchantPriceSteps(string factor, bool requireLocalOwner)
    {
        var trueBranch = new List<NativeBehaviorStep>
        {
            new NativeBehaviorStep
            {
                Kind = "value.set",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["key"] = "modifier_result",
                    ["value"] = "$state.modifier_base"
                }
            },
            new NativeBehaviorStep
            {
                Kind = "value.multiply",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["key"] = "modifier_result",
                    ["factor"] = factor
                }
            }
        };

        if (!requireLocalOwner)
        {
            return
            [
                BuildCompareStep("$state.HookPlayerIsOwner", "eq", bool.TrueString, "owner_merchant_price"),
                BuildBranchStep("owner_merchant_price", trueBranch)
            ];
        }

        return
        [
            BuildCompareStep("$state.HookPlayerIsOwner", "eq", bool.TrueString, "owner_merchant_price"),
            BuildBranchStep(
                "owner_merchant_price",
                new[]
                {
                    BuildCompareStep("$state.IsLocalOwner", "eq", bool.TrueString, "local_owner_merchant"),
                    BuildBranchStep("local_owner_merchant", trueBranch)
                })
        ];
    }

    private static List<NativeBehaviorStep> BuildRingingTriangleShouldFlushSteps()
    {
        return
        [
            BuildCompareStep("$state.CombatRound", "gt", "1", "round_above_one"),
            BuildBranchStep(
                "round_above_one",
                new[]
                {
                    new NativeBehaviorStep
                    {
                        Kind = "value.set",
                        Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["key"] = "hook_result",
                            ["value"] = bool.TrueString
                        }
                    }
                },
                new[]
                {
                    new NativeBehaviorStep
                    {
                        Kind = "value.set",
                        Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["key"] = "hook_result",
                            ["value"] = bool.FalseString
                        }
                    }
                })
        ];
    }

    private List<NativeBehaviorStep> BuildBeltBuckleAfterObtainedSteps(RelicModel relic)
    {
        return
        [
            BuildCompareStep("$state.IsInCombat", "eq", bool.TrueString, "belt_buckle_in_combat"),
            BuildBranchStep(
                "belt_buckle_in_combat",
                new[]
                {
                    BuildCompareStep("$state.OwnerPotionCount", "eq", "0", "belt_buckle_no_potions"),
                    BuildBranchStep(
                        "belt_buckle_no_potions",
                        new[]
                        {
                            BuildRelicPowerAction(relic, "Dexterity", "2", "self"),
                            new NativeBehaviorStep
                            {
                                Kind = "value.set",
                                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                                {
                                    ["key"] = "DexterityApplied",
                                    ["value"] = bool.TrueString
                                }
                            }
                        })
                })
        ];
    }

    private static List<NativeBehaviorStep> BuildDeckEnchantAfterObtainedSteps(string enchantmentId, string amount, bool selectAll)
    {
        return
        [
            new NativeBehaviorStep
            {
                Kind = "card.select_cards",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["state_key"] = "selected_cards",
                    ["selection_mode"] = "simple_grid",
                    ["source_pile"] = PileType.Deck.ToString(),
                    ["count"] = selectAll ? "99" : "1",
                    ["prompt_kind"] = "enchant",
                    ["allow_cancel"] = bool.FalseString
                }
            },
            new NativeBehaviorStep
            {
                Kind = "card.enchant",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["card_state_key"] = "selected_cards",
                    ["enchantment_id"] = enchantmentId,
                    ["amount"] = amount
                }
            }
        ];
    }

    private static List<NativeBehaviorStep> BuildAddPetAfterObtainedSteps(string monsterId)
    {
        return
        [
            new NativeBehaviorStep
            {
                Kind = "player.add_pet",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["monster_id"] = monsterId
                }
            }
        ];
    }

    private List<NativeBehaviorStep> BuildTheAbacusAfterShuffleSteps(RelicModel relic)
    {
        return
        [
            BuildCompareStep("$state.HookPlayerIsOwner", "eq", bool.TrueString, "owner_shuffled"),
            BuildBranchStep(
                "owner_shuffled",
                new[]
                {
                    BuildRelicAmountAction(relic, "combat.gain_block", "Block", "6", "self")
                })
        ];
    }

    private List<NativeBehaviorStep> BuildBoundPhylacteryBeforeCombatStartSteps(RelicModel relic)
    {
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["monster_id"] = "Osty"
        };

        return
        [
            new NativeBehaviorStep
            {
                Kind = "player.add_pet",
                Parameters = parameters
            }
        ];
    }

    private static List<NativeBehaviorStep> BuildFakeSneckoEyeBeforeCombatStartSteps()
    {
        return
        [
            new NativeBehaviorStep
            {
                Kind = "combat.apply_power",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["power_id"] = "ConfusedPower",
                    ["amount"] = "1",
                    ["target"] = "self"
                }
            }
        ];
    }

    private List<NativeBehaviorStep> BuildRoundConditionalRelicSteps(int roundNumber, params NativeBehaviorStep[] trueBranchSteps)
    {
        return
        [
            BuildCompareStep("$state.CombatRound", "eq", roundNumber.ToString(CultureInfo.InvariantCulture), "round_matches"),
            BuildBranchStep("round_matches", trueBranchSteps)
        ];
    }

    private NativeBehaviorStep BuildRelicAmountAction(
        RelicModel relic,
        string kind,
        string preferredVarName,
        string fallbackLiteral,
        string target)
    {
        var parameters = BuildRelicDynamicValueParameters(relic, "amount", preferredVarName, fallbackLiteral);
        parameters["target"] = target;
        return new NativeBehaviorStep
        {
            Kind = kind,
            Parameters = parameters
        };
    }

    private NativeBehaviorStep BuildRelicPowerAction(
        RelicModel relic,
        string powerTypeName,
        string fallbackAmount,
        string target)
    {
        var parameters = BuildRelicDynamicValueParameters(relic, "amount", powerTypeName, fallbackAmount);
        parameters["power_id"] = powerTypeName;
        parameters["target"] = target;
        return new NativeBehaviorStep
        {
            Kind = "combat.apply_power",
            Parameters = parameters
        };
    }

    private List<NativeBehaviorStep> BuildSparklingRougePowerSteps(RelicModel relic)
    {
        var strengthParameters = BuildRelicDynamicValueParameters(relic, "amount", "Strength", "1");
        strengthParameters["power_id"] = "STRENGTH";
        strengthParameters["target"] = "self";

        var dexterityParameters = BuildRelicDynamicValueParameters(relic, "amount", "Dexterity", "1");
        dexterityParameters["power_id"] = "DEXTERITY";
        dexterityParameters["target"] = "self";

        return
        [
            new NativeBehaviorStep
            {
                Kind = "combat.apply_power",
                Parameters = strengthParameters
            },
            new NativeBehaviorStep
            {
                Kind = "combat.apply_power",
                Parameters = dexterityParameters
            }
        ];
    }

    private Dictionary<string, string> BuildEnchantmentModifierParameters(EnchantmentModel enchantment, string preferredVarName, string fallbackLiteral)
    {
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["amount"] = fallbackLiteral
        };

        if (TryGetDynamicVar(enchantment, preferredVarName, out _))
        {
            PopulateDynamicValueParameters(parameters, "amount", enchantment, preferredVarName, ResolveDynamicAmount(enchantment, preferredVarName, fallbackLiteral));
            return parameters;
        }

        if (!string.Equals(preferredVarName, "Amount", StringComparison.OrdinalIgnoreCase) &&
            TryGetDynamicVar(enchantment, "Amount", out _))
        {
            PopulateDynamicValueParameters(parameters, "amount", enchantment, "Amount", ResolveDynamicAmount(enchantment, "Amount", fallbackLiteral));
        }

        return parameters;
    }

    private Dictionary<string, string> BuildRelicNumericModifierParameters(
        RelicModel relic,
        string preferredVarName,
        string fallbackLiteral,
        bool isNegativeDelta = false)
    {
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["amount"] = isNegativeDelta && !fallbackLiteral.StartsWith("-", StringComparison.Ordinal) ? "-" + fallbackLiteral : fallbackLiteral
        };

        if (TryGetDynamicVar(relic, preferredVarName, out _))
        {
            PopulateDynamicValueParameters(parameters, "amount", relic, preferredVarName, ResolveDynamicAmount(relic, preferredVarName, fallbackLiteral));
        }
        else if (TryGetDynamicVar(relic, "Amount", out _))
        {
            PopulateDynamicValueParameters(parameters, "amount", relic, "Amount", ResolveDynamicAmount(relic, "Amount", fallbackLiteral));
        }

        if (isNegativeDelta)
        {
            if (parameters.TryGetValue("amount_var_name", out var varName) && !string.IsNullOrWhiteSpace(varName))
            {
                parameters["amount_template"] = $"-{{{varName}:diff()}}";
            }
            else if (parameters.TryGetValue("amount", out var literalAmount) && !literalAmount.StartsWith("-", StringComparison.Ordinal))
            {
                parameters["amount"] = "-" + literalAmount;
            }
        }

        return parameters;
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
        string? lastStringConstant = null;
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
                            Int32ArgumentHint = lastInt32Constant,
                            StringArgumentHint = lastStringConstant
                        });
                    }
                }
                catch
                {
                    // Ignore tokens that cannot be resolved in the current context.
                }

                lastInt32Constant = null;
                lastStringConstant = null;
                continue;
            }

            if (TryResolveInt32Constant(opcode, il, index, out var int32Constant))
            {
                lastInt32Constant = int32Constant;
            }
            else if (opcode == OpCodes.Ldstr)
            {
                try
                {
                    var token = BitConverter.ToInt32(il, index);
                    lastStringConstant = implementationMethod.Module.ResolveString(token);
                }
                catch
                {
                    lastStringConstant = null;
                }
            }
            else if (opcode != OpCodes.Nop)
            {
                lastInt32Constant = null;
                lastStringConstant = null;
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
