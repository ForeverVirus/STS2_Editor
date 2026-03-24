using System.Globalization;

namespace STS2_Editor.Scripts.Editor.Graph;

public sealed class DynamicValueDefinition
{
    public DynamicValueSourceKind SourceKind { get; set; } = DynamicValueSourceKind.Literal;

    public string LiteralValue { get; set; } = string.Empty;

    public string DynamicVarName { get; set; } = string.Empty;

    public string FormulaRef { get; set; } = string.Empty;

    public DynamicValueOverrideMode BaseOverrideMode { get; set; }

    public string BaseOverrideValue { get; set; } = string.Empty;

    public DynamicValueOverrideMode ExtraOverrideMode { get; set; }

    public string ExtraOverrideValue { get; set; } = string.Empty;

    public string PreviewMultiplierKey { get; set; } = string.Empty;

    public string PreviewMultiplierValue { get; set; } = string.Empty;

    public string TemplateText { get; set; } = string.Empty;

    public string PreviewFormat { get; set; } = string.Empty;

    public DynamicValueDefinition Clone()
    {
        return new DynamicValueDefinition
        {
            SourceKind = SourceKind,
            LiteralValue = LiteralValue,
            DynamicVarName = DynamicVarName,
            FormulaRef = FormulaRef,
            BaseOverrideMode = BaseOverrideMode,
            BaseOverrideValue = BaseOverrideValue,
            ExtraOverrideMode = ExtraOverrideMode,
            ExtraOverrideValue = ExtraOverrideValue,
            PreviewMultiplierKey = PreviewMultiplierKey,
            PreviewMultiplierValue = PreviewMultiplierValue,
            TemplateText = TemplateText,
            PreviewFormat = PreviewFormat
        };
    }

    public decimal ResolveLiteralOrDefault(decimal defaultValue = 0m)
    {
        return decimal.TryParse(LiteralValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : defaultValue;
    }
}
