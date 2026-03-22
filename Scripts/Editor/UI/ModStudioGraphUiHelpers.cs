using System.Text;
using STS2_Editor.Scripts.Editor.Core.Models;
using STS2_Editor.Scripts.Editor.Core.Utilities;
using STS2_Editor.Scripts.Editor.Graph;

namespace STS2_Editor.Scripts.Editor.UI;

internal static class ModStudioGraphUiHelpers
{
    public static IReadOnlyList<GraphTemplatePreset> GetPresets(ModStudioEntityKind kind)
    {
        return BehaviorGraphTemplateFactory
            .GetTemplates(kind)
            .Select(template => new GraphTemplatePreset(template))
            .ToList();
    }

    public static BehaviorGraphDefinition CreateScaffoldGraph(ModStudioEntityKind kind, string graphId, string entityId)
    {
        return BehaviorGraphTemplateFactory.CreateDefaultScaffold(graphId, kind);
    }

    public static BehaviorGraphDefinition CreatePresetGraph(ModStudioEntityKind kind, string graphId, string entityId, GraphTemplatePreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);
        if (BehaviorGraphTemplateFactory.TryCreateTemplate(preset.TemplateId, graphId, kind, out var graph) && graph != null)
        {
            return graph;
        }

        return BehaviorGraphTemplateFactory.CreateDefaultScaffold(graphId, kind);
    }

    public static string BuildNodeCatalogText(BehaviorGraphRegistry registry, ModStudioEntityKind selectedKind)
    {
        ArgumentNullException.ThrowIfNull(registry);

        var builder = new StringBuilder();
        builder.AppendLine(ModStudioLocalization.F("graph.node_catalog_header", ModStudioLocalization.GetEntityKindDisplayName(selectedKind)));
        builder.AppendLine(ModStudioLocalization.T("graph.node_catalog_authoritative"));
        builder.AppendLine();

        var presets = GetPresets(selectedKind);
        if (presets.Count > 0)
        {
            builder.AppendLine(ModStudioLocalization.T("graph.recommended_presets"));
            foreach (var preset in presets)
            {
                builder.AppendLine($"- {preset.Name}: {preset.Description}");
                builder.AppendLine($"  {ModStudioLocalization.F("graph.preset_trigger", preset.TriggerId)}");
            }

            builder.AppendLine();
        }

        foreach (var group in registry.Definitions
                     .OrderBy(definition => GetNodeGroupRank(definition.NodeType))
                     .ThenBy(definition => definition.DisplayName, StringComparer.Ordinal)
                     .GroupBy(definition => GetNodeGroup(definition.NodeType), StringComparer.Ordinal))
        {
            builder.AppendLine($"[{group.Key}]");
            foreach (var definition in group)
            {
                builder.AppendLine($"- {definition.NodeType} | {definition.DisplayName}");
                if (!string.IsNullOrWhiteSpace(definition.Description))
                {
                    builder.AppendLine($"  {definition.Description}");
                }

                builder.AppendLine($"  {ModStudioLocalization.F("graph.catalog.inputs", FormatPorts(definition.Inputs))}");
                builder.AppendLine($"  {ModStudioLocalization.F("graph.catalog.outputs", FormatPorts(definition.Outputs))}");
                if (definition.DefaultProperties.Count > 0)
                {
                    builder.AppendLine($"  {ModStudioLocalization.F("graph.catalog.defaults", FormatDictionary(definition.DefaultProperties))}");
                }
            }

            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    public static string BuildValidationText(
        BehaviorGraphDefinition? graph,
        BehaviorGraphValidationResult? validation,
        string? parseError = null)
    {
        var builder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(parseError))
        {
            builder.AppendLine(ModStudioLocalization.T("graph.validation.parse_failed"));
            builder.AppendLine(parseError);
            return builder.ToString().TrimEnd();
        }

        if (graph == null)
        {
            builder.AppendLine(ModStudioLocalization.T("graph.validation.no_graph"));
            return builder.ToString().TrimEnd();
        }

        var entityKind = graph.EntityKind.HasValue
            ? ModStudioLocalization.GetEntityKindDisplayName(graph.EntityKind.Value)
            : ModStudioLocalization.T("state.none");
        builder.AppendLine(ModStudioLocalization.F("graph.validation.graph", graph.GraphId));
        builder.AppendLine(ModStudioLocalization.F("graph.validation.name", graph.Name));
        builder.AppendLine(ModStudioLocalization.F("graph.validation.entity_kind", entityKind));
        builder.AppendLine(ModStudioLocalization.F("graph.validation.entry", graph.EntryNodeId));
        builder.AppendLine(ModStudioLocalization.F("graph.validation.nodes", graph.Nodes.Count));
        builder.AppendLine(ModStudioLocalization.F("graph.validation.connections", graph.Connections.Count));
        builder.AppendLine(ModStudioLocalization.T(validation?.IsValid == true ? "graph.validation.valid" : "graph.validation.invalid"));

        if (validation != null)
        {
            if (validation.Errors.Count > 0)
            {
                builder.AppendLine(ModStudioLocalization.T("graph.validation.errors"));
                foreach (var error in validation.Errors)
                {
                    builder.AppendLine($"- {error}");
                }
            }

            if (validation.Warnings.Count > 0)
            {
                builder.AppendLine(ModStudioLocalization.T("graph.validation.warnings"));
                foreach (var warning in validation.Warnings)
                {
                    builder.AppendLine($"- {warning}");
                }
            }
        }

        return builder.ToString().TrimEnd();
    }

    public static string DescribePreset(GraphTemplatePreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);
        return $"{preset.Name}{Environment.NewLine}{preset.Description}{Environment.NewLine}{ModStudioLocalization.F("graph.preset_trigger", preset.TriggerId)}";
    }

    private static string FormatPorts(IReadOnlyList<BehaviorGraphPortDefinition> ports)
    {
        if (ports.Count == 0)
        {
            return "none";
        }

        return string.Join(", ", ports.Select(port =>
        {
            var required = port.IsRequired ? "!" : string.Empty;
            var displayName = string.IsNullOrWhiteSpace(port.DisplayName) ? port.PortId : port.DisplayName;
            return $"{port.PortId}:{port.ValueType}{required} ({displayName})";
        }));
    }

    private static string FormatDictionary(IReadOnlyDictionary<string, string> values)
    {
        return string.Join(", ", values.Select(pair => $"{pair.Key}={pair.Value}"));
    }

    private static string GetNodeGroup(string nodeType)
    {
        if (string.IsNullOrWhiteSpace(nodeType))
        {
            return "Other";
        }

        var prefix = nodeType.Split('.', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        return string.IsNullOrWhiteSpace(prefix) ? "Other" : prefix.ToUpperInvariant();
    }

    private static int GetNodeGroupRank(string nodeType)
    {
        return GetNodeGroup(nodeType) switch
        {
            "FLOW" => 0,
            "VALUE" => 1,
            "DEBUG" => 2,
            "COMBAT" => 3,
            "PLAYER" => 4,
            _ => 5
        };
    }
}

internal sealed class GraphTemplatePreset
{
    public GraphTemplatePreset(BehaviorGraphTemplateDescriptor descriptor)
    {
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
    }

    public BehaviorGraphTemplateDescriptor Descriptor { get; }

    public string TemplateId => Descriptor.TemplateId;

    public string Name => Descriptor.DisplayName;

    public string Description => Descriptor.Description;

    public string TriggerId => Descriptor.TriggerId;
}
