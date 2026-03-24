using Godot;
using MegaCrit.Sts2.Core.Models;
using STS2_Editor.Scripts.Editor.Graph;
using static STS2_Editor.Scripts.Editor.UI.ModStudioUiFactory;

namespace STS2_Editor.Scripts.Editor.UI;

internal sealed partial class ModStudioGraphEditor : MarginContainer
{
    private Button? _importButton;
    private Button? _addNodeButton;
    private Button? _deleteNodeButton;
    private Button? _autoLayoutButton;
    private Button? _fitButton;
    private Button? _saveButton;
    private Button? _validateButton;
    private Label? _statusLabel;
    private ModStudioGraphCanvasView? _canvasView;

    public ModStudioGraphCanvasView CanvasView
    {
        get
        {
            EnsureBuilt();
            return _canvasView!;
        }
    }

    public event Action? ImportRequested;
    public event Action? SaveRequested;
    public event Action? ValidateRequested;

    public override void _Ready()
    {
    }

    public void EnsureBuilt()
    {
        BuildUi();
        RefreshTexts();
    }

    public void BindGraph(BehaviorGraphDefinition graph, BehaviorGraphRegistry registry, AbstractModel? sourceModel = null, DynamicPreviewContext? previewContext = null)
    {
        CanvasView.BindGraph(graph, registry, sourceModel, previewContext);
        Callable.From(() => CanvasView.ZoomToFit()).CallDeferred();
        RefreshStatus();
    }

    public void UpdatePreviewContext(AbstractModel? sourceModel = null, DynamicPreviewContext? previewContext = null)
    {
        CanvasView.UpdatePreviewContext(sourceModel, previewContext);
        RefreshStatus();
    }

    public void ClearGraph()
    {
        CanvasView.ClearBinding();
        RefreshStatus();
    }

    public void RefreshTexts()
    {
        if (_importButton != null) _importButton.Text = Dual("导入", "Import");
        if (_addNodeButton != null) _addNodeButton.Text = Dual("添加节点", "Add Node");
        if (_deleteNodeButton != null) _deleteNodeButton.Text = Dual("删除节点", "Delete Node");
        if (_autoLayoutButton != null) _autoLayoutButton.Text = Dual("自动布局", "Auto Layout");
        if (_fitButton != null) _fitButton.Text = Dual("缩放适配", "Zoom To Fit");
        if (_saveButton != null) _saveButton.Text = Dual("保存 Graph", "Save Graph");
        if (_validateButton != null) _validateButton.Text = Dual("校验", "Validate");
        RefreshStatus();
    }

    private void BuildUi()
    {
        if (GetChildCount() > 0)
        {
            return;
        }

        AddThemeConstantOverride("margin_left", 8);
        AddThemeConstantOverride("margin_top", 8);
        AddThemeConstantOverride("margin_right", 8);
        AddThemeConstantOverride("margin_bottom", 8);

        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", 8);
        AddChild(root);

        var toolbar = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        toolbar.AddThemeConstantOverride("separation", 6);
        root.AddChild(toolbar);

        _importButton = MakeButton(string.Empty, () => ImportRequested?.Invoke());
        _addNodeButton = MakeButton(string.Empty, () => CanvasView.ShowNodePaletteAtCanvasCenter());
        _deleteNodeButton = MakeButton(string.Empty, () => CanvasView.DeleteSelectedNode());
        _autoLayoutButton = MakeButton(string.Empty, () => CanvasView.AutoLayout());
        _fitButton = MakeButton(string.Empty, () => CanvasView.ZoomToFit());
        _saveButton = MakeButton(string.Empty, () => SaveRequested?.Invoke());
        _validateButton = MakeButton(string.Empty, () => ValidateRequested?.Invoke());

        toolbar.AddChild(_importButton);
        toolbar.AddChild(_addNodeButton);
        toolbar.AddChild(_deleteNodeButton);
        toolbar.AddChild(_autoLayoutButton);
        toolbar.AddChild(_fitButton);
        toolbar.AddChild(_saveButton);
        toolbar.AddChild(_validateButton);

        _statusLabel = MakeLabel(string.Empty, true);
        _statusLabel.HorizontalAlignment = HorizontalAlignment.Right;
        toolbar.AddChild(_statusLabel);

        _canvasView = new ModStudioGraphCanvasView
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        root.AddChild(_canvasView);

        _canvasView.GraphChanged += _ => RefreshStatus();
        _canvasView.SelectedNodeChanged += _ => RefreshStatus();
    }

    private void RefreshStatus()
    {
        if (_statusLabel == null)
        {
            return;
        }

        var graph = _canvasView?.BoundGraph;
        if (graph == null)
        {
            _statusLabel.Text = Dual("右键空白处添加节点", "Right-click the canvas to add nodes");
            return;
        }

        var selected = _canvasView?.SelectedNodeId;
        _statusLabel.Text = string.IsNullOrWhiteSpace(selected)
            ? Dual($"节点 {graph.Nodes.Count} · 连线 {graph.Connections.Count}", $"Nodes {graph.Nodes.Count} · Connections {graph.Connections.Count}")
            : Dual($"已选节点 {selected} · 节点 {graph.Nodes.Count}", $"Selected {selected} · Nodes {graph.Nodes.Count}");
    }
}
