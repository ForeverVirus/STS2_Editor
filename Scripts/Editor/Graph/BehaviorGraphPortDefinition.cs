namespace STS2_Editor.Scripts.Editor.Graph;

public sealed class BehaviorGraphPortDefinition
{
    public string PortId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public BehaviorGraphPortDirection Direction { get; set; }

    public string ValueType { get; set; } = "flow";

    public bool IsRequired { get; set; }
}

public enum BehaviorGraphPortDirection
{
    Input = 0,
    Output = 1
}
