namespace STS2_Editor.Scripts.Editor.Core.Models;

public sealed class EntityOverrideEnvelope
{
    public ModStudioEntityKind EntityKind { get; set; }

    public string EntityId { get; set; } = string.Empty;

    public BehaviorSource BehaviorSource { get; set; } = BehaviorSource.Native;

    public string? GraphId { get; set; }

    public Dictionary<string, string> Metadata { get; set; } = new();

    public List<AssetRef> Assets { get; set; } = new();

    public MonsterAiDefinition? MonsterAi { get; set; }

    public string? Notes { get; set; }
}
