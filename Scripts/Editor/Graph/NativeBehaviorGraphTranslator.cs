using STS2_Editor.Scripts.Editor.Core.Models;

namespace STS2_Editor.Scripts.Editor.Graph;

public enum NativeBehaviorTranslationStatus
{
    Supported = 0,
    Partial = 1,
    Unsupported = 2
}

public sealed record NativeBehaviorTranslationCapability(
    string Key,
    string Title,
    NativeBehaviorTranslationStatus Status,
    string Description,
    string[] Notes);

public sealed class NativeBehaviorGraphSource
{
    public ModStudioEntityKind EntityKind { get; set; }

    public string GraphId { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = "Native Behavior Graph";

    public string Description { get; set; } = string.Empty;

    public string TriggerId { get; set; } = string.Empty;

    public List<NativeBehaviorStep> Steps { get; set; } = new();

    public Dictionary<string, string> Metadata { get; set; } = new(StringComparer.Ordinal);
}

public sealed class NativeBehaviorStep
{
    public string Kind { get; set; } = string.Empty;

    public Dictionary<string, string> Parameters { get; set; } = new(StringComparer.Ordinal);

    public List<NativeBehaviorStep> TrueBranch { get; set; } = new();

    public List<NativeBehaviorStep> FalseBranch { get; set; } = new();
}

public sealed class NativeBehaviorGraphTranslationResult
{
    public BehaviorGraphDefinition Graph { get; set; } = new();

    public bool IsPartial { get; set; }

    public List<string> UnsupportedStepKinds { get; } = new();

    public List<string> AppliedStepKinds { get; } = new();

    public List<string> Warnings { get; } = new();
}

public sealed class NativeBehaviorGraphTranslator
{
    private readonly GraphDescriptionGenerator _descriptionGenerator = new();

    private static readonly IReadOnlyList<NativeBehaviorTranslationCapability> Catalog = new[]
    {
        new NativeBehaviorTranslationCapability(
            "combat.damage",
            "Damage",
            NativeBehaviorTranslationStatus.Supported,
            "Deals damage to the selected target.",
            new[] { "amount", "target", "props" }),
        new NativeBehaviorTranslationCapability(
            "combat.gain_block",
            "Gain Block",
            NativeBehaviorTranslationStatus.Supported,
            "Grants block to the selected target.",
            new[] { "amount", "target", "props" }),
        new NativeBehaviorTranslationCapability(
            "combat.heal",
            "Heal",
            NativeBehaviorTranslationStatus.Supported,
            "Heals the selected target.",
            new[] { "amount", "target" }),
        new NativeBehaviorTranslationCapability(
            "combat.draw_cards",
            "Draw Cards",
            NativeBehaviorTranslationStatus.Supported,
            "Draws cards for the owner player.",
            new[] { "amount" }),
        new NativeBehaviorTranslationCapability(
            "player.gain_energy",
            "Gain Energy",
            NativeBehaviorTranslationStatus.Supported,
            "Adds energy to the owner player.",
            new[] { "amount" }),
        new NativeBehaviorTranslationCapability(
            "player.gain_gold",
            "Gain Gold",
            NativeBehaviorTranslationStatus.Supported,
            "Adds gold to the owner player.",
            new[] { "amount" }),
        new NativeBehaviorTranslationCapability(
            "player.gain_stars",
            "Gain Stars",
            NativeBehaviorTranslationStatus.Supported,
            "Adds stars to the owner player.",
            new[] { "amount" }),
        new NativeBehaviorTranslationCapability(
            "combat.apply_power",
            "Apply Power",
            NativeBehaviorTranslationStatus.Supported,
            "Applies a power model to the selected target.",
            new[] { "power_id", "amount", "target" }),
        new NativeBehaviorTranslationCapability(
            "flow.branch",
            "Branch",
            NativeBehaviorTranslationStatus.Supported,
            "Splits execution into true and false branches.",
            new[] { "condition", "condition_key" }),
        new NativeBehaviorTranslationCapability(
            "event.reward",
            "Event Reward",
            NativeBehaviorTranslationStatus.Partial,
            "Can translate simple reward payloads such as gold, stars, energy, block, draw, heal, or damage.",
            new[] { "reward_kind", "amount", "target" }),
        new NativeBehaviorTranslationCapability(
            "event.choice",
            "Event Choice",
            NativeBehaviorTranslationStatus.Partial,
            "Can translate simple binary choice branches.",
            new[] { "condition", "true_branch", "false_branch" }),
        new NativeBehaviorTranslationCapability(
            "scene.cutscene",
            "Cutscene / Scene Action",
            NativeBehaviorTranslationStatus.Unsupported,
            "Complex scene-driven or animated actions are not translated in Phase 1.",
            new[] { "custom_scene", "timeline", "animation" }),
        new NativeBehaviorTranslationCapability(
            "deck.mutation",
            "Deck / Card Pool Mutation",
            NativeBehaviorTranslationStatus.Unsupported,
            "Direct deck mutation and complex card pool operations need a later phase.",
            new[] { "add_card", "remove_card", "transform_card" }),
        new NativeBehaviorTranslationCapability(
            "map.mutation",
            "Map / Room Mutation",
            NativeBehaviorTranslationStatus.Unsupported,
            "Map flow and room mutations are out of scope for the first translator pass.",
            new[] { "move_room", "spawn_room", "unlock_map" }),
        new NativeBehaviorTranslationCapability(
            "monster.ai",
            "Monster AI",
            NativeBehaviorTranslationStatus.Unsupported,
            "Monster AI scripting is not auto-translated in Phase 1.",
            new[] { "intent", "turn_script", "behavior_tree" })
    };

    public IReadOnlyList<NativeBehaviorTranslationCapability> SupportCatalog => Catalog;

    public NativeBehaviorGraphTranslationResult Translate(NativeBehaviorGraphSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var result = new NativeBehaviorGraphTranslationResult();
        var graph = BehaviorGraphTemplateFactory.CreateDefaultScaffold(source.GraphId, source.EntityKind, source.Name, source.Description, source.TriggerId);
        graph.Name = string.IsNullOrWhiteSpace(source.Name) ? graph.Name : source.Name;
        graph.Description = string.IsNullOrWhiteSpace(source.Description) ? graph.Description : source.Description;

        if (source.Metadata.Count > 0)
        {
            foreach (var pair in source.Metadata)
            {
                graph.Metadata[pair.Key] = pair.Value;
            }
        }

        var entryNodeId = graph.EntryNodeId;
        var exitNodeId = graph.Nodes.FirstOrDefault(node => string.Equals(node.NodeType, "flow.exit", StringComparison.Ordinal))?.NodeId ?? "exit";
        graph.Nodes = graph.Nodes
            .Where(node => string.Equals(node.NodeType, "flow.entry", StringComparison.Ordinal) ||
                           string.Equals(node.NodeType, "flow.exit", StringComparison.Ordinal))
            .ToList();
        graph.Connections.Clear();

        if (source.Steps.Count == 0)
        {
            graph.Connections.Add(new BehaviorGraphConnectionDefinition
            {
                FromNodeId = entryNodeId,
                FromPortId = "next",
                ToNodeId = exitNodeId,
                ToPortId = "in"
            });
            result.Warnings.Add("No native effect steps were provided; generated a scaffold only.");
            graph.Description = _descriptionGenerator.ResolveDescription(graph, source.Description);
            result.Graph = graph;
            return result;
        }

        var nodeIndex = 0;
        var tailNodeId = entryNodeId;
        var tailPortId = "next";
        var stopAfterCurrentSequence = false;

        foreach (var step in source.Steps)
        {
            if (IsBranchStep(step.Kind))
            {
                AppendBranchStep(graph, step, tailNodeId, tailPortId, exitNodeId, result, ref nodeIndex);
                result.AppliedStepKinds.Add(step.Kind);
                stopAfterCurrentSequence = true;
                break;
            }

            var node = CreateActionNode(step, ref nodeIndex, result);
            result.AppliedStepKinds.Add(step.Kind);
            graph.Nodes.Add(node);
            graph.Connections.Add(new BehaviorGraphConnectionDefinition
            {
                FromNodeId = tailNodeId,
                FromPortId = tailPortId,
                ToNodeId = node.NodeId,
                ToPortId = "in"
            });
            tailNodeId = node.NodeId;
            tailPortId = "out";
        }

        if (!stopAfterCurrentSequence)
        {
            graph.Connections.Add(new BehaviorGraphConnectionDefinition
            {
                FromNodeId = tailNodeId,
                FromPortId = tailPortId,
                ToNodeId = exitNodeId,
                ToPortId = "in"
            });
        }

        graph.Description = _descriptionGenerator.ResolveDescription(graph, source.Description);
        result.Graph = graph;
        result.IsPartial = result.UnsupportedStepKinds.Count > 0;
        return result;
    }

    public static IReadOnlyList<NativeBehaviorTranslationCapability> GetSupportCatalog()
    {
        return Catalog;
    }

    private static BehaviorGraphNodeDefinition CreateActionNode(NativeBehaviorStep step, ref int nodeIndex, NativeBehaviorGraphTranslationResult result)
    {
        var normalizedKind = NormalizeStepKind(step.Kind);
        var nodeId = ResolveNodeId(step, normalizedKind, nodeIndex++);

        return normalizedKind switch
        {
            "combat.damage" => BuildNode(nodeId, "combat.damage", "Damage", step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = GetParameter(step, "amount", "0"),
                ["target"] = GetParameter(step, "target", "current_target"),
                ["props"] = GetParameter(step, "props", "none")
            }),
            "combat.gain_block" or "block" => BuildNode(nodeId, "combat.gain_block", "Gain Block", step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = GetParameter(step, "amount", "0"),
                ["target"] = GetParameter(step, "target", "self"),
                ["props"] = GetParameter(step, "props", "none")
            }),
            "combat.heal" => BuildNode(nodeId, "combat.heal", "Heal", step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = GetParameter(step, "amount", "0"),
                ["target"] = GetParameter(step, "target", "self")
            }),
            "combat.draw_cards" or "draw" => BuildNode(nodeId, "combat.draw_cards", "Draw Cards", step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = GetParameter(step, "amount", "1")
            }),
            "player.gain_energy" or "energy" => BuildNode(nodeId, "player.gain_energy", "Gain Energy", step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = GetParameter(step, "amount", "1")
            }),
            "player.gain_gold" or "gold" => BuildNode(nodeId, "player.gain_gold", "Gain Gold", step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = GetParameter(step, "amount", "1")
            }),
            "player.gain_stars" or "stars" => BuildNode(nodeId, "player.gain_stars", "Gain Stars", step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = GetParameter(step, "amount", "1")
            }),
            "combat.apply_power" or "power" => BuildNode(nodeId, "combat.apply_power", "Apply Power", step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["power_id"] = GetParameter(step, "power_id", string.Empty),
                ["amount"] = GetParameter(step, "amount", "1"),
                ["target"] = GetParameter(step, "target", "current_target")
            }),
            "event.reward" => TranslateRewardStep(step, nodeId, result),
            "debug.log" or "log" => BuildNode(nodeId, "debug.log", "Log Message", step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["message"] = GetParameter(step, "message", "Native behavior step executed.")
            }),
            _ => TranslateUnsupportedStep(step, nodeId, result)
        };
    }

    private static BehaviorGraphNodeDefinition TranslateRewardStep(NativeBehaviorStep step, string nodeId, NativeBehaviorGraphTranslationResult result)
    {
        var rewardKind = GetParameter(step, "reward_kind", "gold").Trim().ToLowerInvariant();
        return rewardKind switch
        {
            "gold" => BuildNode(nodeId, "player.gain_gold", "Reward Gold", step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = GetParameter(step, "amount", "1")
            }),
            "energy" => BuildNode(nodeId, "player.gain_energy", "Reward Energy", step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = GetParameter(step, "amount", "1")
            }),
            "stars" => BuildNode(nodeId, "player.gain_stars", "Reward Stars", step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = GetParameter(step, "amount", "1")
            }),
            "draw" => BuildNode(nodeId, "combat.draw_cards", "Reward Draw", step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = GetParameter(step, "amount", "1")
            }),
            "block" => BuildNode(nodeId, "combat.gain_block", "Reward Block", step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = GetParameter(step, "amount", "1"),
                ["target"] = GetParameter(step, "target", "self"),
                ["props"] = "none"
            }),
            "heal" => BuildNode(nodeId, "combat.heal", "Reward Heal", step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = GetParameter(step, "amount", "1"),
                ["target"] = GetParameter(step, "target", "self")
            }),
            "damage" => BuildNode(nodeId, "combat.damage", "Reward Damage", step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = GetParameter(step, "amount", "1"),
                ["target"] = GetParameter(step, "target", "current_target"),
                ["props"] = GetParameter(step, "props", "none")
            }),
            "power" => BuildNode(nodeId, "combat.apply_power", "Reward Power", step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["power_id"] = GetParameter(step, "power_id", string.Empty),
                ["amount"] = GetParameter(step, "amount", "1"),
                ["target"] = GetParameter(step, "target", "current_target")
            }),
            _ => TranslateUnsupportedStep(step, nodeId, result)
        };
    }

    private static void AppendBranchStep(
        BehaviorGraphDefinition graph,
        NativeBehaviorStep step,
        string tailNodeId,
        string tailPortId,
        string exitNodeId,
        NativeBehaviorGraphTranslationResult result,
        ref int nodeIndex)
    {
        var branchNodeId = ResolveNodeId(step, "flow.branch", nodeIndex++);
        var branchNode = BuildNode(branchNodeId, "flow.branch", GetDisplayName(step, "Branch"), step, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["condition"] = GetParameter(step, "condition", GetParameter(step, "condition_key", string.Empty))
        });
        graph.Nodes.Add(branchNode);
        graph.Connections.Add(new BehaviorGraphConnectionDefinition
        {
            FromNodeId = tailNodeId,
            FromPortId = tailPortId,
            ToNodeId = branchNode.NodeId,
            ToPortId = "in"
        });

        AppendBranchSequence(graph, step.TrueBranch, branchNode.NodeId, "true", exitNodeId, result, ref nodeIndex);
        AppendBranchSequence(graph, step.FalseBranch, branchNode.NodeId, "false", exitNodeId, result, ref nodeIndex);

        result.AppliedStepKinds.Add(step.Kind);
    }

    private static void AppendBranchSequence(
        BehaviorGraphDefinition graph,
        IReadOnlyList<NativeBehaviorStep> branchSteps,
        string previousNodeId,
        string previousPortId,
        string exitNodeId,
        NativeBehaviorGraphTranslationResult result,
        ref int nodeIndex)
    {
        if (branchSteps.Count == 0)
        {
            graph.Connections.Add(new BehaviorGraphConnectionDefinition
            {
                FromNodeId = previousNodeId,
                FromPortId = previousPortId,
                ToNodeId = exitNodeId,
                ToPortId = "in"
            });
            return;
        }

        var currentNodeId = previousNodeId;
        var currentPortId = previousPortId;
        foreach (var step in branchSteps)
        {
            if (IsBranchStep(step.Kind))
            {
                AppendBranchStep(graph, step, currentNodeId, currentPortId, exitNodeId, result, ref nodeIndex);
                return;
            }

            var node = CreateActionNode(step, ref nodeIndex, result);
            result.AppliedStepKinds.Add(step.Kind);
            graph.Nodes.Add(node);
            graph.Connections.Add(new BehaviorGraphConnectionDefinition
            {
                FromNodeId = currentNodeId,
                FromPortId = currentPortId,
                ToNodeId = node.NodeId,
                ToPortId = "in"
            });
            currentNodeId = node.NodeId;
            currentPortId = "out";
        }

        graph.Connections.Add(new BehaviorGraphConnectionDefinition
        {
            FromNodeId = currentNodeId,
            FromPortId = currentPortId,
            ToNodeId = exitNodeId,
            ToPortId = "in"
        });
    }

    private static BehaviorGraphNodeDefinition TranslateUnsupportedStep(NativeBehaviorStep step, string nodeId, NativeBehaviorGraphTranslationResult result)
    {
        var normalizedKind = NormalizeStepKind(step.Kind);
        result.UnsupportedStepKinds.Add(step.Kind);
        result.Warnings.Add($"Unsupported native step '{step.Kind}' was translated as a debug placeholder.");

        return BuildNode(nodeId, "debug.log", GetDisplayName(step, normalizedKind), step, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["message"] = $"Unsupported native step: {step.Kind}"
        });
    }

    private static BehaviorGraphNodeDefinition BuildNode(string nodeId, string nodeType, string displayName, NativeBehaviorStep step, Dictionary<string, string> properties)
    {
        return new BehaviorGraphNodeDefinition
        {
            NodeId = nodeId,
            NodeType = nodeType,
            DisplayName = displayName,
            Description = GetParameter(step, "description", string.Empty),
            Properties = properties
        };
    }

    private static bool IsBranchStep(string? kind)
    {
        var normalized = NormalizeStepKind(kind);
        return normalized is "flow.branch" or "branch" or "event.choice";
    }

    private static string ResolveNodeId(NativeBehaviorStep step, string normalizedKind, int nodeIndex)
    {
        if (step.Parameters.TryGetValue("node_id", out var customNodeId) && !string.IsNullOrWhiteSpace(customNodeId))
        {
            return customNodeId.Trim();
        }

        return $"{normalizedKind.Replace('.', '_')}_{nodeIndex}";
    }

    private static string GetDisplayName(NativeBehaviorStep step, string fallback)
    {
        return GetParameter(step, "display_name", fallback);
    }

    private static string GetParameter(NativeBehaviorStep step, string key, string defaultValue)
    {
        return step.Parameters.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : defaultValue;
    }

    private static string NormalizeStepKind(string? kind)
    {
        return string.IsNullOrWhiteSpace(kind)
            ? string.Empty
            : kind.Trim().ToLowerInvariant();
    }

    public static IReadOnlyList<NativeBehaviorTranslationCapability> GetSupportCatalogSnapshot()
    {
        return Catalog;
    }
}
