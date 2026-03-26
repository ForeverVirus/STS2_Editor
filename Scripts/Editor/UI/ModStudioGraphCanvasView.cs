using System.Globalization;
using Godot;
using MegaCrit.Sts2.Core.Models;
using STS2_Editor.Scripts.Editor.Core.Models;
using STS2_Editor.Scripts.Editor.Graph;
using static STS2_Editor.Scripts.Editor.UI.ModStudioUiFactory;

namespace STS2_Editor.Scripts.Editor.UI;

public sealed partial class ModStudioGraphCanvasView : Control
{
    private const string LayoutKeyPrefix = "layout.node.";
    private const float MinZoom = 0.4f;
    private const float MaxZoom = 1.9f;
    private const float DefaultNodeWidth = 300f;

    private readonly Dictionary<string, GraphNode> _nodeViews = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Label> _nodeDescriptionLabels = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Label> _nodePropertySummaryLabels = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Label> _nodeTypeBadgeLabels = new(StringComparer.Ordinal);
    private readonly Dictionary<string, NodeSlotMap> _slotMaps = new(StringComparer.Ordinal);
    private readonly DynamicPreviewService _dynamicPreviewService = new();

    private GraphEdit? _graphEdit;
    private Label? _hintLabel;
    private PopupPanel? _nodePalettePopup;
    private LineEdit? _nodePaletteSearchEdit;
    private ItemList? _nodePaletteList;
    private RichTextLabel? _nodePaletteDescriptionLabel;

    private BehaviorGraphDefinition? _graph;
    private BehaviorGraphRegistry? _registry;
    private AbstractModel? _sourceModel;
    private DynamicPreviewContext? _previewContext;
    private string? _selectedNodeId;
    private bool _isReady;
    private bool _rebuildQueued;
    private Vector2 _pendingNodeGraphPosition = new(140f, 100f);

    public BehaviorGraphDefinition? BoundGraph => _graph;
    public string? SelectedNodeId => _selectedNodeId;

    public event Action<BehaviorGraphDefinition>? GraphChanged;
    public event Action<string?>? SelectedNodeChanged;

    public override void _Ready()
    {
        BuildUi();
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        _isReady = true;
        RefreshNodePalette();
        QueueCanvasRebuild();
    }

    public void BindGraph(BehaviorGraphDefinition graph, BehaviorGraphRegistry registry, AbstractModel? sourceModel = null, DynamicPreviewContext? previewContext = null)
    {
        _graph = graph;
        _registry = registry;
        _sourceModel = sourceModel;
        _previewContext = previewContext;
        _selectedNodeId = null;
        _pendingNodeGraphPosition = new Vector2(140f, 100f);
        if (_isReady)
        {
            RefreshNodePalette();
            QueueCanvasRebuild();
        }
    }

    public void UpdatePreviewContext(AbstractModel? sourceModel, DynamicPreviewContext? previewContext)
    {
        _sourceModel = sourceModel;
        _previewContext = previewContext;
        if (_graph == null)
        {
            RefreshStatus();
            return;
        }

        foreach (var node in _graph.Nodes)
        {
            UpdateNodePresentation(node);
        }

        if (!string.IsNullOrWhiteSpace(_selectedNodeId) &&
            _graph.Nodes.Any(node => string.Equals(node.NodeId, _selectedNodeId, StringComparison.Ordinal)))
        {
            SetSelectedNode(_selectedNodeId);
        }
        else
        {
            ApplySelectionHighlight();
        }

        RefreshStatus();
    }

    public void ClearBinding()
    {
        _graph = null;
        _registry = null;
        _sourceModel = null;
        _previewContext = null;
        _selectedNodeId = null;
        if (_isReady)
        {
            RefreshNodePalette();
            QueueCanvasRebuild();
        }
    }

    public void RebuildCanvas()
    {
        QueueCanvasRebuild();
    }

    public void RefreshNodePalette()
    {
        if (_nodePaletteList == null)
        {
            return;
        }

        _nodePaletteList.Clear();
        var definitions = GetFilteredDefinitions();
        foreach (var definition in definitions)
        {
            var title = ResolveNodeTitle(definition);
            _nodePaletteList.AddItem(title);
            _nodePaletteList.SetItemMetadata(_nodePaletteList.ItemCount - 1, definition.NodeType);
            _nodePaletteList.SetItemTooltipEnabled(_nodePaletteList.ItemCount - 1, true);
            _nodePaletteList.SetItemTooltip(_nodePaletteList.ItemCount - 1, definition.Description);
        }

        if (_nodePaletteList.ItemCount > 0)
        {
            _nodePaletteList.Select(0);
        }

        RefreshPaletteDescription();
    }

    public void ShowNodePaletteAtCanvasCenter()
    {
        if (_graphEdit == null)
        {
            return;
        }

        OpenNodePalette(_graphEdit.Size * 0.5f);
    }

    public void DeleteSelectedNode()
    {
        if (_graph == null || string.IsNullOrWhiteSpace(_selectedNodeId))
        {
            return;
        }

        _graph.Nodes.RemoveAll(node => string.Equals(node.NodeId, _selectedNodeId, StringComparison.Ordinal));
        _graph.Connections.RemoveAll(connection =>
            string.Equals(connection.FromNodeId, _selectedNodeId, StringComparison.Ordinal) ||
            string.Equals(connection.ToNodeId, _selectedNodeId, StringComparison.Ordinal));

        _selectedNodeId = null;
        QueueCanvasRebuild();
        NotifyGraphChanged();
    }

    public void AutoLayout()
    {
        if (_graph == null)
        {
            return;
        }

        var actionNodes = _graph.Nodes
            .Where(node => !string.Equals(node.NodeId, _graph.EntryNodeId, StringComparison.Ordinal))
            .OrderBy(node => node.NodeType, StringComparer.OrdinalIgnoreCase)
            .ThenBy(node => node.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(node => node.NodeId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var x = 120f;
        var y = 90f;
        var column = 0;
        foreach (var node in actionNodes)
        {
            if (_nodeViews.TryGetValue(node.NodeId, out var nodeView))
            {
                nodeView.PositionOffset = new Vector2(x + column * 380f, y);
            }

            y += 220f;
            if (y > 900f)
            {
                y = 90f;
                column++;
            }
        }

        ExportLayout();
        NotifyGraphChanged();
    }

    public void ZoomToFit()
    {
        if (_graphEdit == null || _nodeViews.Count == 0)
        {
            return;
        }

        var rect = GetBounds();
        var viewportSize = _graphEdit.Size;
        if (rect.Size.X <= 0f || rect.Size.Y <= 0f || viewportSize.X <= 0f || viewportSize.Y <= 0f)
        {
            return;
        }

        var horizontalZoom = viewportSize.X / Math.Max(rect.Size.X + 140f, 1f);
        var verticalZoom = viewportSize.Y / Math.Max(rect.Size.Y + 140f, 1f);
        _graphEdit.Zoom = Math.Clamp(Math.Min(horizontalZoom, verticalZoom), MinZoom, MaxZoom);
        _graphEdit.ScrollOffset = rect.Position - new Vector2(70f, 70f);
    }

    public void ZoomBy(float delta)
    {
        if (_graphEdit == null)
        {
            return;
        }

        _graphEdit.Zoom = Math.Clamp(_graphEdit.Zoom + delta, MinZoom, MaxZoom);
    }

    public bool ExportLayout()
    {
        if (_graph == null)
        {
            return false;
        }

        foreach (var pair in _nodeViews)
        {
            var position = pair.Value.PositionOffset;
            _graph.Metadata[$"{LayoutKeyPrefix}{pair.Key}.x"] = position.X.ToString("R", CultureInfo.InvariantCulture);
            _graph.Metadata[$"{LayoutKeyPrefix}{pair.Key}.y"] = position.Y.ToString("R", CultureInfo.InvariantCulture);
        }

        return true;
    }

    public void UpdateNodePresentation(BehaviorGraphNodeDefinition? node)
    {
        if (node == null || !_nodeViews.TryGetValue(node.NodeId, out var graphNode))
        {
            return;
        }

        graphNode.Title = ResolveNodeTitle(node);
        if (_nodeDescriptionLabels.TryGetValue(node.NodeId, out var descriptionLabel))
        {
            descriptionLabel.Text = BuildDynamicDescription(node, _sourceModel, _previewContext);
        }

        if (_nodePropertySummaryLabels.TryGetValue(node.NodeId, out var summaryLabel))
        {
            summaryLabel.Text = BuildDynamicPropertySummary(node, _sourceModel, _previewContext);
        }

        if (_nodeTypeBadgeLabels.TryGetValue(node.NodeId, out var typeBadge))
        {
            typeBadge.Text = node.NodeType;
            typeBadge.Modulate = ResolveNodeAccent(node.NodeType);
        }

        ApplySelectionHighlight();
    }

    public static string GetSuggestedNodeDescription(BehaviorGraphNodeDefinition node)
    {
        return BuildDynamicDescription(node, null, null);
    }

    public static string GetSuggestedNodeDescription(BehaviorGraphNodeDefinition node, AbstractModel? sourceModel, DynamicPreviewContext? previewContext)
    {
        return BuildDynamicDescription(node, sourceModel, previewContext);
    }

    public void RefreshStatus()
    {
        if (_hintLabel == null)
        {
            return;
        }

        _hintLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _hintLabel.CustomMinimumSize = new Vector2(0f, 24f);
        _hintLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _hintLabel.ClipText = false;
        _hintLabel.Text = _graph == null
            ? Dual("右键空白处添加节点，拖拽空白处平移画布，滚轮缩放。", "Right-click the canvas to add nodes. Drag the background to pan. Use the mouse wheel to zoom.")
            : Dual($"右键空白处添加节点  ·  节点 {_graph.Nodes.Count}  ·  连线 {_graph.Connections.Count}", $"Right-click the canvas to add nodes  ·  Nodes {_graph.Nodes.Count}  ·  Connections {_graph.Connections.Count}");
    }

    private void BuildUi()
    {
        if (_graphEdit != null)
        {
            return;
        }

        var root = new Control
        {
            AnchorRight = 1f,
            AnchorBottom = 1f,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Stop
        };
        AddChild(root);

        var graphPanel = new PanelContainer
        {
            AnchorRight = 1f,
            AnchorBottom = 1f,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        root.AddChild(graphPanel);

        _graphEdit = new GraphEdit
        {
            Name = "ModStudioGraphEdit",
            AnchorRight = 1f,
            AnchorBottom = 1f,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            RightDisconnects = true,
            MouseFilter = MouseFilterEnum.Stop,
            ShowGrid = true,
            SnappingEnabled = true,
            SnappingDistance = 24,
            ShowMenu = false,
            ShowGridButtons = false,
            ShowZoomButtons = false,
            ShowZoomLabel = false,
            MinimapEnabled = true
        };
        _graphEdit.PanningScheme = GraphEdit.PanningSchemeEnum.Pans;
        _graphEdit.ConnectionRequest += OnConnectionRequest;
        _graphEdit.DisconnectionRequest += OnDisconnectionRequest;
        _graphEdit.PopupRequest += OnGraphPopupRequest;
        _graphEdit.GuiInput += OnGraphEditGuiInput;
        graphPanel.AddChild(_graphEdit);

        var overlayMargin = new MarginContainer
        {
            AnchorRight = 1f,
            AnchorBottom = 1f,
            MouseFilter = MouseFilterEnum.Ignore
        };
        overlayMargin.AddThemeConstantOverride("margin_left", 14);
        overlayMargin.AddThemeConstantOverride("margin_top", 14);
        overlayMargin.AddThemeConstantOverride("margin_right", 14);
        overlayMargin.AddThemeConstantOverride("margin_bottom", 14);
        root.AddChild(overlayMargin);

        var overlayStack = new VBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        overlayStack.AddThemeConstantOverride("separation", 8);
        overlayMargin.AddChild(overlayStack);

        var hintPanel = new PanelContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
            CustomMinimumSize = new Vector2(520f, 32f)
        };
        overlayStack.AddChild(hintPanel);

        var hintMargin = new MarginContainer
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        hintMargin.AddThemeConstantOverride("margin_left", 10);
        hintMargin.AddThemeConstantOverride("margin_top", 6);
        hintMargin.AddThemeConstantOverride("margin_right", 10);
        hintMargin.AddThemeConstantOverride("margin_bottom", 6);
        hintPanel.AddChild(hintMargin);

        _hintLabel = MakeLabel(string.Empty, true);
        _hintLabel.MouseFilter = MouseFilterEnum.Ignore;
        _hintLabel.AutowrapMode = TextServer.AutowrapMode.Off;
        _hintLabel.ClipText = true;
        hintMargin.AddChild(_hintLabel);

        _nodePalettePopup = BuildNodePalettePopup();
        AddChild(_nodePalettePopup);

        RefreshStatus();
    }

    private PopupPanel BuildNodePalettePopup()
    {
        var popup = new PopupPanel
        {
            Size = new Vector2I(360, 420),
            Visible = false
        };

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_top", 12);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_bottom", 12);
        popup.AddChild(margin);

        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", 8);
        margin.AddChild(root);

        root.AddChild(MakeLabel(Dual("添加节点", "Add Node"), true));

        _nodePaletteSearchEdit = new LineEdit
        {
            PlaceholderText = Dual("搜索节点名称或类型...", "Search node name or type..."),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _nodePaletteSearchEdit.TextChanged += _ => RefreshNodePalette();
        root.AddChild(_nodePaletteSearchEdit);

        _nodePaletteList = new ItemList
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SelectMode = ItemList.SelectModeEnum.Single
        };
        _nodePaletteList.ItemSelected += _ => RefreshPaletteDescription();
        _nodePaletteList.ItemActivated += _ => CreateNodeFromPalette();
        root.AddChild(_nodePaletteList);

        _nodePaletteDescriptionLabel = MakeDetails(string.Empty, scrollActive: false, fitContent: true, minHeight: 84f);
        root.AddChild(_nodePaletteDescriptionLabel);

        var actionRow = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        actionRow.AddThemeConstantOverride("separation", 8);
        root.AddChild(actionRow);

        actionRow.AddChild(MakeButton(Dual("取消", "Cancel"), () => popup.Hide()));
        actionRow.AddChild(MakeButton(Dual("添加到画布", "Add To Canvas"), CreateNodeFromPalette));

        return popup;
    }

    private void QueueCanvasRebuild()
    {
        if (!_isReady || _graphEdit == null || _rebuildQueued)
        {
            return;
        }

        _rebuildQueued = true;
        Callable.From(RebuildCanvasNow).CallDeferred();
    }

    private void RebuildCanvasNow()
    {
        _rebuildQueued = false;
        if (!_isReady || _graphEdit == null)
        {
            return;
        }

        if (!_graphEdit.IsInsideTree() || !_graphEdit.IsNodeReady())
        {
            QueueCanvasRebuild();
            return;
        }

        ClearGraphEdit();
        _nodeViews.Clear();
        _nodeDescriptionLabels.Clear();
        _nodePropertySummaryLabels.Clear();
        _nodeTypeBadgeLabels.Clear();
        _slotMaps.Clear();

        if (_graph == null)
        {
            RefreshStatus();
            SelectedNodeChanged?.Invoke(null);
            return;
        }

        var layout = BuildLayout(_graph);
        foreach (var node in _graph.Nodes)
        {
            var position = layout.TryGetValue(node.NodeId, out var storedPosition)
                ? storedPosition
                : GetSuggestedNodePosition(node);
            var nodeView = BuildGraphNode(node, position);
            _graphEdit.AddChild(nodeView);
            _nodeViews[node.NodeId] = nodeView;
        }

        SelectInitialNode();
        ApplySelectionHighlight();
        RefreshStatus();
        Callable.From(ApplyConnectionsSafe).CallDeferred();
    }

    private void OnGraphEditGuiInput(InputEvent inputEvent)
    {
        if (_graphEdit == null)
        {
            return;
        }

        switch (inputEvent)
        {
            case InputEventMouseButton button when button.Pressed && (button.ButtonIndex == MouseButton.WheelUp || button.ButtonIndex == MouseButton.WheelDown):
            {
                var delta = button.ButtonIndex == MouseButton.WheelUp ? 0.1f : -0.1f;
                ZoomAt(delta, button.Position);
                _graphEdit.AcceptEvent();
                return;
            }
        }
    }

    private void OnGraphPopupRequest(Vector2 position)
    {
        OpenNodePalette(position);
    }

    private void OpenNodePalette(Vector2 canvasPosition)
    {
        if (_nodePalettePopup == null)
        {
            return;
        }

        _pendingNodeGraphPosition = ToGraphPosition(canvasPosition);
        if (_nodePaletteSearchEdit != null)
        {
            _nodePaletteSearchEdit.Text = string.Empty;
        }

        RefreshNodePalette();

        var popupPosition = GetViewport().GetMousePosition() + new Vector2(12f, 12f);
        _nodePalettePopup.Position = new Vector2I((int)popupPosition.X, (int)popupPosition.Y);
        _nodePalettePopup.Popup();
        _nodePaletteSearchEdit?.GrabFocus();
    }

    private void CreateNodeFromPalette()
    {
        if (_graph == null || _nodePaletteList == null || _nodePaletteList.GetSelectedItems().Length == 0)
        {
            return;
        }

        var index = _nodePaletteList.GetSelectedItems()[0];
        var nodeType = _nodePaletteList.GetItemMetadata(index).AsString();
        if (string.IsNullOrWhiteSpace(nodeType))
        {
            return;
        }

        AddNode(nodeType, _pendingNodeGraphPosition);
        _nodePalettePopup?.Hide();
    }

    private void AddNode(string nodeType, Vector2 position)
    {
        if (_graph == null || !BehaviorGraphPaletteFilter.IsAllowed(_graph.EntityKind ?? ModStudioEntityKind.Card, nodeType))
        {
            return;
        }

        var descriptor = GetDescriptor(nodeType);
        var node = new BehaviorGraphNodeDefinition
        {
            NodeType = nodeType,
            DisplayName = descriptor?.DisplayName ?? nodeType,
            Description = descriptor?.Description ?? string.Empty,
            Properties = descriptor?.DefaultProperties != null
                ? new Dictionary<string, string>(descriptor.DefaultProperties, StringComparer.Ordinal)
                : new Dictionary<string, string>(StringComparer.Ordinal)
        };

        if ((string.Equals(nodeType, "orb.channel", StringComparison.Ordinal) ||
             string.Equals(nodeType, "orb.passive", StringComparison.Ordinal)) &&
            (!node.Properties.TryGetValue("orb_id", out var orbId) || string.IsNullOrWhiteSpace(orbId)))
        {
            var defaultOrbId = FieldChoiceProvider.GetGraphChoices("orb_id").FirstOrDefault().Value;
            if (!string.IsNullOrWhiteSpace(defaultOrbId))
            {
                node.Properties["orb_id"] = defaultOrbId;
            }
        }

        _graph.Nodes.Add(node);
        _graph.Metadata[$"{LayoutKeyPrefix}{node.NodeId}.x"] = position.X.ToString("R", CultureInfo.InvariantCulture);
        _graph.Metadata[$"{LayoutKeyPrefix}{node.NodeId}.y"] = position.Y.ToString("R", CultureInfo.InvariantCulture);
        _selectedNodeId = node.NodeId;
        QueueCanvasRebuild();
        NotifyGraphChanged();
    }

    private void ZoomAt(float delta, Vector2 mousePosition)
    {
        if (_graphEdit == null)
        {
            return;
        }

        var oldZoom = Math.Max(_graphEdit.Zoom, 0.001f);
        var newZoom = Math.Clamp(oldZoom + delta, MinZoom, MaxZoom);
        if (Math.Abs(newZoom - oldZoom) < 0.0001f)
        {
            return;
        }

        var graphPoint = _graphEdit.ScrollOffset + (mousePosition / oldZoom);
        _graphEdit.Zoom = newZoom;
        _graphEdit.ScrollOffset = graphPoint - (mousePosition / newZoom);
    }

    private Vector2 ToGraphPosition(Vector2 canvasPosition)
    {
        if (_graphEdit == null)
        {
            return canvasPosition;
        }

        var zoom = Math.Max(_graphEdit.Zoom, 0.001f);
        return _graphEdit.ScrollOffset + (canvasPosition / zoom);
    }

    private IEnumerable<BehaviorGraphNodeDefinitionDescriptor> GetFilteredDefinitions()
    {
        var search = _nodePaletteSearchEdit?.Text?.Trim() ?? string.Empty;
        var entityKind = _graph?.EntityKind ?? ModStudioEntityKind.Card;
        return BehaviorGraphPaletteFilter.FilterForEntityKind(
                _registry?.Definitions ?? Array.Empty<BehaviorGraphNodeDefinitionDescriptor>(),
                entityKind)
            .Where(definition =>
                string.IsNullOrWhiteSpace(search) ||
                ResolveNodeTitle(definition).Contains(search, StringComparison.OrdinalIgnoreCase) ||
                definition.NodeType.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                definition.Description.Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderBy(definition => ResolveNodeTitle(definition), StringComparer.OrdinalIgnoreCase)
            .ThenBy(definition => definition.NodeType, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void RefreshPaletteDescription()
    {
        if (_nodePaletteDescriptionLabel == null || _nodePaletteList == null)
        {
            return;
        }

        if (_nodePaletteList.GetSelectedItems().Length == 0)
        {
            _nodePaletteDescriptionLabel.Text = Dual("右键空白处可以再次打开节点菜单。", "You can reopen the node menu by right-clicking the canvas.");
            return;
        }

        var index = _nodePaletteList.GetSelectedItems()[0];
        var nodeType = _nodePaletteList.GetItemMetadata(index).AsString();
        var descriptor = GetDescriptor(nodeType);
        if (descriptor == null)
        {
            _nodePaletteDescriptionLabel.Text = string.Empty;
            return;
        }

        var inputs = descriptor.Inputs.Count;
        var outputs = descriptor.Outputs.Count;
        _nodePaletteDescriptionLabel.Text = string.Join(System.Environment.NewLine, new[]
        {
            ResolveNodeTitle(descriptor),
            descriptor.Description,
            Dual($"输入端口: {inputs}  ·  输出端口: {outputs}", $"Input Ports: {inputs}  ·  Output Ports: {outputs}")
        });
    }

    private BehaviorGraphNodeDefinitionDescriptor? GetDescriptor(string? nodeType)
    {
        if (_registry == null || string.IsNullOrWhiteSpace(nodeType))
        {
            return null;
        }

        return _registry.Definitions.FirstOrDefault(definition => string.Equals(definition.NodeType, nodeType, StringComparison.Ordinal));
    }

    private void OnConnectionRequest(StringName fromNode, long fromPortIndex, StringName toNode, long toPortIndex)
    {
        if (_graph == null)
        {
            return;
        }

        var fromNodeId = fromNode.ToString();
        var toNodeId = toNode.ToString();
        if (!TryResolveOutputPortId(fromNodeId, (int)fromPortIndex, out var fromPortId) ||
            !TryResolveInputPortId(toNodeId, (int)toPortIndex, out var toPortId))
        {
            return;
        }

        if (_graph.Connections.Any(connection =>
                string.Equals(connection.FromNodeId, fromNodeId, StringComparison.Ordinal) &&
                string.Equals(connection.FromPortId, fromPortId, StringComparison.Ordinal) &&
                string.Equals(connection.ToNodeId, toNodeId, StringComparison.Ordinal) &&
                string.Equals(connection.ToPortId, toPortId, StringComparison.Ordinal)))
        {
            return;
        }

        _graph.Connections.Add(new BehaviorGraphConnectionDefinition
        {
            FromNodeId = fromNodeId,
            FromPortId = fromPortId,
            ToNodeId = toNodeId,
            ToPortId = toPortId
        });
        _graphEdit?.ConnectNode(fromNodeId, (int)fromPortIndex, toNodeId, (int)toPortIndex);
        NotifyGraphChanged();
    }

    private void OnDisconnectionRequest(StringName fromNode, long fromPortIndex, StringName toNode, long toPortIndex)
    {
        if (_graph == null)
        {
            return;
        }

        var fromNodeId = fromNode.ToString();
        var toNodeId = toNode.ToString();
        if (!TryResolveOutputPortId(fromNodeId, (int)fromPortIndex, out var fromPortId) ||
            !TryResolveInputPortId(toNodeId, (int)toPortIndex, out var toPortId))
        {
            return;
        }

        _graph.Connections.RemoveAll(connection =>
            string.Equals(connection.FromNodeId, fromNodeId, StringComparison.Ordinal) &&
            string.Equals(connection.FromPortId, fromPortId, StringComparison.Ordinal) &&
            string.Equals(connection.ToNodeId, toNodeId, StringComparison.Ordinal) &&
            string.Equals(connection.ToPortId, toPortId, StringComparison.Ordinal));
        _graphEdit?.DisconnectNode(fromNodeId, (int)fromPortIndex, toNodeId, (int)toPortIndex);
        NotifyGraphChanged();
    }

    private void ClearGraphEdit()
    {
        if (_graphEdit == null)
        {
            return;
        }

        if (_graphEdit.HasMethod("clear_connections"))
        {
            _graphEdit.Call("clear_connections");
        }
        else if (_graphEdit.HasMethod("clearConnections"))
        {
            _graphEdit.Call("clearConnections");
        }

        if (_nodeViews.Count == 0)
        {
            return;
        }

        foreach (var nodeView in _nodeViews.Values.ToList())
        {
            if (!GodotObject.IsInstanceValid(nodeView))
            {
                continue;
            }

            if (nodeView.GetParent() == _graphEdit)
            {
                _graphEdit.RemoveChild(nodeView);
            }

            nodeView.QueueFree();
        }
    }

    private Dictionary<string, Vector2> BuildLayout(BehaviorGraphDefinition graph)
    {
        var layout = new Dictionary<string, Vector2>(StringComparer.Ordinal);
        foreach (var node in graph.Nodes)
        {
            var keyX = $"{LayoutKeyPrefix}{node.NodeId}.x";
            var keyY = $"{LayoutKeyPrefix}{node.NodeId}.y";
            if (graph.Metadata.TryGetValue(keyX, out var xText) &&
                graph.Metadata.TryGetValue(keyY, out var yText) &&
                float.TryParse(xText, NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
                float.TryParse(yText, NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
            {
                layout[node.NodeId] = new Vector2(x, y);
            }
        }

        return layout;
    }

    private Vector2 GetSuggestedNodePosition(BehaviorGraphNodeDefinition node)
    {
        if (_graph == null)
        {
            return new Vector2(120f, 90f);
        }

        if (string.Equals(node.NodeId, _graph.EntryNodeId, StringComparison.Ordinal))
        {
            return new Vector2(80f, 140f);
        }

        var index = _graph.Nodes.FindIndex(candidate => string.Equals(candidate.NodeId, node.NodeId, StringComparison.Ordinal));
        if (index < 0)
        {
            index = _graph.Nodes.Count;
        }

        return new Vector2(400f + (index % 2) * 360f, 120f + (index / 2) * 220f);
    }

    private GraphNode BuildGraphNode(BehaviorGraphNodeDefinition node, Vector2 position)
    {
        var descriptor = GetDescriptor(node.NodeType);
        var inputs = descriptor?.Inputs?.ToList() ?? new List<BehaviorGraphPortDefinition>();
        var outputs = descriptor?.Outputs?.ToList() ?? new List<BehaviorGraphPortDefinition>();

        var graphNode = new GraphNode
        {
            Name = node.NodeId,
            Title = ResolveNodeTitle(node),
            PositionOffset = position,
            CustomMinimumSize = new Vector2(DefaultNodeWidth, 180f),
            Size = new Vector2(DefaultNodeWidth, 180f),
            Selectable = true,
            Draggable = true
        };

        var headerRow = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(DefaultNodeWidth - 28f, 28f)
        };
        headerRow.AddThemeConstantOverride("separation", 8);
        graphNode.AddChild(headerRow);

        var typeBadge = MakeNodeLabel(node.NodeType, true);
        typeBadge.Modulate = ResolveNodeAccent(node.NodeType);
        _nodeTypeBadgeLabels[node.NodeId] = typeBadge;
        headerRow.AddChild(typeBadge);

        var idBadge = MakeNodeLabel($"[{node.NodeId}]", true);
        idBadge.HorizontalAlignment = HorizontalAlignment.Right;
        idBadge.Modulate = new Color(0.72f, 0.76f, 0.83f, 0.92f);
        headerRow.AddChild(idBadge);

        var descriptionLabel = MakeNodeLabel(BuildDynamicDescription(node, _sourceModel, _previewContext), true);
        descriptionLabel.CustomMinimumSize = new Vector2(DefaultNodeWidth - 28f, 0f);
        descriptionLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        descriptionLabel.Modulate = new Color(0.90f, 0.92f, 0.97f, 0.96f);
        _nodeDescriptionLabels[node.NodeId] = descriptionLabel;
        graphNode.AddChild(descriptionLabel);

        var propertySummary = MakeNodeLabel(BuildDynamicPropertySummary(node, _sourceModel, _previewContext), true);
        propertySummary.CustomMinimumSize = new Vector2(DefaultNodeWidth - 28f, 0f);
        propertySummary.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        propertySummary.Modulate = new Color(0.72f, 0.78f, 0.88f, 0.96f);
        _nodePropertySummaryLabels[node.NodeId] = propertySummary;
        graphNode.AddChild(propertySummary);

        var slotMap = new NodeSlotMap();
        var slotCount = Math.Max(inputs.Count, outputs.Count);
        if (slotCount == 0)
        {
            slotCount = 1;
        }

        for (var slotIndex = 0; slotIndex < slotCount; slotIndex++)
        {
            var input = slotIndex < inputs.Count ? inputs[slotIndex] : null;
            var output = slotIndex < outputs.Count ? outputs[slotIndex] : null;
            var row = new HBoxContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(DefaultNodeWidth - 28f, 26f)
            };
            row.AddThemeConstantOverride("separation", 10);

            var leftLabel = new Label
            {
                Text = input?.DisplayName ?? string.Empty,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                AutowrapMode = TextServer.AutowrapMode.Off
            };
            leftLabel.Modulate = new Color(0.67f, 0.82f, 0.98f, 1f);

            var rightLabel = new Label
            {
                Text = output?.DisplayName ?? string.Empty,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                HorizontalAlignment = HorizontalAlignment.Right,
                AutowrapMode = TextServer.AutowrapMode.Off
            };
            rightLabel.Modulate = new Color(0.98f, 0.82f, 0.55f, 1f);

            row.AddChild(leftLabel);
            row.AddChild(rightLabel);
            graphNode.AddChild(row);

            var childIndex = graphNode.GetChildCount() - 1;
            graphNode.SetSlot(
                childIndex,
                input != null,
                0,
                new Color(0.43f, 0.74f, 0.98f, 1f),
                output != null,
                0,
                new Color(1f, 0.70f, 0.28f, 1f));

            if (input != null)
            {
                slotMap.InputIndexByPortId[input.PortId] = slotIndex;
                slotMap.InputPortIdByIndex[slotIndex] = input.PortId;
            }

            if (output != null)
            {
                slotMap.OutputIndexByPortId[output.PortId] = slotIndex;
                slotMap.OutputPortIdByIndex[slotIndex] = output.PortId;
            }
        }

        _slotMaps[node.NodeId] = slotMap;
        graphNode.GuiInput += inputEvent => OnGraphNodeInput(node.NodeId, inputEvent);
        return graphNode;
    }

    private void OnGraphNodeInput(string nodeId, InputEvent inputEvent)
    {
        switch (inputEvent)
        {
            case InputEventMouseButton button when button.ButtonIndex == MouseButton.Left && button.Pressed:
                SetSelectedNode(nodeId);
                break;
            case InputEventMouseButton button when button.ButtonIndex == MouseButton.Left && !button.Pressed:
                ExportLayout();
                break;
        }
    }

    private void SetSelectedNode(string? nodeId)
    {
        _selectedNodeId = nodeId;
        ApplySelectionHighlight();
        SelectedNodeChanged?.Invoke(nodeId);
    }

    private void SelectInitialNode()
    {
        if (_graph == null || _graph.Nodes.Count == 0)
        {
            SetSelectedNode(null);
            return;
        }

        if (!string.IsNullOrWhiteSpace(_selectedNodeId) &&
            _graph.Nodes.Any(node => string.Equals(node.NodeId, _selectedNodeId, StringComparison.Ordinal)))
        {
            SetSelectedNode(_selectedNodeId);
            return;
        }

        SetSelectedNode(string.IsNullOrWhiteSpace(_graph.EntryNodeId) ? _graph.Nodes[0].NodeId : _graph.EntryNodeId);
    }

    private void ApplySelectionHighlight()
    {
        foreach (var pair in _nodeViews)
        {
            if (!GodotObject.IsInstanceValid(pair.Value))
            {
                continue;
            }

            var selected = string.Equals(pair.Key, _selectedNodeId, StringComparison.Ordinal);
            pair.Value.Modulate = selected
                ? new Color(1f, 1f, 1f, 1f)
                : new Color(0.90f, 0.93f, 0.98f, 0.96f);
        }
    }

    private void ApplyConnections()
    {
        if (_graph == null || _graphEdit == null)
        {
            return;
        }

        foreach (var connection in _graph.Connections)
        {
            if (!TryResolveOutputPortIndex(connection.FromNodeId, connection.FromPortId, out var fromIndex) ||
                !TryResolveInputPortIndex(connection.ToNodeId, connection.ToPortId, out var toIndex))
            {
                continue;
            }

            _graphEdit.ConnectNode(connection.FromNodeId, fromIndex, connection.ToNodeId, toIndex);
        }
    }

    private void ApplyConnectionsSafe()
    {
        if (_graphEdit == null || !_graphEdit.IsInsideTree() || !_graphEdit.IsNodeReady())
        {
            QueueCanvasRebuild();
            return;
        }

        ApplyConnections();
    }

    private Rect2 GetBounds()
    {
        Rect2? rect = null;
        foreach (var nodeView in _nodeViews.Values)
        {
            if (!GodotObject.IsInstanceValid(nodeView))
            {
                continue;
            }

            var nodeRect = new Rect2(nodeView.PositionOffset, nodeView.Size);
            rect = rect == null ? nodeRect : rect.Value.Merge(nodeRect);
        }

        return rect ?? new Rect2(new Vector2(0f, 0f), new Vector2(1f, 1f));
    }

    private bool TryResolveOutputPortIndex(string nodeId, string portId, out int index)
    {
        index = 0;
        if (!_slotMaps.TryGetValue(nodeId, out var map))
        {
            return false;
        }

        if (map.OutputIndexByPortId.TryGetValue(portId, out index))
        {
            return true;
        }

        return int.TryParse(portId, out index) && map.OutputPortIdByIndex.ContainsKey(index);
    }

    private bool TryResolveInputPortIndex(string nodeId, string portId, out int index)
    {
        index = 0;
        if (!_slotMaps.TryGetValue(nodeId, out var map))
        {
            return false;
        }

        if (map.InputIndexByPortId.TryGetValue(portId, out index))
        {
            return true;
        }

        return int.TryParse(portId, out index) && map.InputPortIdByIndex.ContainsKey(index);
    }

    private bool TryResolveOutputPortId(string nodeId, int index, out string portId)
    {
        portId = string.Empty;
        if (_slotMaps.TryGetValue(nodeId, out var map) &&
            map.OutputPortIdByIndex.TryGetValue(index, out var resolvedPortId) &&
            !string.IsNullOrWhiteSpace(resolvedPortId))
        {
            portId = resolvedPortId;
            return true;
        }

        return false;
    }

    private bool TryResolveInputPortId(string nodeId, int index, out string portId)
    {
        portId = string.Empty;
        if (_slotMaps.TryGetValue(nodeId, out var map) &&
            map.InputPortIdByIndex.TryGetValue(index, out var resolvedPortId) &&
            !string.IsNullOrWhiteSpace(resolvedPortId))
        {
            portId = resolvedPortId;
            return true;
        }

        return false;
    }

    private static string ResolveNodeTitle(BehaviorGraphNodeDefinition node)
    {
        var baseTitle = string.IsNullOrWhiteSpace(node.DisplayName) ? node.NodeType : node.DisplayName;
        var suffix = ResolveInternalNodeTitleSuffix(node);
        return string.IsNullOrWhiteSpace(suffix) ? baseTitle : $"{baseTitle} [{suffix}]";
    }

    private static string ResolveNodeTitle(BehaviorGraphNodeDefinitionDescriptor definition)
    {
        return string.IsNullOrWhiteSpace(definition.DisplayName) ? definition.NodeType : definition.DisplayName;
    }

    private static string ResolveInternalNodeTitleSuffix(BehaviorGraphNodeDefinition node)
    {
        var nodeType = (node.NodeType ?? string.Empty).Trim().ToLowerInvariant();
        var rawSuffix = nodeType switch
        {
            "value.compare" => GetProperty(node, "result_key", string.Empty),
            "flow.branch" => GetProperty(node, "condition_key", string.Empty),
            "value.set" or "value.add" or "value.multiply" => GetProperty(node, "key", string.Empty),
            "enchantment.set_status" => GetProperty(node, "status", string.Empty),
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(rawSuffix))
        {
            return string.Empty;
        }

        return rawSuffix.Length <= 24 ? rawSuffix : $"{rawSuffix[..21]}...";
    }

    private static string BuildDescription(BehaviorGraphNodeDefinition node, AbstractModel? sourceModel, DynamicPreviewContext? previewContext)
    {
        var dynamicDescription = node.NodeType switch
        {
            "flow.entry" => Dual("Graph 入口节点。", "Entry point for this graph."),
            "flow.exit" => Dual("Graph 结束节点。", "Exit point for this graph."),
            "combat.damage" => Dual($"造成 {GetProperty(node, "amount", "0")} 点伤害，目标 {DescribeTarget(GetProperty(node, "target", "current_target"))}。", $"Deal {GetProperty(node, "amount", "0")} damage to {DescribeTarget(GetProperty(node, "target", "current_target"))}."),
            "combat.gain_block" => Dual($"获得 {GetProperty(node, "amount", "0")} 点格挡，目标 {DescribeTarget(GetProperty(node, "target", "self"))}。", $"Gain {GetProperty(node, "amount", "0")} block for {DescribeTarget(GetProperty(node, "target", "self"))}."),
            "combat.heal" => Dual($"恢复 {GetProperty(node, "amount", "0")} 点生命，目标 {DescribeTarget(GetProperty(node, "target", "self"))}。", $"Heal {GetProperty(node, "amount", "0")} HP for {DescribeTarget(GetProperty(node, "target", "self"))}."),
            "combat.draw_cards" => Dual($"抽取 {GetProperty(node, "amount", "1")} 张牌。", $"Draw {GetProperty(node, "amount", "1")} cards."),
            "player.gain_energy" => Dual($"获得 {GetProperty(node, "amount", "1")} 点能量。", $"Gain {GetProperty(node, "amount", "1")} energy."),
            "player.gain_gold" => Dual($"获得 {GetProperty(node, "amount", "1")} 金币。", $"Gain {GetProperty(node, "amount", "1")} gold."),
            "player.gain_stars" => Dual($"获得 {GetProperty(node, "amount", "1")} 星数。", $"Gain {GetProperty(node, "amount", "1")} stars."),
            "combat.apply_power" => Dual($"施加 {GetProperty(node, "power_id", "power")} x{GetProperty(node, "amount", "1")}，目标 {DescribeTarget(GetProperty(node, "target", "current_target"))}。", $"Apply {GetProperty(node, "power_id", "power")} x{GetProperty(node, "amount", "1")} to {DescribeTarget(GetProperty(node, "target", "current_target"))}."),
            "orb.channel" => GraphDescriptionSupport.BuildChannelOrbDescription(GetProperty(node, "orb_id", string.Empty)),
            _ => string.Empty
        };

        if (!string.IsNullOrWhiteSpace(dynamicDescription))
        {
            return dynamicDescription;
        }

        if (!string.IsNullOrWhiteSpace(node.Description))
        {
            return node.Description;
        }

        return node.NodeType;
    }

    private static string BuildPropertySummary(BehaviorGraphNodeDefinition node, AbstractModel? sourceModel, DynamicPreviewContext? previewContext)
    {
        var segments = new List<string>();
        if (node.DynamicValues.TryGetValue("amount", out var amountDefinition))
        {
            var preview = DynamicValueEvaluator.EvaluatePreview(amountDefinition, sourceModel, previewContext);
            segments.Add($"{ModStudioFieldDisplayNames.Get("amount")}: {preview.PreviewText}");
            segments.Add($"{ModStudioLocalizationCatalog.T("graph.value_source")}: {ModStudioFieldDisplayNames.FormatPropertyValue("dynamic_source_kind", amountDefinition.SourceKind.ToString())}");
        }

        if (segments.Count > 0)
        {
            segments.AddRange(node.Properties
                .Where(pair => !string.Equals(pair.Key, "amount", StringComparison.OrdinalIgnoreCase))
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .Select(pair => $"{ModStudioFieldDisplayNames.Get(pair.Key)}: {ModStudioFieldDisplayNames.FormatGraphPropertyValue(pair.Key, pair.Value)}"));
            return string.Join("  |  ", segments);
        }
        if (node.Properties.Count == 0)
        {
            return Dual("无额外属性", "No extra properties");
        }

        return string.Join("  |  ", node.Properties
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .Select(pair => $"{ModStudioFieldDisplayNames.Get(pair.Key)}: {ModStudioFieldDisplayNames.FormatGraphPropertyValue(pair.Key, pair.Value)}"));
    }

    private static string BuildDynamicDescription(BehaviorGraphNodeDefinition node, AbstractModel? sourceModel, DynamicPreviewContext? previewContext)
    {
        string AmountText(string defaultValue)
        {
            var preview = DynamicValueEvaluator.EvaluatePreview(
                node,
                "amount",
                sourceModel,
                previewContext,
                decimal.TryParse(defaultValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0m);

            return string.IsNullOrWhiteSpace(preview.PreviewText) ? defaultValue : preview.PreviewText;
        }

        var description = node.NodeType switch
        {
            "flow.entry" => Dual("Graph 入口节点。", "Entry point for this graph."),
            "flow.exit" => Dual("Graph 结束节点。", "Exit point for this graph."),
            "combat.damage" => Dual($"造成 {AmountText("0")} 点伤害，目标 {DescribeTarget(GetProperty(node, "target", "current_target"))}。", $"Deal {AmountText("0")} damage to {DescribeTarget(GetProperty(node, "target", "current_target"))}."),
            "combat.gain_block" => Dual($"获得 {AmountText("0")} 点格挡，目标 {DescribeTarget(GetProperty(node, "target", "self"))}。", $"Gain {AmountText("0")} block for {DescribeTarget(GetProperty(node, "target", "self"))}."),
            "combat.heal" => Dual($"恢复 {AmountText("0")} 点生命，目标 {DescribeTarget(GetProperty(node, "target", "self"))}。", $"Heal {AmountText("0")} HP for {DescribeTarget(GetProperty(node, "target", "self"))}."),
            "combat.draw_cards" => Dual($"抽取 {AmountText("1")} 张牌。", $"Draw {AmountText("1")} cards."),
            "player.gain_energy" => Dual($"获得 {AmountText("1")} 点能量。", $"Gain {AmountText("1")} energy."),
            "player.gain_gold" => Dual($"获得 {AmountText("1")} 金币。", $"Gain {AmountText("1")} gold."),
            "player.gain_stars" => Dual($"获得 {AmountText("1")} 星数。", $"Gain {AmountText("1")} stars."),
            "combat.apply_power" => Dual($"施加 {GetProperty(node, "power_id", "power")} x{AmountText("1")}，目标 {DescribeTarget(GetProperty(node, "target", "current_target"))}。", $"Apply {GetProperty(node, "power_id", "power")} x{AmountText("1")} to {DescribeTarget(GetProperty(node, "target", "current_target"))}."),
            _ => string.Empty
        };

        if (!string.IsNullOrWhiteSpace(description))
        {
            return description;
        }

        var internalDescription = BuildInternalStateNodeDescription(node);
        if (!string.IsNullOrWhiteSpace(internalDescription))
        {
            return internalDescription;
        }

        if (!string.IsNullOrWhiteSpace(node.Description))
        {
            return node.Description;
        }

        return node.NodeType;
    }

    private static string BuildPropertyEntry(BehaviorGraphNodeDefinition node, string key, string? value, AbstractModel? sourceModel, DynamicPreviewContext? previewContext)
    {
        if (string.Equals(key, "amount", StringComparison.OrdinalIgnoreCase))
        {
            var preview = DynamicValueEvaluator.EvaluatePreview(node, key, sourceModel, previewContext, 0m);
            return $"{ModStudioFieldDisplayNames.Get(key)}: {GetPreviewText(preview, value)}";
        }

        return $"{ModStudioFieldDisplayNames.Get(key)}: {ModStudioFieldDisplayNames.FormatGraphPropertyValue(key, value)}";
    }

    private static string BuildInternalStateNodeDescription(BehaviorGraphNodeDefinition node)
    {
        return (node.NodeType ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "value.compare" => Dual(
                $"比较 {ModStudioFieldDisplayNames.FormatGraphPropertyValue("left", GetProperty(node, "left", string.Empty))} {ModStudioFieldDisplayNames.FormatGraphPropertyValue("operator", GetProperty(node, "operator", "eq"))} {ModStudioFieldDisplayNames.FormatGraphPropertyValue("right", GetProperty(node, "right", string.Empty))}，并把结果写入 {ModStudioFieldDisplayNames.FormatGraphPropertyValue("result_key", GetProperty(node, "result_key", "last_compare"))}。",
                $"Compare {ModStudioFieldDisplayNames.FormatGraphPropertyValue("left", GetProperty(node, "left", string.Empty))} {ModStudioFieldDisplayNames.FormatGraphPropertyValue("operator", GetProperty(node, "operator", "eq"))} {ModStudioFieldDisplayNames.FormatGraphPropertyValue("right", GetProperty(node, "right", string.Empty))}, then store the result in {ModStudioFieldDisplayNames.FormatGraphPropertyValue("result_key", GetProperty(node, "result_key", "last_compare"))}."),
            "flow.branch" when TryGetNonEmptyProperty(node, "condition", out var condition) => Dual(
                $"判断 {ModStudioFieldDisplayNames.FormatGraphPropertyValue("condition", condition)}，为真走 True，为假走 False。",
                $"Evaluate {ModStudioFieldDisplayNames.FormatGraphPropertyValue("condition", condition)}. Use the True port when it passes, otherwise use False."),
            "flow.branch" when TryGetNonEmptyProperty(node, "condition_key", out var conditionKey) => Dual(
                $"读取 {ModStudioFieldDisplayNames.FormatGraphPropertyValue("condition_key", conditionKey)} 的布尔结果，为真走 True，为假走 False。",
                $"Read the boolean stored in {ModStudioFieldDisplayNames.FormatGraphPropertyValue("condition_key", conditionKey)}. Use the True port when it passes, otherwise use False."),
            "flow.branch" => Dual("根据条件进入 True 或 False 分支。", "Branch into the True or False flow path based on a condition."),
            "value.set" => Dual(
                $"把 {ModStudioFieldDisplayNames.FormatGraphPropertyValue("key", GetProperty(node, "key", string.Empty))} 设为 {ModStudioFieldDisplayNames.FormatGraphPropertyValue("value", GetProperty(node, "value", string.Empty))}。",
                $"Set {ModStudioFieldDisplayNames.FormatGraphPropertyValue("key", GetProperty(node, "key", string.Empty))} to {ModStudioFieldDisplayNames.FormatGraphPropertyValue("value", GetProperty(node, "value", string.Empty))}."),
            "value.add" => Dual(
                $"给 {ModStudioFieldDisplayNames.FormatGraphPropertyValue("key", GetProperty(node, "key", string.Empty))} 增加 {ModStudioFieldDisplayNames.FormatGraphPropertyValue("value", GetProperty(node, "delta", "0"))}。",
                $"Add {ModStudioFieldDisplayNames.FormatGraphPropertyValue("value", GetProperty(node, "delta", "0"))} to {ModStudioFieldDisplayNames.FormatGraphPropertyValue("key", GetProperty(node, "key", string.Empty))}."),
            "value.multiply" => Dual(
                $"把 {ModStudioFieldDisplayNames.FormatGraphPropertyValue("key", GetProperty(node, "key", string.Empty))} 乘以 {ModStudioFieldDisplayNames.FormatGraphPropertyValue("value", GetProperty(node, "factor", "1"))}。",
                $"Multiply {ModStudioFieldDisplayNames.FormatGraphPropertyValue("key", GetProperty(node, "key", string.Empty))} by {ModStudioFieldDisplayNames.FormatGraphPropertyValue("value", GetProperty(node, "factor", "1"))}."),
            "enchantment.set_status" => Dual(
                $"把当前附魔状态设为 {ModStudioFieldDisplayNames.FormatGraphPropertyValue("status", GetProperty(node, "status", "Disabled"))}。",
                $"Set the current enchantment status to {ModStudioFieldDisplayNames.FormatGraphPropertyValue("status", GetProperty(node, "status", "Disabled"))}."),
            _ => string.Empty
        };
    }

    private static string BuildInternalStateNodeSummary(BehaviorGraphNodeDefinition node, AbstractModel? sourceModel, DynamicPreviewContext? previewContext)
    {
        IEnumerable<string> entries = (node.NodeType ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "value.compare" => BuildEntries("left", "operator", "right", "result_key"),
            "flow.branch" when TryGetNonEmptyProperty(node, "condition", out _) => BuildEntries("condition"),
            "flow.branch" => BuildEntries("condition_key"),
            "value.set" => BuildEntries("key", "value"),
            "value.add" => BuildEntries("key", "delta"),
            "value.multiply" => BuildEntries("key", "factor"),
            "enchantment.set_status" => BuildEntries("status"),
            _ => Array.Empty<string>()
        };

        var visibleEntries = entries.Where(entry => !string.IsNullOrWhiteSpace(entry)).ToList();
        return visibleEntries.Count == 0
            ? string.Empty
            : string.Join("  |  ", visibleEntries);

        IEnumerable<string> BuildEntries(params string[] keys)
        {
            foreach (var key in keys)
            {
                if (!node.Properties.TryGetValue(key, out var value))
                {
                    continue;
                }

                yield return BuildPropertyEntry(node, key, value, sourceModel, previewContext);
            }
        }
    }

    private static bool TryGetNonEmptyProperty(BehaviorGraphNodeDefinition node, string key, out string value)
    {
        if (node.Properties.TryGetValue(key, out var rawValue) && !string.IsNullOrWhiteSpace(rawValue))
        {
            value = rawValue;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static string BuildDynamicPropertyEntry(string key, DynamicValueDefinition definition, AbstractModel? sourceModel, DynamicPreviewContext? previewContext)
    {
        var preview = DynamicValueEvaluator.EvaluatePreview(definition, sourceModel, previewContext, 0m);
        return $"{ModStudioFieldDisplayNames.Get(key)}: {GetPreviewText(preview, definition.TemplateText)}";
    }

    private static string GetPreviewText(DynamicValuePreviewResult preview, string? fallback)
    {
        if (!string.IsNullOrWhiteSpace(preview.SummaryText))
        {
            return preview.SummaryText;
        }

        if (!string.IsNullOrWhiteSpace(preview.PreviewText))
        {
            return preview.PreviewText;
        }

        return string.IsNullOrWhiteSpace(fallback) ? string.Empty : fallback;
    }

    private static string BuildDynamicPropertySummary(BehaviorGraphNodeDefinition node, AbstractModel? sourceModel, DynamicPreviewContext? previewContext)
    {
        var internalSummary = BuildInternalStateNodeSummary(node, sourceModel, previewContext);
        if (!string.IsNullOrWhiteSpace(internalSummary))
        {
            return internalSummary;
        }

        var entries = new List<string>();

        foreach (var pair in node.Properties.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            entries.Add(BuildPropertyEntry(node, pair.Key, pair.Value, sourceModel, previewContext));
        }

        foreach (var pair in node.DynamicValues.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (node.Properties.ContainsKey(pair.Key))
            {
                continue;
            }

            entries.Add(BuildDynamicPropertyEntry(pair.Key, pair.Value, sourceModel, previewContext));
        }

        if (entries.Count == 0)
        {
            return Dual("无额外属性", "No extra properties");
        }

        return string.Join("  |  ", entries.Take(4));
    }

    private static string GetProperty(BehaviorGraphNodeDefinition node, string key, string defaultValue)
    {
        return node.Properties.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : defaultValue;
    }

    private static string DescribeTarget(string value)
    {
        return value switch
        {
            "self" => Dual("自身", "self"),
            "current_target" => Dual("当前目标", "the current target"),
            "all_enemies" => Dual("所有敌人", "all enemies"),
            "all_allies" => Dual("所有友军", "all allies"),
            "all_targets" => Dual("所有目标", "all targets"),
            _ => value
        };
    }

    private static Label MakeNodeLabel(string text, bool expand = false)
    {
        return new Label
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.Off,
            ClipText = true,
            SizeFlagsHorizontal = expand ? SizeFlags.ExpandFill : SizeFlags.Fill
        };
    }

    private static Color ResolveNodeAccent(string nodeType)
    {
        if (nodeType.StartsWith("flow.", StringComparison.OrdinalIgnoreCase))
        {
            return new Color(0.62f, 0.78f, 1f, 1f);
        }

        if (nodeType.StartsWith("combat.", StringComparison.OrdinalIgnoreCase))
        {
            return new Color(1f, 0.74f, 0.45f, 1f);
        }

        if (nodeType.StartsWith("event.", StringComparison.OrdinalIgnoreCase))
        {
            return new Color(0.70f, 0.94f, 0.62f, 1f);
        }

        if (nodeType.StartsWith("data.", StringComparison.OrdinalIgnoreCase))
        {
            return new Color(0.84f, 0.73f, 1f, 1f);
        }

        return new Color(0.86f, 0.89f, 0.95f, 1f);
    }

    private void NotifyGraphChanged()
    {
        if (_graph == null)
        {
            return;
        }

        GraphChanged?.Invoke(_graph);
        RefreshStatus();
    }

    private sealed class NodeSlotMap
    {
        public Dictionary<string, int> InputIndexByPortId { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, int> OutputIndexByPortId { get; } = new(StringComparer.Ordinal);
        public Dictionary<int, string> InputPortIdByIndex { get; } = new();
        public Dictionary<int, string> OutputPortIdByIndex { get; } = new();
    }
}
