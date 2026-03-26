namespace STS2_Editor.Scripts.Editor.Runtime;

public sealed class EventGraphChoiceBinding
{
    public string PageId { get; set; } = string.Empty;

    public string OptionId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string? NextPageId { get; set; }

    public string? EncounterId { get; set; }

    public string? ResumePageId { get; set; }

    public bool IsProceed { get; set; }

    public bool SaveChoiceToHistory { get; set; } = true;

    public string? RewardKind { get; set; }

    public string? RewardAmount { get; set; }

    public string? RewardTarget { get; set; }

    public string? RewardProps { get; set; }

    public string? RewardPowerId { get; set; }

    public string? RewardCardId { get; set; }

    public string? RewardRelicId { get; set; }

    public string? RewardPotionId { get; set; }

    public string? RewardCount { get; set; }
}
