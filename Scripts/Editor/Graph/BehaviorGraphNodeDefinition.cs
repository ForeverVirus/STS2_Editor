namespace STS2_Editor.Scripts.Editor.Graph;

public sealed class BehaviorGraphNodeDefinition
{
    public string NodeId { get; set; } = Guid.NewGuid().ToString("N");

    public string NodeType { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public Dictionary<string, string> Properties { get; set; } = new();
}
