namespace STS2_Editor.Scripts.Editor.Graph;

public sealed class BehaviorGraphConnectionDefinition
{
    public string FromNodeId { get; set; } = string.Empty;

    public string FromPortId { get; set; } = string.Empty;

    public string ToNodeId { get; set; } = string.Empty;

    public string ToPortId { get; set; } = string.Empty;
}
