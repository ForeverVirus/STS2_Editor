using STS2_Editor.Scripts.Editor.Core.Models;
using STS2_Editor.Scripts.Editor.Core.Services;
using STS2_Editor.Scripts.Editor.Graph;
using STS2_Editor.Scripts.Editor.UI;

namespace STS2_Editor.Scripts.Editor.AI;

public sealed class AiProjectContextService
{
    private static readonly ModStudioEntityKind[] SupportedEntityKinds =
    {
        ModStudioEntityKind.Card,
        ModStudioEntityKind.Relic,
        ModStudioEntityKind.Potion,
        ModStudioEntityKind.Event,
        ModStudioEntityKind.Enchantment
    };

    private readonly ModelMetadataService _metadataService;
    private readonly ProjectAssetBindingService _assetBindingService;
    private readonly BehaviorGraphRegistry _graphRegistry;
    private readonly NativeBehaviorAutoGraphService _autoGraphService = new();

    public AiProjectContextService(
        ModelMetadataService metadataService,
        ProjectAssetBindingService assetBindingService,
        BehaviorGraphRegistry graphRegistry)
    {
        _metadataService = metadataService;
        _assetBindingService = assetBindingService;
        _graphRegistry = graphRegistry;
    }

    public string BuildAmbientContext(EditorProject project, AiExecutionContext context)
    {
        var projectEntities = EnumerateProjectEntities(project);
        var payload = new
        {
            project = new
            {
                project_id = project.Manifest.ProjectId,
                name = project.Manifest.Name,
                author = project.Manifest.Author,
                description = project.Manifest.Description
            },
            current_selection = BuildCurrentSelection(project, context),
            project_entities = projectEntities
        };

        return AiProtocolParser.SerializeQueryResult(payload);
    }

    public string ExecuteQueries(EditorProject project, AiExecutionContext context, IReadOnlyList<AiQueryRequest> queries)
    {
        var results = new List<object>();
        foreach (var query in queries)
        {
            results.Add(ExecuteQuery(project, context, query));
        }

        return AiProtocolParser.SerializeQueryResult(new { results });
    }

    private object ExecuteQuery(EditorProject project, AiExecutionContext context, AiQueryRequest query)
    {
        var queryType = (query.QueryType ?? string.Empty).Trim().ToLowerInvariant();
        return queryType switch
        {
            "get_current_selection" => new { query_type = query.QueryType, data = BuildCurrentSelection(project, context) },
            "list_project_entities" => new { query_type = query.QueryType, data = BuildEntityList(project, query.EntityKind) },
            "get_entity_snapshot" => new { query_type = query.QueryType, data = BuildEntitySnapshot(project, context, query) },
            "get_graph_snapshot" => new { query_type = query.QueryType, data = BuildGraphSnapshot(project, context, query) },
            "list_node_types" => new { query_type = query.QueryType, data = BuildNodeTypeList(query.EntityKind, context.CurrentKind) },
            "get_node_schema" => new { query_type = query.QueryType, data = BuildNodeSchema(query.NodeType) },
            "list_asset_choices" => new { query_type = query.QueryType, data = BuildAssetChoices(project, context, query) },
            _ => new { query_type = query.QueryType, error = $"Unsupported query type '{query.QueryType}'." }
        };
    }

    private object BuildCurrentSelection(EditorProject project, AiExecutionContext context)
    {
        var mergedMetadata = ResolveMergedMetadata(project, context.CurrentKind, context.CurrentEntityId);
        return new
        {
            entity_kind = context.CurrentKind.ToString(),
            entity_id = context.CurrentEntityId,
            selected_graph_node_id = context.SelectedGraphNodeId,
            metadata = mergedMetadata
        };
    }

    private IReadOnlyList<object> BuildEntityList(EditorProject project, string requestedKind)
    {
        var entities = EnumerateProjectEntities(project);
        if (!string.IsNullOrWhiteSpace(requestedKind) && TryParseKind(requestedKind, out var kind))
        {
            entities = entities
                .Where(entity => string.Equals(entity.Kind, kind.ToString(), StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return entities.Select(entity => (object)entity).ToList();
    }

    private object BuildEntitySnapshot(EditorProject project, AiExecutionContext context, AiQueryRequest query)
    {
        if (!TryResolveEntityTarget(query.EntityKind, query.EntityId, query.EntityRef, context, out var kind, out var entityId))
        {
            return new { error = "Entity target could not be resolved." };
        }

        var mergedMetadata = ResolveMergedMetadata(project, kind, entityId);
        var envelope = GetEnvelope(project, kind, entityId);
        return new
        {
            entity_kind = kind.ToString(),
            entity_id = entityId,
            metadata = mergedMetadata,
            behavior_source = envelope?.BehaviorSource.ToString() ?? BehaviorSource.Native.ToString(),
            graph_id = envelope?.GraphId ?? string.Empty,
            notes = envelope?.Notes ?? string.Empty,
            bound_assets = envelope?.Assets.Select(asset => (object)new
            {
                asset_id = asset.Id,
                logical_role = asset.LogicalRole,
                managed_path = asset.ManagedPath
            }).ToList() ?? new List<object>()
        };
    }

    private object BuildGraphSnapshot(EditorProject project, AiExecutionContext context, AiQueryRequest query)
    {
        if (!TryResolveEntityTarget(query.EntityKind, query.EntityId, query.EntityRef, context, out var kind, out var entityId))
        {
            return new { error = "Entity target could not be resolved." };
        }

        if (!TryGetEffectiveGraph(project, kind, entityId, out var graph, out var graphSource))
        {
            return new
            {
                entity_kind = kind.ToString(),
                entity_id = entityId,
                has_graph = false
            };
        }

        return new
        {
            entity_kind = kind.ToString(),
            entity_id = entityId,
            has_graph = true,
            graph_source = graphSource,
            graph = new
            {
                graph_id = graph.GraphId,
                name = graph.Name,
                description = graph.Description,
                entity_kind = graph.EntityKind?.ToString() ?? kind.ToString(),
                entry_node_id = graph.EntryNodeId,
                metadata = graph.Metadata,
                nodes = graph.Nodes.Select(node => new
                {
                    node_id = node.NodeId,
                    node_type = node.NodeType,
                    display_name = node.DisplayName,
                    description = node.Description,
                    properties = node.Properties,
                    dynamic_values = node.DynamicValues.ToDictionary(
                        pair => pair.Key,
                        pair => new
                        {
                            source_kind = pair.Value.SourceKind.ToString(),
                            literal_value = pair.Value.LiteralValue,
                            dynamic_var_name = pair.Value.DynamicVarName,
                            formula_ref = pair.Value.FormulaRef,
                            base_override_mode = pair.Value.BaseOverrideMode.ToString(),
                            base_override_value = pair.Value.BaseOverrideValue,
                            extra_override_mode = pair.Value.ExtraOverrideMode.ToString(),
                            extra_override_value = pair.Value.ExtraOverrideValue,
                            preview_multiplier_key = pair.Value.PreviewMultiplierKey
                        },
                        StringComparer.Ordinal),
                    ports = BuildNodePortSnapshot(node.NodeType)
                }).ToList(),
                connections = graph.Connections.Select(connection => new
                {
                    from_node_id = connection.FromNodeId,
                    from_port_id = connection.FromPortId,
                    to_node_id = connection.ToNodeId,
                    to_port_id = connection.ToPortId
                }).ToList()
            }
        };
    }

    private IReadOnlyList<object> BuildNodeTypeList(string requestedKind, ModStudioEntityKind fallbackKind)
    {
        var entityKind = TryParseKind(requestedKind, out var parsedKind) ? parsedKind : fallbackKind;
        return _graphRegistry.Definitions
            .Where(definition => BehaviorGraphPaletteFilter.IsAllowed(entityKind, definition.NodeType))
            .OrderBy(definition => definition.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(definition => (object)new
            {
                node_type = definition.NodeType,
                display_name = definition.DisplayName,
                description = definition.Description
            })
            .ToList();
    }

    private object BuildNodeSchema(string nodeType)
    {
        if (!_graphRegistry.TryGetNodeDefinition(nodeType, out var definition) || definition == null)
        {
            return new { error = $"Unknown node type '{nodeType}'." };
        }

        var defaultPropertyKeys = definition.DefaultProperties?.Keys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase).ToList() ?? new List<string>();
        return new
        {
            node_type = definition.NodeType,
            display_name = definition.DisplayName,
            description = definition.Description,
            inputs = definition.Inputs.Select(port => new
            {
                port_id = port.PortId,
                display_name = port.DisplayName,
                value_type = port.ValueType
            }).ToList(),
            outputs = definition.Outputs.Select(port => new
            {
                port_id = port.PortId,
                display_name = port.DisplayName,
                value_type = port.ValueType
            }).ToList(),
            default_properties = definition.DefaultProperties ?? new Dictionary<string, string>(StringComparer.Ordinal),
            property_choices = defaultPropertyKeys.ToDictionary(
                key => key,
                key => FieldChoiceProvider.GetGraphChoices(key)
                    .Select(choice => new { value = choice.Value, display = choice.Display })
                    .ToList(),
                StringComparer.Ordinal)
        };
    }

    private object BuildAssetChoices(EditorProject project, AiExecutionContext context, AiQueryRequest query)
    {
        if (!TryResolveEntityTarget(query.EntityKind, query.EntityId, query.EntityRef, context, out var kind, out var entityId))
        {
            return new { error = "Entity target could not be resolved." };
        }

        if (!_assetBindingService.TryGetDescriptor(kind, out var descriptor))
        {
            return new
            {
                entity_kind = kind.ToString(),
                entity_id = entityId,
                supports_assets = false
            };
        }

        return new
        {
            entity_kind = kind.ToString(),
            entity_id = entityId,
            supports_assets = true,
            logical_role = descriptor.LogicalRole,
            metadata_key = descriptor.MetadataKey,
            runtime_assets = _assetBindingService.GetRuntimeAssetCandidates(kind)
                .Select(path => new { runtime_path = path })
                .ToList(),
            imported_assets = project.ProjectAssets
                .Where(asset => string.Equals(asset.LogicalRole, descriptor.LogicalRole, StringComparison.Ordinal))
                .Select(asset => new
                {
                    asset_id = asset.Id,
                    logical_role = asset.LogicalRole,
                    managed_path = asset.ManagedPath,
                    file_name = asset.FileName
                })
                .ToList()
        };
    }

    private IReadOnlyList<object> BuildNodePortSnapshot(string nodeType)
    {
        if (!_graphRegistry.TryGetNodeDefinition(nodeType, out var definition) || definition == null)
        {
            return Array.Empty<object>();
        }

        return definition.Inputs.Select(port => (object)new
            {
                direction = "input",
                port_id = port.PortId,
                display_name = port.DisplayName,
                value_type = port.ValueType
            })
            .Concat(definition.Outputs.Select(port => (object)new
            {
                direction = "output",
                port_id = port.PortId,
                display_name = port.DisplayName,
                value_type = port.ValueType
            }))
            .ToList();
    }

    private IReadOnlyDictionary<string, string> ResolveMergedMetadata(EditorProject project, ModStudioEntityKind kind, string entityId)
    {
        IReadOnlyDictionary<string, string> baseline;
        if (_metadataService.HasRuntimeEntity(kind, entityId))
        {
            baseline = _metadataService.GetEditableMetadata(kind, entityId);
        }
        else
        {
            baseline = _metadataService.CreateDefaultMetadata(kind, entityId);
        }

        var result = new Dictionary<string, string>(baseline, StringComparer.Ordinal);
        var envelope = GetEnvelope(project, kind, entityId);
        if (envelope != null)
        {
            foreach (var pair in envelope.Metadata)
            {
                result[pair.Key] = pair.Value;
            }
        }

        return result;
    }

    private bool TryGetEffectiveGraph(EditorProject project, ModStudioEntityKind kind, string entityId, out BehaviorGraphDefinition graph, out string source)
    {
        if (TryGetProjectGraph(project, kind, entityId, out graph))
        {
            source = "project";
            return true;
        }

        if (_metadataService.HasRuntimeEntity(kind, entityId) &&
            _autoGraphService.TryCreateGraph(kind, entityId, out var result) &&
            result?.Graph != null)
        {
            graph = CloneGraph(result.Graph);
            source = "native_auto";
            return true;
        }

        graph = new BehaviorGraphDefinition();
        source = string.Empty;
        return false;
    }

    private bool TryGetProjectGraph(EditorProject project, ModStudioEntityKind kind, string entityId, out BehaviorGraphDefinition graph)
    {
        var envelope = GetEnvelope(project, kind, entityId);
        if (envelope != null &&
            !string.IsNullOrWhiteSpace(envelope.GraphId) &&
            project.Graphs.TryGetValue(envelope.GraphId, out var projectGraph))
        {
            graph = CloneGraph(projectGraph);
            return true;
        }

        graph = new BehaviorGraphDefinition();
        return false;
    }

    private static EntityOverrideEnvelope? GetEnvelope(EditorProject project, ModStudioEntityKind kind, string entityId)
    {
        return project.Overrides.FirstOrDefault(item =>
            item.EntityKind == kind &&
            string.Equals(item.EntityId, entityId, StringComparison.Ordinal));
    }

    private static bool TryResolveEntityTarget(
        string requestedKind,
        string requestedEntityId,
        string requestedEntityRef,
        AiExecutionContext context,
        out ModStudioEntityKind kind,
        out string entityId)
    {
        if (TryParseKind(requestedKind, out kind) && !string.IsNullOrWhiteSpace(requestedEntityId))
        {
            entityId = requestedEntityId;
            return true;
        }

        if (string.IsNullOrWhiteSpace(requestedKind) &&
            string.IsNullOrWhiteSpace(requestedEntityRef) &&
            string.IsNullOrWhiteSpace(requestedEntityId) &&
            !string.IsNullOrWhiteSpace(context.CurrentEntityId))
        {
            kind = context.CurrentKind;
            entityId = context.CurrentEntityId;
            return true;
        }

        entityId = string.Empty;
        kind = default;
        return false;
    }

    private static bool TryParseKind(string rawValue, out ModStudioEntityKind kind)
    {
        return Enum.TryParse(rawValue, ignoreCase: true, out kind);
    }

    private List<ProjectEntitySummary> EnumerateProjectEntities(EditorProject project)
    {
        return project.Overrides
            .Where(envelope => SupportedEntityKinds.Contains(envelope.EntityKind))
            .OrderBy(envelope => envelope.EntityKind.ToString(), StringComparer.OrdinalIgnoreCase)
            .ThenBy(envelope => envelope.EntityId, StringComparer.OrdinalIgnoreCase)
            .Select(envelope =>
            {
                var metadata = ResolveMergedMetadata(project, envelope.EntityKind, envelope.EntityId);
                var title = metadata.TryGetValue("title", out var titleValue) && !string.IsNullOrWhiteSpace(titleValue)
                    ? titleValue
                    : envelope.EntityId;
                var summary = envelope.EntityKind switch
                {
                    ModStudioEntityKind.Event => metadata.TryGetValue("initial_description", out var eventDescription) ? eventDescription : string.Empty,
                    _ => metadata.TryGetValue("description", out var description) ? description : string.Empty
                };

                return new ProjectEntitySummary
                {
                    Kind = envelope.EntityKind.ToString(),
                    EntityId = envelope.EntityId,
                    Title = title,
                    Summary = summary,
                    IsProjectOnly = !_metadataService.HasRuntimeEntity(envelope.EntityKind, envelope.EntityId),
                    HasProjectGraph = TryGetProjectGraph(project, envelope.EntityKind, envelope.EntityId, out _)
                };
            })
            .ToList();
    }

    private sealed class ProjectEntitySummary
    {
        public string Kind { get; set; } = string.Empty;

        public string EntityId { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string Summary { get; set; } = string.Empty;

        public bool IsProjectOnly { get; set; }

        public bool HasProjectGraph { get; set; }
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
}
