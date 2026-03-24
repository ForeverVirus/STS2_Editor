using MegaCrit.Sts2.Core.Models;

namespace STS2_Editor.Scripts.Editor.Graph;

public sealed class DynamicPreviewService
{
    public DynamicValuePreviewResult Evaluate(BehaviorGraphNodeDefinition node, string propertyKey, AbstractModel? sourceModel, DynamicPreviewContext? previewContext, decimal defaultValue = 0m)
    {
        return DynamicValueEvaluator.EvaluatePreview(node, propertyKey, sourceModel, previewContext, defaultValue);
    }

    public GraphDynamicPreviewSummary EvaluateGraph(BehaviorGraphDefinition graph, AbstractModel? sourceModel, DynamicPreviewContext? previewContext)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var summary = new GraphDynamicPreviewSummary();
        foreach (var node in graph.Nodes)
        {
            if (!MightHaveDynamicAmount(node.NodeType))
            {
                continue;
            }

            var preview = DynamicValueEvaluator.EvaluatePreview(node, "amount", sourceModel, previewContext);
            summary.NodeValues[node.NodeId] = preview;
        }

        return summary;
    }

    private static bool MightHaveDynamicAmount(string? nodeType)
    {
        return nodeType is
            "combat.damage" or
            "combat.gain_block" or
            "combat.heal" or
            "combat.draw_cards" or
            "combat.apply_power" or
            "player.gain_energy" or
            "player.gain_gold" or
            "player.gain_stars";
    }
}

public sealed class GraphDynamicPreviewSummary
{
    public Dictionary<string, DynamicValuePreviewResult> NodeValues { get; } = new(StringComparer.Ordinal);
}
