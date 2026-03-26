using Godot;
using MegaCrit.Sts2.Core.Models;
using STS2_Editor.Scripts.Editor.Core.Utilities;
using STS2_Editor.Scripts.Editor.Core.Models;
using STS2_Editor.Scripts.Editor.Graph;
using STS2_Editor.Scripts.Editor.Packaging;
using STS2_Editor.Scripts.Editor.Runtime;
using static STS2_Editor.Scripts.Editor.UI.ModStudioUiFactory;

namespace STS2_Editor.Scripts.Editor.UI;

public sealed partial class NModStudioProjectWindow
{
    private void RevertAssetBinding()
    {
        if (_project == null || _currentItem == null)
        {
            return;
        }

        _assetBindingService.ClearAssetBinding(_project, _currentKind, _currentItem.EntityId);
        _selectedImportedAsset = null;
        _selectedRuntimeAssetPath = string.Empty;
        _currentResolvedAssetPath = string.Empty;

        var envelope = GetEnvelope(_currentKind, _currentItem.EntityId);
        if (envelope != null)
        {
            TryRemoveEmptyEnvelope(_currentItem, envelope);
        }

        MarkDirty();
        RefreshCurrentEntityCache();
        if (_currentViewCache != null)
        {
            UpdateCachedBrowserItem(_currentItem, _currentViewCache.MergedMetadata);
        }
        RefreshCurrentTabView(forceLoad: true);
    }

    private void RefreshDerivedGraphText(BehaviorGraphDefinition graph, bool updateBasicPreview)
    {
        var sourceModel = _currentItem == null ? null : ResolveSourceModel(_currentKind, _currentItem.EntityId);
        var generation = _graphDescriptionGenerator.Generate(graph, sourceModel, _graphPreviewContext);
        var generatedDescription = GetAppliedGraphDescription(generation);
        graph.Metadata.TryGetValue(GraphAutoDescriptionCacheKey, out var previousAutoDescription);

        foreach (var node in graph.Nodes)
        {
            RefreshDerivedNodeText(graph, node);
        }

        if (!string.IsNullOrWhiteSpace(generatedDescription))
        {
            if (string.IsNullOrWhiteSpace(graph.Description) ||
                string.Equals(graph.Description, previousAutoDescription, StringComparison.Ordinal))
            {
                graph.Description = generatedDescription;
            }

            graph.Metadata[GraphAutoDescriptionCacheKey] = generatedDescription;
            if (!string.IsNullOrWhiteSpace(generation.TemplateDescription))
            {
                graph.Metadata[$"{GraphAutoDescriptionCacheKey}.template"] = generation.TemplateDescription;
            }
            if (!string.IsNullOrWhiteSpace(generation.PreviewDescription))
            {
                graph.Metadata[$"{GraphAutoDescriptionCacheKey}.preview"] = generation.PreviewDescription;
            }
        }

        if (_detailPanel != null)
        {
            WithSuppressedDirty(() => _detailPanel.GraphDescriptionEdit.Text = graph.Description ?? string.Empty);
        }
    }

    private void TrySyncBasicDescriptionTemplate(string generatedDescription, string? previousAutoDescription)
    {
        if (_centerEditor?.BasicEditor == null || _currentItem == null)
        {
            return;
        }

        var metadataKey = GetDescriptionMetadataKey(_currentKind);
        if (string.IsNullOrWhiteSpace(metadataKey) || string.IsNullOrWhiteSpace(generatedDescription))
        {
            return;
        }

        if (!_centerEditor.BasicEditor.TryGetFieldValue(metadataKey, out var currentFieldValue))
        {
            return;
        }

        var originalMetadata = GetOriginalMetadata(_currentItem);
        originalMetadata.TryGetValue(metadataKey, out var originalValue);

        var envelope = GetEnvelope(_currentKind, _currentItem.EntityId);
        string? envelopeValue = null;
        string? cachedAutoDescription = null;
        envelope?.Metadata.TryGetValue(metadataKey, out envelopeValue);
        envelope?.Metadata.TryGetValue(BuildAutoDescriptionCacheKey(metadataKey), out cachedAutoDescription);

        var canReplace =
            string.IsNullOrWhiteSpace(currentFieldValue) ||
            string.Equals(currentFieldValue, previousAutoDescription, StringComparison.Ordinal) ||
            string.Equals(currentFieldValue, cachedAutoDescription, StringComparison.Ordinal) ||
            string.Equals(currentFieldValue, envelopeValue, StringComparison.Ordinal) ||
            string.Equals(currentFieldValue, originalValue, StringComparison.Ordinal);

        if (!canReplace)
        {
            return;
        }

        _centerEditor.BasicEditor.TrySetFieldValue(metadataKey, generatedDescription, raiseChanged: false);
    }

    private static string GetAppliedGraphDescription(GraphDescriptionGenerationResult generation)
    {
        if (!string.IsNullOrWhiteSpace(generation.TemplateDescription))
        {
            return generation.TemplateDescription;
        }

        return generation.PreviewDescription;
    }

    private void RefreshDerivedNodeText(BehaviorGraphNodeDefinition node)
    {
        var graph = _centerEditor?.GraphEditor.CanvasView.BoundGraph;
        if (graph == null)
        {
            return;
        }

        RefreshDerivedNodeText(graph, node);
    }

    private void RefreshDerivedNodeText(BehaviorGraphDefinition graph, BehaviorGraphNodeDefinition node)
    {
        var sourceModel = _currentItem == null ? null : ResolveSourceModel(_currentKind, _currentItem.EntityId);
        var suggestedDescription = ModStudioGraphCanvasView.GetSuggestedNodeDescription(node, sourceModel, _graphPreviewContext);
        if (string.IsNullOrWhiteSpace(suggestedDescription))
        {
            return;
        }

        var cacheKey = BuildNodeAutoDescriptionCacheKey(node.NodeId);
        graph.Metadata.TryGetValue(cacheKey, out var previousAutoDescription);
        if (string.IsNullOrWhiteSpace(node.Description) ||
            string.Equals(node.Description, previousAutoDescription, StringComparison.Ordinal))
        {
            node.Description = suggestedDescription;
        }

        SetNodeAutoDescriptionCache(node.NodeId, suggestedDescription, graph);
    }

    private string BuildGraphOverviewText(BehaviorGraphDefinition graph, EntityOverrideEnvelope? envelope)
    {
        var sourceModel = _currentItem == null ? null : ResolveSourceModel(_currentKind, _currentItem.EntityId);
        var descriptionResult = _graphDescriptionGenerator.Generate(graph, sourceModel, _graphPreviewContext);
        var originalDescription = GetOriginalEntityDescription();
        var templateDescription = string.IsNullOrWhiteSpace(descriptionResult.TemplateDescription)
            ? Dual("不可用", "Unavailable")
            : descriptionResult.TemplateDescription;
        var previewDescription = string.IsNullOrWhiteSpace(descriptionResult.PreviewDescription)
            ? Dual("不可用", "Unavailable")
            : descriptionResult.PreviewDescription;
        return string.Join(System.Environment.NewLine, new[]
        {
            $"{Dual("Graph ID", "Graph Id")}: {graph.GraphId}",
            $"{Dual("模式", "Mode")}: {(envelope?.BehaviorSource == BehaviorSource.Graph ? Dual("Graph", "Graph") : Dual("原版", "Native"))}",
            $"{Dual("节点数", "Nodes")}: {graph.Nodes.Count}",
            $"{Dual("连线数", "Connections")}: {graph.Connections.Count}",
            $"{Dual("原版描述", "Original Description")}: {(string.IsNullOrWhiteSpace(originalDescription) ? Dual("不可用", "Unavailable") : originalDescription)}",
            $"{Dual("模板描述", "Template Description")}: {templateDescription}",
            $"{Dual("预览描述", "Preview Description")}: {previewDescription}"
        });
    }

    private string BuildGraphInfoText(BehaviorGraphDefinition graph, EntityOverrideEnvelope? envelope)
    {
        var sourceModel = _currentItem == null ? null : ResolveSourceModel(_currentKind, _currentItem.EntityId);
        var descriptionResult = _graphDescriptionGenerator.Generate(graph, sourceModel, _graphPreviewContext);
        return string.Join(System.Environment.NewLine, new[]
        {
            $"{Dual("Graph ID", "Graph Id")}: {graph.GraphId}",
            $"{Dual("模式", "Mode")}: {(envelope?.BehaviorSource == BehaviorSource.Graph ? Dual("Graph", "Graph") : Dual("原版", "Native"))}",
            $"{Dual("节点数", "Nodes")}: {graph.Nodes.Count}",
            $"{Dual("连线数", "Connections")}: {graph.Connections.Count}",
            $"Auto Description: {(string.IsNullOrWhiteSpace(descriptionResult.Description) ? Dual("不可用", "Unavailable") : descriptionResult.Description)}"
        });
    }

    private static string GetDescriptionMetadataKey(ModStudioEntityKind kind)
    {
        return kind switch
        {
            ModStudioEntityKind.Card => "description",
            ModStudioEntityKind.Relic => "description",
            ModStudioEntityKind.Potion => "description",
            ModStudioEntityKind.Event => "initial_description",
            ModStudioEntityKind.Enchantment => "description",
            _ => string.Empty
        };
    }

    private static string BuildAutoDescriptionCacheKey(string metadataKey)
    {
        return $"{AutoDescriptionCacheKeyPrefix}{metadataKey}";
    }

    private string GetOriginalEntityDescription()
    {
        if (_currentItem == null)
        {
            return string.Empty;
        }

        var metadataKey = GetDescriptionMetadataKey(_currentKind);
        if (string.IsNullOrWhiteSpace(metadataKey))
        {
            return string.Empty;
        }

        var originalMetadata = GetOriginalMetadata(_currentItem);
        return originalMetadata.TryGetValue(metadataKey, out var value) ? value : string.Empty;
    }

    private static string BuildNodeAutoDescriptionCacheKey(string nodeId)
    {
        return $"{NodeAutoDescriptionCacheKeyPrefix}{nodeId}";
    }

    private static void SetNodeAutoDescriptionCache(string nodeId, string description, BehaviorGraphDefinition graph)
    {
        var cacheKey = BuildNodeAutoDescriptionCacheKey(nodeId);
        if (string.IsNullOrWhiteSpace(description))
        {
            graph.Metadata.Remove(cacheKey);
            return;
        }

        graph.Metadata[cacheKey] = description;
    }

    private void WithSuppressedDirty(Action action)
    {
        var previous = _suppressDirty;
        _suppressDirty = true;
        try
        {
            action();
        }
        finally
        {
            _suppressDirty = previous;
        }
    }

    private void SaveBasic()
    {
        if (_project == null || _currentItem == null || _centerEditor == null)
        {
            return;
        }

        var values = _centerEditor.BasicEditor.GetFieldValues();
        var envelope = GetOrCreateEnvelope(_currentKind, _currentItem.EntityId);
        foreach (var key in GetBasicFieldKeys(_currentKind))
        {
            if (values.TryGetValue(key, out var value))
            {
                envelope.Metadata[key] = value;
            }
            else
            {
                envelope.Metadata.Remove(key);
            }
        }

        _basicDraftDirty = false;
        MarkDirty();
        RefreshCurrentEntityCache();
        if (_currentViewCache != null)
        {
            UpdateCachedBrowserItem(_currentItem, _currentViewCache.MergedMetadata);
        }
        RefreshCurrentTabView(forceLoad: true);
    }

    private void RevertBasic()
    {
        if (_project == null || _currentItem == null)
        {
            return;
        }

        var envelope = GetEnvelope(_currentKind, _currentItem.EntityId);
        if (envelope != null)
        {
            if (_currentItem.IsProjectOnly)
            {
                var defaults = _metadataService.CreateDefaultMetadata(_currentKind, _currentItem.EntityId);
                foreach (var key in GetBasicFieldKeys(_currentKind))
                {
                    if (defaults.TryGetValue(key, out var value))
                    {
                        envelope.Metadata[key] = value;
                    }
                    else
                    {
                        envelope.Metadata.Remove(key);
                    }
                }
            }
            else
            {
                foreach (var key in GetBasicFieldKeys(_currentKind))
                {
                    envelope.Metadata.Remove(key);
                }
            }

            TryRemoveEmptyEnvelope(_currentItem, envelope);
        }

        _basicDraftDirty = false;
        MarkDirty();
        RefreshCurrentEntityCache();
        if (_currentViewCache != null)
        {
            UpdateCachedBrowserItem(_currentItem, _currentViewCache.MergedMetadata);
        }
        RefreshCurrentTabView(forceLoad: true);
    }

    private void ImportGraphTemplate()
    {
        if (_currentItem == null || _project == null || _graphImportDialog == null)
        {
            return;
        }

        var candidates = _metadataService.GetItems(_currentKind, _project)
            .Where(item => !string.Equals(item.EntityId, _currentItem.EntityId, StringComparison.Ordinal))
            .Select(item => new ModStudioGraphImportCandidate
            {
                EntityId = item.EntityId,
                Title = item.Title,
                Summary = item.Summary,
                IsProjectOnly = item.IsProjectOnly,
                IsCurrentEntity = string.Equals(item.EntityId, _currentItem.EntityId, StringComparison.Ordinal),
                SourceItem = item
            })
            .ToList();

        if (candidates.Count == 0)
        {
            CreateDefaultGraphForCurrentItem();
            return;
        }

        _graphImportDialog.SetCandidates(candidates);
        _graphImportDialog.ShowDialog();
    }

    private void ImportGraphFromCandidate(ModStudioGraphImportCandidate candidate)
    {
        if (_project == null || _currentItem == null)
        {
            return;
        }

        var sourceItem = candidate.SourceItem;
        if (sourceItem == null)
        {
            return;
        }

        if (!TryCreateImportGraph(sourceItem, out var importedGraph, out var graphInfo) || importedGraph == null)
        {
            CreateDefaultGraphForCurrentItem();
            return;
        }

        importedGraph.GraphId = Guid.NewGuid().ToString("N");
        importedGraph.Name = _currentItem.Title;
        importedGraph.EntityKind = _currentKind;
        BindDraftGraph(importedGraph, graphInfo, true);
    }

    private void CreateDefaultGraphForCurrentItem()
    {
        if (_currentItem == null)
        {
            return;
        }

        var graphId = Guid.NewGuid().ToString("N");
        var graph = BehaviorGraphTemplateFactory.CreateDefaultScaffold(graphId, _currentKind, _currentItem.Title, _currentItem.Summary);
        BindDraftGraph(graph, BuildGraphOverviewText(graph, null), true);
    }

    private void BindDraftGraph(BehaviorGraphDefinition graph, string graphInfo, bool enableGraphBehavior)
    {
        _ = graphInfo;
        RefreshDerivedGraphText(graph, updateBasicPreview: true);
        var sourceModel = _currentItem == null ? null : ResolveSourceModel(_currentKind, _currentItem.EntityId);
        _centerEditor?.BindGraph(graph, _graphRegistry, sourceModel, _graphPreviewContext);
        if (_detailPanel != null)
        {
            _detailPanel.SetGraphDetails(graph.GraphId, graph.Name, graph.Description, enableGraphBehavior);
            _detailPanel.SetGraphInfo(BuildGraphOverviewText(graph, GetEnvelope(_currentKind, _currentEntityId)));
            _detailPanel.SetPreviewContext(_graphPreviewContext);
        }
        _graphDraftDirty = true;
        MarkDirty();
        UpdateSelectedNodeDetails(graph, graph.EntryNodeId);
    }

    private bool TryCreateImportGraph(EntityBrowserItem sourceItem, out BehaviorGraphDefinition? graph, out string graphInfo)
    {
        graph = null;
        graphInfo = string.Empty;

        var sourceEnvelope = GetEnvelope(sourceItem.Kind, sourceItem.EntityId);
        var existingGraph = GetGraph(sourceItem, sourceEnvelope);
        if (existingGraph != null)
        {
            graph = CloneGraph(existingGraph);
            graphInfo = BuildGraphOverviewText(graph, sourceEnvelope);
            return true;
        }

        if (!sourceItem.IsProjectOnly &&
            _nativeAutoGraphService.TryCreateGraph(sourceItem.Kind, sourceItem.EntityId, out var autoGraphResult) &&
            autoGraphResult != null)
        {
            graph = CloneGraph(autoGraphResult.Graph);
            graphInfo = autoGraphResult.Summary;
            return true;
        }

        return false;
    }

    private void SaveGraph()
    {
        if (_project == null || _currentItem == null || _centerEditor == null || _detailPanel == null)
        {
            return;
        }

        var detailPanel = _detailPanel;
        var graph = _centerEditor.GraphEditor.CanvasView.BoundGraph;
        if (graph == null)
        {
            CreateDefaultGraphForCurrentItem();
            graph = _centerEditor.GraphEditor.CanvasView.BoundGraph;
            if (graph == null)
            {
                return;
            }
        }

        _centerEditor.GraphEditor.CanvasView.ExportLayout();
        var previousGraphId = GetEnvelope(_currentKind, _currentItem.EntityId)?.GraphId;
        graph.Description = detailPanel.GraphDescriptionEdit.Text ?? graph.Description;
        graph.Name = detailPanel.GraphNameEdit.Text ?? graph.Name;
        graph.GraphId = detailPanel.GraphIdEdit.Text ?? graph.GraphId;
        graph.EntityKind = _currentKind;
        if (!detailPanel.GraphEnabledCheck.ButtonPressed)
        {
            detailPanel.GraphEnabledCheck.ButtonPressed = true;
        }

        var envelope = GetOrCreateEnvelope(_currentKind, _currentItem.EntityId);
        envelope.BehaviorSource = detailPanel.GraphEnabledCheck.ButtonPressed ? BehaviorSource.Graph : BehaviorSource.Native;
        envelope.GraphId = graph.GraphId;
        if (!string.IsNullOrWhiteSpace(previousGraphId) &&
            !string.Equals(previousGraphId, graph.GraphId, StringComparison.Ordinal))
        {
            _project.Graphs.Remove(previousGraphId);
        }
        _project.Graphs[graph.GraphId] = CloneGraph(graph);
        ApplyGeneratedDescription(envelope, graph);
        _graphDraftDirty = false;
        MarkDirty();
        RefreshGraphDetails(_currentItem, envelope);
        RefreshCurrentEntityCache();
        if (_currentViewCache != null)
        {
            UpdateCachedBrowserItem(_currentItem, _currentViewCache.MergedMetadata);
        }
    }

    private void ValidateGraph()
    {
        if (_centerEditor?.GraphEditor.CanvasView.BoundGraph == null || _detailPanel == null)
        {
            return;
        }

        var validation = _graphRegistry.Validate(_centerEditor.GraphEditor.CanvasView.BoundGraph);
        _detailPanel.SetGraphInfo(string.Join(System.Environment.NewLine, validation.Errors.DefaultIfEmpty(Dual("Graph 校验通过。", "Graph validation passed."))));
    }

    private void OnGraphEnabledToggled(bool toggled)
    {
        if (_suppressDirty)
        {
            return;
        }

        if (toggled && _centerEditor?.GraphEditor.CanvasView.BoundGraph == null)
        {
            CreateDefaultGraphForCurrentItem();
            return;
        }

        MarkGraphDirty();
    }

    private void OnGraphNodeChanged(string? nodeId)
    {
        if (_centerEditor?.GraphEditor.CanvasView.BoundGraph == null)
        {
            return;
        }

        var graph = _centerEditor.GraphEditor.CanvasView.BoundGraph;
        var node = string.IsNullOrWhiteSpace(nodeId)
            ? null
            : graph.Nodes.FirstOrDefault(item => string.Equals(item.NodeId, nodeId, StringComparison.Ordinal));
        UpdateSelectedNodeDetails(graph, node?.NodeId);
    }

    private void UpdateSelectedNodeDetails(BehaviorGraphDefinition graph, string? selectedNodeId)
    {
        if (_detailPanel == null)
        {
            return;
        }

        var selectedNode = string.IsNullOrWhiteSpace(selectedNodeId)
            ? null
            : graph.Nodes.FirstOrDefault(node => string.Equals(node.NodeId, selectedNodeId, StringComparison.Ordinal));

        WithSuppressedDirty(() =>
        {
            var summaryText = BuildSelectedNodeInspectorSummaryText(graph, selectedNode);
            _detailPanel.SetInspectorGraphContext(graph, selectedNode);
            _detailPanel.SetSelectedNode(selectedNode);
            _detailPanel.SetSelectedNodeDynamicSummary(summaryText);
            _detailPanel.SetSelectedNodeProperties(BuildEditableNodeProperties(selectedNode));
            _detailPanel.SetPreviewContextVisible(ShouldShowPreviewContextForSelectedNode(selectedNode));
        });
    }

    private IReadOnlyDictionary<string, string> BuildEditableNodeProperties(BehaviorGraphNodeDefinition? node)
    {
        if (node == null)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        var properties = new Dictionary<string, string>(node.Properties, StringComparer.Ordinal);
        if (_graphRegistry.TryGetNodeDefinition(node.NodeType, out var definition) && definition?.DefaultProperties != null)
        {
            foreach (var pair in definition.DefaultProperties)
            {
                if (!properties.ContainsKey(pair.Key))
                {
                    properties[pair.Key] = pair.Value;
                }
            }
        }

        foreach (var legacyKey in node.Properties.Keys
                     .Where(key => key.EndsWith("_source_kind", StringComparison.OrdinalIgnoreCase) ||
                                   key.EndsWith("_var_name", StringComparison.OrdinalIgnoreCase) ||
                                   key.EndsWith("_formula_ref", StringComparison.OrdinalIgnoreCase) ||
                                   key.EndsWith("_template", StringComparison.OrdinalIgnoreCase) ||
                                   key.EndsWith("_preview_format", StringComparison.OrdinalIgnoreCase) ||
                                   key.EndsWith("_preview_multiplier_key", StringComparison.OrdinalIgnoreCase) ||
                                   key.EndsWith("_preview_multiplier_value", StringComparison.OrdinalIgnoreCase) ||
                                   key.EndsWith("_base_override_mode", StringComparison.OrdinalIgnoreCase) ||
                                   key.EndsWith("_base_override_value", StringComparison.OrdinalIgnoreCase) ||
                                   key.EndsWith("_extra_override_mode", StringComparison.OrdinalIgnoreCase) ||
                                   key.EndsWith("_extra_override_value", StringComparison.OrdinalIgnoreCase))
                     .ToList())
        {
            properties.Remove(legacyKey);
        }

        var dynamicPropertyKey = GetPrimaryDynamicPropertyKey(node);
        var dynamicAmount = dynamicPropertyKey == null ? null : EnsureDynamicValueDefinition(node, dynamicPropertyKey);
        if (dynamicAmount != null)
        {
            properties["dynamic_source_kind"] = dynamicAmount.SourceKind.ToString();
            switch (dynamicAmount.SourceKind)
            {
                case DynamicValueSourceKind.Literal:
                    properties[dynamicPropertyKey!] = string.IsNullOrWhiteSpace(dynamicAmount.LiteralValue)
                        ? DynamicValueEvaluator.GetLegacyProperty(node, dynamicPropertyKey!, GetDynamicPropertyDefaultText(dynamicPropertyKey!))
                        : dynamicAmount.LiteralValue;
                    break;
                case DynamicValueSourceKind.DynamicVar:
                    properties["dynamic_var_name"] = dynamicAmount.DynamicVarName;
                    properties["base_override_mode"] = dynamicAmount.BaseOverrideMode.ToString();
                    properties["base_override_value"] = dynamicAmount.BaseOverrideValue;
                    break;
                case DynamicValueSourceKind.FormulaRef:
                    properties["dynamic_var_name"] = dynamicAmount.DynamicVarName;
                    properties["formula_ref"] = dynamicAmount.FormulaRef;
                    properties["base_override_mode"] = dynamicAmount.BaseOverrideMode.ToString();
                    properties["base_override_value"] = dynamicAmount.BaseOverrideValue;
                    properties["extra_override_mode"] = dynamicAmount.ExtraOverrideMode.ToString();
                    properties["extra_override_value"] = dynamicAmount.ExtraOverrideValue;
                    properties["preview_multiplier_key"] = dynamicAmount.PreviewMultiplierKey;
                    break;
            }
        }

        return FilterInspectorProperties(node, properties);
    }

    private IReadOnlyDictionary<string, string> FilterInspectorProperties(BehaviorGraphNodeDefinition node, IReadOnlyDictionary<string, string> properties)
    {
        var nodeType = (node.NodeType ?? string.Empty).Trim().ToLowerInvariant();
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var key in GetAllowedInspectorKeys(node, properties))
        {
            if (properties.TryGetValue(key, out var value))
            {
                result[key] = value;
            }
        }

        return result;
    }

    private IReadOnlyCollection<string> GetAllowedInspectorKeys(BehaviorGraphNodeDefinition node, IReadOnlyDictionary<string, string> properties)
    {
        var nodeType = (node.NodeType ?? string.Empty).Trim().ToLowerInvariant();
        var dynamicPropertyKey = GetPrimaryDynamicPropertyKey(node);
        var dynamicAmount = dynamicPropertyKey == null ? null : EnsureDynamicValueDefinition(node, dynamicPropertyKey);

        if (nodeType is "flow.entry" or "flow.exit" or "event.proceed")
        {
            return Array.Empty<string>();
        }

        if (nodeType == "flow.branch")
        {
            return new[] { "condition", "condition_key" };
        }

        if (nodeType == "debug.log")
        {
            return new[] { "message" };
        }

        if (nodeType == "event.page")
        {
            return new[] { "page_id", "title", "description", "is_start", "option_order" };
        }

        if (nodeType == "event.option")
        {
            return new[]
            {
                "page_id",
                "option_id",
                "title",
                "description",
                "next_page_id",
                "encounter_id",
                "resume_page_id",
                "is_proceed",
                "save_choice_to_history",
                "reward_kind",
                "reward_amount",
                "reward_count",
                "reward_target",
                "reward_props",
                "reward_power_id",
                "card_id",
                "relic_id",
                "potion_id"
            };
        }

        if (nodeType == "event.goto_page")
        {
            return new[] { "next_page_id" };
        }

        if (nodeType == "event.start_combat")
        {
            return new[] { "encounter_id", "resume_page_id" };
        }

        if (nodeType == "event.reward")
        {
            return new[] { "reward_kind", "reward_amount", "reward_count", "reward_target", "reward_props", "reward_power_id", "card_id", "relic_id", "potion_id" };
        }

        if (nodeType == "reward.offer_custom")
        {
            return new[] { "reward_kind", "reward_count", "amount", "card_id", "relic_id", "potion_id" };
        }

        if (nodeType == "card.select_cards")
        {
            return new[] { "state_key", "selection_mode", "source_pile", "count", "prompt_kind", "allow_cancel", "enchantment_id" };
        }

        if (dynamicAmount == null)
        {
            return properties.Keys.ToList();
        }

        var allowed = new List<string> { "dynamic_source_kind" };
        switch (dynamicAmount.SourceKind)
        {
            case DynamicValueSourceKind.Literal:
                allowed.Add(dynamicPropertyKey!);
                break;
            case DynamicValueSourceKind.DynamicVar:
                allowed.Add("dynamic_var_name");
                allowed.Add("base_override_mode");
                allowed.Add("base_override_value");
                break;
            case DynamicValueSourceKind.FormulaRef:
                allowed.Add("dynamic_var_name");
                allowed.Add("formula_ref");
                allowed.Add("base_override_mode");
                allowed.Add("base_override_value");
                allowed.Add("extra_override_mode");
                allowed.Add("extra_override_value");
                allowed.Add("preview_multiplier_key");
                break;
        }

        foreach (var key in properties.Keys)
        {
            if (key == dynamicPropertyKey ||
                key is "target" or "props" or "power_id" or "keyword" or "card_id" or "replacement_card_id" or "relic_id" or "replacement_relic_id" or "potion_id" or "monster_id" or "orb_id" or "enchantment_id" or "target_pile" or "source_pile" or "position" or "count" or "card_state_key" or "selection_mode" or "prompt_kind" or "allow_cancel" or "auto_play_type" or "skip_x_capture" or "skip_card_pile_visuals" or "gold_loss_type" or "card_preview_style" or "target_type_scope" or "card_type_scope" or "exact_energy_cost" or "include_x_cost" or "result_key" or "operator")
            {
                if (!allowed.Contains(key, StringComparer.Ordinal))
                {
                    allowed.Add(key);
                }
            }
        }

        return allowed;
    }

    private string BuildSelectedNodeInspectorSummaryText(BehaviorGraphDefinition graph, BehaviorGraphNodeDefinition? node)
    {
        var dynamicSummary = BuildSelectedNodeDynamicSummaryTextV3(node);
        if (!string.IsNullOrWhiteSpace(dynamicSummary))
        {
            return dynamicSummary;
        }

        var internalStateSummary = BuildSelectedInternalStateNodeSummaryText(node);
        if (!string.IsNullOrWhiteSpace(internalStateSummary))
        {
            return internalStateSummary;
        }

        return BuildSelectedEventNodeSummaryText(graph, node);
    }

    private bool ShouldShowPreviewContextForSelectedNode(BehaviorGraphNodeDefinition? node)
    {
        if (node == null)
        {
            return false;
        }

        if (_currentKind is ModStudioEntityKind.Event)
        {
            return false;
        }

        var normalizedNodeType = (node.NodeType ?? string.Empty).Trim().ToLowerInvariant();
        if (normalizedNodeType is "flow.entry" or "flow.exit" or "event.page" or "event.option" or "event.goto_page" or "event.proceed" or "event.start_combat" or "event.reward")
        {
            return false;
        }

        return GetPrimaryDynamicPropertyKey(node) != null;
    }

    private string BuildSelectedEventNodeSummaryText(BehaviorGraphDefinition graph, BehaviorGraphNodeDefinition? node)
    {
        if (node == null || !_currentKind.Equals(ModStudioEntityKind.Event))
        {
            return string.Empty;
        }

        var nodeType = (node.NodeType ?? string.Empty).Trim().ToLowerInvariant();
        switch (nodeType)
        {
            case "event.option":
                return BuildEventOptionSummary(graph, node);
            case "event.reward":
                return BuildEventRewardSummary(node);
            case "event.proceed":
                return string.Join(System.Environment.NewLine, new[]
                {
                    Dual("该节点只负责结束当前事件交互，本身不携带奖励或数值。", "This node only ends the current event interaction and does not carry a reward payload by itself."),
                    Dual("如果你想让选项真正给金币、治疗、伤害或能力，请把 event.reward 串在前面，或者直接在 event.option 上填写奖励字段。", "If the option should actually grant gold, heal, deal damage, or apply a power, place an event.reward node before this node or fill the reward fields directly on the event.option node.")
                });
            case "event.goto_page":
                return string.Join(System.Environment.NewLine, new[]
                {
                    Dual("该节点负责跳转到另一页。", "This node routes the event to another page."),
                    $"{Dual("下一页", "Next Page")}: {FormatOptional(ModStudioFieldDisplayNames.FormatPropertyValue("next_page_id", node.Properties.TryGetValue("next_page_id", out var nextPageId) ? nextPageId : string.Empty))}"
                });
            case "event.start_combat":
                return string.Join(System.Environment.NewLine, new[]
                {
                    Dual("该节点负责从事件进入战斗。", "This node starts combat from the event."),
                    $"{Dual("遭遇", "Encounter")}: {FormatOptional(FormatEncounterId(node.Properties.TryGetValue("encounter_id", out var encounterId) ? encounterId : string.Empty))}",
                    $"{Dual("返回页", "Resume Page")}: {FormatOptional(ModStudioFieldDisplayNames.FormatPropertyValue("resume_page_id", node.Properties.TryGetValue("resume_page_id", out var resumePageId) ? resumePageId : string.Empty))}"
                });
            case "event.page":
                return string.Join(System.Environment.NewLine, new[]
                {
                    Dual("该节点定义事件页面的文本和选项顺序。", "This node defines the event page text and option order."),
                    $"{Dual("页面 ID", "Page Id")}: {FormatOptional(node.Properties.TryGetValue("page_id", out var pageId) ? pageId : string.Empty)}",
                    $"{Dual("起始页", "Start Page")}: {FormatOptional(ModStudioFieldDisplayNames.FormatPropertyValue("is_start", node.Properties.TryGetValue("is_start", out var isStart) ? isStart : string.Empty))}"
                });
            default:
                return string.Empty;
        }
    }

    private string BuildSelectedInternalStateNodeSummaryText(BehaviorGraphNodeDefinition? node)
    {
        if (node == null)
        {
            return string.Empty;
        }

        var nodeType = (node.NodeType ?? string.Empty).Trim().ToLowerInvariant();
        return nodeType switch
        {
            "value.compare" => BuildCompareNodeSummary(node),
            "flow.branch" => BuildBranchNodeSummary(node),
            "value.set" => BuildSetValueNodeSummary(node),
            "value.add" => BuildAddValueNodeSummary(node),
            "value.multiply" => BuildMultiplyValueNodeSummary(node),
            "enchantment.set_status" => BuildEnchantmentStatusNodeSummary(node),
            _ => string.Empty
        };
    }

    private static string BuildCompareNodeSummary(BehaviorGraphNodeDefinition node)
    {
        var left = ModStudioFieldDisplayNames.FormatGraphPropertyValue("left", node.Properties.TryGetValue("left", out var leftValue) ? leftValue : string.Empty);
        var comparisonOperator = ModStudioFieldDisplayNames.FormatGraphPropertyValue("operator", node.Properties.TryGetValue("operator", out var operatorValue) ? operatorValue : "eq");
        var right = ModStudioFieldDisplayNames.FormatGraphPropertyValue("right", node.Properties.TryGetValue("right", out var rightValue) ? rightValue : string.Empty);
        var resultKey = ModStudioFieldDisplayNames.FormatGraphPropertyValue("result_key", node.Properties.TryGetValue("result_key", out var resultKeyValue) ? resultKeyValue : "last_compare");
        return string.Join(System.Environment.NewLine, new[]
        {
            Dual("这个节点不会额外输出一根“比较结果线”，而是把布尔结果写进图状态。", "This node does not emit a separate data wire. It stores the boolean comparison result in graph state."),
            $"{Dual("比较", "Comparison")}: {left} {comparisonOperator} {right}",
            $"{Dual("结果键", "Result Key")}: {resultKey}",
            Dual("后面的 branch 节点通常会在 condition_key 里读取这个结果键。", "A later branch node usually reads this result through condition_key.")
        });
    }

    private static string BuildBranchNodeSummary(BehaviorGraphNodeDefinition node)
    {
        if (node.Properties.TryGetValue("condition", out var condition) && !string.IsNullOrWhiteSpace(condition))
        {
            return string.Join(System.Environment.NewLine, new[]
            {
                Dual("这个节点直接计算条件表达式，再决定走 True 还是 False。", "This node evaluates a condition expression directly, then chooses the True or False output."),
                $"{Dual("条件", "Condition")}: {ModStudioFieldDisplayNames.FormatGraphPropertyValue("condition", condition)}",
                Dual("condition 可以直接写固定值，也可以写 $state.xxx、$target 这类引用。", "condition can use a literal value or a reference such as $state.xxx or $target.")
            });
        }

        var conditionKey = ModStudioFieldDisplayNames.FormatGraphPropertyValue("condition_key", node.Properties.TryGetValue("condition_key", out var conditionKeyValue) ? conditionKeyValue : string.Empty);
        return string.Join(System.Environment.NewLine, new[]
        {
            Dual("这个节点会从图状态里读取一个布尔键，再决定走 True 还是 False。", "This node reads a boolean key from graph state, then chooses the True or False output."),
            $"{Dual("条件键", "Condition Key")}: {FormatOptional(conditionKey)}",
            Dual("常见模式是上游 compare 把结果写进 result_key，这里再通过 condition_key 读取它。", "A common pattern is that an upstream compare writes result_key, then this branch reads it through condition_key.")
        });
    }

    private static string BuildSetValueNodeSummary(BehaviorGraphNodeDefinition node)
    {
        var key = node.Properties.TryGetValue("key", out var keyValue) ? keyValue?.Trim() : string.Empty;
        var formattedKey = ModStudioFieldDisplayNames.FormatGraphPropertyValue("key", key);
        var formattedValue = ModStudioFieldDisplayNames.FormatGraphPropertyValue("value", node.Properties.TryGetValue("value", out var value) ? value : string.Empty);
        var lines = new List<string>
        {
            Dual("这个节点把值写进图状态，不会再额外生成一根数据输出线。", "This node writes into graph state instead of creating a separate data output wire."),
            $"{Dual("写入", "Write")}: {formattedKey} = {formattedValue}"
        };

        if (string.Equals(key, "hook_result", StringComparison.OrdinalIgnoreCase))
        {
            lines.Add(Dual("这个键通常会作为当前原版 hook 的返回值。", "This key usually becomes the return value for the current native hook."));
        }
        else if (string.Equals(key, "Status", StringComparison.OrdinalIgnoreCase))
        {
            lines.Add(Dual("这个键通常映射原版对象的 Status 字段。新建附魔图时，更推荐直接使用 enchantment.set_status 节点。", "This key usually mirrors the native Status field. For new enchantment graphs, prefer the dedicated enchantment.set_status node."));
        }

        return string.Join(System.Environment.NewLine, lines);
    }

    private static string BuildAddValueNodeSummary(BehaviorGraphNodeDefinition node)
    {
        var key = ModStudioFieldDisplayNames.FormatGraphPropertyValue("key", node.Properties.TryGetValue("key", out var keyValue) ? keyValue : string.Empty);
        var delta = ModStudioFieldDisplayNames.FormatGraphPropertyValue("value", node.Properties.TryGetValue("delta", out var deltaValue) ? deltaValue : string.Empty);
        return string.Join(System.Environment.NewLine, new[]
        {
            Dual("这个节点会读取当前状态值，在原值基础上增加一个数。", "This node reads the current state value and adds a numeric delta on top of it."),
            $"{Dual("目标键", "Target Key")}: {key}",
            $"{Dual("增量", "Delta")}: {delta}"
        });
    }

    private static string BuildMultiplyValueNodeSummary(BehaviorGraphNodeDefinition node)
    {
        var key = ModStudioFieldDisplayNames.FormatGraphPropertyValue("key", node.Properties.TryGetValue("key", out var keyValue) ? keyValue : string.Empty);
        var factor = ModStudioFieldDisplayNames.FormatGraphPropertyValue("value", node.Properties.TryGetValue("factor", out var factorValue) ? factorValue : string.Empty);
        return string.Join(System.Environment.NewLine, new[]
        {
            Dual("这个节点会读取当前状态值，再乘上一个倍率。", "This node reads the current state value and multiplies it by a factor."),
            $"{Dual("目标键", "Target Key")}: {key}",
            $"{Dual("倍率", "Factor")}: {factor}"
        });
    }

    private static string BuildEnchantmentStatusNodeSummary(BehaviorGraphNodeDefinition node)
    {
        var status = ModStudioFieldDisplayNames.FormatGraphPropertyValue("status", node.Properties.TryGetValue("status", out var statusValue) ? statusValue : "Disabled");
        return string.Join(System.Environment.NewLine, new[]
        {
            Dual("这个节点会直接修改当前附魔对象的状态。", "This node directly changes the current enchantment status."),
            $"{Dual("状态", "Status")}: {status}",
            Dual("相比把字符串写进 State.Status，这个专用节点更容易理解，也更适合手工搭图。", "Compared with writing a raw string into State.Status, this dedicated node is easier to understand and better for manual authoring.")
        });
    }

    private string BuildEventOptionSummary(BehaviorGraphDefinition graph, BehaviorGraphNodeDefinition node)
    {
        var properties = BuildEditableNodeProperties(node);
        var title = properties.TryGetValue("title", out var titleValue) ? titleValue : string.Empty;
        var pageId = properties.TryGetValue("page_id", out var pageIdValue) ? pageIdValue : string.Empty;
        var optionId = properties.TryGetValue("option_id", out var optionIdValue) ? optionIdValue : string.Empty;
        var saveChoiceToHistory = properties.TryGetValue("save_choice_to_history", out var saveChoiceValue) ? saveChoiceValue : string.Empty;
        var effectiveChoice = ResolveEffectiveEventChoice(graph, node);

        var lines = new List<string>
        {
            Dual("说明：标题和描述只是事件里显示给玩家看的文本，不会自动改变真正执行的奖励。", "Tip: title and description are only the text shown to the player. They do not automatically change the actual reward."),
            $"{Dual("显示文本", "Display Text")}: {FormatOptional(title)}",
            $"{Dual("页面 ID", "Page Id")}: {FormatOptional(pageId)}",
            $"{Dual("选项 ID", "Option Id")}: {FormatOptional(optionId)}",
            $"{Dual("写入历史", "Save To History")}: {FormatOptional(ModStudioFieldDisplayNames.FormatPropertyValue("save_choice_to_history", saveChoiceToHistory))}"
        };

        if (effectiveChoice != null)
        {
            lines.Add($"{Dual("实际奖励", "Effective Reward")}: {DescribeEffectiveReward(effectiveChoice)}");
            lines.Add($"{Dual("下一步", "Next Action")}: {DescribeEffectiveTransition(effectiveChoice)}");
        }
        else
        {
            lines.Add(Dual("当前这个选项还没有编译出任何运行时动作。只改文字，游戏里不会额外发生效果。", "This option does not currently compile into any runtime action. Changing the text alone will not add gameplay behavior."));
        }

        lines.Add(Dual("如果要修改实际效果，请编辑奖励字段，或者把 event.reward / event.goto_page / event.start_combat / event.proceed 串在这个选项后面。", "To change the actual effect, edit the reward fields or chain event.reward / event.goto_page / event.start_combat / event.proceed after this option."));
        return string.Join(System.Environment.NewLine, lines);
    }

    private string BuildEventRewardSummary(BehaviorGraphNodeDefinition node)
    {
        var properties = BuildEditableNodeProperties(node);
        var rewardKind = properties.TryGetValue("reward_kind", out var rewardKindValue) ? rewardKindValue : string.Empty;
        var rewardAmount = properties.TryGetValue("reward_amount", out var rewardAmountValue) ? rewardAmountValue : string.Empty;
        var rewardTarget = properties.TryGetValue("reward_target", out var rewardTargetValue) ? rewardTargetValue : string.Empty;
        var rewardProps = properties.TryGetValue("reward_props", out var rewardPropsValue) ? rewardPropsValue : string.Empty;
        var rewardPowerId = properties.TryGetValue("reward_power_id", out var rewardPowerIdValue) ? rewardPowerIdValue : string.Empty;
        var rewardCount = properties.TryGetValue("reward_count", out var rewardCountValue) ? rewardCountValue : string.Empty;
        var rewardCardId = properties.TryGetValue("card_id", out var rewardCardIdValue) ? rewardCardIdValue : string.Empty;
        var rewardRelicId = properties.TryGetValue("relic_id", out var rewardRelicIdValue) ? rewardRelicIdValue : string.Empty;
        var rewardPotionId = properties.TryGetValue("potion_id", out var rewardPotionIdValue) ? rewardPotionIdValue : string.Empty;

        var lines = new List<string>
        {
            Dual("该节点负责真正的事件奖励执行。", "This node carries the actual event reward payload."),
            $"{Dual("奖励类型", "Reward Kind")}: {FormatOptional(ModStudioFieldDisplayNames.FormatGraphPropertyValue("reward_kind", rewardKind))}",
            $"{Dual("奖励数值", "Reward Amount")}: {FormatOptional(rewardAmount)}"
        };

        if (!string.IsNullOrWhiteSpace(rewardTarget))
        {
            lines.Add($"{Dual("目标", "Target")}: {ModStudioFieldDisplayNames.FormatGraphPropertyValue("target", rewardTarget)}");
        }

        if (!string.IsNullOrWhiteSpace(rewardProps))
        {
            lines.Add($"{Dual("属性", "Props")}: {ModStudioFieldDisplayNames.FormatGraphPropertyValue("props", rewardProps)}");
        }

        if (!string.IsNullOrWhiteSpace(rewardPowerId))
        {
            lines.Add($"{Dual("能力", "Power")}: {FormatPowerId(rewardPowerId)}");
        }

        if (!string.IsNullOrWhiteSpace(rewardCount))
        {
            lines.Add($"{Dual("数量", "Count")}: {rewardCount}");
        }

        if (!string.IsNullOrWhiteSpace(rewardCardId))
        {
            lines.Add($"{Dual("卡牌", "Card")}: {FormatCardId(rewardCardId)}");
        }

        if (!string.IsNullOrWhiteSpace(rewardRelicId))
        {
            lines.Add($"{Dual("遗物", "Relic")}: {FormatRelicId(rewardRelicId)}");
        }

        if (!string.IsNullOrWhiteSpace(rewardPotionId))
        {
            lines.Add($"{Dual("药水", "Potion")}: {FormatPotionId(rewardPotionId)}");
        }

        return string.Join(System.Environment.NewLine, lines);
    }

    private EventGraphChoiceBinding? ResolveEffectiveEventChoice(BehaviorGraphDefinition graph, BehaviorGraphNodeDefinition node)
    {
        if (!string.Equals(node.NodeType, "event.option", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var validation = GetOrCompileEventGraph(graph);
        var pageId = node.Properties.TryGetValue("page_id", out var explicitPageId) ? explicitPageId?.Trim() : string.Empty;
        var optionId = node.Properties.TryGetValue("option_id", out var explicitOptionId) ? explicitOptionId?.Trim() : string.Empty;
        return validation.Choices.FirstOrDefault(choice =>
            (string.IsNullOrWhiteSpace(pageId) || string.Equals(choice.PageId, pageId, StringComparison.Ordinal)) &&
            (string.IsNullOrWhiteSpace(optionId)
                ? string.Equals(choice.OptionId, node.NodeId, StringComparison.Ordinal) || string.Equals(choice.OptionId, node.Properties.GetValueOrDefault("title"), StringComparison.Ordinal)
                : string.Equals(choice.OptionId, optionId, StringComparison.Ordinal)));
    }

    private string DescribeEffectiveReward(EventGraphChoiceBinding choice)
    {
        if (string.IsNullOrWhiteSpace(choice.RewardKind))
        {
            return Dual("无", "None");
        }

        var parts = new List<string>
        {
            ModStudioFieldDisplayNames.FormatGraphPropertyValue("reward_kind", choice.RewardKind)
        };

        if (!string.IsNullOrWhiteSpace(choice.RewardAmount))
        {
            parts.Add(choice.RewardAmount!);
        }

        if (!string.IsNullOrWhiteSpace(choice.RewardTarget))
        {
            parts.Add(ModStudioFieldDisplayNames.FormatGraphPropertyValue("target", choice.RewardTarget));
        }

        if (!string.IsNullOrWhiteSpace(choice.RewardPowerId))
        {
            parts.Add($"[{choice.RewardPowerId}]");
        }

        if (!string.IsNullOrWhiteSpace(choice.RewardCount))
        {
            parts.Add($"x{choice.RewardCount}");
        }

        if (!string.IsNullOrWhiteSpace(choice.RewardCardId))
        {
            parts.Add($"card:{choice.RewardCardId}");
        }

        if (!string.IsNullOrWhiteSpace(choice.RewardRelicId))
        {
            parts.Add($"relic:{choice.RewardRelicId}");
        }

        if (!string.IsNullOrWhiteSpace(choice.RewardPotionId))
        {
            parts.Add($"potion:{choice.RewardPotionId}");
        }

        return string.Join(" | ", parts);
    }

    private string DescribeEffectiveTransition(EventGraphChoiceBinding choice)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(choice.NextPageId))
        {
            parts.Add($"{Dual("跳页", "Go To")}: {choice.NextPageId}");
        }

        if (!string.IsNullOrWhiteSpace(choice.EncounterId))
        {
            parts.Add($"{Dual("战斗", "Combat")}: {choice.EncounterId}");
            if (!string.IsNullOrWhiteSpace(choice.ResumePageId))
            {
                parts.Add($"{Dual("返回页", "Resume")}: {choice.ResumePageId}");
            }
        }

        if (choice.IsProceed)
        {
            parts.Add(Dual("结束事件", "Proceed"));
        }

        return parts.Count == 0 ? Dual("无", "None") : string.Join(" | ", parts);
    }

    private static string FormatOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? Dual("未设置", "Unset") : value!;
    }

    private static string FormatCardId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var card = ModelDb.AllCards.FirstOrDefault(candidate => string.Equals(candidate.Id.Entry, value, StringComparison.Ordinal));
        return card == null ? value : $"{card.Title} [{value}]";
    }

    private static string FormatRelicId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var relic = ModelDb.AllRelics.FirstOrDefault(candidate => string.Equals(candidate.Id.Entry, value, StringComparison.Ordinal));
        return relic == null ? value : $"{SafeLocalized(relic.Title)} [{value}]";
    }

    private static string FormatPotionId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var potion = ModelDb.AllPotions.FirstOrDefault(candidate => string.Equals(candidate.Id.Entry, value, StringComparison.Ordinal));
        return potion == null ? value : $"{SafeLocalized(potion.Title)} [{value}]";
    }

    private static string FormatPowerId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var power = ModelDb.AllPowers.FirstOrDefault(candidate => string.Equals(candidate.Id.Entry, value, StringComparison.OrdinalIgnoreCase));
        return power == null ? value : $"{SafeLocalized(power.Title)} [{value}]";
    }

    private static string FormatEncounterId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var encounter = ModelDb.AllEncounters.FirstOrDefault(candidate => string.Equals(candidate.Id.Entry, value, StringComparison.OrdinalIgnoreCase));
        return encounter == null ? value : $"{SafeLocalized(encounter.Title)} [{value}]";
    }

    private string BuildSelectedNodeDynamicSummaryText(BehaviorGraphNodeDefinition? node)
    {
        if (node == null)
        {
            return string.Empty;
        }

        var dynamicPropertyKey = GetPrimaryDynamicPropertyKey(node);
        var dynamicAmount = dynamicPropertyKey == null ? null : EnsureDynamicValueDefinition(node, dynamicPropertyKey);
        if (dynamicAmount == null)
        {
            return string.Empty;
        }

        var sourceModel = _currentItem == null ? null : ResolveSourceModel(_currentKind, _currentItem.EntityId);
        var preview = DynamicValueEvaluator.EvaluatePreview(dynamicAmount, sourceModel, _graphPreviewContext, GetDynamicPropertyDefaultValue(dynamicPropertyKey!));
        var previewDescription = ModStudioGraphCanvasView.GetSuggestedNodeDescription(node, sourceModel, _graphPreviewContext);
        var sourceLabel = ModStudioFieldDisplayNames.FormatGraphPropertyValue("dynamic_source_kind", dynamicAmount.SourceKind.ToString());
        var templateText = string.IsNullOrWhiteSpace(preview.TemplateText) ? Dual("不可用", "Unavailable") : preview.TemplateText;
        var previewText = string.IsNullOrWhiteSpace(preview.PreviewText) ? Dual("不可用", "Unavailable") : preview.PreviewText;
        var previewDescriptionText = string.IsNullOrWhiteSpace(previewDescription) ? Dual("不可用", "Unavailable") : previewDescription;
        var summaryText = string.IsNullOrWhiteSpace(preview.SummaryText) ? Dual("不可用", "Unavailable") : preview.SummaryText;

        var helpLines = new List<string>
        {
            $"{Dual("值来源", "Value Source")}: {sourceLabel}",
            $"{Dual("当前模板", "Template")}: {templateText}",
            $"{Dual("数值预览", "Value Preview")}: {previewText}",
            $"{Dual("描述预览", "Description Preview")}: {previewDescriptionText}",
            $"{Dual("计算说明", "Computation")}: {summaryText}"
        };

        if (!string.IsNullOrWhiteSpace(dynamicAmount.DynamicVarName))
        {
            helpLines.Insert(1, $"{Dual("动态变量", "Dynamic Variable")}: {dynamicAmount.DynamicVarName}");
        }

        if (!string.IsNullOrWhiteSpace(dynamicAmount.FormulaRef))
        {
            helpLines.Insert(2, $"{Dual("公式引用", "Formula Reference")}: {dynamicAmount.FormulaRef}");
        }

        switch (dynamicAmount.SourceKind)
        {
            case DynamicValueSourceKind.DynamicVar:
                helpLines.Add(Dual("说明：基础值覆盖会修改原版动态变量的基础值；额外值和预览乘数不适用于该模式。", "Tip: base override changes the original dynamic variable base value. Extra override and preview multiplier do not apply in this mode."));
                break;
            case DynamicValueSourceKind.FormulaRef:
                helpLines.Add(Dual("说明：基础值/额外值覆盖会作用在原版公式上；预览乘数只影响编辑器里的公式预览，不会直接写入游戏公式。", "Tip: base and extra overrides apply on top of the original formula. Preview multiplier only affects editor-side preview and does not directly rewrite the game's formula."));
                break;
            default:
                helpLines.Add(Dual("说明：固定值模式不会引用原版动态变量或公式。", "Tip: literal mode does not reference the original dynamic variable or formula."));
                break;
        }

        return string.Join(System.Environment.NewLine, helpLines);
    }

    private DynamicValueDefinition? EnsureDynamicAmountDefinition(BehaviorGraphNodeDefinition node)
    {
        return EnsureDynamicValueDefinition(node, "amount");
    }

    private DynamicValueDefinition? EnsureDynamicValueDefinition(BehaviorGraphNodeDefinition node, string propertyKey)
    {
        if (node.DynamicValues.TryGetValue(propertyKey, out var existingDefinition))
        {
            if (string.IsNullOrWhiteSpace(existingDefinition.LiteralValue) &&
                node.Properties.TryGetValue(propertyKey, out var legacyLiteral))
            {
                existingDefinition.LiteralValue = legacyLiteral;
            }

            return existingDefinition;
        }

        var hasLegacyDynamicMetadata =
            node.Properties.ContainsKey($"{propertyKey}_source_kind") ||
            node.Properties.ContainsKey($"{propertyKey}_var_name") ||
            node.Properties.ContainsKey($"{propertyKey}_formula_ref") ||
            node.Properties.ContainsKey($"{propertyKey}_template") ||
            node.Properties.ContainsKey($"{propertyKey}_preview_format") ||
            node.Properties.ContainsKey($"{propertyKey}_preview_multiplier_key") ||
            node.Properties.ContainsKey($"{propertyKey}_preview_multiplier_value") ||
            node.Properties.ContainsKey($"{propertyKey}_base_override_mode") ||
            node.Properties.ContainsKey($"{propertyKey}_base_override_value") ||
            node.Properties.ContainsKey($"{propertyKey}_extra_override_mode") ||
            node.Properties.ContainsKey($"{propertyKey}_extra_override_value") ||
            node.Properties.ContainsKey(propertyKey);

        if (!hasLegacyDynamicMetadata)
        {
            return null;
        }

        var definition = new DynamicValueDefinition
        {
            LiteralValue = node.Properties.TryGetValue(propertyKey, out var amount) ? amount : string.Empty,
            DynamicVarName = node.Properties.TryGetValue($"{propertyKey}_var_name", out var varName) ? varName : string.Empty,
            FormulaRef = node.Properties.TryGetValue($"{propertyKey}_formula_ref", out var formulaRef) ? formulaRef : string.Empty,
            TemplateText = node.Properties.TryGetValue($"{propertyKey}_template", out var template) ? template : string.Empty,
            PreviewFormat = node.Properties.TryGetValue($"{propertyKey}_preview_format", out var previewFormat) ? previewFormat : string.Empty,
            PreviewMultiplierKey = node.Properties.TryGetValue($"{propertyKey}_preview_multiplier_key", out var multiplierKey)
                ? NormalizePreviewMultiplierKey(multiplierKey)
                : string.Empty,
            PreviewMultiplierValue = node.Properties.TryGetValue($"{propertyKey}_preview_multiplier_value", out var multiplierValue) ? multiplierValue : string.Empty
        };

        if (node.Properties.TryGetValue($"{propertyKey}_source_kind", out var sourceKindText) &&
            Enum.TryParse<DynamicValueSourceKind>(sourceKindText, ignoreCase: true, out var sourceKind))
        {
            definition.SourceKind = sourceKind;
        }

        if (node.Properties.TryGetValue($"{propertyKey}_base_override_mode", out var baseModeText) &&
            Enum.TryParse<DynamicValueOverrideMode>(baseModeText, ignoreCase: true, out var baseMode))
        {
            definition.BaseOverrideMode = baseMode;
        }

        if (node.Properties.TryGetValue($"{propertyKey}_base_override_value", out var baseOverrideValue))
        {
            definition.BaseOverrideValue = baseOverrideValue;
        }

        if (node.Properties.TryGetValue($"{propertyKey}_extra_override_mode", out var extraModeText) &&
            Enum.TryParse<DynamicValueOverrideMode>(extraModeText, ignoreCase: true, out var extraMode))
        {
            definition.ExtraOverrideMode = extraMode;
        }

        if (node.Properties.TryGetValue($"{propertyKey}_extra_override_value", out var extraOverrideValue))
        {
            definition.ExtraOverrideValue = extraOverrideValue;
        }

        node.DynamicValues[propertyKey] = definition;
        return definition;
    }

    private void OnNewProjectChosen(string path)
    {
        var normalized = ModStudioPaths.NormalizeProjectRootPath(path);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        _project = _projectStore.CreateProject(normalized, Path.GetFileName(normalized), overwriteExistingProject: true);
        _currentProjectPath = normalized;
        _currentKind = ModStudioEntityKind.Card;
        _currentEntityId = null;
        _currentItem = null;
        _dirty = false;
        _basicDraftDirty = false;
        _graphDraftDirty = false;
        ClearEditorCaches();
        ModStudioSettingsStore.RecordRecentProject(normalized);
        RefreshProjectGate();
        RefreshBrowserItems(selectFirstIfPossible: true);
        RefreshProjectState();
    }

    private string BuildSelectedNodeDynamicSummaryTextV2(BehaviorGraphNodeDefinition? node)
    {
        if (node == null)
        {
            return string.Empty;
        }

        var dynamicAmount = EnsureDynamicAmountDefinition(node);
        if (dynamicAmount == null)
        {
            return string.Empty;
        }

        var sourceModel = _currentItem == null ? null : ResolveSourceModel(_currentKind, _currentItem.EntityId);
        var preview = DynamicValueEvaluator.EvaluatePreview(dynamicAmount, sourceModel, _graphPreviewContext, 0m);
        var previewDescription = ModStudioGraphCanvasView.GetSuggestedNodeDescription(node, sourceModel, _graphPreviewContext);
        var sourceLabel = ModStudioFieldDisplayNames.FormatGraphPropertyValue("dynamic_source_kind", dynamicAmount.SourceKind.ToString());
        var sourceToken = DynamicValueEvaluator.GetSourceToken(dynamicAmount);
        var authoringTemplate = DynamicValueEvaluator.GetAuthoringTemplate(dynamicAmount);
        var previewText = string.IsNullOrWhiteSpace(preview.PreviewText) ? Dual("不可用", "Unavailable") : preview.PreviewText;
        var previewDescriptionText = string.IsNullOrWhiteSpace(previewDescription) ? Dual("不可用", "Unavailable") : previewDescription;
        var summaryText = string.IsNullOrWhiteSpace(preview.SummaryText) ? Dual("不可用", "Unavailable") : preview.SummaryText;

        var helpLines = new List<string>
        {
            $"{Dual("值来源", "Value Source")}: {sourceLabel}",
            $"{Dual("原始占位符", "Source Token")}: {sourceToken}",
            $"{Dual("Graph 模板", "Graph Template")}: {authoringTemplate}",
            $"{Dual("数值预览", "Value Preview")}: {previewText}",
            $"{Dual("描述预览", "Description Preview")}: {previewDescriptionText}",
            $"{Dual("计算说明", "Computation")}: {summaryText}"
        };

        if (!string.IsNullOrWhiteSpace(dynamicAmount.DynamicVarName))
        {
            helpLines.Insert(1, $"{Dual("动态变量", "Dynamic Variable")}: {dynamicAmount.DynamicVarName}");
        }

        if (!string.IsNullOrWhiteSpace(dynamicAmount.FormulaRef))
        {
            helpLines.Insert(2, $"{Dual("公式引用", "Formula Reference")}: {dynamicAmount.FormulaRef}");
        }

        switch (dynamicAmount.SourceKind)
        {
            case DynamicValueSourceKind.DynamicVar:
                helpLines.Add(Dual("说明：动态变量模式会保留原版变量语义。绝对值覆盖会直接改基础值，增量覆盖会在原值基础上追加。", "Tip: dynamic-variable mode preserves the original variable semantics. Absolute override replaces the base value, while delta override adds on top of the original value."));
                break;
            case DynamicValueSourceKind.FormulaRef:
                helpLines.Add(Dual("说明：公式模式会按“基础值 + 额外值 x 上下文乘数来源”来预览和执行。右侧上下文变化会直接影响当前预览结果。", "Tip: formula mode evaluates as base value + extra value x context multiplier source. Changing the preview context directly changes the current preview result."));
                break;
            default:
                helpLines.Add(Dual("说明：固定值模式不会引用原版动态变量或公式。", "Tip: literal mode does not reference the original dynamic variable or formula."));
                break;
        }

        return string.Join(System.Environment.NewLine, helpLines);
    }

    private void OnOpenProjectChosen(string path)
    {
        if (_projectStore.TryLoad(path, out var project) && project != null)
        {
            _project = project;
            _currentProjectPath = _projectStore.GetProjectDirectory(path);
            _currentEntityId = null;
            _currentItem = null;
            _currentKind = ModStudioEntityKind.Card;
            _dirty = false;
            _basicDraftDirty = false;
            _graphDraftDirty = false;
            ClearEditorCaches();
            ModStudioSettingsStore.RecordRecentProject(_currentProjectPath);
            RefreshProjectGate();
            RefreshBrowserItems(selectFirstIfPossible: true);
            RefreshProjectState();
        }
    }

    private void SaveProject()
    {
        if (_project == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_currentProjectPath))
        {
            return;
        }

        CommitPendingDrafts();
        _projectStore.Save(_project, _currentProjectPath);
        ModStudioSettingsStore.RecordRecentProject(_currentProjectPath);
        _dirty = false;
        _basicDraftDirty = false;
        _graphDraftDirty = false;
        RefreshProjectState();
    }

    private void ExportProject()
    {
        if (_project == null || string.IsNullOrWhiteSpace(_currentProjectPath))
        {
            return;
        }

        if (_exportDialog != null)
        {
            Directory.CreateDirectory(ModStudioPaths.PublishedPackagesRootPath);
            _exportDialog.CurrentDir = ModStudioPaths.PublishedPackagesRootPath;
            _exportDialog.CurrentFile = $"{SanitizeFileName(_project.Manifest.Name)}.sts2pack";
            _exportDialog.PopupCentered();
        }
    }

    private void ExportProjectToPath(string filePath)
    {
        if (_project == null)
        {
            return;
        }

        CommitPendingDrafts();

        var options = new PackageExportOptions
        {
            PackageId = _project.Manifest.ProjectId,
            DisplayName = _project.Manifest.Name,
            Version = "1.0.0",
            Author = _project.Manifest.Author,
            Description = _project.Manifest.Description
        };
        _packageArchiveService.Export(_project, options, filePath);

        var publishedRoot = Path.GetFullPath(ModStudioPaths.PublishedPackagesRootPath);
        var exportedPath = Path.GetFullPath(filePath);
        if (exportedPath.StartsWith(publishedRoot, StringComparison.OrdinalIgnoreCase))
        {
            ModStudioBootstrap.RuntimeRegistry.Refresh();
        }
    }

    private void RequestSwitchMode()
    {
        if (_dirty)
        {
            ShowUnsavedDialog(ReturnToChooser, ReturnToChooser);
            return;
        }

        ReturnToChooser();
    }

    private void RequestExit()
    {
        if (_dirty)
        {
            ShowUnsavedDialog(ExitToMainMenu, ExitToMainMenu);
            return;
        }

        ExitToMainMenu();
    }

    private void ShowUnsavedDialog(Action afterSave, Action afterDiscard)
    {
        _pendingAfterSave = afterSave;
        _pendingAfterDiscard = afterDiscard;
        _unsavedDialog?.Show();
    }

    private void HandleUnsavedSave()
    {
        _unsavedDialog?.Hide();
        var action = _pendingAfterSave;
        _pendingAfterSave = null;
        _pendingAfterDiscard = null;
        SaveProject();
        action?.Invoke();
    }

    private void HandleUnsavedDiscard()
    {
        _unsavedDialog?.Hide();
        var action = _pendingAfterDiscard;
        _pendingAfterSave = null;
        _pendingAfterDiscard = null;
        action?.Invoke();
    }

    private void HideUnsavedDialog()
    {
        _unsavedDialog?.Hide();
        _pendingAfterSave = null;
        _pendingAfterDiscard = null;
    }

    private void ReturnToChooser()
    {
        _stack.Pop();
    }

    private void ExitToMainMenu()
    {
        while (_stack.Peek() != null)
        {
            _stack.Pop();
        }
    }

    private void RefreshBrowserItems(bool selectFirstIfPossible)
    {
        if (_project == null)
        {
            _browserPanel?.BindItems(Array.Empty<EntityBrowserItem>());
            _browserPanel?.SetSelection(_currentKind, null);
            return;
        }

        if (!_browserItemsCache.TryGetValue(_currentKind, out var items))
        {
            items = _metadataService.GetItems(_currentKind, _project).ToList();
            _browserItemsCache[_currentKind] = items;
        }

        _browserPanel?.BindItems(items);
        _browserPanel?.SetSelection(_currentKind, _currentEntityId);

        if (selectFirstIfPossible && items.Count > 0 && string.IsNullOrWhiteSpace(_currentEntityId))
        {
            SelectEntity(items[0].EntityId);
        }
    }

    private void SelectEntity(string entityId)
    {
        if (_project == null)
        {
            return;
        }

        if (!_browserItemsCache.TryGetValue(_currentKind, out var items))
        {
            items = _metadataService.GetItems(_currentKind, _project).ToList();
            _browserItemsCache[_currentKind] = items;
        }

        var item = items.FirstOrDefault(entry => string.Equals(entry.EntityId, entityId, StringComparison.Ordinal));
        if (item == null)
        {
            return;
        }

        _currentItem = item;
        _currentEntityId = item.EntityId;
        LoadEntity(item);
    }

    private EntityOverrideEnvelope? GetEnvelope(ModStudioEntityKind kind, string? entityId)
    {
        if (_project == null || string.IsNullOrWhiteSpace(entityId))
        {
            return null;
        }

        return _project.Overrides.FirstOrDefault(item => item.EntityKind == kind && string.Equals(item.EntityId, entityId, StringComparison.Ordinal));
    }

    private EntityOverrideEnvelope GetOrCreateEnvelope(ModStudioEntityKind kind, string entityId)
    {
        if (_project == null)
        {
            throw new InvalidOperationException("Project is not loaded.");
        }

        var envelope = GetEnvelope(kind, entityId);
        if (envelope != null)
        {
            return envelope;
        }

        envelope = new EntityOverrideEnvelope
        {
            EntityKind = kind,
            EntityId = entityId
        };
        _project.Overrides.Add(envelope);
        return envelope;
    }

    private string BuildBasicDetailsText(EntityBrowserItem item, IReadOnlyDictionary<string, string> metadata)
    {
        var lines = new List<string>
        {
            $"{Dual("类型", "Kind")}: {ModStudioLocalization.GetEntityKindDisplayName(item.Kind)}",
            $"{Dual("ID", "Id")}: {item.EntityId}",
            $"{Dual("名称", "Title")}: {item.Title}",
            $"{Dual("摘要", "Summary")}: {item.Summary}"
        };

        foreach (var pair in metadata.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            lines.Add($"{ModStudioFieldDisplayNames.Get(pair.Key)}: {FormatBasicDetailValue(pair.Key, pair.Value)}");
        }

        return string.Join(System.Environment.NewLine, lines);
    }

    private string FormatBasicDetailValue(string key, string? value)
    {
        return key switch
        {
            "starting_deck_ids" => FormatCardIdList(value),
            "starting_relic_ids" => FormatRelicIdList(value),
            "starting_potion_ids" => FormatPotionIdList(value),
            _ => ModStudioFieldDisplayNames.FormatPropertyValue(key, value)
        };
    }

    private static string FormatCardIdList(string? value)
    {
        return FormatIdList(value, id =>
        {
            var card = ModelDb.AllCards.FirstOrDefault(candidate => string.Equals(candidate.Id.Entry, id, StringComparison.Ordinal));
            return card == null ? id : $"{card.Title} [{id}]";
        });
    }

    private static string FormatRelicIdList(string? value)
    {
        return FormatIdList(value, id =>
        {
            var relic = ModelDb.AllRelics.FirstOrDefault(candidate => string.Equals(candidate.Id.Entry, id, StringComparison.Ordinal));
            return relic == null ? id : $"{SafeLocalized(relic.Title)} [{id}]";
        });
    }

    private static string FormatPotionIdList(string? value)
    {
        return FormatIdList(value, id =>
        {
            var potion = ModelDb.AllPotions.FirstOrDefault(candidate => string.Equals(candidate.Id.Entry, id, StringComparison.Ordinal));
            return potion == null ? id : $"{SafeLocalized(potion.Title)} [{id}]";
        });
    }

    private static string FormatIdList(string? value, Func<string, string> formatter)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var values = value
            .Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(entry => entry.Trim())
            .Where(entry => !string.IsNullOrWhiteSpace(entry))
            .Select(formatter)
            .ToList();

        return values.Count == 0 ? string.Empty : string.Join(", ", values);
    }

    private static List<string> ParseCsvValues(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new List<string>();
        }

        return value
            .Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(entry => entry.Trim())
            .Where(entry => !string.IsNullOrWhiteSpace(entry))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static string SafeLocalized(MegaCrit.Sts2.Core.Localization.LocString? locString)
    {
        if (locString == null)
        {
            return string.Empty;
        }

        try
        {
            return locString.GetRawText();
        }
        catch
        {
            return string.Empty;
        }
    }

    private string BuildAssetDetails(EntityBrowserItem item, string currentPath, string candidatePath)
    {
        return string.Join(System.Environment.NewLine, new[]
        {
            $"{Dual("当前路径", "Current Path")}: {currentPath ?? string.Empty}",
            $"{Dual("候选路径", "Candidate Path")}: {candidatePath ?? string.Empty}",
            $"{Dual("游戏内素材数量", "Game Asset Count")}: {_assetBindingService.GetRuntimeAssetCandidates(item.Kind).Count}",
            $"{Dual("已导入素材数量", "Imported Asset Count")}: {GetImportedAssetsForKind(item.Kind).Count}"
        });
    }

    private string? GetRuntimeAssetPath(ModStudioEntityKind kind, string entityId, EntityOverrideEnvelope? envelope)
    {
        if (envelope != null)
        {
            var binding = _assetBindingService.TryGetDescriptor(kind, out var descriptor) ? descriptor.MetadataKey : string.Empty;
            if (!string.IsNullOrWhiteSpace(binding) && envelope.Metadata.TryGetValue(binding, out var path) && !string.IsNullOrWhiteSpace(path))
            {
                return path;
            }
        }

        return _assetBindingService.GetRuntimeAssetPath(kind, entityId);
    }

    private List<AssetRef> GetImportedAssetsForKind(ModStudioEntityKind kind)
    {
        if (_project == null || !_assetBindingService.TryGetDescriptor(kind, out var descriptor))
        {
            return new List<AssetRef>();
        }

        return _project.ProjectAssets
            .Where(asset => string.Equals(asset.LogicalRole, descriptor.LogicalRole, StringComparison.Ordinal))
            .OrderBy(asset => asset.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static Texture2D? LoadTexture(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            if (path.StartsWith("res://", StringComparison.OrdinalIgnoreCase) || path.StartsWith("user://", StringComparison.OrdinalIgnoreCase))
            {
                return ResourceLoader.Load<Texture2D>(path);
            }

            var image = new Image();
            if (image.Load(path) == Error.Ok)
            {
                return ImageTexture.CreateFromImage(image);
            }
        }
        catch
        {
            // Ignore preview load failures.
        }

        return null;
    }

    private void RefreshTabAvailability()
    {
        var supportsAssets = SupportsAssets(_currentKind);
        var supportsGraph = SupportsGraph(_currentKind);
        _centerEditor?.SetFeatureAvailability(supportsAssets, supportsGraph);
        _detailPanel?.SetFeatureAvailability(supportsAssets, supportsGraph);
    }

    private bool SupportsAssets(ModStudioEntityKind kind)
    {
        return _assetBindingService.TryGetDescriptor(kind, out var descriptor) &&
               (descriptor.SupportsRuntimeCatalog || descriptor.SupportsExternalImport);
    }

    private static bool SupportsGraph(ModStudioEntityKind kind)
    {
        return kind is ModStudioEntityKind.Card or
               ModStudioEntityKind.Relic or
               ModStudioEntityKind.Potion or
               ModStudioEntityKind.Event or
               ModStudioEntityKind.Enchantment;
    }

    private void TryRestoreLastProject()
    {
        if (_project != null || !string.IsNullOrWhiteSpace(_currentProjectPath))
        {
            return;
        }

        var settings = ModStudioSettingsStore.Load();
        if (string.IsNullOrWhiteSpace(settings.LastProjectPath))
        {
            return;
        }

        if (_projectStore.TryLoad(settings.LastProjectPath, out var restoredProject) && restoredProject != null)
        {
            _project = restoredProject;
            _currentProjectPath = _projectStore.GetProjectDirectory(settings.LastProjectPath);
        }
    }

    private string ResolvePreferredProjectDirectory()
    {
        if (!string.IsNullOrWhiteSpace(_currentProjectPath))
        {
            return _currentProjectPath;
        }

        var settings = ModStudioSettingsStore.Load();
        if (!string.IsNullOrWhiteSpace(settings.LastProjectPath))
        {
            return settings.LastProjectPath;
        }

        return ModStudioPaths.LegacyProjectsPath;
    }

    private static string SanitizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "asset";
        }

        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalid, '_');
        }

        return value;
    }

    private void MarkDirty()
    {
        if (_suppressDirty)
        {
            return;
        }

        _dirty = true;
        RefreshProjectState();
    }

    private void MarkBasicDirty()
    {
        if (_suppressDirty)
        {
            return;
        }

        _basicDraftDirty = true;
        MarkDirty();
    }

    private void MarkGraphDirty()
    {
        if (_suppressDirty)
        {
            return;
        }

        _graphDraftDirty = true;
        MarkDirty();
    }

    private void OnGraphChanged(BehaviorGraphDefinition graph)
    {
        EnsureGraphBehaviorEnabledForEdits();
        MarkGraphDirty();
        RefreshDerivedGraphText(graph, updateBasicPreview: true);
        _cachedCompiledEventGraph = null;
        _cachedCompiledEventResult = null;
        _detailPanel?.InvalidatePropertyEditors();
        _detailPanel?.SetGraphInfo(BuildGraphOverviewText(graph, GetEnvelope(_currentKind, _currentEntityId)));
        UpdateSelectedNodeDetails(graph, _centerEditor?.GraphEditor.CanvasView.SelectedNodeId);
    }

    private void OnPreviewContextChanged(DynamicPreviewContext context)
    {
        if (_currentItem == null)
        {
            return;
        }

        context.EntityKind = _currentKind;
        context.EntityId = _currentItem.EntityId;
        PopulatePreviewMultipliers(context);
        _graphPreviewContext = context;

        if (_graphTabLoadedForCurrentItem)
        {
            var graph = _centerEditor?.GraphEditor.CanvasView.BoundGraph;
            var sourceModel = ResolveSourceModel(_currentKind, _currentItem.EntityId);
            if (graph != null)
            {
                _centerEditor?.UpdateGraphPreviewContext(sourceModel, _graphPreviewContext);
            }

            RefreshCurrentGraphInfo();
        }
    }

    private void OnSelectedNodePropertyChanged(string propertyKey, string propertyValue)
    {
        var node = GetSelectedGraphNode();
        if (node == null)
        {
            return;
        }

        EnsureGraphBehaviorEnabledForEdits();
        var handledDynamicValue = ApplyDynamicNodePropertyChange(node, propertyKey, propertyValue);
        if (!handledDynamicValue)
        {
            node.Properties[propertyKey] = propertyValue;
        }

        RefreshDerivedNodeText(node);
        _centerEditor?.GraphEditor.CanvasView.UpdateNodePresentation(node);
        RefreshCurrentGraphInfo();
        MarkGraphDirty();
    }

    private void OnEventOptionAddRequested(string pageId)
    {
        var graph = _centerEditor?.GraphEditor.CanvasView.BoundGraph;
        if (graph == null || string.IsNullOrWhiteSpace(pageId))
        {
            return;
        }

        var pageNode = graph.Nodes.FirstOrDefault(node =>
            string.Equals(node.NodeType, "event.page", StringComparison.OrdinalIgnoreCase) &&
            node.Properties.TryGetValue("page_id", out var candidatePageId) &&
            string.Equals(candidatePageId, pageId, StringComparison.Ordinal));
        if (pageNode == null)
        {
            return;
        }

        EnsureGraphBehaviorEnabledForEdits();

        var index = 1;
        string optionId;
        do
        {
            optionId = $"OPTION_{index:000}";
            index++;
        }
        while (graph.Nodes.Any(node =>
            string.Equals(node.NodeType, "event.option", StringComparison.OrdinalIgnoreCase) &&
            node.Properties.TryGetValue("option_id", out var existingOptionId) &&
            string.Equals(existingOptionId, optionId, StringComparison.Ordinal)));

        var nodeId = $"event_option_{pageId.ToLowerInvariant()}_{optionId.ToLowerInvariant()}";
        graph.Nodes.Add(new BehaviorGraphNodeDefinition
        {
            NodeId = nodeId,
            NodeType = "event.option",
            DisplayName = "Event Option",
            Description = string.Empty,
            Properties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["page_id"] = pageId,
                ["option_id"] = optionId,
                ["title"] = optionId,
                ["description"] = string.Empty,
                ["next_page_id"] = string.Empty,
                ["encounter_id"] = string.Empty,
                ["resume_page_id"] = string.Empty,
                ["is_proceed"] = false.ToString(),
                ["save_choice_to_history"] = true.ToString()
            }
        });

        var optionOrder = ParseCsvValues(pageNode.Properties.TryGetValue("option_order", out var rawOptionOrder) ? rawOptionOrder : string.Empty);
        if (!optionOrder.Contains(optionId, StringComparer.Ordinal))
        {
            optionOrder.Add(optionId);
        }

        pageNode.Properties["option_order"] = string.Join(",", optionOrder);

        if (!graph.Connections.Any(connection =>
            string.Equals(connection.FromNodeId, pageNode.NodeId, StringComparison.Ordinal) &&
            string.Equals(connection.ToNodeId, nodeId, StringComparison.Ordinal)))
        {
            graph.Connections.Add(new BehaviorGraphConnectionDefinition
            {
                FromNodeId = pageNode.NodeId,
                FromPortId = "next",
                ToNodeId = nodeId,
                ToPortId = "in"
            });
        }

        _centerEditor?.GraphEditor.CanvasView.RebuildCanvas();
        OnGraphChanged(graph);
        UpdateSelectedNodeDetails(graph, pageNode.NodeId);
    }

    private EventGraphValidationResult GetOrCompileEventGraph(BehaviorGraphDefinition graph)
    {
        if (ReferenceEquals(_cachedCompiledEventGraph, graph) && _cachedCompiledEventResult != null)
        {
            return _cachedCompiledEventResult;
        }

        var compiler = new EventGraphCompiler();
        _cachedCompiledEventGraph = graph;
        _cachedCompiledEventResult = compiler.Compile(graph);
        return _cachedCompiledEventResult;
    }

    private bool ApplyDynamicNodePropertyChange(BehaviorGraphNodeDefinition node, string propertyKey, string propertyValue)
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

    private void OnSelectedNodeDisplayNameChanged(string displayName)
    {
        var node = GetSelectedGraphNode();
        if (node == null)
        {
            return;
        }

        EnsureGraphBehaviorEnabledForEdits();
        node.DisplayName = displayName;
        _centerEditor?.GraphEditor.CanvasView.UpdateNodePresentation(node);
        RefreshCurrentGraphInfo();
        MarkGraphDirty();
    }

    private void OnSelectedNodeDescriptionChanged(string description)
    {
        var node = GetSelectedGraphNode();
        if (node == null)
        {
            return;
        }

        EnsureGraphBehaviorEnabledForEdits();
        node.Description = description;
        var graph = _centerEditor?.GraphEditor.CanvasView.BoundGraph;
        if (graph != null)
        {
            SetNodeAutoDescriptionCache(node.NodeId, string.Empty, graph);
        }
        _centerEditor?.GraphEditor.CanvasView.UpdateNodePresentation(node);
        RefreshCurrentGraphInfo();
        MarkGraphDirty();
    }

    private void RefreshCurrentGraphInfo()
    {
        var graph = _centerEditor?.GraphEditor.CanvasView.BoundGraph;
        if (graph == null || _detailPanel == null)
        {
            return;
        }

        RefreshDerivedGraphText(graph, updateBasicPreview: true);
        _detailPanel.SetGraphInfo(BuildGraphOverviewText(graph, GetEnvelope(_currentKind, _currentEntityId)));
        _detailPanel.SetPreviewContext(_graphPreviewContext);
        UpdateSelectedNodeDetails(graph, _centerEditor?.GraphEditor.CanvasView.SelectedNodeId);
    }

    private BehaviorGraphNodeDefinition? GetSelectedGraphNode()
    {
        var graph = _centerEditor?.GraphEditor.CanvasView.BoundGraph;
        var selectedNodeId = _centerEditor?.GraphEditor.CanvasView.SelectedNodeId;
        if (graph == null || string.IsNullOrWhiteSpace(selectedNodeId))
        {
            return null;
        }

        return graph.Nodes.FirstOrDefault(node => string.Equals(node.NodeId, selectedNodeId, StringComparison.Ordinal));
    }

    private IReadOnlyDictionary<string, string> GetOriginalMetadata(EntityBrowserItem item)
    {
        if (item.IsProjectOnly)
        {
            return new Dictionary<string, string>(_metadataService.CreateDefaultMetadata(item.Kind, item.EntityId), StringComparer.OrdinalIgnoreCase);
        }

        return new Dictionary<string, string>(_metadataService.GetEditableMetadata(item.Kind, item.EntityId), StringComparer.OrdinalIgnoreCase);
    }

    private Dictionary<string, string> MergeMetadata(IReadOnlyDictionary<string, string> originalMetadata, EntityOverrideEnvelope? envelope)
    {
        var merged = new Dictionary<string, string>(originalMetadata, StringComparer.OrdinalIgnoreCase);
        if (envelope == null)
        {
            return merged;
        }

        foreach (var pair in envelope.Metadata)
        {
            merged[pair.Key] = pair.Value;
        }

        return merged;
    }

    private Dictionary<string, string> BuildBasicEditorMetadata(ModStudioEntityKind kind, IReadOnlyDictionary<string, string> metadata)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in GetBasicFieldKeys(kind))
        {
            if (metadata.TryGetValue(key, out var value))
            {
                result[key] = value;
            }
        }

        return result;
    }

    private static IReadOnlyList<string> GetBasicFieldKeys(ModStudioEntityKind kind)
    {
        return kind switch
        {
            ModStudioEntityKind.Character => new[] { "title", "starting_hp", "starting_gold", "max_energy", "base_orb_slot_count", "starting_deck_ids", "starting_relic_ids", "starting_potion_ids" },
            ModStudioEntityKind.Card => new[] { "title", "description", "pool_id", "type", "rarity", "target_type", "energy_cost", "energy_cost_x", "canonical_star_cost", "star_cost_x", "can_be_generated_in_combat" },
            ModStudioEntityKind.Relic => new[] { "title", "description", "rarity", "pool_id" },
            ModStudioEntityKind.Potion => new[] { "title", "description", "rarity", "usage", "target_type", "pool_id", "can_be_generated_in_combat" },
            ModStudioEntityKind.Event => new[] { "title", "initial_description", "layout_type", "is_shared" },
            ModStudioEntityKind.Enchantment => new[] { "title", "description", "show_amount", "has_extra_card_text", "extra_card_text" },
            _ => Array.Empty<string>()
        };
    }

    private AssetRef? ResolveImportedAssetBinding(ModStudioEntityKind kind, EntityOverrideEnvelope? envelope, string resolvedPath)
    {
        if (_project == null || envelope == null || string.IsNullOrWhiteSpace(resolvedPath) || !_assetBindingService.TryGetDescriptor(kind, out var descriptor))
        {
            return null;
        }

        return envelope.Assets.FirstOrDefault(asset =>
                   string.Equals(asset.LogicalRole, descriptor.LogicalRole, StringComparison.Ordinal) &&
                   string.Equals(asset.ManagedPath, resolvedPath, StringComparison.OrdinalIgnoreCase)) ??
               _project.ProjectAssets.FirstOrDefault(asset =>
                   string.Equals(asset.LogicalRole, descriptor.LogicalRole, StringComparison.Ordinal) &&
                   string.Equals(asset.ManagedPath, resolvedPath, StringComparison.OrdinalIgnoreCase));
    }

    private bool TryRemoveEmptyEnvelope(EntityBrowserItem item, EntityOverrideEnvelope envelope)
    {
        if (_project == null || item.IsProjectOnly)
        {
            return false;
        }

        if (envelope.Metadata.Count > 0 || envelope.Assets.Count > 0 || !string.IsNullOrWhiteSpace(envelope.GraphId) || envelope.BehaviorSource == BehaviorSource.Graph)
        {
            return false;
        }

        return _project.Overrides.Remove(envelope);
    }

    private BehaviorGraphDefinition CloneGraph(BehaviorGraphDefinition graph)
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

    private void ApplyGeneratedDescription(EntityOverrideEnvelope envelope, BehaviorGraphDefinition graph)
    {
        if (envelope.BehaviorSource != BehaviorSource.Graph)
        {
            return;
        }

        var metadataKey = GetDescriptionMetadataKey(_currentKind);
        if (string.IsNullOrWhiteSpace(metadataKey))
        {
            return;
        }

        var sourceModel = _currentItem == null ? null : ResolveSourceModel(_currentKind, _currentItem.EntityId);
        var generation = _graphDescriptionGenerator.Generate(graph, sourceModel, _graphPreviewContext);
        var generatedDescription = GetAppliedGraphDescription(generation);
        if (string.IsNullOrWhiteSpace(generatedDescription))
        {
            return;
        }

        var cacheKey = BuildAutoDescriptionCacheKey(metadataKey);
        envelope.Metadata.TryGetValue(metadataKey, out var existingDescription);
        envelope.Metadata.TryGetValue(cacheKey, out var cachedAutoDescription);
        if (!string.IsNullOrWhiteSpace(existingDescription) &&
            !string.Equals(existingDescription, cachedAutoDescription, StringComparison.Ordinal))
        {
            return;
        }

        envelope.Metadata[metadataKey] = generatedDescription;
        envelope.Metadata[cacheKey] = generatedDescription;
        if (!string.IsNullOrWhiteSpace(generation.TemplateDescription))
        {
            envelope.Metadata[$"{cacheKey}.template"] = generation.TemplateDescription;
        }
        if (!string.IsNullOrWhiteSpace(generation.PreviewDescription))
        {
            envelope.Metadata[$"{cacheKey}.preview"] = generation.PreviewDescription;
        }
    }

    private string BuildSelectedNodeDynamicSummaryTextV3(BehaviorGraphNodeDefinition? node)
    {
        if (node == null)
        {
            return string.Empty;
        }

        var dynamicPropertyKey = GetPrimaryDynamicPropertyKey(node);
        var dynamicAmount = dynamicPropertyKey == null ? null : EnsureDynamicValueDefinition(node, dynamicPropertyKey);
        if (dynamicAmount == null)
        {
            return string.Empty;
        }

        var sourceModel = _currentItem == null ? null : ResolveSourceModel(_currentKind, _currentItem.EntityId);
        var preview = DynamicValueEvaluator.EvaluatePreview(dynamicAmount, sourceModel, _graphPreviewContext, GetDynamicPropertyDefaultValue(dynamicPropertyKey!));
        var previewDescription = ModStudioGraphCanvasView.GetSuggestedNodeDescription(node, sourceModel, _graphPreviewContext);
        var sourceLabel = ModStudioFieldDisplayNames.FormatGraphPropertyValue("dynamic_source_kind", dynamicAmount.SourceKind.ToString());
        var sourceToken = DynamicValueEvaluator.GetSourceToken(dynamicAmount);
        var authoringTemplate = DynamicValueEvaluator.GetAuthoringTemplate(dynamicAmount);
        var previewText = string.IsNullOrWhiteSpace(preview.PreviewText) ? Dual("不可用", "Unavailable") : preview.PreviewText;
        var previewDescriptionText = string.IsNullOrWhiteSpace(previewDescription) ? Dual("不可用", "Unavailable") : previewDescription;
        var summaryText = string.IsNullOrWhiteSpace(preview.SummaryText) ? Dual("不可用", "Unavailable") : preview.SummaryText;

        var helpLines = new List<string>
        {
            $"{Dual("值来源", "Value Source")}: {sourceLabel}",
            $"{Dual("原版模板", "Original Template")}: {sourceToken}",
            $"{Dual("Graph 模板", "Graph Template")}: {authoringTemplate}",
            $"{Dual("数值预览", "Value Preview")}: {previewText}",
            $"{Dual("描述预览", "Description Preview")}: {previewDescriptionText}",
            $"{Dual("计算说明", "Computation")}: {summaryText}"
        };

        if (!string.IsNullOrWhiteSpace(dynamicAmount.DynamicVarName))
        {
            helpLines.Insert(1, $"{Dual("动态变量", "Dynamic Variable")}: {dynamicAmount.DynamicVarName}");
        }

        if (!string.IsNullOrWhiteSpace(dynamicAmount.FormulaRef))
        {
            helpLines.Insert(2, $"{Dual("公式引用", "Formula Reference")}: {dynamicAmount.FormulaRef}");
        }

        switch (dynamicAmount.SourceKind)
        {
            case DynamicValueSourceKind.DynamicVar:
                helpLines.Add(Dual(
                    "说明：动态变量模式会保留原版变量语义。绝对值覆盖会把原变量基础值改成指定数值，增量覆盖会在原值基础上追加。",
                    "Tip: dynamic-variable mode preserves the original variable semantics. Absolute override replaces the base value, while delta override adds on top of the original value."));
                break;
            case DynamicValueSourceKind.FormulaRef:
                helpLines.Add(Dual(
                    "说明：公式模式会按“基础值 + 额外值 x 右侧上下文来源”来预览和执行。修改右侧上下文，会直接改变当前预览结果。",
                    "Tip: formula mode evaluates as base value + extra value x the selected context source. Changing the preview context directly changes the current preview result."));
                break;
            default:
                helpLines.Add(Dual(
                    "说明：固定值模式不会引用原版动态变量或原版公式。",
                    "Tip: literal mode does not reference the original dynamic variable or formula."));
                break;
        }

        return string.Join(System.Environment.NewLine, helpLines);
    }

    private void EnsureGraphBehaviorEnabledForEdits()
    {
        if (_detailPanel?.GraphEnabledCheck == null || _detailPanel.GraphEnabledCheck.ButtonPressed)
        {
            return;
        }

        _detailPanel.GraphEnabledCheck.ButtonPressed = true;
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

    private static decimal GetDynamicPropertyDefaultValue(string propertyKey)
    {
        return string.Equals(propertyKey, "count", StringComparison.OrdinalIgnoreCase) ? 1m : 0m;
    }

    private static string GetDynamicPropertyDefaultText(string propertyKey)
    {
        return GetDynamicPropertyDefaultValue(propertyKey).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
