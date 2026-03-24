using Godot;
using MegaCrit.Sts2.Core.Models;
using STS2_Editor.Scripts.Editor.Graph;
using static STS2_Editor.Scripts.Editor.UI.ModStudioUiFactory;

namespace STS2_Editor.Scripts.Editor.UI;

internal sealed partial class ModStudioCenterEditor : PanelContainer
{
    private TabContainer? _tabs;
    private ModStudioBasicEditor? _basicEditor;
    private ModStudioAssetEditor? _assetEditor;
    private ModStudioGraphEditor? _graphEditor;

    public ModStudioBasicEditor BasicEditor
    {
        get
        {
            EnsureBuilt();
            return _basicEditor!;
        }
    }

    public ModStudioAssetEditor AssetEditor
    {
        get
        {
            EnsureBuilt();
            return _assetEditor!;
        }
    }

    public ModStudioGraphEditor GraphEditor
    {
        get
        {
            EnsureBuilt();
            return _graphEditor!;
        }
    }

    public TabContainer Tabs
    {
        get
        {
            EnsureBuilt();
            return _tabs!;
        }
    }

    public override void _Ready()
    {
        EnsureBuilt();
    }

    public void EnsureBuilt()
    {
        BuildUi();
        _basicEditor?.EnsureBuilt();
        RefreshTexts();
    }

    public void EnsureAssetsBuilt()
    {
        EnsureBuilt();
        _assetEditor?.EnsureBuilt();
    }

    public void EnsureGraphBuilt()
    {
        EnsureBuilt();
        _graphEditor?.EnsureBuilt();
    }

    public void RefreshTexts()
    {
        if (_tabs != null)
        {
            _tabs.SetTabTitle(0, Dual("基础信息", "Basic"));
            _tabs.SetTabTitle(1, Dual("资源", "Assets"));
            _tabs.SetTabTitle(2, Dual("Graph", "Graph"));
        }

        _basicEditor?.RefreshTexts();
        _assetEditor?.RefreshTexts();
        _graphEditor?.RefreshTexts();
    }

    public void BindGraph(BehaviorGraphDefinition graph, BehaviorGraphRegistry registry, AbstractModel? sourceModel = null, DynamicPreviewContext? previewContext = null)
    {
        _graphEditor?.BindGraph(graph, registry, sourceModel, previewContext);
    }

    public void UpdateGraphPreviewContext(AbstractModel? sourceModel = null, DynamicPreviewContext? previewContext = null)
    {
        _graphEditor?.UpdatePreviewContext(sourceModel, previewContext);
    }

    public void ClearGraph()
    {
        _graphEditor?.ClearGraph();
    }

    public void SetFeatureAvailability(bool showAssets, bool showGraph)
    {
        EnsureBuilt();
        if (_tabs == null)
        {
            return;
        }

        _tabs.SetTabHidden(1, !showAssets);
        _tabs.SetTabHidden(2, !showGraph);

        if ((_tabs.CurrentTab == 1 && !showAssets) || (_tabs.CurrentTab == 2 && !showGraph))
        {
            _tabs.CurrentTab = 0;
        }
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

        _tabs = new TabContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        AddChild(_tabs);

        _basicEditor = new ModStudioBasicEditor { Name = "BasicTab", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _assetEditor = new ModStudioAssetEditor { Name = "AssetsTab", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _graphEditor = new ModStudioGraphEditor { Name = "GraphTab", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };

        _tabs.AddChild(_basicEditor);
        _tabs.AddChild(_assetEditor);
        _tabs.AddChild(_graphEditor);
    }
}
