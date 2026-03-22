namespace STS2_Editor.Scripts.Editor.Graph;

public sealed class BehaviorGraphValidator
{
    public BehaviorGraphValidationResult Validate(BehaviorGraphDefinition graph, BehaviorGraphRegistry? registry = null)
    {
        var result = new BehaviorGraphValidationResult();

        if (graph is null)
        {
            result.AddError("Graph definition is null.");
            return result;
        }

        var nodeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in graph.Nodes)
        {
            if (string.IsNullOrWhiteSpace(node.NodeId))
            {
                result.AddError("Graph contains a node with an empty NodeId.");
                continue;
            }

            if (!nodeIds.Add(node.NodeId))
            {
                result.AddError($"Duplicate node id '{node.NodeId}'.");
            }

            if (registry is not null && !registry.TryGetNodeDefinition(node.NodeType, out _))
            {
                result.AddWarning($"Unknown node type '{node.NodeType}' on node '{node.NodeId}'.");
            }
        }

        if (!string.IsNullOrWhiteSpace(graph.EntryNodeId) && !nodeIds.Contains(graph.EntryNodeId))
        {
            result.AddError($"Entry node '{graph.EntryNodeId}' does not exist.");
        }
        else if (string.IsNullOrWhiteSpace(graph.EntryNodeId) && graph.Nodes.Count > 0)
        {
            result.AddWarning("Graph entry node is empty.");
        }

        foreach (var connection in graph.Connections)
        {
            if (!nodeIds.Contains(connection.FromNodeId))
            {
                result.AddError($"Connection source node '{connection.FromNodeId}' does not exist.");
            }

            if (!nodeIds.Contains(connection.ToNodeId))
            {
                result.AddError($"Connection target node '{connection.ToNodeId}' does not exist.");
            }
        }

        if (!string.IsNullOrWhiteSpace(graph.EntryNodeId) && nodeIds.Contains(graph.EntryNodeId))
        {
            if (HasCycle(graph))
            {
                result.AddError("Graph contains a cycle.");
            }
        }

        return result;
    }

    private static bool HasCycle(BehaviorGraphDefinition graph)
    {
        var adjacency = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var node in graph.Nodes)
        {
            adjacency[node.NodeId] = new List<string>();
        }

        foreach (var connection in graph.Connections)
        {
            if (!adjacency.TryGetValue(connection.FromNodeId, out var list))
            {
                continue;
            }

            list.Add(connection.ToNodeId);
        }

        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);

        return graph.Nodes.Any(node => Visit(node.NodeId, adjacency, visiting, visited));
    }

    private static bool Visit(
        string nodeId,
        IReadOnlyDictionary<string, List<string>> adjacency,
        HashSet<string> visiting,
        HashSet<string> visited)
    {
        if (visited.Contains(nodeId))
        {
            return false;
        }

        if (!visiting.Add(nodeId))
        {
            return true;
        }

        if (adjacency.TryGetValue(nodeId, out var nextNodes))
        {
            foreach (var next in nextNodes)
            {
                if (Visit(next, adjacency, visiting, visited))
                {
                    return true;
                }
            }
        }

        visiting.Remove(nodeId);
        visited.Add(nodeId);
        return false;
    }
}
