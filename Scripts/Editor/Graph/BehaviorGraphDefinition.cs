using STS2_Editor.Scripts.Editor.Core.Models;

namespace STS2_Editor.Scripts.Editor.Graph;

public sealed class BehaviorGraphDefinition
{
    public string GraphId { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = "New Graph";

    public string Description { get; set; } = string.Empty;

    public string Version { get; set; } = "1.0.0";

    public ModStudioEntityKind? EntityKind { get; set; }

    public string EntryNodeId { get; set; } = string.Empty;

    public List<BehaviorGraphNodeDefinition> Nodes { get; set; } = new();

    public List<BehaviorGraphConnectionDefinition> Connections { get; set; } = new();

    public Dictionary<string, string> Metadata { get; set; } = new();
}
