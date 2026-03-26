using System.Globalization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2_Editor.Scripts.Editor.Core.Utilities;

namespace STS2_Editor.Scripts.Editor.Graph;

public static class DynamicValueEvaluator
{
    public static DynamicValueDefinition GetDefinition(BehaviorGraphNodeDefinition node, string propertyKey, string defaultLiteral = "0")
    {
        ArgumentNullException.ThrowIfNull(node);
        if (node.DynamicValues.TryGetValue(propertyKey, out var definition) && definition != null)
        {
            return definition;
        }

        return new DynamicValueDefinition
        {
            SourceKind = DynamicValueSourceKind.Literal,
            LiteralValue = GetLegacyProperty(node, propertyKey, defaultLiteral)
        };
    }

    public static decimal EvaluateRuntimeDecimal(BehaviorGraphNodeDefinition node, string propertyKey, BehaviorGraphExecutionContext context, decimal defaultValue = 0m)
    {
        ArgumentNullException.ThrowIfNull(context);
        var definition = GetDefinition(node, propertyKey, defaultValue.ToString(CultureInfo.InvariantCulture));
        return EvaluateRuntimeDecimal(definition, ResolveSourceModel(context), context, defaultValue);
    }

    public static DynamicValuePreviewResult EvaluatePreview(
        BehaviorGraphNodeDefinition node,
        string propertyKey,
        AbstractModel? sourceModel,
        DynamicPreviewContext? previewContext,
        decimal defaultValue = 0m)
    {
        var definition = GetDefinition(node, propertyKey, defaultValue.ToString(CultureInfo.InvariantCulture));
        return EvaluatePreview(definition, sourceModel, previewContext, defaultValue);
    }

    public static DynamicValuePreviewResult EvaluatePreview(
        DynamicValueDefinition definition,
        AbstractModel? sourceModel,
        DynamicPreviewContext? previewContext,
        decimal defaultValue = 0m)
    {
        var result = new DynamicValuePreviewResult
        {
            TemplateText = string.IsNullOrWhiteSpace(definition.TemplateText)
                ? GetAuthoringTemplate(definition)
                : definition.TemplateText
        };

        switch (definition.SourceKind)
        {
            case DynamicValueSourceKind.Literal:
            {
                if (IsStateReference(definition.LiteralValue))
                {
                    var label = FormatStateReference(definition.LiteralValue, definition.TemplateText);
                    result.BaseValue = defaultValue;
                    result.Value = defaultValue;
                    result.PreviewText = label;
                    result.SummaryText = ModStudioLocalization.IsChinese
                        ? $"引用运行时状态：{label}"
                        : $"Runtime state reference: {label}";
                }
                else
                {
                    var literal = definition.ResolveLiteralOrDefault(defaultValue);
                    result.BaseValue = literal;
                    result.Value = literal;
                    result.PreviewText = FormatPreview(literal, definition);
                    result.SummaryText = ModStudioLocalization.IsChinese
                        ? $"固定值 {result.PreviewText}"
                        : $"Literal {result.PreviewText}";
                }

                return result;
            }
            case DynamicValueSourceKind.DynamicVar:
            {
                if (!TryGetDynamicVar(sourceModel, definition.DynamicVarName, out var dynamicVar))
                {
                    result.IsSupported = false;
                    result.IsApproximate = true;
                    result.Value = definition.ResolveLiteralOrDefault(defaultValue);
                    result.PreviewText = result.Value.ToString(CultureInfo.InvariantCulture);
                    result.SummaryText = ModStudioLocalization.IsChinese
                        ? $"未找到原版动态变量，回退到 {result.PreviewText}"
                        : $"Dynamic variable not found, fallback to {result.PreviewText}";
                    return result;
                }

                var originalValue = dynamicVar?.BaseValue ?? 0m;
                result.BaseValue = originalValue;
                result.Value = ApplyOverride(originalValue, definition.BaseOverrideMode, definition.BaseOverrideValue);
                result.PreviewText = FormatPreview(result.Value, definition);
                result.SummaryText = ModStudioLocalization.IsChinese
                    ? $"引用 {definition.DynamicVarName}：原始值 {originalValue}，当前结果 {result.Value}"
                    : $"Using {definition.DynamicVarName}: original value {originalValue}, current result {result.Value}";
                return result;
            }
            case DynamicValueSourceKind.FormulaRef:
            {
                var (baseValue, extraValue) = ResolveFormulaComponents(sourceModel, definition);
                result.BaseValue = ApplyOverride(baseValue, definition.BaseOverrideMode, definition.BaseOverrideValue);
                result.ExtraValue = ApplyOverride(extraValue, definition.ExtraOverrideMode, definition.ExtraOverrideValue);
                result.Multiplier = ResolvePreviewMultiplier(definition, previewContext);
                result.Value = result.BaseValue + (result.ExtraValue * result.Multiplier);
                result.IsApproximate = true;
                result.PreviewText = FormatPreview(result.Value, definition);
                result.SummaryText = ModStudioLocalization.IsChinese
                    ? $"基础值 {result.BaseValue} + 额外值 {result.ExtraValue} x {BuildMultiplierSummary(definition, result.Multiplier)} = {result.Value}"
                    : $"base {result.BaseValue} + extra {result.ExtraValue} x {BuildMultiplierSummary(definition, result.Multiplier)} = {result.Value}";
                return result;
            }
            default:
                result.IsSupported = false;
                result.Value = defaultValue;
                result.PreviewText = defaultValue.ToString(CultureInfo.InvariantCulture);
                result.SummaryText = ModStudioLocalization.IsChinese
                    ? $"不支持的值来源，回退到 {result.PreviewText}"
                    : $"Unsupported source, fallback to {result.PreviewText}";
                return result;
        }
    }

    public static string GetTemplateToken(BehaviorGraphNodeDefinition node, string propertyKey)
    {
        return GetTemplateToken(GetDefinition(node, propertyKey));
    }

    public static string GetTemplateToken(DynamicValueDefinition definition)
    {
        return definition.SourceKind switch
        {
            DynamicValueSourceKind.DynamicVar => GetSourceToken(definition),
            DynamicValueSourceKind.FormulaRef => GetSourceToken(definition),
            _ => string.IsNullOrWhiteSpace(definition.LiteralValue) ? "0" : definition.LiteralValue
        };
    }

    public static string GetSourceToken(DynamicValueDefinition definition)
    {
        if (definition.SourceKind == DynamicValueSourceKind.FormulaRef)
        {
            var formulaToken = string.IsNullOrWhiteSpace(definition.FormulaRef)
                ? definition.DynamicVarName
                : definition.FormulaRef;
            return string.IsNullOrWhiteSpace(formulaToken)
                ? "{CalculatedValue:diff()}"
                : $"{{{formulaToken}:diff()}}";
        }

        if (string.IsNullOrWhiteSpace(definition.DynamicVarName))
        {
            return "{Value:diff()}";
        }

        if (string.Equals(definition.DynamicVarName, "Repeat", StringComparison.OrdinalIgnoreCase))
        {
            return "{Repeat}";
        }

        return $"{{{definition.DynamicVarName}:diff()}}";
    }

    public static string GetAuthoringTemplate(DynamicValueDefinition definition)
    {
        return definition.SourceKind switch
        {
            DynamicValueSourceKind.DynamicVar => BuildDynamicVarAuthoringTemplate(definition),
            DynamicValueSourceKind.FormulaRef => BuildFormulaAuthoringTemplate(definition),
            _ => string.IsNullOrWhiteSpace(definition.LiteralValue) ? "0" : definition.LiteralValue
        };
    }

    public static string GetLegacyProperty(BehaviorGraphNodeDefinition node, string key, string defaultValue = "")
    {
        return node.Properties.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : defaultValue;
    }

    private static decimal EvaluateRuntimeDecimal(
        DynamicValueDefinition definition,
        AbstractModel? sourceModel,
        BehaviorGraphExecutionContext? executionContext,
        decimal defaultValue)
    {
        return definition.SourceKind switch
        {
            DynamicValueSourceKind.Literal => ResolveLiteralRuntimeValue(definition, executionContext, defaultValue),
            DynamicValueSourceKind.DynamicVar => EvaluateDynamicRuntimeValue(definition, sourceModel, defaultValue),
            DynamicValueSourceKind.FormulaRef => EvaluateFormulaRuntimeValue(definition, sourceModel, executionContext),
            _ => defaultValue
        };
    }

    private static decimal ResolveLiteralRuntimeValue(
        DynamicValueDefinition definition,
        BehaviorGraphExecutionContext? executionContext,
        decimal defaultValue)
    {
        if (executionContext != null && IsStateReference(definition.LiteralValue))
        {
            return executionContext.ResolveDecimal(definition.LiteralValue, defaultValue);
        }

        return definition.ResolveLiteralOrDefault(defaultValue);
    }

    private static decimal EvaluateDynamicRuntimeValue(DynamicValueDefinition definition, AbstractModel? sourceModel, decimal defaultValue)
    {
        if (!TryGetDynamicVar(sourceModel, definition.DynamicVarName, out var dynamicVar))
        {
            return definition.ResolveLiteralOrDefault(defaultValue);
        }

        return ApplyOverride(dynamicVar?.BaseValue ?? 0m, definition.BaseOverrideMode, definition.BaseOverrideValue);
    }

    private static decimal EvaluateFormulaRuntimeValue(
        DynamicValueDefinition definition,
        AbstractModel? sourceModel,
        BehaviorGraphExecutionContext? executionContext)
    {
        var (baseValue, extraValue) = ResolveFormulaComponents(sourceModel, definition);
        var resolvedBase = ApplyOverride(baseValue, definition.BaseOverrideMode, definition.BaseOverrideValue);
        var resolvedExtra = ApplyOverride(extraValue, definition.ExtraOverrideMode, definition.ExtraOverrideValue);
        var multiplier = ResolveRuntimeMultiplier(definition, executionContext);
        return resolvedBase + (resolvedExtra * multiplier);
    }

    private static (decimal BaseValue, decimal ExtraValue) ResolveFormulaComponents(AbstractModel? sourceModel, DynamicValueDefinition definition)
    {
        var baseValue = 0m;
        var extraValue = 0m;

        if (sourceModel is CardModel card)
        {
            TryGetDynamicVar(card, "CalculationBase", out var baseVar);
            TryGetDynamicVar(card, "CalculationExtra", out var extraVar);
            baseValue = baseVar?.BaseValue ?? 0m;
            extraValue = extraVar?.BaseValue ?? 0m;
        }

        var formulaVarName = string.IsNullOrWhiteSpace(definition.FormulaRef)
            ? definition.DynamicVarName
            : definition.FormulaRef;

        if (TryGetDynamicVar(sourceModel, formulaVarName, out var formulaVar) && formulaVar is CalculatedVar)
        {
            baseValue = baseValue == 0m ? formulaVar.BaseValue : baseValue;
        }

        return (baseValue, extraValue);
    }

    private static AbstractModel? ResolveSourceModel(BehaviorGraphExecutionContext context)
    {
        if (context.SourceModel != null)
        {
            return context.SourceModel;
        }

        if (context.Card != null)
        {
            return context.Card;
        }

        if (context.Potion != null)
        {
            return context.Potion;
        }

        if (context.Relic != null)
        {
            return context.Relic;
        }

        if (context.Event != null)
        {
            return context.Event;
        }

        return context.Enchantment;
    }

    private static bool TryGetDynamicVar(AbstractModel? model, string varName, out DynamicVar? dynamicVar)
    {
        dynamicVar = null;
        if (model == null || string.IsNullOrWhiteSpace(varName))
        {
            return false;
        }

        return model switch
        {
            CardModel card => card.DynamicVars.TryGetValue(varName, out dynamicVar!),
            PotionModel potion => potion.DynamicVars.TryGetValue(varName, out dynamicVar!),
            RelicModel relic => relic.DynamicVars.TryGetValue(varName, out dynamicVar!),
            EnchantmentModel enchantment when string.Equals(varName, "Amount", StringComparison.OrdinalIgnoreCase) =>
                TryCreateSyntheticEnchantmentAmountVar(enchantment, out dynamicVar!),
            EnchantmentModel enchantment => enchantment.DynamicVars.TryGetValue(varName, out dynamicVar!),
            _ => false
        };
    }

    private static bool TryCreateSyntheticEnchantmentAmountVar(EnchantmentModel enchantment, out DynamicVar dynamicVar)
    {
        dynamicVar = new IntVar("Amount", enchantment.Amount);
        return true;
    }

    private static decimal ApplyOverride(decimal originalValue, DynamicValueOverrideMode mode, string rawOverride)
    {
        if (!decimal.TryParse(rawOverride, NumberStyles.Number, CultureInfo.InvariantCulture, out var overrideValue))
        {
            return originalValue;
        }

        return mode switch
        {
            DynamicValueOverrideMode.Absolute => overrideValue,
            DynamicValueOverrideMode.Delta => originalValue + overrideValue,
            _ => originalValue
        };
    }

    private static decimal ResolvePreviewMultiplier(DynamicValueDefinition definition, DynamicPreviewContext? previewContext)
    {
        if (previewContext == null)
        {
            return 0m;
        }

        var multiplierKey = NormalizeMultiplierKey(definition.PreviewMultiplierKey);
        if (!string.IsNullOrWhiteSpace(multiplierKey) &&
            previewContext.FormulaMultipliers.TryGetValue(multiplierKey, out var multiplier))
        {
            return multiplier;
        }

        return 0m;
    }

    private static decimal ResolveRuntimeMultiplier(DynamicValueDefinition definition, BehaviorGraphExecutionContext? executionContext)
    {
        var multiplierKey = NormalizeMultiplierKey(definition.PreviewMultiplierKey);
        if (executionContext != null &&
            !string.IsNullOrWhiteSpace(multiplierKey) &&
            TryGetRuntimeFormulaMultiplier(executionContext, multiplierKey, out var runtimeMultiplier))
        {
            return runtimeMultiplier;
        }

        return 0m;
    }

    private static bool TryGetRuntimeFormulaMultiplier(BehaviorGraphExecutionContext context, string key, out decimal value)
    {
        value = 0m;
        var combat = context.Owner?.PlayerCombatState;
        var owner = context.Owner;
        return key.ToLowerInvariant() switch
        {
            "hand_count" or "cards" => TrySet(ResolveRuntimeHandCount(context, combat), out value),
            "stars" => TrySet(combat?.Stars ?? 0, out value),
            "energy" => TrySet(combat?.Energy ?? 0, out value),
            "current_block" => TrySet(owner?.Creature?.Block ?? 0, out value),
            "draw_pile" => TrySet(combat?.DrawPile?.Cards.Count ?? 0, out value),
            "discard_pile" => TrySet(combat?.DiscardPile?.Cards.Count ?? 0, out value),
            "exhaust_pile" => TrySet(combat?.ExhaustPile?.Cards.Count ?? 0, out value),
            "missing_hp" => TrySet(Math.Max((owner?.Creature?.MaxHp ?? 0) - (owner?.Creature?.CurrentHp ?? 0), 0), out value),
            _ => false
        };
    }

    private static bool TrySet(decimal source, out decimal value)
    {
        value = source;
        return true;
    }

    private static decimal ResolveRuntimeHandCount(BehaviorGraphExecutionContext context, MegaCrit.Sts2.Core.Entities.Players.PlayerCombatState? combat)
    {
        var handCount = combat?.Hand?.Cards.Count ?? 0;
        if (context.Card?.Pile?.Type == MegaCrit.Sts2.Core.Entities.Cards.PileType.Hand &&
            combat?.Hand?.Cards.Contains(context.Card) == true)
        {
            handCount = Math.Max(0, handCount - 1);
        }

        return handCount;
    }

    private static bool TryParseOptionalDecimal(string? rawValue, out decimal value)
    {
        return decimal.TryParse(rawValue, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    private static string BuildMultiplierSummary(DynamicValueDefinition definition, decimal resolvedMultiplier)
    {
        var multiplierKey = NormalizeMultiplierKey(definition.PreviewMultiplierKey);
        if (!string.IsNullOrWhiteSpace(multiplierKey))
        {
            return $"{FormatMultiplierKey(multiplierKey)} ({resolvedMultiplier.ToString(CultureInfo.InvariantCulture)})";
        }

        return resolvedMultiplier.ToString(CultureInfo.InvariantCulture);
    }

    private static string FormatPreview(decimal value, DynamicValueDefinition definition)
    {
        if (!string.IsNullOrWhiteSpace(definition.PreviewFormat))
        {
            return definition.PreviewFormat.Replace("{value}", value.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        }

        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static string BuildDynamicVarAuthoringTemplate(DynamicValueDefinition definition)
    {
        var token = GetSourceToken(definition);
        return definition.BaseOverrideMode switch
        {
            DynamicValueOverrideMode.Absolute when !string.IsNullOrWhiteSpace(definition.BaseOverrideValue) => definition.BaseOverrideValue,
            DynamicValueOverrideMode.Delta when TryParseOptionalDecimal(definition.BaseOverrideValue, out var delta) => FormatDeltaTemplate(token, delta),
            _ => token
        };
    }

    private static string BuildFormulaAuthoringTemplate(DynamicValueDefinition definition)
    {
        var hasBaseOverride = definition.BaseOverrideMode != DynamicValueOverrideMode.None && !string.IsNullOrWhiteSpace(definition.BaseOverrideValue);
        var hasExtraOverride = definition.ExtraOverrideMode != DynamicValueOverrideMode.None && !string.IsNullOrWhiteSpace(definition.ExtraOverrideValue);
        var normalizedMultiplierKey = NormalizeMultiplierKey(definition.PreviewMultiplierKey);
        var hasMultiplierKey = !string.IsNullOrWhiteSpace(normalizedMultiplierKey);

        if (!hasBaseOverride && !hasExtraOverride && !hasMultiplierKey)
        {
            return GetSourceToken(definition);
        }

        var baseExpr = BuildOverrideExpression(
            ModStudioLocalization.IsChinese ? "原基础值" : "original base",
            definition.BaseOverrideMode,
            definition.BaseOverrideValue);
        var extraExpr = BuildOverrideExpression(
            ModStudioLocalization.IsChinese ? "原额外值" : "original extra",
            definition.ExtraOverrideMode,
            definition.ExtraOverrideValue);
        var multiplierExpr = hasMultiplierKey
            ? FormatMultiplierKey(normalizedMultiplierKey)
            : (ModStudioLocalization.IsChinese ? "上下文值" : "context");

        return ModStudioLocalization.IsChinese
            ? $"{baseExpr} + {extraExpr} x {multiplierExpr}"
            : $"{baseExpr} + {extraExpr} x {multiplierExpr}";
    }

    private static string BuildOverrideExpression(string originalLabel, DynamicValueOverrideMode mode, string rawValue)
    {
        if (!TryParseOptionalDecimal(rawValue, out var parsed))
        {
            return originalLabel;
        }

        return mode switch
        {
            DynamicValueOverrideMode.Absolute => parsed.ToString(CultureInfo.InvariantCulture),
            DynamicValueOverrideMode.Delta when parsed > 0m => $"{originalLabel} + {parsed.ToString(CultureInfo.InvariantCulture)}",
            DynamicValueOverrideMode.Delta when parsed < 0m => $"{originalLabel} - {Math.Abs(parsed).ToString(CultureInfo.InvariantCulture)}",
            _ => originalLabel
        };
    }

    private static string FormatDeltaTemplate(string token, decimal delta)
    {
        if (delta > 0m)
        {
            return $"{delta.ToString(CultureInfo.InvariantCulture)} + {token}";
        }

        if (delta < 0m)
        {
            return $"{token} - {Math.Abs(delta).ToString(CultureInfo.InvariantCulture)}";
        }

        return token;
    }

    private static string FormatMultiplierKey(string rawKey)
    {
        return NormalizeMultiplierKey(rawKey).ToLowerInvariant() switch
        {
            "hand_count" or "cards" => ModStudioLocalization.IsChinese ? "手牌数" : "Hand Count",
            "stars" => ModStudioLocalization.IsChinese ? "星数" : "Stars",
            "energy" => ModStudioLocalization.IsChinese ? "能量" : "Energy",
            "current_block" => ModStudioLocalization.IsChinese ? "当前格挡" : "Current Block",
            "draw_pile" => ModStudioLocalization.IsChinese ? "抽牌堆数量" : "Draw Pile Count",
            "discard_pile" => ModStudioLocalization.IsChinese ? "弃牌堆数量" : "Discard Pile Count",
            "exhaust_pile" => ModStudioLocalization.IsChinese ? "消耗堆数量" : "Exhaust Pile Count",
            "missing_hp" => ModStudioLocalization.IsChinese ? "已损生命" : "Missing HP",
            _ => rawKey
        };
    }

    public static string NormalizeMultiplierKey(string? rawKey)
    {
        return string.Equals(rawKey, "cards", StringComparison.OrdinalIgnoreCase)
            ? "hand_count"
            : rawKey ?? string.Empty;
    }

    private static bool IsStateReference(string? rawValue)
    {
        return !string.IsNullOrWhiteSpace(rawValue) && rawValue.StartsWith("$state.", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatStateReference(string rawValue, string? templateText)
    {
        if (!string.IsNullOrWhiteSpace(templateText))
        {
            return templateText;
        }

        var stateKey = rawValue["$state.".Length..];
        return stateKey.ToLowerInvariant() switch
        {
            "last_damage_total_plus_overkill" => ModStudioLocalization.IsChinese ? "上一段实际伤害" : "Previous dealt damage",
            "last_damage_total" => ModStudioLocalization.IsChinese ? "上一段总伤害" : "Previous total damage",
            "last_damage_overkill" => ModStudioLocalization.IsChinese ? "上一段溢出伤害" : "Previous overkill damage",
            _ => stateKey
        };
    }
}
