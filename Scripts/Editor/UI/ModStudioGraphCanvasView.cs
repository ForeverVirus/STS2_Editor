using System.Globalization;
using Godot;
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

    private GraphEdit? _graphEdit;
    private Label? _hintLabel;
    private PopupPanel? _nodePalettePopup;
    private LineEdit? _nodePaletteSearchEdit;
    private ItemList? _nodePaletteList;
    private RichTextLabel? _nodePaletteDescriptionLabel;

    private BehaviorGraphDefinition? _graph;
    private BehaviorGraphRegistry? _registry;
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

    public void BindGraph(BehaviorGraphDefinition graph, BehaviorGraphRegistry registry)
    {
        _graph = graph;
        _registry = registry;
        _selectedNodeId = null;
        _pendingNodeGraphPosition = new Vector2(140f, 100f);
        if (_isReady)
        {
            RefreshNodePalette();
            QueueCanvasRebuild();
        }
    }

    public void ClearBinding()
    {
        _graph = null;
        _registry = null;
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
            descriptionLabel.Text = BuildDescription(node);
        }

        if (_nodePropertySummaryLabels.TryGetValue(node.NodeId, out var summaryLabel))
        {
            summaryLabel.Text = BuildPropertySummary(node);
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
        return BuildDescription(node);
    }

    public void RefreshStatus()
    {
        if (_hintLabel == null)
        {
            return;
        }

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
        if (_graph == null)
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
        return (_registry?.Definitions ?? Array.Empty<BehaviorGraphNodeDefinitionDescriptor>())
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
        if (_graphEdit == null || _nodeViews.Count == 0)
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

        var descriptionLabel = MakeNodeLabel(BuildDescription(node), true);
        descriptionLabel.CustomMinimumSize = new Vector2(DefaultNodeWidth - 28f, 0f);
        descriptionLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        descriptionLabel.Modulate = new Color(0.90f, 0.92f, 0.97f, 0.96f);
        _nodeDescriptionLabels[node.NodeId] = descriptionLabel;
        graphNode.AddChild(descriptionLabel);

        var propertySummary = MakeNodeLabel(BuildPropertySummary(node), true);
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
        return string.IsNullOrWhiteSpace(node.DisplayName) ? node.NodeType : node.DisplayName;
    }

    private static string ResolveNodeTitle(BehaviorGraphNodeDefinitionDescriptor definition)
    {
        return string.IsNullOrWhiteSpace(definition.DisplayName) ? definition.NodeType : definition.DisplayName;
    }

    private static string BuildDescription(BehaviorGraphNodeDefinition node)
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

    private static string BuildPropertySummary(BehaviorGraphNodeDefinition node)
    {
        if (node.Properties.Count == 0)
        {
            return Dual("无额外属性", "No extra properties");
        }

        return string.Join("  •  ", node.Properties
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .Select(pair => $"{ModStudioFieldDisplayNames.Get(pair.Key)}: {ModStudioFieldDisplayNames.FormatGraphPropertyValue(pair.Key, pair.Value)}"));
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
