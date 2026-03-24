namespace STS2_Editor.Scripts.Editor.Runtime;

public sealed class EventGraphPageDefinition
{
    public string PageId { get; set; } = "INITIAL";

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public bool IsStart { get; set; }

    public List<string> OptionOrder { get; } = new();

    public Dictionary<string, EventGraphChoiceBinding> Choices { get; } = new(StringComparer.Ordinal);
}
