using STS2_Editor.Scripts.Editor.Graph;

namespace STS2_Editor.Scripts.Editor.Runtime;

public sealed class EventGraphCompiler
{
    private const string StartPageMetadataKey = "event_start_page_id";
    private const string EventPagePrefix = "event_page.";
    private const string EventOptionPrefix = "event_option.";

    public EventGraphValidationResult Compile(BehaviorGraphDefinition graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var result = new EventGraphValidationResult
        {
            EventId = graph.GraphId
        };

        if (graph.EntityKind is not STS2_Editor.Scripts.Editor.Core.Models.ModStudioEntityKind.Event)
        {
            result.AddWarning($"Graph '{graph.GraphId}' is not marked as an event graph, but compilation was requested.");
        }

        var nodesById = graph.Nodes
            .Where(node => !string.IsNullOrWhiteSpace(node.NodeId))
            .ToDictionary(node => node.NodeId, StringComparer.Ordinal);

        var outgoingByNode = graph.Connections
            .Where(connection => !string.IsNullOrWhiteSpace(connection.FromNodeId) && !string.IsNullOrWhiteSpace(connection.ToNodeId))
            .GroupBy(connection => connection.FromNodeId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

        var incomingByNode = graph.Connections
            .Where(connection => !string.IsNullOrWhiteSpace(connection.FromNodeId) && !string.IsNullOrWhiteSpace(connection.ToNodeId))
            .GroupBy(connection => connection.ToNodeId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

        var pages = new Dictionary<string, EventGraphPageDefinition>(StringComparer.Ordinal);
        foreach (var node in graph.Nodes.Where(node => IsNodeType(node.NodeType, "event.page")))
        {
            var page = BuildPageDefinition(node);
            if (string.IsNullOrWhiteSpace(page.PageId))
            {
                result.AddError($"Event page node '{node.NodeId}' is missing a page_id.");
                continue;
            }

            if (!pages.TryAdd(page.PageId, page))
            {
                result.AddError($"Duplicate event page id '{page.PageId}'.");
            }
        }

        if (pages.Count == 0)
        {
            result.AddError("Event graph does not contain any event.page nodes.");
        }

        var choices = new Dictionary<(string PageId, string OptionId), EventGraphChoiceBinding>();
        foreach (var node in graph.Nodes.Where(node => IsNodeType(node.NodeType, "event.option")))
        {
            var choice = BuildChoiceBinding(graph, node, pages, incomingByNode, result);
            if (choice == null)
            {
                continue;
            }

            var key = (choice.PageId, choice.OptionId);
            if (!choices.TryAdd(key, choice))
            {
                result.AddError($"Duplicate event option id '{choice.OptionId}' on page '{choice.PageId}'.");
                continue;
            }
        }

        foreach (var pair in choices)
        {
            if (!pages.TryGetValue(pair.Key.PageId, out var page))
            {
                continue;
            }

            page.Choices[pair.Key.OptionId] = pair.Value;
            result.MutableChoices.Add(pair.Value);
        }

        foreach (var page in pages.Values)
        {
            if (page.OptionOrder.Count == 0 && page.Choices.Count > 0)
            {
                foreach (var optionId in page.Choices.Values.OrderBy(choice => choice.OptionId, StringComparer.Ordinal).Select(choice => choice.OptionId))
                {
                    page.OptionOrder.Add(optionId);
                }
            }
        }

        var startPageId = ResolveStartPageId(graph, pages, result);
        result.StartPageId = startPageId;
        result.Metadata[StartPageMetadataKey] = startPageId;

        foreach (var page in pages.Values.OrderBy(page => page.PageId, StringComparer.Ordinal))
        {
            CompilePageMetadata(page, result.Metadata);
            result.MutablePages.Add(page);
        }

        result.Seal();
        return result;
    }

    public bool TryCompile(BehaviorGraphDefinition graph, out EventGraphValidationResult result)
    {
        result = Compile(graph);
        return result.IsValid;
    }

    private static EventGraphPageDefinition BuildPageDefinition(BehaviorGraphNodeDefinition node)
    {
        var page = new EventGraphPageDefinition
        {
            PageId = GetProperty(node, "page_id", node.NodeId).Trim(),
            Title = GetProperty(node, "title", string.Empty).Trim(),
            Description = GetProperty(node, "description", string.Empty).Trim(),
            IsStart = GetBoolProperty(node, "is_start", false)
        };

        foreach (var optionId in ParseList(GetProperty(node, "option_order", string.Empty)))
        {
            page.OptionOrder.Add(optionId);
        }

        return page;
    }

    private static EventGraphChoiceBinding? BuildChoiceBinding(
        BehaviorGraphDefinition graph,
        BehaviorGraphNodeDefinition node,
        IReadOnlyDictionary<string, EventGraphPageDefinition> pages,
        IReadOnlyDictionary<string, List<BehaviorGraphConnectionDefinition>> incomingByNode,
        EventGraphValidationResult result)
    {
        var optionId = GetProperty(node, "option_id", node.NodeId).Trim();
        if (string.IsNullOrWhiteSpace(optionId))
        {
            result.AddError($"Event option node '{node.NodeId}' is missing an option_id.");
            return null;
        }

        var pageId = ResolvePageId(node, pages, incomingByNode);
        if (string.IsNullOrWhiteSpace(pageId))
        {
            result.AddError($"Event option '{optionId}' is missing a page_id or page connection.");
            return null;
        }

        var binding = new EventGraphChoiceBinding
        {
            PageId = pageId,
            OptionId = optionId,
            Title = GetProperty(node, "title", optionId).Trim(),
            Description = GetProperty(node, "description", string.Empty).Trim(),
            NextPageId = NormalizeOptional(GetProperty(node, "next_page_id", string.Empty)),
            EncounterId = NormalizeOptional(GetProperty(node, "encounter_id", string.Empty)),
            ResumePageId = NormalizeOptional(GetProperty(node, "resume_page_id", string.Empty)),
            IsProceed = GetBoolProperty(node, "is_proceed", false),
            SaveChoiceToHistory = GetBoolProperty(node, "save_choice_to_history", true),
            RewardKind = NormalizeOptional(GetProperty(node, "reward_kind", string.Empty)),
            RewardAmount = NormalizeOptional(GetProperty(node, "reward_amount", string.Empty)),
            RewardTarget = NormalizeOptional(GetProperty(node, "reward_target", string.Empty)),
            RewardProps = NormalizeOptional(GetProperty(node, "reward_props", string.Empty)),
            RewardPowerId = NormalizeOptional(GetProperty(node, "reward_power_id", string.Empty)),
            RewardCardId = NormalizeOptional(GetProperty(node, "card_id", string.Empty)),
            RewardRelicId = NormalizeOptional(GetProperty(node, "relic_id", string.Empty)),
            RewardPotionId = NormalizeOptional(GetProperty(node, "potion_id", string.Empty)),
            RewardCount = NormalizeOptional(GetProperty(node, "reward_count", string.Empty))
        };

        foreach (var actionNode in TraverseActionChain(graph, node.NodeId))
        {
            ApplyActionNode(actionNode, binding, result);
        }

        return binding;
    }

    private static void ApplyActionNode(BehaviorGraphNodeDefinition node, EventGraphChoiceBinding binding, EventGraphValidationResult result)
    {
        switch (Normalize(node.NodeType))
        {
            case "event.goto_page":
                binding.NextPageId = NormalizeOptional(GetProperty(node, "next_page_id", binding.NextPageId ?? string.Empty)) ?? binding.NextPageId;
                break;
            case "event.start_combat":
                binding.EncounterId = NormalizeOptional(GetProperty(node, "encounter_id", binding.EncounterId ?? string.Empty)) ?? binding.EncounterId;
                binding.ResumePageId = NormalizeOptional(GetProperty(node, "resume_page_id", binding.ResumePageId ?? string.Empty)) ?? binding.ResumePageId;
                break;
            case "event.proceed":
                binding.IsProceed = true;
                break;
            case "event.reward":
                binding.RewardKind = NormalizeOptional(GetProperty(node, "reward_kind", binding.RewardKind ?? string.Empty)) ?? binding.RewardKind;
                binding.RewardAmount = NormalizeOptional(GetProperty(node, "reward_amount", binding.RewardAmount ?? string.Empty)) ?? binding.RewardAmount;
                binding.RewardTarget = NormalizeOptional(GetProperty(node, "reward_target", binding.RewardTarget ?? string.Empty)) ?? binding.RewardTarget;
                binding.RewardProps = NormalizeOptional(GetProperty(node, "reward_props", binding.RewardProps ?? string.Empty)) ?? binding.RewardProps;
                binding.RewardPowerId = NormalizeOptional(GetProperty(node, "reward_power_id", binding.RewardPowerId ?? string.Empty)) ?? binding.RewardPowerId;
                break;
            case "reward.offer_custom":
                binding.RewardKind = NormalizeOptional(GetProperty(node, "reward_kind", binding.RewardKind ?? string.Empty)) ?? binding.RewardKind;
                binding.RewardAmount = NormalizeOptional(GetProperty(node, "amount", binding.RewardAmount ?? string.Empty)) ?? binding.RewardAmount;
                binding.RewardCount = NormalizeOptional(GetProperty(node, "reward_count", binding.RewardCount ?? string.Empty)) ?? binding.RewardCount;
                binding.RewardCardId = NormalizeOptional(GetProperty(node, "card_id", binding.RewardCardId ?? string.Empty)) ?? binding.RewardCardId;
                binding.RewardRelicId = NormalizeOptional(GetProperty(node, "relic_id", binding.RewardRelicId ?? string.Empty)) ?? binding.RewardRelicId;
                binding.RewardPotionId = NormalizeOptional(GetProperty(node, "potion_id", binding.RewardPotionId ?? string.Empty)) ?? binding.RewardPotionId;
                break;
            case "event.option":
            case "event.page":
                break;
            default:
                result.AddWarning($"Unsupported event action node '{node.NodeType}' on '{node.NodeId}' was ignored by the compiler.");
                break;
        }
    }

    private static IEnumerable<BehaviorGraphNodeDefinition> TraverseActionChain(
        BehaviorGraphDefinition graph,
        string startNodeId)
    {
        var nodesById = graph.Nodes.ToDictionary(node => node.NodeId, StringComparer.Ordinal);
        var outgoingByNode = graph.Connections
            .GroupBy(connection => connection.FromNodeId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

        var queue = new Queue<string>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        if (outgoingByNode.TryGetValue(startNodeId, out var connections))
        {
            foreach (var connection in connections)
            {
                queue.Enqueue(connection.ToNodeId);
            }
        }

        while (queue.Count > 0)
        {
            var nodeId = queue.Dequeue();
            if (!visited.Add(nodeId))
            {
                continue;
            }

            if (!nodesById.TryGetValue(nodeId, out var node))
            {
                continue;
            }

            var nodeType = Normalize(node.NodeType);
            if (nodeType is not ("event.goto_page" or "event.start_combat" or "event.proceed" or "event.reward" or "reward.offer_custom"))
            {
                continue;
            }

            yield return node;

            if (outgoingByNode.TryGetValue(nodeId, out var nextConnections))
            {
                foreach (var next in nextConnections)
                {
                    queue.Enqueue(next.ToNodeId);
                }
            }
        }
    }

    private static string ResolvePageId(
        BehaviorGraphNodeDefinition optionNode,
        IReadOnlyDictionary<string, EventGraphPageDefinition> pages,
        IReadOnlyDictionary<string, List<BehaviorGraphConnectionDefinition>> incomingByNode)
    {
        var explicitPageId = NormalizeOptional(GetProperty(optionNode, "page_id", string.Empty));
        if (!string.IsNullOrWhiteSpace(explicitPageId))
        {
            return explicitPageId!;
        }

        if (!incomingByNode.TryGetValue(optionNode.NodeId, out var incoming))
        {
            return string.Empty;
        }

        foreach (var connection in incoming)
        {
            if (!pages.TryGetValue(connection.FromNodeId, out var page))
            {
                continue;
            }

            return page.PageId;
        }

        return string.Empty;
    }

    private static string ResolveStartPageId(
        BehaviorGraphDefinition graph,
        IReadOnlyDictionary<string, EventGraphPageDefinition> pages,
        EventGraphValidationResult result)
    {
        if (graph.Metadata.TryGetValue(StartPageMetadataKey, out var metadataStartPage) &&
            !string.IsNullOrWhiteSpace(metadataStartPage) &&
            pages.ContainsKey(metadataStartPage.Trim()))
        {
            return metadataStartPage.Trim();
        }

        var explicitStartPage = pages.Values.FirstOrDefault(page => page.IsStart);
        if (explicitStartPage != null)
        {
            return explicitStartPage.PageId;
        }

        var firstPage = pages.Values.OrderBy(page => page.PageId, StringComparer.Ordinal).FirstOrDefault();
        if (firstPage != null)
        {
            return firstPage.PageId;
        }

        result.AddWarning("Event graph did not define a start page. Defaulting to INITIAL.");
        return "INITIAL";
    }

    private static void CompilePageMetadata(EventGraphPageDefinition page, IDictionary<string, string> metadata)
    {
        metadata[$"{EventPagePrefix}{page.PageId}.description"] = page.Description;
        if (!string.IsNullOrWhiteSpace(page.Title))
        {
            metadata[$"{EventPagePrefix}{page.PageId}.title"] = page.Title;
        }

        if (page.OptionOrder.Count > 0)
        {
            metadata[$"{EventPagePrefix}{page.PageId}.option_order"] = string.Join(",", page.OptionOrder);
        }
        else if (page.Choices.Count > 0)
        {
            metadata[$"{EventPagePrefix}{page.PageId}.option_order"] = string.Join(",", page.Choices.Values.OrderBy(choice => choice.OptionId, StringComparer.Ordinal).Select(choice => choice.OptionId));
        }

        foreach (var choice in page.Choices.Values.OrderBy(choice => choice.OptionId, StringComparer.Ordinal))
        {
            var prefix = $"{EventOptionPrefix}{page.PageId}.{choice.OptionId}";
            metadata[$"{prefix}.title"] = choice.Title;
            metadata[$"{prefix}.description"] = choice.Description;
            if (!string.IsNullOrWhiteSpace(choice.NextPageId))
            {
                metadata[$"{prefix}.next_page_id"] = choice.NextPageId!;
            }

            if (!string.IsNullOrWhiteSpace(choice.EncounterId))
            {
                metadata[$"{prefix}.encounter_id"] = choice.EncounterId!;
            }

            if (!string.IsNullOrWhiteSpace(choice.ResumePageId))
            {
                metadata[$"{prefix}.resume_page_id"] = choice.ResumePageId!;
            }

            metadata[$"{prefix}.is_proceed"] = choice.IsProceed.ToString();
            metadata[$"{prefix}.save_choice_to_history"] = choice.SaveChoiceToHistory.ToString();

            if (!string.IsNullOrWhiteSpace(choice.RewardKind))
            {
                metadata[$"{prefix}.reward_kind"] = choice.RewardKind!;
            }

            if (!string.IsNullOrWhiteSpace(choice.RewardAmount))
            {
                metadata[$"{prefix}.reward_amount"] = choice.RewardAmount!;
            }

            if (!string.IsNullOrWhiteSpace(choice.RewardTarget))
            {
                metadata[$"{prefix}.reward_target"] = choice.RewardTarget!;
            }

            if (!string.IsNullOrWhiteSpace(choice.RewardProps))
            {
                metadata[$"{prefix}.reward_props"] = choice.RewardProps!;
            }

            if (!string.IsNullOrWhiteSpace(choice.RewardPowerId))
            {
                metadata[$"{prefix}.reward_power_id"] = choice.RewardPowerId!;
            }

            if (!string.IsNullOrWhiteSpace(choice.RewardCardId))
            {
                metadata[$"{prefix}.card_id"] = choice.RewardCardId!;
            }

            if (!string.IsNullOrWhiteSpace(choice.RewardRelicId))
            {
                metadata[$"{prefix}.relic_id"] = choice.RewardRelicId!;
            }

            if (!string.IsNullOrWhiteSpace(choice.RewardPotionId))
            {
                metadata[$"{prefix}.potion_id"] = choice.RewardPotionId!;
            }

            if (!string.IsNullOrWhiteSpace(choice.RewardCount))
            {
                metadata[$"{prefix}.reward_count"] = choice.RewardCount!;
            }
        }
    }

    private static string GetProperty(BehaviorGraphNodeDefinition node, string key, string defaultValue = "")
    {
        return node.Properties.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : defaultValue;
    }

    private static bool GetBoolProperty(BehaviorGraphNodeDefinition node, string key, bool defaultValue)
    {
        return node.Properties.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed)
            ? parsed
            : defaultValue;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool IsNodeType(string? nodeType, string expected)
    {
        return string.Equals(Normalize(nodeType), expected, StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> ParseList(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Array.Empty<string>();
        }

        return raw
            .Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
    }
}
