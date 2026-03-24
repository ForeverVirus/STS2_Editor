namespace STS2_Editor.Scripts.Editor.Graph;

public sealed class DynamicValuePreviewResult
{
    public decimal Value { get; set; }

    public decimal BaseValue { get; set; }

    public decimal ExtraValue { get; set; }

    public decimal Multiplier { get; set; }

    public string TemplateText { get; set; } = string.Empty;

    public string PreviewText { get; set; } = string.Empty;

    public string SummaryText { get; set; } = string.Empty;

    public bool IsApproximate { get; set; }

    public bool IsSupported { get; set; } = true;
}
