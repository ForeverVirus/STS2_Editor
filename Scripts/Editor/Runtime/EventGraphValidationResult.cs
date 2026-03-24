namespace STS2_Editor.Scripts.Editor.Runtime;

public sealed class EventGraphValidationResult
{
    private readonly List<string> _errors = new();
    private readonly List<string> _warnings = new();

    public string EventId { get; set; } = string.Empty;

    public string StartPageId { get; set; } = "INITIAL";

    public IReadOnlyList<string> Errors => _errors;

    public IReadOnlyList<string> Warnings => _warnings;

    public IDictionary<string, string> Metadata { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    public IReadOnlyList<EventGraphPageDefinition> Pages { get; } = new List<EventGraphPageDefinition>();

    public IReadOnlyList<EventGraphChoiceBinding> Choices { get; } = new List<EventGraphChoiceBinding>();

    public bool IsValid => _errors.Count == 0;

    internal List<EventGraphPageDefinition> MutablePages { get; } = new();

    internal List<EventGraphChoiceBinding> MutableChoices { get; } = new();

    public void AddError(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            _errors.Add(message);
        }
    }

    public void AddWarning(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            _warnings.Add(message);
        }
    }

    internal void Seal()
    {
        if (Pages is List<EventGraphPageDefinition> pages)
        {
            pages.AddRange(MutablePages);
        }

        if (Choices is List<EventGraphChoiceBinding> choices)
        {
            choices.AddRange(MutableChoices);
        }
    }
}
