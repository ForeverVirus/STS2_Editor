using STS2_Editor.Scripts.Editor.Core.Models;

namespace STS2_Editor.Scripts.Editor.Graph;

public sealed class BehaviorGraphTemplateDescriptor
{
    public string TemplateId { get; init; } = string.Empty;

    public ModStudioEntityKind EntityKind { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string TriggerId { get; init; } = string.Empty;

    public string DefaultAmount { get; init; } = string.Empty;
}
