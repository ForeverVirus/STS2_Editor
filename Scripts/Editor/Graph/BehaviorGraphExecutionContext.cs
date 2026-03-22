namespace STS2_Editor.Scripts.Editor.Graph;

public sealed class BehaviorGraphExecutionContext
{
    private readonly Dictionary<string, object?> _state = new(StringComparer.Ordinal);

    public IDictionary<string, object?> State => _state;

    public object? this[string key]
    {
        get => _state.TryGetValue(key, out var value) ? value : null;
        set => _state[key] = value;
    }
}
