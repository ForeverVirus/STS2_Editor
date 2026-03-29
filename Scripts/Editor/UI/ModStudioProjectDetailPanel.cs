using Godot;
using MegaCrit.Sts2.Core.ValueProps;
using System.IO;
using System;
using STS2_Editor.Scripts.Editor.Core.Utilities;
using STS2_Editor.Scripts.Editor.Core.Models;
using STS2_Editor.Scripts.Editor.Graph;
using static STS2_Editor.Scripts.Editor.UI.ModStudioUiFactory;

namespace STS2_Editor.Scripts.Editor.UI;

internal sealed partial class ModStudioProjectDetailPanel : PanelContainer
{
    private readonly List<AssetRef> _importedAssets = new();
    private readonly List<string> _runtimeAssets = new();
    private readonly Dictionary<string, Control> _propertyEditors = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, int>> _choiceIndexCache = new(StringComparer.Ordinal);
    private string _propertyEditorSignature = string.Empty;
    private bool _propertyEditorsInvalidated = true;
    private BehaviorGraphDefinition? _inspectorGraph;
    private BehaviorGraphNodeDefinition? _inspectorNode;
    private IReadOnlyList<PropertyChoice>? _cachedEventPageChoices;
    private IReadOnlyList<PropertyChoice>? _cachedEventOptionChoices;

    private Label? _titleLabel;
    private RichTextLabel? _basicReadOnlyLabel;
    private TabContainer? _tabs;

    private VBoxContainer? _runtimeAssetPage;
    private VBoxContainer? _importedAssetPage;
    private LineEdit? _runtimeSearchEdit;
    private ItemList? _runtimeAssetList;
    private LineEdit? _importedSearchEdit;
    private ItemList? _importedAssetList;
    private Button? _runtimeAssetsTabButton;
    private Button? _importedAssetsTabButton;

    private RichTextLabel? _graphInfoLabel;
    private CheckBox? _graphEnabledCheck;
    private Label? _graphIdLabel;
    private LineEdit? _graphIdEdit;
    private Label? _graphNameLabel;
    private LineEdit? _graphNameEdit;
    private Label? _graphDescriptionLabel;
    private TextEdit? _graphDescriptionEdit;
    private VBoxContainer? _previewContextRoot;
    private Label? _previewContextLabel;
    private CheckBox? _previewUpgradedCheck;
    private Label? _previewTargetLabel;
    private OptionButton? _previewTargetSelector;
    private Label? _previewCurrentBlockLabel;
    private SpinBox? _previewCurrentBlockSpin;
    private Label? _previewCurrentStarsLabel;
    private SpinBox? _previewCurrentStarsSpin;
    private Label? _previewCurrentEnergyLabel;
    private SpinBox? _previewCurrentEnergySpin;
    private Label? _previewHandCountLabel;
    private SpinBox? _previewHandCountSpin;
    private Label? _previewDrawPileLabel;
    private SpinBox? _previewDrawPileSpin;
    private Label? _previewDiscardPileLabel;
    private SpinBox? _previewDiscardPileSpin;
    private Label? _previewExhaustPileLabel;
    private SpinBox? _previewExhaustPileSpin;
    private Label? _previewMissingHpLabel;
    private SpinBox? _previewMissingHpSpin;
    private Label? _selectedNodeTypeLabel;
    private Label? _selectedNodeIdLabel;
    private Label? _selectedNodeDisplayNameLabel;
    private LineEdit? _selectedNodeDisplayNameEdit;
    private Label? _selectedNodeDescriptionLabel;
    private TextEdit? _selectedNodeDescriptionEdit;
    private RichTextLabel? _selectedNodeDynamicSummaryLabel;
    private Label? _selectedNodePropertiesLabel;
    private VBoxContainer? _selectedNodePropertyHost;
    private bool _suppressPreviewContextChanged;

    public LineEdit RuntimeAssetSearchEdit
    {
        get
        {
            EnsureBuilt();
            return _runtimeSearchEdit!;
        }
    }

    public ItemList RuntimeAssetList
    {
        get
        {
            EnsureBuilt();
            return _runtimeAssetList!;
        }
    }

    public LineEdit ImportedAssetSearchEdit
    {
        get
        {
            EnsureBuilt();
            return _importedSearchEdit!;
        }
    }

    public ItemList ImportedAssetList
    {
        get
        {
            EnsureBuilt();
            return _importedAssetList!;
        }
    }

    public CheckBox GraphEnabledCheck
    {
        get
        {
            EnsureBuilt();
            return _graphEnabledCheck!;
        }
    }

    public LineEdit GraphIdEdit
    {
        get
        {
            EnsureBuilt();
            return _graphIdEdit!;
        }
    }

    public LineEdit GraphNameEdit
    {
        get
        {
            EnsureBuilt();
            return _graphNameEdit!;
        }
    }

    public TextEdit GraphDescriptionEdit
    {
        get
        {
            EnsureBuilt();
            return _graphDescriptionEdit!;
        }
    }

    public Label SelectedNodeTypeLabel
    {
        get
        {
            EnsureBuilt();
            return _selectedNodeTypeLabel!;
        }
    }

    public Label SelectedNodeIdLabel
    {
        get
        {
            EnsureBuilt();
            return _selectedNodeIdLabel!;
        }
    }

    public LineEdit SelectedNodeDisplayNameEdit
    {
        get
        {
            EnsureBuilt();
            return _selectedNodeDisplayNameEdit!;
        }
    }

    public TextEdit SelectedNodeDescriptionEdit
    {
        get
        {
            EnsureBuilt();
            return _selectedNodeDescriptionEdit!;
        }
    }

    public event Action<string, string>? NodePropertyChanged;
    public event Action<string>? SelectedNodeDisplayNameChanged;
    public event Action<string>? SelectedNodeDescriptionChanged;
    public event Action<DynamicPreviewContext>? PreviewContextChanged;
    public event Action<string>? EventOptionAddRequested;

    public override void _Ready()
    {
        EnsureBuilt();
    }

    public void EnsureBuilt()
    {
        BuildUi();
        RefreshTexts();
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

    public void SetTab(int index)
    {
        if (_tabs != null)
        {
            _tabs.CurrentTab = Mathf.Clamp(index, 0, _tabs.GetTabCount() - 1);
        }
    }

    public void SetBasicText(string text)
    {
        if (_basicReadOnlyLabel != null)
        {
            _basicReadOnlyLabel.Text = text;
        }
    }

    public void SetRuntimeAssets(IEnumerable<string> assets)
    {
        _runtimeAssets.Clear();
        _runtimeAssets.AddRange(assets.Where(path => !string.IsNullOrWhiteSpace(path)));
        RefreshRuntimeAssets();
    }

    public void SetImportedAssets(IEnumerable<AssetRef> assets)
    {
        _importedAssets.Clear();
        _importedAssets.AddRange(assets);
        RefreshImportedAssets();
    }

    public void SetGraphInfo(string text)
    {
        if (_graphInfoLabel != null)
        {
            _graphInfoLabel.Text = text;
        }
    }

    public void SetGraphDetails(string graphId, string graphName, string graphDescription, bool useGraphBehavior)
    {
        if (_graphIdEdit != null) _graphIdEdit.Text = graphId ?? string.Empty;
        if (_graphNameEdit != null) _graphNameEdit.Text = graphName ?? string.Empty;
        if (_graphDescriptionEdit != null) _graphDescriptionEdit.Text = graphDescription ?? string.Empty;
        if (_graphEnabledCheck != null) _graphEnabledCheck.ButtonPressed = useGraphBehavior;
    }

    public void SetPreviewContext(DynamicPreviewContext? context)
    {
        context ??= new DynamicPreviewContext();
        _suppressPreviewContextChanged = true;
        try
        {
            if (_previewUpgradedCheck != null)
            {
                _previewUpgradedCheck.ButtonPressed = context.Upgraded;
            }

            if (_previewTargetSelector != null)
            {
                SelectPreviewTarget(context.TargetSelector);
            }

            if (_previewCurrentBlockSpin != null) _previewCurrentBlockSpin.Value = (double)context.CurrentBlock;
            if (_previewCurrentStarsSpin != null) _previewCurrentStarsSpin.Value = (double)context.CurrentStars;
            if (_previewCurrentEnergySpin != null) _previewCurrentEnergySpin.Value = (double)context.CurrentEnergy;
            if (_previewHandCountSpin != null) _previewHandCountSpin.Value = context.HandCount;
            if (_previewDrawPileSpin != null) _previewDrawPileSpin.Value = context.DrawPileCount;
            if (_previewDiscardPileSpin != null) _previewDiscardPileSpin.Value = context.DiscardPileCount;
            if (_previewExhaustPileSpin != null) _previewExhaustPileSpin.Value = context.ExhaustPileCount;
            if (_previewMissingHpSpin != null) _previewMissingHpSpin.Value = (double)context.MissingHp;
        }
        finally
        {
            _suppressPreviewContextChanged = false;
        }
    }

    public void SetSelectedNode(BehaviorGraphNodeDefinition? node)
    {
        _inspectorNode = node;
        if (_selectedNodeIdLabel != null)
        {
            _selectedNodeIdLabel.Text = node == null
                ? Dual("节点 ID：未选择", "Node Id: none")
                : $"{Dual("节点 ID", "Node Id")}: {node.NodeId}";
        }

        if (_selectedNodeTypeLabel != null)
        {
            _selectedNodeTypeLabel.Text = node == null
                ? Dual("节点类型：未选择", "Node Type: none")
                : $"{Dual("节点类型", "Node Type")}: {ResolveNodeTypeDisplay(node)}";
        }

        if (_selectedNodeDisplayNameEdit != null)
        {
            _selectedNodeDisplayNameEdit.Editable = node != null;
            _selectedNodeDisplayNameEdit.Text = node?.DisplayName ?? string.Empty;
        }

        if (_selectedNodeDescriptionEdit != null)
        {
            _selectedNodeDescriptionEdit.Editable = node != null;
            _selectedNodeDescriptionEdit.Text = node?.Description ?? string.Empty;
        }

        if (_selectedNodeDynamicSummaryLabel != null && node == null)
        {
            _selectedNodeDynamicSummaryLabel.Text = string.Empty;
        }
    }

    public void SetInspectorGraphContext(BehaviorGraphDefinition? graph, BehaviorGraphNodeDefinition? selectedNode)
    {
        _inspectorGraph = graph;
        _inspectorNode = selectedNode;
    }

    public void InvalidatePropertyEditors()
    {
        _propertyEditorsInvalidated = true;
        _propertyEditorSignature = string.Empty;
        _cachedEventPageChoices = null;
        _cachedEventOptionChoices = null;
    }

    public void SetSelectedNodeProperties(IReadOnlyDictionary<string, string> properties)
    {
        if (_selectedNodePropertyHost == null)
        {
            return;
        }

        var signature = string.Join("|", properties.Keys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase));
        if (!_propertyEditorsInvalidated &&
            string.Equals(_propertyEditorSignature, signature, StringComparison.Ordinal) &&
            _propertyEditors.Count == properties.Count)
        {
            ApplyPropertyEditorValues(properties);
            return;
        }

        _propertyEditorsInvalidated = false;
        _propertyEditorSignature = signature;
        _propertyEditors.Clear();
        _choiceIndexCache.Clear();
        foreach (var child in _selectedNodePropertyHost.GetChildren())
        {
            child.QueueFree();
        }

        if (properties.Count == 0)
        {
            _selectedNodePropertyHost.AddChild(MakeLabel(ModStudioLocalizationCatalog.T("placeholder.graph_node_properties_empty"), true));
            return;
        }

        foreach (var pair in properties.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            var row = new VBoxContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            row.AddThemeConstantOverride("separation", 4);
            var label = MakeLabel(ModStudioFieldDisplayNames.Get(pair.Key), true);
            var helpText = GetGraphPropertyHelpText(pair.Key);
            if (!string.IsNullOrWhiteSpace(helpText))
            {
                row.TooltipText = helpText;
                label.TooltipText = helpText;
            }
            row.AddChild(label);

            var editor = BuildPropertyEditor(pair.Key, pair.Value ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(helpText))
            {
                editor.TooltipText = helpText;
            }
            row.AddChild(editor);
            _propertyEditors[pair.Key] = editor;
            _selectedNodePropertyHost.AddChild(row);
        }
    }

    private void ApplyPropertyEditorValues(IReadOnlyDictionary<string, string> properties)
    {
        foreach (var pair in properties)
        {
            if (!_propertyEditors.TryGetValue(pair.Key, out var editor))
            {
                continue;
            }

            switch (editor)
            {
                case LineEdit lineEdit:
                    lineEdit.Text = pair.Value ?? string.Empty;
                    break;
                case TextEdit textEdit:
                    textEdit.Text = pair.Value ?? string.Empty;
                    break;
                case CheckBox checkBox when bool.TryParse(pair.Value, out var boolValue):
                    checkBox.ButtonPressed = boolValue;
                    break;
                case SpinBox spinBox when double.TryParse(pair.Value, out var numericValue):
                    spinBox.Value = numericValue;
                    break;
                case SearchableOptionPicker searchableOptionPicker:
                    searchableOptionPicker.SetValue(pair.Value ?? string.Empty);
                    break;
                case OptionButton optionButton:
                    ApplyOptionValue(optionButton, pair.Key, pair.Value ?? string.Empty);
                    break;
            }
        }
    }

    private void ApplyOptionValue(OptionButton optionButton, string propertyKey, string propertyValue)
    {
        if (!_choiceIndexCache.TryGetValue(propertyKey, out var indexMap))
        {
            indexMap = BuildChoiceIndexMap(optionButton);
            _choiceIndexCache[propertyKey] = indexMap;
        }

        if (indexMap.TryGetValue(propertyValue, out var selectedIndex))
        {
            optionButton.Select(selectedIndex);
            return;
        }

        if (!string.IsNullOrWhiteSpace(propertyValue))
        {
            optionButton.AddItem($"{ModStudioFieldDisplayNames.FormatGraphPropertyValue(propertyKey, propertyValue)} [{propertyValue}]");
            optionButton.SetItemMetadata(optionButton.ItemCount - 1, propertyValue);
            _choiceIndexCache[propertyKey][propertyValue] = optionButton.ItemCount - 1;
            optionButton.Select(optionButton.ItemCount - 1);
            return;
        }

        if (optionButton.ItemCount > 0)
        {
            optionButton.Select(0);
        }
    }

    private static Dictionary<string, int> BuildChoiceIndexMap(OptionButton optionButton)
    {
        var map = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < optionButton.ItemCount; index++)
        {
            var key = optionButton.GetItemMetadata(index).AsString();
            if (!string.IsNullOrWhiteSpace(key) && !map.ContainsKey(key))
            {
                map[key] = index;
            }
        }

        return map;
    }

    public void SetSelectedNodeDynamicSummary(string text)
    {
        if (_selectedNodeDynamicSummaryLabel != null)
        {
            _selectedNodeDynamicSummaryLabel.Text = text ?? string.Empty;
            _selectedNodeDynamicSummaryLabel.Visible = !string.IsNullOrWhiteSpace(text);
        }
    }

    public void SetPreviewContextVisible(bool visible)
    {
        if (_previewContextRoot != null)
        {
            _previewContextRoot.Visible = visible;
        }
    }

    public void RefreshTexts()
    {
        if (_titleLabel != null)
        {
            _titleLabel.Text = Dual("详细信息", "Details");
        }

        if (_tabs != null)
        {
            _tabs.SetTabTitle(0, Dual("基础", "Basic"));
            _tabs.SetTabTitle(1, Dual("资源", "Assets"));
            _tabs.SetTabTitle(2, Dual("Graph", "Graph"));
        }

        if (_runtimeSearchEdit != null)
        {
            _runtimeSearchEdit.PlaceholderText = Dual("搜索名称包含...", "Search contains...");
        }

        if (_importedSearchEdit != null)
        {
            _importedSearchEdit.PlaceholderText = Dual("搜索名称包含...", "Search contains...");
        }

        if (_runtimeAssetsTabButton != null)
        {
            _runtimeAssetsTabButton.Text = ModStudioLocalizationCatalog.T("tab.asset_runtime");
        }

        if (_importedAssetsTabButton != null)
        {
            _importedAssetsTabButton.Text = ModStudioLocalizationCatalog.T("tab.asset_project");
        }

        if (_graphEnabledCheck != null)
        {
            _graphEnabledCheck.Text = Dual("启用 Graph 行为", "Use Graph Behavior");
        }

        if (_graphIdLabel != null)
        {
            _graphIdLabel.Text = Dual("Graph ID", "Graph Id");
        }

        if (_graphNameLabel != null)
        {
            _graphNameLabel.Text = Dual("Graph 名称", "Graph Name");
        }

        if (_graphDescriptionLabel != null)
        {
            _graphDescriptionLabel.Text = Dual("Graph 描述", "Graph Description");
        }

        if (_graphIdEdit != null)
        {
            _graphIdEdit.PlaceholderText = Dual("输入 Graph ID", "Enter graph id");
        }

        if (_graphNameEdit != null)
        {
            _graphNameEdit.PlaceholderText = Dual("输入 Graph 名称", "Enter graph name");
        }

        if (_previewContextLabel != null)
        {
            _previewContextLabel.Text = ModStudioLocalizationCatalog.T("graph.preview_context");
        }

        if (_previewUpgradedCheck != null)
        {
            _previewUpgradedCheck.Text = ModStudioLocalizationCatalog.T("graph.preview_context.upgraded");
        }

        if (_previewTargetLabel != null)
        {
            _previewTargetLabel.Text = ModStudioLocalizationCatalog.T("graph.preview_context.target");
        }

        if (_previewCurrentBlockLabel != null)
        {
            _previewCurrentBlockLabel.Text = ModStudioLocalizationCatalog.T("graph.preview_context.current_block");
        }

        if (_previewCurrentStarsLabel != null)
        {
            _previewCurrentStarsLabel.Text = ModStudioLocalizationCatalog.T("graph.preview_context.current_stars");
        }

        if (_previewCurrentEnergyLabel != null)
        {
            _previewCurrentEnergyLabel.Text = ModStudioLocalizationCatalog.T("graph.preview_context.current_energy");
        }

        if (_previewHandCountLabel != null)
        {
            _previewHandCountLabel.Text = ModStudioLocalizationCatalog.T("graph.preview_context.hand_count");
        }

        if (_previewDrawPileLabel != null)
        {
            _previewDrawPileLabel.Text = ModStudioLocalizationCatalog.T("graph.preview_context.draw_pile_count");
        }

        if (_previewDiscardPileLabel != null)
        {
            _previewDiscardPileLabel.Text = ModStudioLocalizationCatalog.T("graph.preview_context.discard_pile_count");
        }

        if (_previewExhaustPileLabel != null)
        {
            _previewExhaustPileLabel.Text = ModStudioLocalizationCatalog.T("graph.preview_context.exhaust_pile_count");
        }

        if (_previewMissingHpLabel != null)
        {
            _previewMissingHpLabel.Text = ModStudioLocalizationCatalog.T("graph.preview_context.missing_hp");
        }

        if (_selectedNodeDisplayNameLabel != null)
        {
            _selectedNodeDisplayNameLabel.Text = Dual("节点显示名称", "Node Display Name");
        }

        if (_selectedNodeDescriptionLabel != null)
        {
            _selectedNodeDescriptionLabel.Text = Dual("节点描述", "Node Description");
        }

        if (_selectedNodePropertiesLabel != null)
        {
            _selectedNodePropertiesLabel.Text = ModStudioLocalizationCatalog.T("label.node_properties");
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

        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", 8);
        AddChild(root);

        _titleLabel = MakeLabel(string.Empty, true);
        root.AddChild(_titleLabel);

        _tabs = new TabContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            TabsVisible = false
        };
        root.AddChild(_tabs);

        _tabs.AddChild(BuildBasicPage());
        _tabs.AddChild(BuildAssetsPage());
        _tabs.AddChild(BuildGraphPage());
    }

    private Control BuildBasicPage()
    {
        var page = new VBoxContainer
        {
            Name = "BasicTab",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        _basicReadOnlyLabel = MakeDetails(string.Empty, scrollActive: true, fitContent: false, minHeight: 260f);
        page.AddChild(_basicReadOnlyLabel);
        return page;
    }

    private Control BuildAssetsPage()
    {
        var page = new VBoxContainer
        {
            Name = "AssetsTab",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        page.AddThemeConstantOverride("separation", 8);

        var switchRow = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        switchRow.AddThemeConstantOverride("separation", 6);

        _runtimeAssetsTabButton = MakeButton(string.Empty, () => ShowAssetPage(true), toggle: true);
        _importedAssetsTabButton = MakeButton(string.Empty, () => ShowAssetPage(false), toggle: true);
        switchRow.AddChild(_runtimeAssetsTabButton);
        switchRow.AddChild(_importedAssetsTabButton);
        page.AddChild(switchRow);

        _runtimeAssetPage = BuildAssetListPage(out _runtimeSearchEdit, out _runtimeAssetList);
        _runtimeSearchEdit.TextChanged += _ => RefreshRuntimeAssets();
        page.AddChild(_runtimeAssetPage);

        _importedAssetPage = BuildAssetListPage(out _importedSearchEdit, out _importedAssetList);
        _importedSearchEdit.TextChanged += _ => RefreshImportedAssets();
        page.AddChild(_importedAssetPage);

        ShowAssetPage(true);
        return page;
    }

    private Control BuildGraphPage()
    {
        var pageScroll = new ScrollContainer
        {
            Name = "GraphTab",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        var page = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin
        };
        page.AddThemeConstantOverride("separation", 8);
        pageScroll.AddChild(page);

        _graphInfoLabel = MakeDetails(string.Empty, scrollActive: false, fitContent: true, minHeight: 72f);
        page.AddChild(_graphInfoLabel);

        _graphEnabledCheck = new CheckBox();
        page.AddChild(_graphEnabledCheck);

        _graphIdLabel = MakeLabel(string.Empty, true);
        _graphIdEdit = new LineEdit();
        page.AddChild(_graphIdLabel);
        page.AddChild(_graphIdEdit);

        _graphNameLabel = MakeLabel(string.Empty, true);
        _graphNameEdit = new LineEdit();
        page.AddChild(_graphNameLabel);
        page.AddChild(_graphNameEdit);

        _graphDescriptionLabel = MakeLabel(string.Empty, true);
        _graphDescriptionEdit = new TextEdit
        {
            CustomMinimumSize = new Vector2(0f, 110f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.Fill
        };
        page.AddChild(_graphDescriptionLabel);
        page.AddChild(_graphDescriptionEdit);

        var previewContextPanel = new PanelContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        page.AddChild(previewContextPanel);

        _previewContextRoot = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _previewContextRoot.AddThemeConstantOverride("separation", 6);
        previewContextPanel.AddChild(_previewContextRoot);

        _previewContextLabel = MakeLabel(string.Empty, true);
        _previewContextRoot.AddChild(_previewContextLabel);

        _previewUpgradedCheck = new CheckBox();
        _previewUpgradedCheck.Toggled += _ => EmitPreviewContextChanged();
        _previewContextRoot.AddChild(_previewUpgradedCheck);

        _previewContextRoot.AddChild(BuildPreviewTargetRow());
        _previewContextRoot.AddChild(BuildPreviewNumericRow(out _previewCurrentBlockLabel, out _previewCurrentBlockSpin, 0d, 999d, 1d, 0));
        _previewContextRoot.AddChild(BuildPreviewNumericRow(out _previewCurrentStarsLabel, out _previewCurrentStarsSpin, 0d, 999d, 1d, 0));
        _previewContextRoot.AddChild(BuildPreviewNumericRow(out _previewCurrentEnergyLabel, out _previewCurrentEnergySpin, 0d, 20d, 1d, 0));
        _previewContextRoot.AddChild(BuildPreviewNumericRow(out _previewHandCountLabel, out _previewHandCountSpin, 0d, 50d, 1d, 0));
        _previewContextRoot.AddChild(BuildPreviewNumericRow(out _previewDrawPileLabel, out _previewDrawPileSpin, 0d, 200d, 1d, 0));
        _previewContextRoot.AddChild(BuildPreviewNumericRow(out _previewDiscardPileLabel, out _previewDiscardPileSpin, 0d, 200d, 1d, 0));
        _previewContextRoot.AddChild(BuildPreviewNumericRow(out _previewExhaustPileLabel, out _previewExhaustPileSpin, 0d, 200d, 1d, 0));
        _previewContextRoot.AddChild(BuildPreviewNumericRow(out _previewMissingHpLabel, out _previewMissingHpSpin, 0d, 999d, 1d, 0));

        _selectedNodeTypeLabel = MakeLabel(string.Empty, true);
        _selectedNodeIdLabel = MakeLabel(string.Empty, true);
        page.AddChild(_selectedNodeTypeLabel);
        page.AddChild(_selectedNodeIdLabel);

        _selectedNodeDisplayNameLabel = MakeLabel(string.Empty, true);
        _selectedNodeDisplayNameEdit = new LineEdit();
        _selectedNodeDisplayNameEdit.TextChanged += value => SelectedNodeDisplayNameChanged?.Invoke(value);
        page.AddChild(_selectedNodeDisplayNameLabel);
        page.AddChild(_selectedNodeDisplayNameEdit);

        _selectedNodeDescriptionLabel = MakeLabel(string.Empty, true);
        _selectedNodeDescriptionEdit = new TextEdit
        {
            CustomMinimumSize = new Vector2(0f, 90f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.Fill
        };
        _selectedNodeDescriptionEdit.TextChanged += () => SelectedNodeDescriptionChanged?.Invoke(_selectedNodeDescriptionEdit.Text ?? string.Empty);
        page.AddChild(_selectedNodeDescriptionLabel);
        page.AddChild(_selectedNodeDescriptionEdit);

        _selectedNodeDynamicSummaryLabel = MakeDetails(string.Empty, scrollActive: false, fitContent: true, minHeight: 72f);
        page.AddChild(_selectedNodeDynamicSummaryLabel);

        _selectedNodePropertiesLabel = MakeLabel(string.Empty, true);
        page.AddChild(_selectedNodePropertiesLabel);

        _selectedNodePropertyHost = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin
        };
        _selectedNodePropertyHost.AddThemeConstantOverride("separation", 6);
        page.AddChild(_selectedNodePropertyHost);

        return pageScroll;
    }

    private VBoxContainer BuildAssetListPage(out LineEdit searchEdit, out ItemList list)
    {
        var page = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        page.AddThemeConstantOverride("separation", 6);

        searchEdit = new LineEdit
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        page.AddChild(searchEdit);

        list = new ItemList
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        page.AddChild(list);
        return page;
    }

    private void ShowAssetPage(bool runtime)
    {
        if (_runtimeAssetPage != null) _runtimeAssetPage.Visible = runtime;
        if (_importedAssetPage != null) _importedAssetPage.Visible = !runtime;
        if (_runtimeAssetsTabButton != null) _runtimeAssetsTabButton.ButtonPressed = runtime;
        if (_importedAssetsTabButton != null) _importedAssetsTabButton.ButtonPressed = !runtime;
    }

    private void RefreshRuntimeAssets()
    {
        if (_runtimeAssetList == null)
        {
            return;
        }

        _runtimeAssetList.Clear();
        var search = _runtimeSearchEdit?.Text ?? string.Empty;
        foreach (var path in _runtimeAssets
                     .Where(path => Contains(path, search))
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var display = Path.GetFileName(path);
            if (string.IsNullOrWhiteSpace(display))
            {
                display = path;
            }

            _runtimeAssetList.AddItem(display);
            _runtimeAssetList.SetItemMetadata(_runtimeAssetList.ItemCount - 1, path);
            _runtimeAssetList.SetItemTooltipEnabled(_runtimeAssetList.ItemCount - 1, true);
            _runtimeAssetList.SetItemTooltip(_runtimeAssetList.ItemCount - 1, path);
        }
    }

    private void RefreshImportedAssets()
    {
        if (_importedAssetList == null)
        {
            return;
        }

        _importedAssetList.Clear();
        _importedAssetList.AddItem(Dual("添加外部素材", "Add External Asset"));
        _importedAssetList.SetItemMetadata(0, "__add_external__");

        var search = _importedSearchEdit?.Text ?? string.Empty;
        foreach (var asset in _importedAssets
                     .Where(asset => Contains(asset.FileName, search) || Contains(asset.ManagedPath, search) || Contains(asset.LogicalRole, search))
                     .OrderBy(asset => asset.FileName, StringComparer.OrdinalIgnoreCase))
        {
            var display = string.IsNullOrWhiteSpace(asset.FileName) ? asset.ManagedPath : asset.FileName;
            _importedAssetList.AddItem(display);
            _importedAssetList.SetItemMetadata(_importedAssetList.ItemCount - 1, asset.Id);
            _importedAssetList.SetItemTooltipEnabled(_importedAssetList.ItemCount - 1, true);
            _importedAssetList.SetItemTooltip(_importedAssetList.ItemCount - 1, asset.ManagedPath);
        }
    }

    private Control BuildPropertyEditor(string propertyKey, string propertyValue)
    {
        if (TryCreateGraphContextEditor(propertyKey, propertyValue, out var contextEditor))
        {
            return contextEditor;
        }

        if (TryCreateChoiceEditor(propertyKey, propertyValue, out var choiceEditor))
        {
            return choiceEditor;
        }

        if (IsNumericProperty(propertyKey))
        {
            var spinBox = new SpinBox
            {
                MinValue = -9999,
                MaxValue = 9999,
                Step = 1,
                Rounded = false,
                AllowGreater = true,
                AllowLesser = true,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            if (double.TryParse(propertyValue, out var numericValue))
            {
                spinBox.Value = numericValue;
            }
            spinBox.ValueChanged += value => NodePropertyChanged?.Invoke(propertyKey, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return spinBox;
        }

        if (bool.TryParse(propertyValue, out var boolValue))
        {
            var checkBox = new CheckBox
            {
                ButtonPressed = boolValue,
                Text = Dual("启用", "Enabled"),
                SizeFlagsHorizontal = Control.SizeFlags.Fill
            };
            checkBox.Toggled += value => NodePropertyChanged?.Invoke(propertyKey, value.ToString());
            return checkBox;
        }

        var lineEdit = new LineEdit
        {
            Text = propertyValue ?? string.Empty,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        lineEdit.TextChanged += value => NodePropertyChanged?.Invoke(propertyKey, value);
        return lineEdit;
    }

    private bool TryCreateGraphContextEditor(string propertyKey, string propertyValue, out Control editor)
    {
        editor = null!;

        if (string.Equals(propertyKey, "option_order", StringComparison.Ordinal))
        {
            editor = BuildOptionOrderEditor(propertyKey, propertyValue);
            return true;
        }

        if (string.Equals(propertyKey, "next_page_id", StringComparison.Ordinal) ||
            string.Equals(propertyKey, "resume_page_id", StringComparison.Ordinal) ||
            (string.Equals(propertyKey, "page_id", StringComparison.Ordinal) && !string.Equals(_inspectorNode?.NodeType, "event.page", StringComparison.OrdinalIgnoreCase)))
        {
            editor = BuildGraphPageChoiceEditor(propertyKey, propertyValue, allowEmpty: !string.Equals(propertyKey, "page_id", StringComparison.Ordinal));
            return true;
        }

        return false;
    }

    private Control BuildGraphPageChoiceEditor(string propertyKey, string propertyValue, bool allowEmpty)
    {
        var optionButton = new OptionButton
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };

        if (allowEmpty)
        {
            optionButton.AddItem(Dual("未设置", "Unset"));
            optionButton.SetItemMetadata(0, string.Empty);
        }

        var pages = GetEventPageChoices();
        var selectedIndex = -1;
        foreach (var page in pages)
        {
            optionButton.AddItem(page.DisplayText);
            optionButton.SetItemMetadata(optionButton.ItemCount - 1, page.Value);
            if (string.Equals(page.Value, propertyValue, StringComparison.Ordinal))
            {
                selectedIndex = optionButton.ItemCount - 1;
            }
        }

        if (selectedIndex < 0 && !string.IsNullOrWhiteSpace(propertyValue))
        {
            optionButton.AddItem($"{propertyValue} [{Dual("无效旧值", "Invalid legacy value")}]");
            optionButton.SetItemMetadata(optionButton.ItemCount - 1, propertyValue);
            selectedIndex = optionButton.ItemCount - 1;
        }

        if (selectedIndex >= 0)
        {
            optionButton.Select(selectedIndex);
        }
        else if (optionButton.ItemCount > 0)
        {
            optionButton.Select(0);
        }

        _choiceIndexCache[propertyKey] = BuildChoiceIndexMap(optionButton);

        optionButton.ItemSelected += index =>
        {
            var selected = optionButton.GetItemMetadata((int)index).AsString();
            NodePropertyChanged?.Invoke(propertyKey, selected);
        };

        return optionButton;
    }

    private Control BuildOptionOrderEditor(string propertyKey, string propertyValue)
    {
        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", 4);

        var rowsHost = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        rowsHost.AddThemeConstantOverride("separation", 4);
        root.AddChild(rowsHost);

        var optionChoices = GetEventOptionChoices();
        var currentValues = ParseCsvValues(propertyValue);
        if (currentValues.Count == 0)
        {
            currentValues.Add(string.Empty);
        }

        void EmitCurrentValue()
        {
            var values = rowsHost.GetChildren()
                .OfType<HBoxContainer>()
                .SelectMany(row => row.GetChildren().OfType<OptionButton>())
                .Select(button => button.GetItemMetadata(button.Selected).AsString())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList();
            NodePropertyChanged?.Invoke(propertyKey, string.Join(",", values));
        }

        void AddRow(string selectedValue)
        {
            var row = new HBoxContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            row.AddThemeConstantOverride("separation", 4);

            var optionButton = new OptionButton
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            optionButton.AddItem(Dual("未设置", "Unset"));
            optionButton.SetItemMetadata(0, string.Empty);
            foreach (var choice in optionChoices)
            {
                optionButton.AddItem(choice.DisplayText);
                optionButton.SetItemMetadata(optionButton.ItemCount - 1, choice.Value);
            }

            var selectedIndex = 0;
            for (var index = 0; index < optionButton.ItemCount; index++)
            {
                if (string.Equals(optionButton.GetItemMetadata(index).AsString(), selectedValue, StringComparison.Ordinal))
                {
                    selectedIndex = index;
                    break;
                }
            }

            optionButton.Select(selectedIndex);
            _choiceIndexCache[$"{propertyKey}:{row.GetInstanceId()}"] = BuildChoiceIndexMap(optionButton);
            optionButton.ItemSelected += _ => EmitCurrentValue();
            row.AddChild(optionButton);

            var removeButton = MakeButton(Dual("删除", "Remove"), () =>
            {
                row.QueueFree();
                CallDeferred(nameof(EmitDeferredNodePropertyChanged), propertyKey, string.Join(",", rowsHost.GetChildren()
                    .OfType<HBoxContainer>()
                    .Where(existing => existing != row)
                    .SelectMany(existing => existing.GetChildren().OfType<OptionButton>())
                    .Select(button => button.GetItemMetadata(button.Selected).AsString())
                    .Where(value => !string.IsNullOrWhiteSpace(value))));
            });
            row.AddChild(removeButton);
            rowsHost.AddChild(row);
        }

        foreach (var value in currentValues)
        {
            AddRow(value);
        }

        var addButton = MakeButton(Dual("添加选项", "Add Option"), () =>
        {
            if (string.Equals(_inspectorNode?.NodeType, "event.page", StringComparison.OrdinalIgnoreCase) &&
                _inspectorNode.Properties.TryGetValue("page_id", out var pageId) &&
                !string.IsNullOrWhiteSpace(pageId))
            {
                EventOptionAddRequested?.Invoke(pageId);
                return;
            }

            AddRow(string.Empty);
            EmitCurrentValue();
        });
        root.AddChild(addButton);

        return root;
    }

    private static string GetGraphPropertyHelpText(string propertyKey)
    {
        return propertyKey switch
        {
            "dynamic_source_kind" => Dual("决定当前节点的数值来自固定值、原版动态变量，还是原版公式。", "Determines whether this node uses a literal value, an original dynamic variable, or an original formula."),
            "dynamic_var_name" => Dual("选择要引用的原版动态变量，例如 Damage、Block、Cards、Stars。", "Choose the original dynamic variable to reference, such as Damage, Block, Cards, or Stars."),
            "formula_ref" => Dual("选择要复用的原版公式类型。只有“原版公式”模式会使用该字段。", "Choose the original formula reference. This field is only used in formula mode."),
            "base_override_mode" => Dual("决定是否覆盖原版基础值。绝对值会直接改成该数，增量会在原值上加减。", "Controls how the original base value is overridden. Absolute replaces it, delta adds on top of it."),
            "base_override_value" => Dual("基础值覆盖的数值。", "The numeric value used by the base override."),
            "extra_override_mode" => Dual("决定是否覆盖原版公式里的额外值。只对公式模式生效。", "Controls how the original formula extra value is overridden. Only applies in formula mode."),
            "extra_override_value" => Dual("额外值覆盖的数值。只对公式模式生效。", "The numeric value used by the extra override. Only applies in formula mode."),
            "preview_multiplier_key" => Dual("选择公式乘数来自哪个上下文值，例如手牌数、能量、当前星数。真正的预览结果始终由右侧上下文决定。", "Chooses which context value drives the formula multiplier, such as hand count, energy, or stars. The final preview always comes from the preview context on the right."),
            "amount" => Dual("固定值模式下的直接数值。", "Direct numeric value used in literal mode."),
            "count" => Dual("要移动或生成的数量。填 0 代表全部。", "How many cards to move or create. Use 0 to mean all matching cards."),
            "left" => Dual("比较左值。可以填固定值，也可以填 $state.xxx、$target 这类引用。", "Left side of the comparison. Supports literal values and references such as $state.xxx or $target."),
            "right" => Dual("比较右值。可以填固定值，也可以填 $state.xxx、$target 这类引用。", "Right side of the comparison. Supports literal values and references such as $state.xxx or $target."),
            "key" => Dual("要写入的图状态键。后续节点可以通过 $state.xxx 读取它。", "Graph state key to write. Later nodes can read it through $state.xxx."),
            "value" => Dual("写入到状态键里的值。可以是固定值，也可以是 $state.xxx、$target 这类引用。", "Value written into the state key. Supports literals and references such as $state.xxx or $target."),
            "delta" => Dual("在当前状态值基础上增加的数值。", "Numeric amount added on top of the current stored value."),
            "factor" => Dual("当前状态值要乘上的倍率。", "Multiplier applied to the current stored value."),
            "target" => Dual("节点作用的目标。", "The target affected by this node."),
            "source_pile" => Dual("从哪个牌堆筛选现有卡牌。", "Which pile to search for existing cards."),
            "target_pile" => Dual("把卡牌移动到哪个牌堆。", "Which pile receives the moved cards."),
            "exact_energy_cost" => Dual("按精确能量费用筛选。填 -1 代表不限。", "Filter by exact energy cost. Use -1 for no cost filter."),
            "include_x_cost" => Dual("筛选时是否把 X 费牌也视为可匹配。", "Whether X-cost cards are allowed by this filter."),
            "card_type_scope" => Dual("按卡牌类型筛选，例如只拿攻击/技能/能力牌。", "Filter by card type, such as attack, skill, or power cards."),
            "props" => Dual("附加行为标记，例如不可格挡、位移等。", "Additional behavior flags such as unblockable or move."),
            "condition" => Dual("分支要直接计算的条件表达式。也可以不填，改用下方的条件键。", "Condition expression evaluated by the branch. Leave empty to use the condition key instead."),
            "condition_key" => Dual("分支读取的布尔状态键。通常由上游 compare 节点通过 result_key 写入。", "Boolean state key read by the branch. This is commonly written by an upstream compare node via result_key."),
            "result_key" => Dual("compare 节点会把比较结果写到这个状态键里，branch 再通过 condition_key 读取。", "The compare node writes its boolean result into this state key, and a later branch reads it through condition_key."),
            "status" => Dual("要设置到当前附魔上的状态。", "Status applied to the current enchantment."),
            "page_id" => Dual("事件页面的唯一 ID。其他跳页和选项会引用它。", "Unique id for this event page. Other event nodes can jump to or reference it."),
            "option_id" => Dual("事件选项的唯一 ID。页面会用它决定选项顺序。", "Unique id for this event option. Pages use it to determine option order."),
            "next_page_id" => Dual("选项或跳页节点执行后要前往的页面 ID。", "Page id to move to after this option or goto-page node resolves."),
            "resume_page_id" => Dual("事件战斗结束后返回的页面 ID。", "Page id to return to after event combat completes."),
            "encounter_id" => Dual("事件触发战斗时要进入的遭遇 ID。", "Encounter id used when this event starts combat."),
            "reward_kind" => Dual("事件选项实际发放的奖励类型。仅修改标题文字不会改变真正奖励。", "Actual reward type granted by this event choice. Changing title text alone does not change the reward."),
            "reward_amount" => Dual("事件奖励的数值，例如金币数量、抽牌数、伤害值。", "Numeric amount applied by the event reward, such as gold, draw count, or damage."),
            "reward_count" => Dual("当奖励可堆叠时，表示要发放多少份奖励。", "When the reward kind supports stacking, this controls how many copies are granted."),
            "reward_target" => Dual("事件奖励作用的目标。", "Target affected by the event reward."),
            "reward_props" => Dual("事件伤害或格挡奖励的附加属性。", "Additional flags used by event damage or block rewards."),
            "reward_power_id" => Dual("当奖励类型为能力时，要施加的能力 ID。", "Power id to apply when the reward kind is power."),
            "card_id" => Dual("当奖励类型是卡牌时，要发放的具体卡牌 ID。", "Concrete card id used when the reward kind grants cards."),
            "relic_id" => Dual("当奖励类型是遗物时，要发放的具体遗物 ID。", "Concrete relic id used when the reward kind grants relics."),
            "potion_id" => Dual("当奖励类型是药水时，要发放的具体药水 ID。", "Concrete potion id used when the reward kind grants potions."),
            "is_proceed" => Dual("该选项执行后是否直接结束当前事件。", "Whether this option should immediately end the current event after resolving."),
            "save_choice_to_history" => Dual("是否把这个选项写入事件历史。", "Whether this option should be saved into event choice history."),
            _ => string.Empty
        };
    }

    private bool TryCreateChoiceEditor(string propertyKey, string propertyValue, out Control editor)
    {
        editor = null!;
        var choices = GetChoiceOptionsForCurrentNode(propertyKey);
        if (choices.Count == 0)
        {
            return false;
        }

        var picker = new SearchableOptionPicker(
            choices.Select(choice => (choice.Value, choice.DisplayText)).ToList(),
            propertyValue,
            fallbackDisplayFactory: unknown => $"{ModStudioFieldDisplayNames.FormatGraphPropertyValue(propertyKey, unknown)} [{unknown}]");

        string? autoSelectedValue = null;

        if (string.IsNullOrWhiteSpace(propertyValue))
        {
            autoSelectedValue = picker.GetValue();
        }

        picker.ValueChanged += selectedValue => NodePropertyChanged?.Invoke(propertyKey, selectedValue);

        if (string.IsNullOrWhiteSpace(propertyValue) && !string.IsNullOrWhiteSpace(autoSelectedValue))
        {
            CallDeferred(nameof(EmitDeferredNodePropertyChanged), propertyKey, autoSelectedValue);
        }

        editor = picker;
        return true;
    }

    private IReadOnlyList<PropertyChoice> GetChoiceOptionsForCurrentNode(string propertyKey)
    {
        if (ShouldUseStatusValueChoices(propertyKey))
        {
            return FieldChoiceProvider.GetGraphChoices("status")
                .Select(choice => new PropertyChoice(choice.Value, choice.Display))
                .ToList();
        }

        return TryGetChoiceOptions(propertyKey, out var choices)
            ? choices
            : Array.Empty<PropertyChoice>();
    }

    private bool ShouldUseStatusValueChoices(string propertyKey)
    {
        if (!string.Equals(propertyKey, "value", StringComparison.Ordinal) || _inspectorNode == null)
        {
            return false;
        }

        if (!string.Equals(_inspectorNode.NodeType, "value.set", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return _inspectorNode.Properties.TryGetValue("key", out var key) &&
               string.Equals(key?.Trim(), "Status", StringComparison.OrdinalIgnoreCase);
    }

    private IReadOnlyList<PropertyChoice> GetEventPageChoices()
    {
        if (_cachedEventPageChoices != null)
        {
            return _cachedEventPageChoices;
        }

        if (_inspectorGraph == null)
        {
            return Array.Empty<PropertyChoice>();
        }

        _cachedEventPageChoices = _inspectorGraph.Nodes
            .Where(node => string.Equals(node.NodeType, "event.page", StringComparison.OrdinalIgnoreCase))
            .Select(node =>
            {
                var pageId = node.Properties.TryGetValue("page_id", out var rawPageId) && !string.IsNullOrWhiteSpace(rawPageId)
                    ? rawPageId
                    : node.NodeId;
                var title = node.Properties.TryGetValue("title", out var rawTitle) && !string.IsNullOrWhiteSpace(rawTitle)
                    ? rawTitle
                    : pageId;
                return new PropertyChoice(pageId, $"{title} [{pageId}]");
            })
            .GroupBy(choice => choice.Value, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(choice => choice.DisplayText, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return _cachedEventPageChoices;
    }

    private IReadOnlyList<PropertyChoice> GetEventOptionChoices()
    {
        if (_cachedEventOptionChoices != null)
        {
            return _cachedEventOptionChoices;
        }

        if (_inspectorGraph == null)
        {
            return Array.Empty<PropertyChoice>();
        }

        _cachedEventOptionChoices = _inspectorGraph.Nodes
            .Where(node => string.Equals(node.NodeType, "event.option", StringComparison.OrdinalIgnoreCase))
            .Select(node =>
            {
                var optionId = node.Properties.TryGetValue("option_id", out var rawOptionId) && !string.IsNullOrWhiteSpace(rawOptionId)
                    ? rawOptionId
                    : node.NodeId;
                var title = node.Properties.TryGetValue("title", out var rawTitle) && !string.IsNullOrWhiteSpace(rawTitle)
                    ? rawTitle
                    : optionId;
                var pageId = node.Properties.TryGetValue("page_id", out var rawPageId) && !string.IsNullOrWhiteSpace(rawPageId)
                    ? rawPageId
                    : Dual("未绑定页", "No Page");
                return new PropertyChoice(optionId, $"{title} [{optionId}] · {pageId}");
            })
            .GroupBy(choice => choice.Value, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(choice => choice.DisplayText, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return _cachedEventOptionChoices;
    }

    private static List<string> ParseCsvValues(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return new List<string>();
        }

        return rawValue
            .Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();
    }

    private void EmitDeferredNodePropertyChanged(string propertyKey, string value)
    {
        NodePropertyChanged?.Invoke(propertyKey, value);
    }

    private static bool TryGetChoiceOptions(string propertyKey, out IReadOnlyList<PropertyChoice> choices)
    {
        var normalizedPropertyKey = propertyKey switch
        {
            "reward_target" => "target",
            "reward_props" => "props",
            "reward_power_id" => "power_id",
            _ => propertyKey
        };

        var providerChoices = FieldChoiceProvider.GetGraphChoices(normalizedPropertyKey);
        if (providerChoices.Count == 0)
        {
            choices = Array.Empty<PropertyChoice>();
            return false;
        }

        if (string.Equals(propertyKey, "preview_multiplier_key", StringComparison.Ordinal))
        {
            choices = providerChoices
                .Select(choice => (Value: NormalizePreviewMultiplierChoice(choice.Value), choice.Display))
                .GroupBy(choice => choice.Value, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .Select(choice => new PropertyChoice(choice.Value, choice.Display))
                .ToList();
            return true;
        }

        choices = providerChoices
            .Select(choice => new PropertyChoice(choice.Value, choice.Display))
            .ToList();
        return true;
    }

    private static string NormalizePreviewMultiplierChoice(string rawValue)
    {
        return string.Equals(rawValue, "cards", StringComparison.OrdinalIgnoreCase)
            ? "hand_count"
            : rawValue;
    }

    private static bool IsNumericProperty(string propertyKey)
    {
        return propertyKey is "amount" or
            "count" or
            "exact_energy_cost" or
            "base_override_value" or
            "extra_override_value";
    }

    private Control BuildPreviewTargetRow()
    {
        var row = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        row.AddThemeConstantOverride("separation", 6);

        _previewTargetLabel = MakeLabel(string.Empty, true);
        _previewTargetLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(_previewTargetLabel);

        _previewTargetSelector = new OptionButton
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        AddPreviewTargetChoice("self");
        AddPreviewTargetChoice("current_target");
        AddPreviewTargetChoice("all_enemies");
        AddPreviewTargetChoice("all_allies");
        AddPreviewTargetChoice("all_targets");
        _previewTargetSelector.ItemSelected += _ => EmitPreviewContextChanged();
        row.AddChild(_previewTargetSelector);

        return row;
    }

    private Control BuildPreviewNumericRow(out Label label, out SpinBox spinBox, double minValue, double maxValue, double step, int decimalPlaces)
    {
        var row = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        row.AddThemeConstantOverride("separation", 6);

        label = MakeLabel(string.Empty, true);
        label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(label);

        spinBox = new SpinBox
        {
            MinValue = minValue,
            MaxValue = maxValue,
            Step = step,
            AllowGreater = true,
            AllowLesser = true,
            Rounded = decimalPlaces == 0,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        spinBox.ValueChanged += _ => EmitPreviewContextChanged();
        row.AddChild(spinBox);

        return row;
    }

    private void AddPreviewTargetChoice(string value)
    {
        if (_previewTargetSelector == null)
        {
            return;
        }

        _previewTargetSelector.AddItem(ModStudioFieldDisplayNames.FormatGraphPropertyValue("target", value));
        _previewTargetSelector.SetItemMetadata(_previewTargetSelector.ItemCount - 1, value);
    }

    private void SelectPreviewTarget(string? targetSelector)
    {
        if (_previewTargetSelector == null)
        {
            return;
        }

        var normalized = string.IsNullOrWhiteSpace(targetSelector) ? "current_target" : targetSelector;
        for (var index = 0; index < _previewTargetSelector.ItemCount; index++)
        {
            if (string.Equals(_previewTargetSelector.GetItemMetadata(index).AsString(), normalized, StringComparison.Ordinal))
            {
                _previewTargetSelector.Select(index);
                return;
            }
        }

        if (_previewTargetSelector.ItemCount > 0)
        {
            _previewTargetSelector.Select(0);
        }
    }

    private void EmitPreviewContextChanged()
    {
        if (_suppressPreviewContextChanged)
        {
            return;
        }

        PreviewContextChanged?.Invoke(ReadPreviewContext());
    }

    private DynamicPreviewContext ReadPreviewContext()
    {
        var context = new DynamicPreviewContext
        {
            Upgraded = _previewUpgradedCheck?.ButtonPressed ?? false,
            TargetSelector = ResolveSelectedPreviewTarget(),
            CurrentBlock = (decimal)(_previewCurrentBlockSpin?.Value ?? 0d),
            CurrentStars = (decimal)(_previewCurrentStarsSpin?.Value ?? 0d),
            CurrentEnergy = (decimal)(_previewCurrentEnergySpin?.Value ?? 0d),
            HandCount = (int)Math.Round(_previewHandCountSpin?.Value ?? 0d),
            DrawPileCount = (int)Math.Round(_previewDrawPileSpin?.Value ?? 0d),
            DiscardPileCount = (int)Math.Round(_previewDiscardPileSpin?.Value ?? 0d),
            ExhaustPileCount = (int)Math.Round(_previewExhaustPileSpin?.Value ?? 0d),
            MissingHp = (decimal)(_previewMissingHpSpin?.Value ?? 0d)
        };

        context.FormulaMultipliers["hand_count"] = context.HandCount;
        context.FormulaMultipliers["cards"] = context.HandCount;
        context.FormulaMultipliers["stars"] = context.CurrentStars;
        context.FormulaMultipliers["energy"] = context.CurrentEnergy;
        context.FormulaMultipliers["current_block"] = context.CurrentBlock;
        context.FormulaMultipliers["draw_pile"] = context.DrawPileCount;
        context.FormulaMultipliers["discard_pile"] = context.DiscardPileCount;
        context.FormulaMultipliers["exhaust_pile"] = context.ExhaustPileCount;
        context.FormulaMultipliers["missing_hp"] = context.MissingHp;
        return context;
    }

    private string ResolveSelectedPreviewTarget()
    {
        if (_previewTargetSelector == null || _previewTargetSelector.ItemCount == 0 || _previewTargetSelector.Selected < 0)
        {
            return "current_target";
        }

        return _previewTargetSelector.GetItemMetadata(_previewTargetSelector.Selected).AsString();
    }

    private static string ResolveNodeTypeDisplay(BehaviorGraphNodeDefinition node)
    {
        return string.IsNullOrWhiteSpace(node.DisplayName)
            ? node.NodeType
            : $"{node.DisplayName} [{node.NodeType}]";
    }

    private static bool Contains(string? source, string? search)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        return source.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private static string Dual(string zh, string en)
    {
        return ModStudioLocalization.IsChinese ? zh : en;
    }

    private sealed record PropertyChoice(string Value, string DisplayText);
}
