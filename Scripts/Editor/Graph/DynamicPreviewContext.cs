using STS2_Editor.Scripts.Editor.Core.Models;

namespace STS2_Editor.Scripts.Editor.Graph;

public sealed class DynamicPreviewContext
{
    public ModStudioEntityKind EntityKind { get; set; }

    public string EntityId { get; set; } = string.Empty;

    public bool Upgraded { get; set; }

    public string TargetSelector { get; set; } = "current_target";

    public decimal CurrentBlock { get; set; }

    public decimal CurrentStars { get; set; }

    public decimal CurrentEnergy { get; set; }

    public int HandCount { get; set; }

    public int DrawPileCount { get; set; }

    public int DiscardPileCount { get; set; }

    public int ExhaustPileCount { get; set; }

    public decimal MissingHp { get; set; }

    public Dictionary<string, decimal> FormulaMultipliers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
