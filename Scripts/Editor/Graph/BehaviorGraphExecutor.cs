using MegaCrit.Sts2.Core.Logging;

namespace STS2_Editor.Scripts.Editor.Graph;

public sealed class BehaviorGraphExecutor
{
    private readonly BehaviorGraphRegistry _registry;

    public BehaviorGraphExecutor(BehaviorGraphRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public async Task<BehaviorGraphValidationResult> ExecuteAsync(
        BehaviorGraphDefinition graph,
        BehaviorGraphExecutionContext context,
        string? entryNodeId = null)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(context);

        var validation = _registry.Validate(graph);
        if (!validation.IsValid)
        {
            Log.Warn($"Mod Studio graph '{graph.GraphId}' failed validation: {string.Join(" | ", validation.Errors)}");
            return validation;
        }

        var selectedEntryNodeId = string.IsNullOrWhiteSpace(entryNodeId) ? graph.EntryNodeId : entryNodeId;
        if (string.IsNullOrWhiteSpace(selectedEntryNodeId))
        {
            validation.AddError($"Graph '{graph.GraphId}' has no entry node.");
            return validation;
        }

        var nodesById = graph.Nodes.ToDictionary(node => node.NodeId, StringComparer.Ordinal);
        var outgoingConnections = graph.Connections
            .GroupBy(connection => connection.FromNodeId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

        context.Graph = graph;
        await ExecuteNodeAsync(selectedEntryNodeId, nodesById, outgoingConnections, context, new HashSet<string>(StringComparer.Ordinal));
        return validation;
    }

    private async Task ExecuteNodeAsync(
        string nodeId,
        IReadOnlyDictionary<string, BehaviorGraphNodeDefinition> nodesById,
        IReadOnlyDictionary<string, List<BehaviorGraphConnectionDefinition>> outgoingConnections,
        BehaviorGraphExecutionContext context,
        HashSet<string> activeNodes)
    {
        if (!nodesById.TryGetValue(nodeId, out var node))
        {
            Log.Warn($"Mod Studio graph '{context.Graph?.GraphId}' tried to execute missing node '{nodeId}'.");
            return;
        }

        if (!activeNodes.Add(nodeId))
        {
            throw new InvalidOperationException($"Cycle detected while executing graph '{context.Graph?.GraphId}' at node '{nodeId}'.");
        }

        try
        {
            if (_registry.TryGetExecutor(node.NodeType, out var executor) && executor != null && executor.CanExecute(node))
            {
                await executor.ExecuteAsync(node, context);
            }

            foreach (var nextNodeId in ResolveNextNodeIds(node, outgoingConnections, context))
            {
                await ExecuteNodeAsync(nextNodeId, nodesById, outgoingConnections, context, activeNodes);
            }
        }
        finally
        {
            activeNodes.Remove(nodeId);
        }
    }

    private IEnumerable<string> ResolveNextNodeIds(
        BehaviorGraphNodeDefinition node,
        IReadOnlyDictionary<string, List<BehaviorGraphConnectionDefinition>> outgoingConnections,
        BehaviorGraphExecutionContext context)
    {
        if (!outgoingConnections.TryGetValue(node.NodeId, out var connections) || connections.Count == 0)
        {
            return Array.Empty<string>();
        }

        if (string.Equals(node.NodeType, "flow.exit", StringComparison.Ordinal))
        {
            return Array.Empty<string>();
        }

        var orderedPorts = GetOutputPortOrder(node, connections);

        return node.NodeType switch
        {
            "flow.sequence" => ResolveNodesForPorts(connections, orderedPorts),
            "flow.branch" => ResolveNodesForPorts(connections, new[] { ResolveBranchPort(node, context) }),
            "flow.random_choice" => ResolveRandomChoice(connections, orderedPorts),
            _ => ResolveDefaultNextNodes(connections, orderedPorts)
        };
    }

    private IEnumerable<string> ResolveDefaultNextNodes(
        IEnumerable<BehaviorGraphConnectionDefinition> connections,
        IReadOnlyList<string> orderedPorts)
    {
        if (orderedPorts.Contains("out", StringComparer.Ordinal))
        {
            return ResolveNodesForPorts(connections, new[] { "out" });
        }

        if (orderedPorts.Contains("next", StringComparer.Ordinal))
        {
            return ResolveNodesForPorts(connections, new[] { "next" });
        }

        return ResolveNodesForPorts(connections, orderedPorts.Take(1));
    }

    private IEnumerable<string> ResolveRandomChoice(
        IEnumerable<BehaviorGraphConnectionDefinition> connections,
        IReadOnlyList<string> orderedPorts)
    {
        var candidatePorts = orderedPorts
            .Where(port => connections.Any(connection => string.Equals(connection.FromPortId, port, StringComparison.Ordinal)))
            .ToList();
        if (candidatePorts.Count == 0)
        {
            return Array.Empty<string>();
        }

        var index = Random.Shared.Next(candidatePorts.Count);
        return ResolveNodesForPorts(connections, new[] { candidatePorts[index] });
    }

    private static IEnumerable<string> ResolveNodesForPorts(
        IEnumerable<BehaviorGraphConnectionDefinition> connections,
        IEnumerable<string> ports)
    {
        var selectedPorts = ports.ToHashSet(StringComparer.Ordinal);
        return connections
            .Where(connection => selectedPorts.Contains(connection.FromPortId))
            .Select(connection => connection.ToNodeId);
    }

    private static string ResolveBranchPort(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        var conditionExpression = node.Properties.TryGetValue("condition", out var rawCondition)
            ? rawCondition
            : node.Properties.TryGetValue("condition_key", out var conditionKey)
                ? $"$state.{conditionKey}"
                : string.Empty;
        return context.ResolveBool(conditionExpression) ? "true" : "false";
    }

    private IReadOnlyList<string> GetOutputPortOrder(
        BehaviorGraphNodeDefinition node,
        IReadOnlyCollection<BehaviorGraphConnectionDefinition> connections)
    {
        if (_registry.TryGetNodeDefinition(node.NodeType, out var definition) && definition != null)
        {
            var definedPorts = definition.Outputs
                .Select(output => output.PortId)
                .Where(portId => !string.IsNullOrWhiteSpace(portId))
                .ToList();
            if (definedPorts.Count > 0)
            {
                return definedPorts;
            }
        }

        return connections
            .Select(connection => connection.FromPortId)
            .Where(portId => !string.IsNullOrWhiteSpace(portId))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }
}
