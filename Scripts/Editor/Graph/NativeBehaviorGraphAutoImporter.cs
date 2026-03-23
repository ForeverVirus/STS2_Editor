using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
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
        var card = ModelDb.AllCards.FirstOrDefault(candidate => string.Equals(candidate.Id.Entry, entityId, StringComparison.Ordinal));
        if (card == null)
        {
            return SetUnsupported(result, $"Could not resolve runtime card '{entityId}'.");
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
        return FinalizeTranslation(ModStudioEntityKind.Card, entityId, card.Title, steps, "card.on_play", result);
    }

    private bool TryCreatePotionGraph(string entityId, NativeBehaviorGraphAutoImportResult result)
    {
        var potion = ModelDb.AllPotions.FirstOrDefault(candidate => string.Equals(candidate.Id.Entry, entityId, StringComparison.Ordinal));
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
        return FinalizeTranslation(ModStudioEntityKind.Potion, entityId, potion.Title.GetRawText(), steps, "potion.on_use", result);
    }

    private bool TryCreateRelicGraph(string entityId, NativeBehaviorGraphAutoImportResult result)
    {
        var relic = ModelDb.AllRelics.FirstOrDefault(candidate => string.Equals(candidate.Id.Entry, entityId, StringComparison.Ordinal));
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

            return FinalizeTranslation(ModStudioEntityKind.Relic, entityId, relic.Title.GetRawText(), steps, candidate.TriggerId, result);
        }

        return SetUnsupported(result, $"Relic '{entityId}' does not currently map to a supported graph hook trigger.");
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
        foreach (var calledMethod in EnumerateCalledMethods(method))
        {
            if (TryTranslateRecognizedCall(model, calledMethod, defaultTargetSelector, defaultSelfSelector, out var step, out var callLabel))
            {
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

        if (steps.Count == 0 && result.UnsupportedCalls.Count > 0)
        {
            result.Summary = $"Trigger '{triggerId}' only contained unsupported native gameplay calls.";
        }

        return steps;
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
        out NativeBehaviorStep step,
        out string callLabel)
    {
        step = new NativeBehaviorStep();
        callLabel = $"{method.DeclaringType?.Name}.{method.Name}";

        if (method.DeclaringType?.Name == "DamageCmd" && string.Equals(method.Name, "Attack", StringComparison.Ordinal))
        {
            step = new NativeBehaviorStep
            {
                Kind = "combat.damage",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["amount"] = ResolveDynamicAmount(model, "Damage", "0"),
                    ["target"] = defaultTargetSelector,
                    ["props"] = ResolveDamageProps(model)
                }
            };
            return true;
        }

        if (method.DeclaringType?.Name == "CreatureCmd" && string.Equals(method.Name, "GainBlock", StringComparison.Ordinal))
        {
            step = new NativeBehaviorStep
            {
                Kind = "combat.gain_block",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["amount"] = ResolveDynamicAmount(model, "Block", "0"),
                    ["target"] = defaultSelfSelector,
                    ["props"] = "none"
                }
            };
            return true;
        }

        if (method.DeclaringType?.Name == "CreatureCmd" && string.Equals(method.Name, "Heal", StringComparison.Ordinal))
        {
            step = new NativeBehaviorStep
            {
                Kind = "combat.heal",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["amount"] = ResolveDynamicAmount(model, "Heal", "0"),
                    ["target"] = defaultSelfSelector
                }
            };
            return true;
        }

        if (method.DeclaringType?.Name == "CardPileCmd" && string.Equals(method.Name, "Draw", StringComparison.Ordinal))
        {
            step = new NativeBehaviorStep
            {
                Kind = "combat.draw_cards",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["amount"] = ResolveDynamicAmount(model, "Cards", "1")
                }
            };
            return true;
        }

        if (method.DeclaringType?.Name == "PlayerCmd" && string.Equals(method.Name, "GainEnergy", StringComparison.Ordinal))
        {
            step = new NativeBehaviorStep
            {
                Kind = "player.gain_energy",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["amount"] = ResolveDynamicAmount(model, "Energy", "1")
                }
            };
            return true;
        }

        if (method.DeclaringType?.Name == "PlayerCmd" && string.Equals(method.Name, "GainGold", StringComparison.Ordinal))
        {
            step = new NativeBehaviorStep
            {
                Kind = "player.gain_gold",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["amount"] = ResolveDynamicAmount(model, "Gold", "1")
                }
            };
            return true;
        }

        if (method.DeclaringType?.Name == "PlayerCmd" && string.Equals(method.Name, "GainStars", StringComparison.Ordinal))
        {
            step = new NativeBehaviorStep
            {
                Kind = "player.gain_stars",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["amount"] = ResolveDynamicAmount(model, "Stars", "1")
                }
            };
            return true;
        }

        if (method.DeclaringType?.Name == "PowerCmd" && string.Equals(method.Name, "Apply", StringComparison.Ordinal) &&
            TryResolvePowerId(model, out var powerId, out var amount))
        {
            step = new NativeBehaviorStep
            {
                Kind = "combat.apply_power",
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["power_id"] = powerId,
                    ["amount"] = amount,
                    ["target"] = defaultTargetSelector
                }
            };
            return true;
        }

        return false;
    }

    private static bool ShouldIgnoreCall(MethodBase method)
    {
        var typeName = method.DeclaringType?.Name ?? string.Empty;
        var methodName = method.Name;

        if (typeName.Contains("DamageCmd", StringComparison.Ordinal) &&
            methodName is "FromCard" or "Targeting" or "WithHitFx" or "Execute")
        {
            return true;
        }

        if (typeName is "CreatureCmd" && methodName == "TriggerAnim")
        {
            return true;
        }

        if (typeName is "Cmd" && methodName is "Wait" or "CustomScaledWait")
        {
            return true;
        }

        if (typeName is "EventModel" && methodName is "SetEventFinished" or "SetEventState")
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

    private static string ResolveDamageProps(AbstractModel model)
    {
        if (TryGetDynamicVar(model, "Damage", out var dynamicVar) && dynamicVar is DamageVar damageVar)
        {
            return damageVar.Props == 0 ? "none" : damageVar.Props.ToString();
        }

        return "none";
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
            var powerModel = ModelDb.AllPowers.FirstOrDefault(power => power.GetType() == powerType);
            if (powerModel == null)
            {
                continue;
            }

            powerId = powerModel.Id.Entry;
            amount = dynamicVar.BaseValue.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        return false;
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

    private static IReadOnlyList<MethodBase> EnumerateCalledMethods(MethodInfo method)
    {
        var implementationMethod = ResolveImplementationMethod(method);
        var body = implementationMethod.GetMethodBody();
        if (body == null)
        {
            return Array.Empty<MethodBase>();
        }

        var il = body.GetILAsByteArray();
        if (il == null || il.Length == 0)
        {
            return Array.Empty<MethodBase>();
        }

        var results = new List<MethodBase>();
        var index = 0;
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
                        results.Add(resolved);
                    }
                }
                catch
                {
                    // Ignore tokens that cannot be resolved in the current context.
                }

                continue;
            }

            index += GetOperandSize(opcode, il, index);
        }

        return results;
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
