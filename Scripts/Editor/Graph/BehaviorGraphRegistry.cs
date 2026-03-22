namespace STS2_Editor.Scripts.Editor.Graph;

public sealed class BehaviorGraphRegistry
{
    private readonly Dictionary<string, BehaviorGraphNodeDefinitionDescriptor> _definitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IBehaviorNodeExecutor> _executors = new(StringComparer.Ordinal);
    private readonly List<IBehaviorNodeDefinitionProvider> _providers = new();

    public BehaviorGraphRegistry()
    {
    }

    public IReadOnlyCollection<BehaviorGraphNodeDefinitionDescriptor> Definitions => _definitions.Values;

    public IReadOnlyCollection<IBehaviorNodeExecutor> Executors => _executors.Values;

    public void RegisterBuiltIns()
    {
        RegisterProvider(new BuiltInBehaviorNodeDefinitionProvider());
    }

    public void RegisterProvider(IBehaviorNodeDefinitionProvider provider)
    {
        if (provider is null)
        {
            throw new ArgumentNullException(nameof(provider));
        }

        _providers.Add(provider);
        foreach (var definition in provider.GetDefinitions())
        {
            RegisterDefinition(definition);
        }
    }

    public void RegisterDefinition(BehaviorGraphNodeDefinitionDescriptor definition)
    {
        if (definition is null)
        {
            throw new ArgumentNullException(nameof(definition));
        }

        if (string.IsNullOrWhiteSpace(definition.NodeType))
        {
            throw new ArgumentException("Node type is required.", nameof(definition));
        }

        _definitions[definition.NodeType] = definition;
    }

    public void RegisterExecutor(IBehaviorNodeExecutor executor)
    {
        if (executor is null)
        {
            throw new ArgumentNullException(nameof(executor));
        }

        if (string.IsNullOrWhiteSpace(executor.NodeType))
        {
            throw new ArgumentException("Executor node type is required.", nameof(executor));
        }

        _executors[executor.NodeType] = executor;
    }

    public bool TryGetNodeDefinition(string nodeType, out BehaviorGraphNodeDefinitionDescriptor? definition)
    {
        if (_definitions.TryGetValue(nodeType, out var value))
        {
            definition = value;
            return true;
        }

        definition = null;
        return false;
    }

    public bool TryGetExecutor(string nodeType, out IBehaviorNodeExecutor? executor)
    {
        if (_executors.TryGetValue(nodeType, out var value))
        {
            executor = value;
            return true;
        }

        executor = null;
        return false;
    }

    public BehaviorGraphValidationResult Validate(BehaviorGraphDefinition graph)
    {
        return new BehaviorGraphValidator().Validate(graph, this);
    }
}
