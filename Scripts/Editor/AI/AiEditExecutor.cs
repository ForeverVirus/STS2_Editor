using STS2_Editor.Scripts.Editor.Core.Models;
using STS2_Editor.Scripts.Editor.Core.Services;
using STS2_Editor.Scripts.Editor.Graph;
using STS2_Editor.Scripts.Editor.UI;

namespace STS2_Editor.Scripts.Editor.AI;

public sealed class AiEditExecutor
{
    private const string LayoutKeyPrefix = "layout.node.";

    private readonly ModelMetadataService _metadataService;
    private readonly ProjectAssetBindingService _assetBindingService;
    private readonly BehaviorGraphRegistry _graphRegistry;
    private readonly NativeBehaviorAutoGraphService _autoGraphService = new();

    public AiEditExecutor(
        ModelMetadataService metadataService,
        ProjectAssetBindingService assetBindingService,
        BehaviorGraphRegistry graphRegistry)
    {
        _metadataService = metadataService;
        _assetBindingService = assetBindingService;
        _graphRegistry = graphRegistry;
    }

    public AiPlanPreview Preview(EditorProject project, AiEditPlan plan, AiExecutionContext context)
    {
        var preview = new AiPlanPreview
        {
            ProjectSnapshot = CloneProject(project),
            Plan = plan
        };

        var entityRefs = new Dictionary<string, (ModStudioEntityKind Kind, string EntityId)>(StringComparer.Ordinal);
        var nodeRefs = new Dictionary<string, (ModStudioEntityKind Kind, string EntityId, string NodeId)>(StringComparer.Ordinal);

        foreach (var operation in plan.Operations)
        {
            ExecuteOperation(preview, operation, context, entityRefs, nodeRefs);
        }

        return preview;
    }

    private void ExecuteOperation(
        AiPlanPreview preview,
        AiEditOperation operation,
        AiExecutionContext context,
        Dictionary<string, (ModStudioEntityKind Kind, string EntityId)> entityRefs,
        Dictionary<string, (ModStudioEntityKind Kind, string EntityId, string NodeId)> nodeRefs)
    {
        switch (Normalize(operation.Type))
        {
            case "create_entity":
                ExecuteCreateEntity(preview, operation, entityRefs);
                return;
            case "set_basic_fields":
                ExecuteSetBasicFields(preview, operation, context, entityRefs);
                return;
            case "set_behavior_mode":
                ExecuteSetBehaviorMode(preview, operation, context, entityRefs);
                return;
            case "set_asset_binding":
                ExecuteSetAssetBinding(preview, operation, context, entityRefs);
                return;
            case "clear_asset_binding":
                ExecuteClearAssetBinding(preview, operation, context, entityRefs);
                return;
            case "ensure_graph":
                ExecuteEnsureGraph(preview, operation, context, entityRefs);
                return;
            case "set_graph_meta":
                ExecuteSetGraphMeta(preview, operation, context, entityRefs);
                return;
            case "set_graph_entry":
                ExecuteSetGraphEntry(preview, operation, context, entityRefs, nodeRefs);
                return;
            case "add_graph_node":
                ExecuteAddGraphNode(preview, operation, context, entityRefs, nodeRefs);
                return;
            case "update_graph_node":
                ExecuteUpdateGraphNode(preview, operation, context, entityRefs, nodeRefs);
                return;
            case "remove_graph_node":
                ExecuteRemoveGraphNode(preview, operation, context, entityRefs, nodeRefs);
                return;
            case "connect_graph_nodes":
                ExecuteConnectGraphNodes(preview, operation, context, entityRefs, nodeRefs);
                return;
            case "disconnect_graph_nodes":
                ExecuteDisconnectGraphNodes(preview, operation, context, entityRefs, nodeRefs);
                return;
            default:
                preview.ErrorLines.Add($"Unsupported operation type '{operation.Type}'.");
                return;
        }
    }

    private void ExecuteCreateEntity(
        AiPlanPreview preview,
        AiEditOperation operation,
        Dictionary<string, (ModStudioEntityKind Kind, string EntityId)> entityRefs)
    {
        if (!TryParseEntityKind(operation.EntityKind, out var kind))
        {
            preview.ErrorLines.Add("create_entity requires a valid entity_kind.");
            return;
        }

        var entityId = string.IsNullOrWhiteSpace(operation.EntityId)
            ? _metadataService.GenerateProjectEntityId(preview.ProjectSnapshot, kind)
            : operation.EntityId.Trim();

        if (EntityExists(preview.ProjectSnapshot, kind, entityId))
        {
            preview.ErrorLines.Add($"Entity '{kind}:{entityId}' already exists.");
            return;
        }

        var metadata = _metadataService.CreateDefaultMetadata(kind, entityId);
        preview.ProjectSnapshot.Overrides.Add(new EntityOverrideEnvelope
        {
            EntityKind = kind,
            EntityId = entityId,
            BehaviorSource = BehaviorSource.Native,
            Metadata = new Dictionary<string, string>(metadata, StringComparer.Ordinal)
        });

        preview.FocusEntityKind = kind;
        preview.FocusEntityId = entityId;
        preview.SummaryLines.Add($"Create {kind}:{entityId}");

        if (!string.IsNullOrWhiteSpace(operation.OpRef))
        {
            entityRefs[operation.OpRef] = (kind, entityId);
        }
    }

    private void ExecuteSetBasicFields(
        AiPlanPreview preview,
        AiEditOperation operation,
        AiExecutionContext context,
        Dictionary<string, (ModStudioEntityKind Kind, string EntityId)> entityRefs)
    {
        if (!TryResolveEntity(preview.ProjectSnapshot, operation, context, entityRefs, out var kind, out var entityId))
        {
            preview.ErrorLines.Add("set_basic_fields could not resolve its target entity.");
            return;
        }

        if (operation.Fields.Count == 0)
        {
            preview.WarningLines.Add($"set_basic_fields for {kind}:{entityId} did not include any fields.");
            return;
        }

        var allowedKeys = GetAllowedBasicFieldKeys(preview.ProjectSnapshot, kind, entityId);
        var envelope = GetOrCreateEnvelope(preview.ProjectSnapshot, kind, entityId);
        foreach (var pair in operation.Fields)
        {
            if (!allowedKeys.Contains(pair.Key, StringComparer.OrdinalIgnoreCase))
            {
                preview.ErrorLines.Add($"Basic field '{pair.Key}' is not valid for {kind}:{entityId}.");
                continue;
            }

            envelope.Metadata[pair.Key] = pair.Value ?? string.Empty;
            preview.SummaryLines.Add($"Set {kind}:{entityId}.{pair.Key} -> {pair.Value}");
        }

        preview.FocusEntityKind ??= kind;
        preview.FocusEntityId = string.IsNullOrWhiteSpace(preview.FocusEntityId) ? entityId : preview.FocusEntityId;
    }

    private void ExecuteSetBehaviorMode(
        AiPlanPreview preview,
        AiEditOperation operation,
        AiExecutionContext context,
        Dictionary<string, (ModStudioEntityKind Kind, string EntityId)> entityRefs)
    {
        if (!TryResolveEntity(preview.ProjectSnapshot, operation, context, entityRefs, out var kind, out var entityId))
        {
            preview.ErrorLines.Add("set_behavior_mode could not resolve its target entity.");
            return;
        }

        var modeText = Normalize(operation.BehaviorMode);
        if (modeText is not ("graph" or "native"))
        {
            preview.ErrorLines.Add($"Unsupported behavior mode '{operation.BehaviorMode}'.");
            return;
        }

        var envelope = GetOrCreateEnvelope(preview.ProjectSnapshot, kind, entityId);
        envelope.BehaviorSource = modeText == "graph" ? BehaviorSource.Graph : BehaviorSource.Native;
        preview.SummaryLines.Add($"Set {kind}:{entityId} behavior mode -> {envelope.BehaviorSource}");
        SetFocus(preview, kind, entityId);
    }

    private void ExecuteSetAssetBinding(
        AiPlanPreview preview,
        AiEditOperation operation,
        AiExecutionContext context,
        Dictionary<string, (ModStudioEntityKind Kind, string EntityId)> entityRefs)
    {
        if (!TryResolveEntity(preview.ProjectSnapshot, operation, context, entityRefs, out var kind, out var entityId))
        {
            preview.ErrorLines.Add("set_asset_binding could not resolve its target entity.");
            return;
        }

        var sourceKind = Normalize(operation.AssetSourceKind);
        var sourceValue = operation.AssetValue?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(sourceValue))
        {
            preview.ErrorLines.Add("set_asset_binding requires asset_value.");
            return;
        }

        try
        {
            switch (sourceKind)
            {
                case "runtime":
                    var runtimeCandidates = _assetBindingService.GetRuntimeAssetCandidates(kind);
                    if (!runtimeCandidates.Contains(sourceValue, StringComparer.Ordinal))
                    {
                        preview.ErrorLines.Add($"Runtime asset '{sourceValue}' is not available for {kind}:{entityId}.");
                        return;
                    }

                    _assetBindingService.BindRuntimeAsset(preview.ProjectSnapshot, kind, entityId, sourceValue);
                    preview.SummaryLines.Add($"Bind runtime asset for {kind}:{entityId}");
                    break;
                case "imported":
                    _assetBindingService.BindProjectAsset(preview.ProjectSnapshot, kind, entityId, sourceValue);
                    preview.SummaryLines.Add($"Bind imported asset '{sourceValue}' for {kind}:{entityId}");
                    break;
                default:
                    preview.ErrorLines.Add($"Unsupported asset_source_kind '{operation.AssetSourceKind}'.");
                    break;
            }
        }
        catch (Exception ex)
        {
            preview.ErrorLines.Add(ex.Message);
        }

        SetFocus(preview, kind, entityId);
    }

    private void ExecuteClearAssetBinding(
        AiPlanPreview preview,
        AiEditOperation operation,
        AiExecutionContext context,
        Dictionary<string, (ModStudioEntityKind Kind, string EntityId)> entityRefs)
    {
        if (!TryResolveEntity(preview.ProjectSnapshot, operation, context, entityRefs, out var kind, out var entityId))
        {
            preview.ErrorLines.Add("clear_asset_binding could not resolve its target entity.");
            return;
        }

        try
        {
            _assetBindingService.ClearAssetBinding(preview.ProjectSnapshot, kind, entityId);
            preview.SummaryLines.Add($"Clear asset binding for {kind}:{entityId}");
        }
        catch (Exception ex)
        {
            preview.ErrorLines.Add(ex.Message);
        }

        SetFocus(preview, kind, entityId);
    }

    private void ExecuteEnsureGraph(
        AiPlanPreview preview,
        AiEditOperation operation,
        AiExecutionContext context,
        Dictionary<string, (ModStudioEntityKind Kind, string EntityId)> entityRefs)
    {
        if (!TryResolveEntity(preview.ProjectSnapshot, operation, context, entityRefs, out var kind, out var entityId))
        {
            preview.ErrorLines.Add("ensure_graph could not resolve its target entity.");
            return;
        }

        if (TryGetWorkingGraph(preview.ProjectSnapshot, kind, entityId, out _, out var created, out var createdFromAutoGraph) &&
            created)
        {
            preview.SummaryLines.Add(createdFromAutoGraph
                ? $"Create editable graph from native auto graph for {kind}:{entityId}"
                : $"Create scaffold graph for {kind}:{entityId}");
        }

        SetFocus(preview, kind, entityId);
    }

    private void ExecuteSetGraphMeta(
        AiPlanPreview preview,
        AiEditOperation operation,
        AiExecutionContext context,
        Dictionary<string, (ModStudioEntityKind Kind, string EntityId)> entityRefs)
    {
        if (!TryResolveEntity(preview.ProjectSnapshot, operation, context, entityRefs, out var kind, out var entityId))
        {
            preview.ErrorLines.Add("set_graph_meta could not resolve its target entity.");
            return;
        }

        if (!TryGetWorkingGraph(preview.ProjectSnapshot, kind, entityId, out var graph, out _, out _))
        {
            preview.ErrorLines.Add($"Graph could not be created for {kind}:{entityId}.");
            return;
        }

        if (!string.IsNullOrWhiteSpace(operation.GraphName))
        {
            graph.Name = operation.GraphName.Trim();
            preview.SummaryLines.Add($"Set graph name for {kind}:{entityId}");
        }

        if (!string.IsNullOrWhiteSpace(operation.GraphDescription))
        {
            graph.Description = operation.GraphDescription.Trim();
            preview.SummaryLines.Add($"Set graph description for {kind}:{entityId}");
        }

        if (!string.IsNullOrWhiteSpace(operation.GraphId) &&
            !string.Equals(graph.GraphId, operation.GraphId, StringComparison.Ordinal))
        {
            if (preview.ProjectSnapshot.Graphs.ContainsKey(operation.GraphId))
            {
                preview.ErrorLines.Add($"Graph id '{operation.GraphId}' already exists.");
                return;
            }

            RenameGraph(preview.ProjectSnapshot, kind, entityId, graph, operation.GraphId.Trim());
            preview.SummaryLines.Add($"Rename graph id for {kind}:{entityId} -> {operation.GraphId}");
        }

        SetFocus(preview, kind, entityId);
    }

    private void ExecuteSetGraphEntry(
        AiPlanPreview preview,
        AiEditOperation operation,
        AiExecutionContext context,
        Dictionary<string, (ModStudioEntityKind Kind, string EntityId)> entityRefs,
        Dictionary<string, (ModStudioEntityKind Kind, string EntityId, string NodeId)> nodeRefs)
    {
        if (!TryResolveEntity(preview.ProjectSnapshot, operation, context, entityRefs, out var kind, out var entityId))
        {
            preview.ErrorLines.Add("set_graph_entry could not resolve its target entity.");
            return;
        }

        if (!TryGetWorkingGraph(preview.ProjectSnapshot, kind, entityId, out var graph, out _, out _))
        {
            preview.ErrorLines.Add($"Graph could not be created for {kind}:{entityId}.");
            return;
        }

        if (!TryResolveNode(graph, kind, entityId, operation.NodeId, operation.NodeRef, nodeRefs, out var node))
        {
            preview.ErrorLines.Add("set_graph_entry could not resolve its target node.");
            return;
        }

        graph.EntryNodeId = node.NodeId;
        preview.SummaryLines.Add($"Set graph entry for {kind}:{entityId} -> {node.NodeId}");
        SetFocus(preview, kind, entityId);
    }

    private void ExecuteAddGraphNode(
        AiPlanPreview preview,
        AiEditOperation operation,
        AiExecutionContext context,
        Dictionary<string, (ModStudioEntityKind Kind, string EntityId)> entityRefs,
        Dictionary<string, (ModStudioEntityKind Kind, string EntityId, string NodeId)> nodeRefs)
    {
        if (!TryResolveEntity(preview.ProjectSnapshot, operation, context, entityRefs, out var kind, out var entityId))
        {
            preview.ErrorLines.Add("add_graph_node could not resolve its target entity.");
            return;
        }

        if (!TryGetWorkingGraph(preview.ProjectSnapshot, kind, entityId, out var graph, out _, out _))
        {
            preview.ErrorLines.Add($"Graph could not be created for {kind}:{entityId}.");
            return;
        }

        var nodeType = operation.NodeType?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(nodeType) || !_graphRegistry.TryGetNodeDefinition(nodeType, out var definition) || definition == null)
        {
            preview.ErrorLines.Add($"Unknown node type '{operation.NodeType}'.");
            return;
        }

        if (!BehaviorGraphPaletteFilter.IsAllowed(kind, nodeType))
        {
            preview.ErrorLines.Add($"Node type '{nodeType}' is not allowed for {kind} graphs.");
            return;
        }

        var nodeId = string.IsNullOrWhiteSpace(operation.NodeId)
            ? GenerateNodeId(graph, nodeType)
            : operation.NodeId.Trim();
        if (graph.Nodes.Any(existing => string.Equals(existing.NodeId, nodeId, StringComparison.Ordinal)))
        {
            preview.ErrorLines.Add($"Node id '{nodeId}' already exists.");
            return;
        }

        var node = new BehaviorGraphNodeDefinition
        {
            NodeId = nodeId,
            NodeType = nodeType,
            DisplayName = string.IsNullOrWhiteSpace(operation.DisplayName) ? definition.DisplayName : operation.DisplayName.Trim(),
            Description = operation.Description?.Trim() ?? string.Empty,
            Properties = definition.DefaultProperties != null
                ? new Dictionary<string, string>(definition.DefaultProperties, StringComparer.Ordinal)
                : new Dictionary<string, string>(StringComparer.Ordinal)
        };

        if ((string.Equals(nodeType, "orb.channel", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(nodeType, "orb.passive", StringComparison.OrdinalIgnoreCase)) &&
            (!node.Properties.TryGetValue("orb_id", out var orbId) || string.IsNullOrWhiteSpace(orbId)))
        {
            var defaultOrbId = FieldChoiceProvider.GetGraphChoices("orb_id").FirstOrDefault().Value;
            if (!string.IsNullOrWhiteSpace(defaultOrbId))
            {
                node.Properties["orb_id"] = defaultOrbId;
            }
        }

        foreach (var pair in operation.Properties)
        {
            if (!ApplyDynamicNodePropertyChange(node, pair.Key, pair.Value))
            {
                node.Properties[pair.Key] = pair.Value;
            }
        }

        graph.Nodes.Add(node);
        SetSuggestedNodeLayout(graph, node.NodeId, operation.NearNodeId, operation.NearNodeRef, nodeRefs);
        preview.SummaryLines.Add($"Add graph node {node.NodeType}:{node.NodeId} to {kind}:{entityId}");
        SetFocus(preview, kind, entityId);

        if (!string.IsNullOrWhiteSpace(operation.OpRef))
        {
            nodeRefs[operation.OpRef] = (kind, entityId, node.NodeId);
        }
    }

    private void ExecuteUpdateGraphNode(
        AiPlanPreview preview,
        AiEditOperation operation,
        AiExecutionContext context,
        Dictionary<string, (ModStudioEntityKind Kind, string EntityId)> entityRefs,
        Dictionary<string, (ModStudioEntityKind Kind, string EntityId, string NodeId)> nodeRefs)
    {
        if (!TryResolveEntity(preview.ProjectSnapshot, operation, context, entityRefs, out var kind, out var entityId))
        {
            preview.ErrorLines.Add("update_graph_node could not resolve its target entity.");
            return;
        }

        if (!TryGetWorkingGraph(preview.ProjectSnapshot, kind, entityId, out var graph, out _, out _))
        {
            preview.ErrorLines.Add($"Graph could not be created for {kind}:{entityId}.");
            return;
        }

        if (!TryResolveNode(graph, kind, entityId, operation.NodeId, operation.NodeRef, nodeRefs, out var node))
        {
            preview.ErrorLines.Add("update_graph_node could not resolve its target node.");
            return;
        }

        if (!string.IsNullOrWhiteSpace(operation.DisplayName))
        {
            node.DisplayName = operation.DisplayName.Trim();
        }

        if (operation.Description != null)
        {
            node.Description = operation.Description.Trim();
        }

        foreach (var pair in operation.Properties)
        {
            if (!ApplyDynamicNodePropertyChange(node, pair.Key, pair.Value))
            {
                node.Properties[pair.Key] = pair.Value ?? string.Empty;
            }
        }

        preview.SummaryLines.Add($"Update graph node {node.NodeId} in {kind}:{entityId}");
        SetFocus(preview, kind, entityId);
    }

    private void ExecuteRemoveGraphNode(
        AiPlanPreview preview,
        AiEditOperation operation,
        AiExecutionContext context,
        Dictionary<string, (ModStudioEntityKind Kind, string EntityId)> entityRefs,
        Dictionary<string, (ModStudioEntityKind Kind, string EntityId, string NodeId)> nodeRefs)
    {
        if (!TryResolveEntity(preview.ProjectSnapshot, operation, context, entityRefs, out var kind, out var entityId))
        {
            preview.ErrorLines.Add("remove_graph_node could not resolve its target entity.");
            return;
        }

        if (!TryGetWorkingGraph(preview.ProjectSnapshot, kind, entityId, out var graph, out _, out _))
        {
            preview.ErrorLines.Add($"Graph could not be created for {kind}:{entityId}.");
            return;
        }

        if (!TryResolveNode(graph, kind, entityId, operation.NodeId, operation.NodeRef, nodeRefs, out var node))
        {
            preview.ErrorLines.Add("remove_graph_node could not resolve its target node.");
            return;
        }

        graph.Nodes.RemoveAll(existing => string.Equals(existing.NodeId, node.NodeId, StringComparison.Ordinal));
        graph.Connections.RemoveAll(connection =>
            string.Equals(connection.FromNodeId, node.NodeId, StringComparison.Ordinal) ||
            string.Equals(connection.ToNodeId, node.NodeId, StringComparison.Ordinal));
        graph.Metadata.Remove($"{LayoutKeyPrefix}{node.NodeId}.x");
        graph.Metadata.Remove($"{LayoutKeyPrefix}{node.NodeId}.y");
        NormalizeEntryNode(graph);
        preview.SummaryLines.Add($"Remove graph node {node.NodeId} from {kind}:{entityId}");
        SetFocus(preview, kind, entityId);
    }

    private void ExecuteConnectGraphNodes(
        AiPlanPreview preview,
        AiEditOperation operation,
        AiExecutionContext context,
        Dictionary<string, (ModStudioEntityKind Kind, string EntityId)> entityRefs,
        Dictionary<string, (ModStudioEntityKind Kind, string EntityId, string NodeId)> nodeRefs)
    {
        if (!TryResolveEntity(preview.ProjectSnapshot, operation, context, entityRefs, out var kind, out var entityId))
        {
            preview.ErrorLines.Add("connect_graph_nodes could not resolve its target entity.");
            return;
        }

        if (!TryGetWorkingGraph(preview.ProjectSnapshot, kind, entityId, out var graph, out _, out _))
        {
            preview.ErrorLines.Add($"Graph could not be created for {kind}:{entityId}.");
            return;
        }

        if (!TryResolveNode(graph, kind, entityId, operation.FromNodeId, operation.FromNodeRef, nodeRefs, out var fromNode))
        {
            preview.ErrorLines.Add("connect_graph_nodes could not resolve from_node.");
            return;
        }

        if (!TryResolveNode(graph, kind, entityId, operation.ToNodeId, operation.ToNodeRef, nodeRefs, out var toNode))
        {
            preview.ErrorLines.Add("connect_graph_nodes could not resolve to_node.");
            return;
        }

        if (!HasOutputPort(fromNode.NodeType, operation.FromPortId))
        {
            preview.ErrorLines.Add($"Node '{fromNode.NodeId}' does not expose output port '{operation.FromPortId}'.");
            return;
        }

        if (!HasInputPort(toNode.NodeType, operation.ToPortId))
        {
            preview.ErrorLines.Add($"Node '{toNode.NodeId}' does not expose input port '{operation.ToPortId}'.");
            return;
        }

        var exists = graph.Connections.Any(connection =>
            string.Equals(connection.FromNodeId, fromNode.NodeId, StringComparison.Ordinal) &&
            string.Equals(connection.FromPortId, operation.FromPortId, StringComparison.Ordinal) &&
            string.Equals(connection.ToNodeId, toNode.NodeId, StringComparison.Ordinal) &&
            string.Equals(connection.ToPortId, operation.ToPortId, StringComparison.Ordinal));
        if (exists)
        {
            preview.WarningLines.Add($"Connection {fromNode.NodeId}:{operation.FromPortId} -> {toNode.NodeId}:{operation.ToPortId} already exists.");
            return;
        }

        graph.Connections.Add(new BehaviorGraphConnectionDefinition
        {
            FromNodeId = fromNode.NodeId,
            FromPortId = operation.FromPortId,
            ToNodeId = toNode.NodeId,
            ToPortId = operation.ToPortId
        });
        preview.SummaryLines.Add($"Connect {fromNode.NodeId}:{operation.FromPortId} -> {toNode.NodeId}:{operation.ToPortId}");
        SetFocus(preview, kind, entityId);
    }

    private void ExecuteDisconnectGraphNodes(
        AiPlanPreview preview,
        AiEditOperation operation,
        AiExecutionContext context,
        Dictionary<string, (ModStudioEntityKind Kind, string EntityId)> entityRefs,
        Dictionary<string, (ModStudioEntityKind Kind, string EntityId, string NodeId)> nodeRefs)
    {
        if (!TryResolveEntity(preview.ProjectSnapshot, operation, context, entityRefs, out var kind, out var entityId))
        {
            preview.ErrorLines.Add("disconnect_graph_nodes could not resolve its target entity.");
            return;
        }

        if (!TryGetWorkingGraph(preview.ProjectSnapshot, kind, entityId, out var graph, out _, out _))
        {
            preview.ErrorLines.Add($"Graph could not be created for {kind}:{entityId}.");
            return;
        }

        if (!TryResolveNode(graph, kind, entityId, operation.FromNodeId, operation.FromNodeRef, nodeRefs, out var fromNode))
        {
            preview.ErrorLines.Add("disconnect_graph_nodes could not resolve from_node.");
            return;
        }

        if (!TryResolveNode(graph, kind, entityId, operation.ToNodeId, operation.ToNodeRef, nodeRefs, out var toNode))
        {
            preview.ErrorLines.Add("disconnect_graph_nodes could not resolve to_node.");
            return;
        }

        var removed = graph.Connections.RemoveAll(connection =>
            string.Equals(connection.FromNodeId, fromNode.NodeId, StringComparison.Ordinal) &&
            string.Equals(connection.FromPortId, operation.FromPortId, StringComparison.Ordinal) &&
            string.Equals(connection.ToNodeId, toNode.NodeId, StringComparison.Ordinal) &&
            string.Equals(connection.ToPortId, operation.ToPortId, StringComparison.Ordinal));
        if (removed == 0)
        {
            preview.WarningLines.Add($"Connection {fromNode.NodeId}:{operation.FromPortId} -> {toNode.NodeId}:{operation.ToPortId} was not present.");
            return;
        }

        preview.SummaryLines.Add($"Disconnect {fromNode.NodeId}:{operation.FromPortId} -> {toNode.NodeId}:{operation.ToPortId}");
        SetFocus(preview, kind, entityId);
    }

    private bool TryResolveEntity(
        EditorProject project,
        AiEditOperation operation,
        AiExecutionContext context,
        Dictionary<string, (ModStudioEntityKind Kind, string EntityId)> entityRefs,
        out ModStudioEntityKind kind,
        out string entityId)
    {
        if (!string.IsNullOrWhiteSpace(operation.EntityRef) &&
            entityRefs.TryGetValue(operation.EntityRef, out var entityTarget))
        {
            kind = entityTarget.Kind;
            entityId = entityTarget.EntityId;
            return true;
        }

        var kindText = string.IsNullOrWhiteSpace(operation.EntityKind)
            ? context.CurrentKind.ToString()
            : operation.EntityKind;
        if (!TryParseEntityKind(kindText, out kind))
        {
            entityId = string.Empty;
            return false;
        }

        entityId = string.IsNullOrWhiteSpace(operation.EntityId)
            ? context.CurrentEntityId
            : operation.EntityId.Trim();
        if (string.IsNullOrWhiteSpace(entityId))
        {
            return false;
        }

        return EntityExists(project, kind, entityId);
    }

    private bool TryResolveNode(
        BehaviorGraphDefinition graph,
        ModStudioEntityKind kind,
        string entityId,
        string requestedNodeId,
        string requestedNodeRef,
        Dictionary<string, (ModStudioEntityKind Kind, string EntityId, string NodeId)> nodeRefs,
        out BehaviorGraphNodeDefinition node)
    {
        if (!string.IsNullOrWhiteSpace(requestedNodeRef) &&
            nodeRefs.TryGetValue(requestedNodeRef, out var nodeTarget) &&
            nodeTarget.Kind == kind &&
            string.Equals(nodeTarget.EntityId, entityId, StringComparison.Ordinal))
        {
            var referencedNode = graph.Nodes.FirstOrDefault(existing => string.Equals(existing.NodeId, nodeTarget.NodeId, StringComparison.Ordinal));
            if (referencedNode != null)
            {
                node = referencedNode;
                return true;
            }
        }

        var directNode = graph.Nodes.FirstOrDefault(existing => string.Equals(existing.NodeId, requestedNodeId, StringComparison.Ordinal));
        node = directNode!;
        return directNode != null;
    }

    private bool TryGetWorkingGraph(
        EditorProject project,
        ModStudioEntityKind kind,
        string entityId,
        out BehaviorGraphDefinition graph,
        out bool created,
        out bool createdFromAutoGraph)
    {
        created = false;
        createdFromAutoGraph = false;
        var envelope = GetOrCreateEnvelope(project, kind, entityId);
        if (!string.IsNullOrWhiteSpace(envelope.GraphId) &&
            project.Graphs.TryGetValue(envelope.GraphId, out graph!))
        {
            return true;
        }

        if (SafeHasRuntimeEntity(kind, entityId) &&
            _autoGraphService.TryCreateGraph(kind, entityId, out var autoGraphResult) &&
            autoGraphResult?.Graph != null)
        {
            graph = CloneGraph(autoGraphResult.Graph);
            graph.EntityKind = kind;
            project.Graphs[graph.GraphId] = graph;
            envelope.GraphId = graph.GraphId;
            envelope.BehaviorSource = BehaviorSource.Graph;
            created = true;
            createdFromAutoGraph = true;
            return true;
        }

        var graphId = $"ai_{kind.ToString().ToLowerInvariant()}_{Guid.NewGuid():N}";
        graph = BehaviorGraphTemplateFactory.CreateDefaultScaffold(graphId, kind, ResolveEntityTitle(project, kind, entityId), ResolveEntitySummary(project, kind, entityId));
        graph.EntityKind = kind;
        project.Graphs[graph.GraphId] = graph;
        envelope.GraphId = graph.GraphId;
        envelope.BehaviorSource = BehaviorSource.Graph;
        created = true;
        return true;
    }

    private static EntityOverrideEnvelope GetOrCreateEnvelope(EditorProject project, ModStudioEntityKind kind, string entityId)
    {
        var existing = project.Overrides.FirstOrDefault(item =>
            item.EntityKind == kind &&
            string.Equals(item.EntityId, entityId, StringComparison.Ordinal));
        if (existing != null)
        {
            return existing;
        }

        var envelope = new EntityOverrideEnvelope
        {
            EntityKind = kind,
            EntityId = entityId
        };
        project.Overrides.Add(envelope);
        return envelope;
    }

    private IReadOnlyCollection<string> GetAllowedBasicFieldKeys(EditorProject project, ModStudioEntityKind kind, string entityId)
    {
        var envelope = project.Overrides.FirstOrDefault(item =>
            item.EntityKind == kind &&
            string.Equals(item.EntityId, entityId, StringComparison.Ordinal));
        if (envelope != null && envelope.Metadata.Count > 0)
        {
            return envelope.Metadata.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        if (SafeHasRuntimeEntity(kind, entityId))
        {
            return SafeGetRuntimeMetadata(kind, entityId).Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        var defaults = _metadataService.CreateDefaultMetadata(kind, entityId).Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (envelope != null)
        {
            defaults.UnionWith(envelope.Metadata.Keys);
        }

        return defaults;
    }

    private bool EntityExists(EditorProject project, ModStudioEntityKind kind, string entityId)
    {
        if (project.Overrides.Any(item =>
                item.EntityKind == kind &&
                string.Equals(item.EntityId, entityId, StringComparison.Ordinal)))
        {
            return true;
        }

        return SafeHasRuntimeEntity(kind, entityId);
    }

    private string ResolveEntityTitle(EditorProject project, ModStudioEntityKind kind, string entityId)
    {
        var metadata = ResolveMergedMetadata(project, kind, entityId);
        return metadata.TryGetValue("title", out var title) && !string.IsNullOrWhiteSpace(title)
            ? title
            : entityId;
    }

    private string ResolveEntitySummary(EditorProject project, ModStudioEntityKind kind, string entityId)
    {
        var metadata = ResolveMergedMetadata(project, kind, entityId);
        return kind switch
        {
            ModStudioEntityKind.Card => metadata.TryGetValue("description", out var description) ? description : string.Empty,
            ModStudioEntityKind.Relic => metadata.TryGetValue("description", out var relicDescription) ? relicDescription : string.Empty,
            ModStudioEntityKind.Potion => metadata.TryGetValue("description", out var potionDescription) ? potionDescription : string.Empty,
            ModStudioEntityKind.Event => metadata.TryGetValue("initial_description", out var eventDescription) ? eventDescription : string.Empty,
            ModStudioEntityKind.Enchantment => metadata.TryGetValue("description", out var enchantmentDescription) ? enchantmentDescription : string.Empty,
            _ => string.Empty
        };
    }

    private bool SafeHasRuntimeEntity(ModStudioEntityKind kind, string entityId)
    {
        try
        {
            return _metadataService.HasRuntimeEntity(kind, entityId);
        }
        catch
        {
            return false;
        }
    }

    private IReadOnlyDictionary<string, string> SafeGetRuntimeMetadata(ModStudioEntityKind kind, string entityId)
    {
        try
        {
            return _metadataService.GetEditableMetadata(kind, entityId);
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    private IReadOnlyDictionary<string, string> ResolveMergedMetadata(EditorProject project, ModStudioEntityKind kind, string entityId)
    {
        var envelope = project.Overrides.FirstOrDefault(item =>
            item.EntityKind == kind &&
            string.Equals(item.EntityId, entityId, StringComparison.Ordinal));
        if (envelope != null && envelope.Metadata.Count > 0)
        {
            return new Dictionary<string, string>(envelope.Metadata, StringComparer.Ordinal);
        }

        IReadOnlyDictionary<string, string> baseline = SafeHasRuntimeEntity(kind, entityId)
            ? SafeGetRuntimeMetadata(kind, entityId)
            : _metadataService.CreateDefaultMetadata(kind, entityId);
        var result = new Dictionary<string, string>(baseline, StringComparer.Ordinal);
        if (envelope != null)
        {
            foreach (var pair in envelope.Metadata)
            {
                result[pair.Key] = pair.Value;
            }
        }

        return result;
    }

    private void RenameGraph(EditorProject project, ModStudioEntityKind kind, string entityId, BehaviorGraphDefinition graph, string newGraphId)
    {
        var envelope = GetOrCreateEnvelope(project, kind, entityId);
        var previousGraphId = graph.GraphId;
        graph.GraphId = newGraphId;
        project.Graphs.Remove(previousGraphId);
        project.Graphs[newGraphId] = graph;
        envelope.GraphId = newGraphId;
    }

    private static void SetFocus(AiPlanPreview preview, ModStudioEntityKind kind, string entityId)
    {
        preview.FocusEntityKind = kind;
        preview.FocusEntityId = entityId;
    }

    private void SetSuggestedNodeLayout(
        BehaviorGraphDefinition graph,
        string nodeId,
        string nearNodeId,
        string nearNodeRef,
        Dictionary<string, (ModStudioEntityKind Kind, string EntityId, string NodeId)> nodeRefs)
    {
        var anchorNodeId = !string.IsNullOrWhiteSpace(nearNodeId)
            ? nearNodeId
            : !string.IsNullOrWhiteSpace(nearNodeRef) && nodeRefs.TryGetValue(nearNodeRef, out var nodeTarget)
                ? nodeTarget.NodeId
                : string.Empty;
        if (TryGetNodePosition(graph, anchorNodeId, out var nearX, out var nearY))
        {
            graph.Metadata[$"{LayoutKeyPrefix}{nodeId}.x"] = (nearX + 360f).ToString(System.Globalization.CultureInfo.InvariantCulture);
            graph.Metadata[$"{LayoutKeyPrefix}{nodeId}.y"] = nearY.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return;
        }

        var positionedNodeIds = graph.Nodes
            .Select(existing => existing.NodeId)
            .Where(existing => TryGetNodePosition(graph, existing, out _, out _))
            .ToList();
        if (positionedNodeIds.Count > 0)
        {
            var maxX = positionedNodeIds
                .Select(existing => TryGetNodePosition(graph, existing, out var x, out _) ? x : 120f)
                .Max();
            graph.Metadata[$"{LayoutKeyPrefix}{nodeId}.x"] = (maxX + 360f).ToString(System.Globalization.CultureInfo.InvariantCulture);
            graph.Metadata[$"{LayoutKeyPrefix}{nodeId}.y"] = "120";
            return;
        }

        graph.Metadata[$"{LayoutKeyPrefix}{nodeId}.x"] = "120";
        graph.Metadata[$"{LayoutKeyPrefix}{nodeId}.y"] = "120";
    }

    private static bool TryGetNodePosition(BehaviorGraphDefinition graph, string nodeId, out float x, out float y)
    {
        x = 0f;
        y = 0f;
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return false;
        }

        var keyX = $"{LayoutKeyPrefix}{nodeId}.x";
        var keyY = $"{LayoutKeyPrefix}{nodeId}.y";
        return graph.Metadata.TryGetValue(keyX, out var xText) &&
               graph.Metadata.TryGetValue(keyY, out var yText) &&
               float.TryParse(xText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out x) &&
               float.TryParse(yText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out y);
    }

    private void NormalizeEntryNode(BehaviorGraphDefinition graph)
    {
        if (graph.Nodes.Any(node => string.Equals(node.NodeId, graph.EntryNodeId, StringComparison.Ordinal)))
        {
            return;
        }

        var flowEntry = graph.Nodes.FirstOrDefault(node => string.Equals(node.NodeType, "flow.entry", StringComparison.OrdinalIgnoreCase));
        graph.EntryNodeId = flowEntry?.NodeId ?? graph.Nodes.FirstOrDefault()?.NodeId ?? string.Empty;
    }

    private bool HasOutputPort(string nodeType, string portId)
    {
        return _graphRegistry.TryGetNodeDefinition(nodeType, out var definition) &&
               definition != null &&
               definition.Outputs.Any(port => string.Equals(port.PortId, portId, StringComparison.Ordinal));
    }

    private bool HasInputPort(string nodeType, string portId)
    {
        return _graphRegistry.TryGetNodeDefinition(nodeType, out var definition) &&
               definition != null &&
               definition.Inputs.Any(port => string.Equals(port.PortId, portId, StringComparison.Ordinal));
    }

    private static string GenerateNodeId(BehaviorGraphDefinition graph, string nodeType)
    {
        var baseId = nodeType.Replace('.', '_').ToLowerInvariant();
        var index = 1;
        var candidate = baseId;
        while (graph.Nodes.Any(node => string.Equals(node.NodeId, candidate, StringComparison.Ordinal)))
        {
            candidate = $"{baseId}_{index}";
            index++;
        }

        return candidate;
    }

    private static bool TryParseEntityKind(string rawValue, out ModStudioEntityKind kind)
    {
        return Enum.TryParse(rawValue, ignoreCase: true, out kind) &&
               kind is ModStudioEntityKind.Card or
                   ModStudioEntityKind.Relic or
                   ModStudioEntityKind.Potion or
                   ModStudioEntityKind.Event or
                   ModStudioEntityKind.Enchantment;
    }

    private static string Normalize(string? value)
    {
        return (value ?? string.Empty).Trim().ToLowerInvariant();
    }

    private static EditorProject CloneProject(EditorProject project)
    {
        return new EditorProject
        {
            Manifest = new EditorProjectManifest
            {
                ProjectId = project.Manifest.ProjectId,
                Name = project.Manifest.Name,
                Author = project.Manifest.Author,
                Description = project.Manifest.Description,
                EditorVersion = project.Manifest.EditorVersion,
                TargetGameVersion = project.Manifest.TargetGameVersion,
                CreatedAtUtc = project.Manifest.CreatedAtUtc,
                UpdatedAtUtc = project.Manifest.UpdatedAtUtc
            },
            Overrides = project.Overrides.Select(CloneEnvelope).ToList(),
            Graphs = project.Graphs.ToDictionary(pair => pair.Key, pair => CloneGraph(pair.Value), StringComparer.Ordinal),
            ProjectAssets = project.ProjectAssets.Select(CloneAsset).ToList(),
            SourceOfTruthIsRuntimeModelDb = project.SourceOfTruthIsRuntimeModelDb
        };
    }

    private static EntityOverrideEnvelope CloneEnvelope(EntityOverrideEnvelope envelope)
    {
        return new EntityOverrideEnvelope
        {
            EntityKind = envelope.EntityKind,
            EntityId = envelope.EntityId,
            BehaviorSource = envelope.BehaviorSource,
            GraphId = envelope.GraphId,
            MonsterAi = MonsterAiDefinitionCloner.Clone(envelope.MonsterAi),
            Notes = envelope.Notes,
            Metadata = new Dictionary<string, string>(envelope.Metadata, StringComparer.Ordinal),
            Assets = envelope.Assets.Select(CloneAsset).ToList()
        };
    }

    private static AssetRef CloneAsset(AssetRef asset)
    {
        return new AssetRef
        {
            Id = asset.Id,
            SourceType = asset.SourceType,
            LogicalRole = asset.LogicalRole,
            SourcePath = asset.SourcePath,
            ManagedPath = asset.ManagedPath,
            PackagePath = asset.PackagePath,
            FileName = asset.FileName
        };
    }

    private static BehaviorGraphDefinition CloneGraph(BehaviorGraphDefinition graph)
    {
        return new BehaviorGraphDefinition
        {
            GraphId = graph.GraphId,
            Name = graph.Name,
            Description = graph.Description,
            Version = graph.Version,
            EntityKind = graph.EntityKind,
            EntryNodeId = graph.EntryNodeId,
            Metadata = new Dictionary<string, string>(graph.Metadata, StringComparer.Ordinal),
            Nodes = graph.Nodes.Select(node => new BehaviorGraphNodeDefinition
            {
                NodeId = node.NodeId,
                NodeType = node.NodeType,
                DisplayName = node.DisplayName,
                Description = node.Description,
                Properties = new Dictionary<string, string>(node.Properties, StringComparer.Ordinal),
                DynamicValues = node.DynamicValues.ToDictionary(pair => pair.Key, pair => pair.Value.Clone(), StringComparer.Ordinal)
            }).ToList(),
            Connections = graph.Connections.Select(connection => new BehaviorGraphConnectionDefinition
            {
                FromNodeId = connection.FromNodeId,
                FromPortId = connection.FromPortId,
                ToNodeId = connection.ToNodeId,
                ToPortId = connection.ToPortId
            }).ToList()
        };
    }

    private static bool ApplyDynamicNodePropertyChange(BehaviorGraphNodeDefinition node, string propertyKey, string propertyValue)
    {
        var dynamicPropertyKey = GetPrimaryDynamicPropertyKey(node);
        var dynamicAmount = dynamicPropertyKey == null ? null : EnsureDynamicValueDefinition(node, dynamicPropertyKey);
        if (dynamicAmount == null)
        {
            if (propertyKey is not "amount" and not "count")
            {
                return false;
            }

            dynamicPropertyKey = propertyKey;
            dynamicAmount = new DynamicValueDefinition
            {
                SourceKind = DynamicValueSourceKind.Literal,
                LiteralValue = propertyValue
            };
            node.DynamicValues[dynamicPropertyKey] = dynamicAmount;
            node.Properties[dynamicPropertyKey] = propertyValue;
        }

        switch (propertyKey)
        {
            case "amount" when string.Equals(dynamicPropertyKey, "amount", StringComparison.Ordinal):
            case "count" when string.Equals(dynamicPropertyKey, "count", StringComparison.Ordinal):
                dynamicAmount.LiteralValue = propertyValue;
                node.Properties[dynamicPropertyKey!] = propertyValue;
                return true;
            case "dynamic_source_kind":
                if (Enum.TryParse<DynamicValueSourceKind>(propertyValue, ignoreCase: true, out var sourceKind))
                {
                    dynamicAmount.SourceKind = sourceKind;
                }
                return true;
            case "dynamic_var_name":
                dynamicAmount.DynamicVarName = propertyValue;
                return true;
            case "formula_ref":
                dynamicAmount.FormulaRef = propertyValue;
                return true;
            case "base_override_mode":
                if (Enum.TryParse<DynamicValueOverrideMode>(propertyValue, ignoreCase: true, out var baseMode))
                {
                    dynamicAmount.BaseOverrideMode = baseMode;
                }
                return true;
            case "base_override_value":
                dynamicAmount.BaseOverrideValue = propertyValue;
                return true;
            case "extra_override_mode":
                if (Enum.TryParse<DynamicValueOverrideMode>(propertyValue, ignoreCase: true, out var extraMode))
                {
                    dynamicAmount.ExtraOverrideMode = extraMode;
                }
                return true;
            case "extra_override_value":
                dynamicAmount.ExtraOverrideValue = propertyValue;
                return true;
            case "preview_multiplier_key":
                dynamicAmount.PreviewMultiplierKey = NormalizePreviewMultiplierKey(propertyValue);
                return true;
            case "preview_multiplier_value":
                dynamicAmount.PreviewMultiplierValue = propertyValue;
                return true;
            case "template_text":
                dynamicAmount.TemplateText = propertyValue;
                return true;
            case "preview_format":
                dynamicAmount.PreviewFormat = propertyValue;
                return true;
            default:
                return false;
        }
    }

    private static string NormalizePreviewMultiplierKey(string? propertyValue)
    {
        return string.Equals(propertyValue, "cards", StringComparison.OrdinalIgnoreCase)
            ? "hand_count"
            : propertyValue ?? string.Empty;
    }

    private static string? GetPrimaryDynamicPropertyKey(BehaviorGraphNodeDefinition node)
    {
        if (node.DynamicValues.ContainsKey("amount"))
        {
            return "amount";
        }

        if (node.DynamicValues.ContainsKey("count"))
        {
            return "count";
        }

        if (node.DynamicValues.Count == 1)
        {
            return node.DynamicValues.Keys.First();
        }

        foreach (var key in node.Properties.Keys)
        {
            if (key.EndsWith("_source_kind", StringComparison.OrdinalIgnoreCase))
            {
                return key[..^"_source_kind".Length];
            }
        }

        return node.NodeType switch
        {
            "combat.repeat" => "count",
            _ => node.Properties.ContainsKey("amount") ? "amount" : null
        };
    }

    private static DynamicValueDefinition EnsureDynamicValueDefinition(BehaviorGraphNodeDefinition node, string dynamicPropertyKey)
    {
        if (node.DynamicValues.TryGetValue(dynamicPropertyKey, out var existing))
        {
            return existing;
        }

        var dynamicValue = new DynamicValueDefinition
        {
            SourceKind = DynamicValueSourceKind.Literal,
            LiteralValue = node.Properties.TryGetValue(dynamicPropertyKey, out var literalValue) ? literalValue : string.Empty
        };
        node.DynamicValues[dynamicPropertyKey] = dynamicValue;
        return dynamicValue;
    }
}
