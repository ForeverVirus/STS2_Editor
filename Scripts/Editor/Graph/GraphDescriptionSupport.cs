using System.Globalization;
using System.Text.RegularExpressions;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using STS2_Editor.Scripts.Editor.Core.Utilities;

namespace STS2_Editor.Scripts.Editor.Graph;

internal static class GraphDescriptionSupport
{
    private static readonly Regex AmountTokenRegex = new(@"\{Amount(?=[:}])", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex WholeAmountTokenRegex = new(@"\{Amount(?:[^}]*)\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static IReadOnlyList<BehaviorGraphNodeDefinition> BuildPrimaryActionSequence(BehaviorGraphDefinition graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var nodesById = graph.Nodes.ToDictionary(node => node.NodeId, StringComparer.Ordinal);
        var outgoing = graph.Connections
            .GroupBy(connection => connection.FromNodeId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

        var ordered = new List<BehaviorGraphNodeDefinition>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var currentNodeId = graph.EntryNodeId;
        while (!string.IsNullOrWhiteSpace(currentNodeId) &&
               outgoing.TryGetValue(currentNodeId, out var connections) &&
               TryResolvePrimaryNextNodeId(connections, out var nextNodeId) &&
               nodesById.TryGetValue(nextNodeId, out var nextNode))
        {
            if (!visited.Add(nextNode.NodeId))
            {
                break;
            }

            if (string.Equals(nextNode.NodeType, "flow.exit", StringComparison.Ordinal))
            {
                break;
            }

            if (!string.Equals(nextNode.NodeType, "flow.entry", StringComparison.Ordinal) &&
                !string.Equals(nextNode.NodeType, "flow.sequence", StringComparison.Ordinal))
            {
                ordered.Add(nextNode);
            }

            currentNodeId = nextNode.NodeId;
        }

        if (ordered.Count > 0)
        {
            return ordered;
        }

        return graph.Nodes
            .Where(node => !string.Equals(node.NodeType, "flow.entry", StringComparison.Ordinal) &&
                           !string.Equals(node.NodeType, "flow.exit", StringComparison.Ordinal) &&
                           !string.Equals(node.NodeType, "flow.sequence", StringComparison.Ordinal))
            .ToList();
    }

    public static string GetCountTemplate(BehaviorGraphNodeDefinition node, string propertyKey = "count", string defaultLiteral = "1")
    {
        var definition = DynamicValueEvaluator.GetDefinition(node, propertyKey, defaultLiteral);
        return DynamicValueEvaluator.GetAuthoringTemplate(definition);
    }

    public static bool TryBuildPowerTemplate(BehaviorGraphNodeDefinition node, out string text)
    {
        text = string.Empty;
        var powerId = GetProperty(node, "power_id", string.Empty);
        if (string.IsNullOrWhiteSpace(powerId))
        {
            return false;
        }

        var power = ResolvePower(powerId);
        if (power == null || !TryGetPowerRawTemplate(power, out var rawTemplate))
        {
            return false;
        }

        var amountDefinition = DynamicValueEvaluator.GetDefinition(node, "amount", "1");
        text = ReplaceAmountTemplate(rawTemplate, amountDefinition);
        return !string.IsNullOrWhiteSpace(text);
    }

    public static bool TryBuildPowerPreview(BehaviorGraphNodeDefinition node, DynamicValuePreviewResult amountPreview, out string text)
    {
        text = string.Empty;
        var powerId = GetProperty(node, "power_id", string.Empty);
        if (string.IsNullOrWhiteSpace(powerId))
        {
            return false;
        }

        var power = ResolvePower(powerId);
        if (power == null)
        {
            return false;
        }

        if (TryFormatPowerPreview(power, amountPreview, out text))
        {
            return true;
        }

        if (!TryGetPowerRawTemplate(power, out var rawTemplate))
        {
            return false;
        }

        text = ReplaceAmountPreview(rawTemplate, amountPreview.PreviewText);
        return !string.IsNullOrWhiteSpace(text);
    }

    public static string DescribeTarget(string value)
    {
        return value switch
        {
            "self" => Dual("自身", "self"),
            "current_target" => Dual("当前目标", "the current target"),
            "other_enemies" => Dual("其他敌人", "all other enemies"),
            "all_enemies" => Dual("所有敌人", "all enemies"),
            "all_allies" => Dual("所有友方", "all allies"),
            "all_targets" => Dual("所有目标", "all targets"),
            _ => value
        };
    }

    public static string DescribeOrb(string orbId)
    {
        if (TryResolveOrbName(orbId, out var orbName))
        {
            return orbName;
        }

        return string.IsNullOrWhiteSpace(orbId)
            ? Dual("球体", "orb")
            : orbId;
    }

    public static string BuildChannelOrbDescription(string orbId, string countText = "1")
    {
        var resolvedCount = string.IsNullOrWhiteSpace(countText) ? "1" : countText.Trim();
        var orbName = DescribeOrb(orbId);
        return Dual(
            $"充能{resolvedCount}个{orbName}。",
            $"Channel {resolvedCount} {orbName}.");
    }

    public static string TrimSentenceEnding(string text)
    {
        return string.IsNullOrWhiteSpace(text)
            ? string.Empty
            : text.Trim().TrimEnd('。', '.', '!', '?');
    }

    public static string Dual(string zh, string en)
    {
        return ModStudioLocalization.IsChinese ? zh : en;
    }

    public static string GetProperty(BehaviorGraphNodeDefinition node, string key, string defaultValue = "")
    {
        return node.Properties.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : defaultValue;
    }

    private static bool TryResolvePrimaryNextNodeId(
        IReadOnlyList<BehaviorGraphConnectionDefinition> connections,
        out string nextNodeId)
    {
        foreach (var preferredPort in new[] { "out", "next", "true", "false" })
        {
            var preferred = connections.FirstOrDefault(connection =>
                string.Equals(connection.FromPortId, preferredPort, StringComparison.Ordinal));
            if (preferred != null && !string.IsNullOrWhiteSpace(preferred.ToNodeId))
            {
                nextNodeId = preferred.ToNodeId;
                return true;
            }
        }

        var fallback = connections.FirstOrDefault(connection => !string.IsNullOrWhiteSpace(connection.ToNodeId));
        if (fallback != null)
        {
            nextNodeId = fallback.ToNodeId;
            return true;
        }

        nextNodeId = string.Empty;
        return false;
    }

    private static bool TryResolveOrbName(string orbId, out string text)
    {
        text = string.Empty;
        if (string.IsNullOrWhiteSpace(orbId))
        {
            return false;
        }

        try
        {
            var orb = ModelDb.Orbs.FirstOrDefault(candidate =>
                string.Equals(candidate.Id.Entry, orbId, StringComparison.OrdinalIgnoreCase));
            if (orb == null)
            {
                return false;
            }

            text = SafeLocText(orb.Title);
        }
        catch
        {
            text = string.Empty;
        }

        return !string.IsNullOrWhiteSpace(text);
    }

    private static PowerModel? ResolvePower(string powerId)
    {
        try
        {
            var resolved = ModelDb.AllPowers.FirstOrDefault(power =>
                string.Equals(power.Id.Entry, powerId, StringComparison.OrdinalIgnoreCase));
            if (resolved != null)
            {
                return resolved;
            }
        }
        catch
        {
        }

        var powerType = typeof(PowerModel).Assembly.GetTypes()
            .FirstOrDefault(type =>
                typeof(PowerModel).IsAssignableFrom(type) &&
                !type.IsAbstract &&
                TryBuildModelIdFromType(type, out var candidateId) &&
                string.Equals(candidateId, powerId, StringComparison.OrdinalIgnoreCase));
        if (powerType == null)
        {
            return null;
        }

        try
        {
            var modelId = ModelDb.GetId(powerType);
            return ModelDb.GetByIdOrNull<PowerModel>(modelId);
        }
        catch
        {
            return null;
        }
    }

    private static bool TryGetPowerRawTemplate(PowerModel power, out string rawTemplate)
    {
        rawTemplate = string.Empty;

        try
        {
            var locString = power.HasSmartDescription ? power.SmartDescription : power.Description;
            rawTemplate = locString.GetRawText() ?? string.Empty;
        }
        catch
        {
            rawTemplate = string.Empty;
        }

        return !string.IsNullOrWhiteSpace(rawTemplate);
    }

    private static bool TryFormatPowerPreview(PowerModel power, DynamicValuePreviewResult amountPreview, out string previewText)
    {
        previewText = string.Empty;

        try
        {
            var locString = power.HasSmartDescription ? power.SmartDescription : power.Description;
            locString.Add("Amount", amountPreview.Value);
            locString.Add("OnPlayer", true);
            locString.Add("IsMultiplayer", false);
            locString.Add("PlayerCount", 1);
            locString.Add("OwnerName", Dual("你", "you"));
            locString.Add("ApplierName", Dual("你", "you"));
            locString.Add("TargetName", Dual("目标", "target"));
            power.DynamicVars.AddTo(locString);
            previewText = locString.GetFormattedText() ?? string.Empty;
        }
        catch
        {
            previewText = string.Empty;
        }

        return !string.IsNullOrWhiteSpace(previewText);
    }

    private static string SafeLocText(LocString? locString)
    {
        if (locString == null)
        {
            return string.Empty;
        }

        return NativeLocalizationTableFallback.TryGetText(locString);
    }

    private static bool TryBuildModelIdFromType(Type targetType, out string id)
    {
        id = string.Empty;
        if (!typeof(AbstractModel).IsAssignableFrom(targetType))
        {
            return false;
        }

        var typeName = targetType.Name;
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return false;
        }

        var builder = new System.Text.StringBuilder(typeName.Length + 8);
        for (var index = 0; index < typeName.Length; index++)
        {
            var current = typeName[index];
            if (index > 0 && char.IsUpper(current) &&
                (char.IsLower(typeName[index - 1]) ||
                 (index + 1 < typeName.Length && char.IsLower(typeName[index + 1]))))
            {
                builder.Append('_');
            }

            builder.Append(char.ToUpperInvariant(current));
        }

        id = builder.ToString();
        return !string.IsNullOrWhiteSpace(id);
    }

    private static string ReplaceAmountTemplate(string rawTemplate, DynamicValueDefinition amountDefinition)
    {
        if (string.IsNullOrWhiteSpace(rawTemplate))
        {
            return string.Empty;
        }

        var authoringTemplate = DynamicValueEvaluator.GetAuthoringTemplate(amountDefinition);
        var result = rawTemplate.Replace("{Amount}", authoringTemplate, StringComparison.Ordinal);
        if (TryResolveTemplateCompatibleTokenKey(amountDefinition, out var tokenKey))
        {
            result = AmountTokenRegex.Replace(result, "{" + tokenKey);
            return result;
        }

        return WholeAmountTokenRegex.Replace(result, authoringTemplate);
    }

    private static string ReplaceAmountPreview(string rawTemplate, string previewText)
    {
        if (string.IsNullOrWhiteSpace(rawTemplate))
        {
            return string.Empty;
        }

        var resolvedText = string.IsNullOrWhiteSpace(previewText) ? "0" : previewText;
        var result = rawTemplate.Replace("{Amount}", resolvedText, StringComparison.Ordinal);
        return WholeAmountTokenRegex.Replace(result, resolvedText);
    }

    private static bool TryResolveTemplateCompatibleTokenKey(DynamicValueDefinition amountDefinition, out string tokenKey)
    {
        tokenKey = string.Empty;

        if (amountDefinition.SourceKind == DynamicValueSourceKind.DynamicVar &&
            amountDefinition.BaseOverrideMode == DynamicValueOverrideMode.None &&
            !string.IsNullOrWhiteSpace(amountDefinition.DynamicVarName))
        {
            tokenKey = amountDefinition.DynamicVarName;
            return true;
        }

        if (amountDefinition.SourceKind == DynamicValueSourceKind.FormulaRef &&
            amountDefinition.BaseOverrideMode == DynamicValueOverrideMode.None &&
            amountDefinition.ExtraOverrideMode == DynamicValueOverrideMode.None &&
            string.IsNullOrWhiteSpace(amountDefinition.PreviewMultiplierKey))
        {
            tokenKey = string.IsNullOrWhiteSpace(amountDefinition.FormulaRef)
                ? amountDefinition.DynamicVarName
                : amountDefinition.FormulaRef;
            return !string.IsNullOrWhiteSpace(tokenKey);
        }

        return false;
    }
}
