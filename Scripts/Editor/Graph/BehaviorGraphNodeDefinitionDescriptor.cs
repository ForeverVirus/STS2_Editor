namespace STS2_Editor.Scripts.Editor.Graph;

public sealed class BehaviorGraphNodeDefinitionDescriptor
{
    public string NodeType { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public IReadOnlyList<BehaviorGraphPortDefinition> Inputs { get; set; } = Array.Empty<BehaviorGraphPortDefinition>();

    public IReadOnlyList<BehaviorGraphPortDefinition> Outputs { get; set; } = Array.Empty<BehaviorGraphPortDefinition>();

    public IReadOnlyDictionary<string, string> DefaultProperties { get; set; } = new Dictionary<string, string>();
}
