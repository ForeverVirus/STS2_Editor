namespace STS2_Editor.Scripts.Editor.Core.Models;

public sealed class EntityBrowserItem
{
    public ModStudioEntityKind Kind { get; set; }

    public string EntityId { get; set; } = string.Empty;

    public bool IsProjectOnly { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string DetailText { get; set; } = string.Empty;
}
