using STS2_Editor.Scripts.Editor.Core.Models;
using STS2_Editor.Scripts.Editor.Core.Utilities;
using STS2_Editor.Scripts.Editor.UI;

namespace STS2_Editor.Scripts.Editor.Graph;

public sealed class GraphDescriptionGenerationResult
{
    public string Description { get; set; } = string.Empty;

    public List<string> UsedNodeTypes { get; } = new();

    public List<string> UnsupportedNodeTypes { get; } = new();

    public bool IsComplete => UnsupportedNodeTypes.Count == 0;
}

public sealed class GraphDescriptionGenerator
{
    private static readonly IReadOnlyList<string> SupportedNodeTypes =
    [
        "flow.sequence",
        "flow.branch",
        "combat.damage",
        "combat.gain_block",
        "combat.heal",
        "combat.draw_cards",
        "combat.apply_power",
        "player.gain_energy",
        "player.gain_gold",
        "player.gain_stars",
        "debug.log"
    ];

    public IReadOnlyList<string> SupportedNodeKinds => SupportedNodeTypes;

    public GraphDescriptionGenerationResult Generate(BehaviorGraphDefinition graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var result = new GraphDescriptionGenerationResult();
        var fragments = new List<string>();

        foreach (var node in graph.Nodes)
        {
            var fragment = DescribeNode(node, result);
            if (!string.IsNullOrWhiteSpace(fragment))
            {
                fragments.Add(fragment);
            }
        }

        if (fragments.Count == 0)
        {
            result.Description = string.IsNullOrWhiteSpace(graph.Description) ? graph.Name : graph.Description;
            return result;
        }

        result.Description = string.Join(" ", fragments);
        return result;
    }

    public string ResolveDescription(BehaviorGraphDefinition graph, string? manualDescription = null)
    {
        if (!string.IsNullOrWhiteSpace(manualDescription))
        {
            return manualDescription.Trim();
        }

        return Generate(graph).Description;
    }

    public bool CanGenerateAutomatically(BehaviorGraphDefinition graph)
    {
        return Generate(graph).IsComplete;
    }

    private static string DescribeNode(BehaviorGraphNodeDefinition node, GraphDescriptionGenerationResult result)
    {
        var kind = Normalize(node.NodeType);
        if (string.IsNullOrWhiteSpace(kind) || kind is "flow.entry" or "flow.exit" or "flow.sequence")
        {
            return string.Empty;
        }

        result.UsedNodeTypes.Add(kind);
        return kind switch
        {
            "flow.branch" => DescribeBranch(node),
            "combat.damage" => DescribeDamage(node),
            "combat.gain_block" => DescribeGainBlock(node),
            "combat.heal" => DescribeHeal(node),
            "combat.draw_cards" => DescribeDrawCards(node),
            "combat.apply_power" => DescribeApplyPower(node),
            "player.gain_energy" => DescribeGain("energy", "能量", node),
            "player.gain_gold" => DescribeGain("gold", "金币", node),
            "player.gain_stars" => DescribeGain("stars", "星数", node),
            "debug.log" => DescribeLog(node),
            _ => MarkUnsupported(node, result)
        };
    }

    private static string DescribeBranch(BehaviorGraphNodeDefinition node)
    {
        var condition = GetProperty(node, "condition");
        if (string.IsNullOrWhiteSpace(condition))
        {
            condition = GetProperty(node, "condition_key");
            if (!string.IsNullOrWhiteSpace(condition))
            {
                condition = $"state.{condition}";
            }
        }

        return string.IsNullOrWhiteSpace(condition)
            ? Dual("根据条件分支。", "Branch based on a condition.")
            : Dual($"如果 {condition}，走真分支；否则走假分支。", $"If {condition}, follow the true branch; otherwise follow the false branch.");
    }

    private static string DescribeDamage(BehaviorGraphNodeDefinition node)
    {
        return Dual(
            $"造成 {GetProperty(node, "amount", "0")} 点伤害，目标 {DescribeTarget(GetProperty(node, "target", "current_target"))}。",
            $"Deal {GetProperty(node, "amount", "0")} damage to {DescribeTarget(GetProperty(node, "target", "current_target"))}.");
    }

    private static string DescribeGainBlock(BehaviorGraphNodeDefinition node)
    {
        return Dual(
            $"获得 {GetProperty(node, "amount", "0")} 点格挡，目标 {DescribeTarget(GetProperty(node, "target", "self"))}。",
            $"Gain {GetProperty(node, "amount", "0")} block for {DescribeTarget(GetProperty(node, "target", "self"))}.");
    }

    private static string DescribeHeal(BehaviorGraphNodeDefinition node)
    {
        return Dual(
            $"恢复 {GetProperty(node, "amount", "0")} 点生命，目标 {DescribeTarget(GetProperty(node, "target", "self"))}。",
            $"Heal {GetProperty(node, "amount", "0")} HP for {DescribeTarget(GetProperty(node, "target", "self"))}.");
    }

    private static string DescribeDrawCards(BehaviorGraphNodeDefinition node)
    {
        return Dual(
            $"抽取 {GetProperty(node, "amount", "1")} 张牌。",
            $"Draw {GetProperty(node, "amount", "1")} cards.");
    }

    private static string DescribeApplyPower(BehaviorGraphNodeDefinition node)
    {
        var powerId = GetProperty(node, "power_id", "power");
        return Dual(
            $"对 {DescribeTarget(GetProperty(node, "target", "current_target"))} 施加 {powerId} x{GetProperty(node, "amount", "1")}。",
            $"Apply {powerId} x{GetProperty(node, "amount", "1")} to {DescribeTarget(GetProperty(node, "target", "current_target"))}.");
    }

    private static string DescribeGain(string resourceNameEn, string resourceNameZh, BehaviorGraphNodeDefinition node)
    {
        return Dual(
            $"获得 {GetProperty(node, "amount", "1")} 点{resourceNameZh}。",
            $"Gain {GetProperty(node, "amount", "1")} {resourceNameEn}.");
    }

    private static string DescribeLog(BehaviorGraphNodeDefinition node)
    {
        var message = GetProperty(node, "message");
        if (string.IsNullOrWhiteSpace(message))
        {
            return string.Empty;
        }

        return Dual($"记录日志：{message}。", $"Log: {message}.");
    }

    private static string MarkUnsupported(BehaviorGraphNodeDefinition node, GraphDescriptionGenerationResult result)
    {
        result.UnsupportedNodeTypes.Add(node.NodeType);
        return string.Empty;
    }

    private static string DescribeTarget(string value)
    {
        return value switch
        {
            "self" => Dual("自身", "self"),
            "current_target" => Dual("当前目标", "the current target"),
            "all_enemies" => Dual("全体敌人", "all enemies"),
            "all_allies" => Dual("全体友方", "all allies"),
            "all_targets" => Dual("所有目标", "all targets"),
            _ => value
        };
    }

    private static string GetProperty(BehaviorGraphNodeDefinition node, string key, string defaultValue = "")
    {
        return node.Properties.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : defaultValue;
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
    }

    private static string Dual(string zh, string en)
    {
        return ModStudioLocalization.IsChinese ? zh : en;
    }
}
