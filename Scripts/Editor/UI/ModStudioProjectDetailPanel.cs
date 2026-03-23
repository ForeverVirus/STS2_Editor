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
    private Label? _selectedNodeTypeLabel;
    private Label? _selectedNodeIdLabel;
    private Label? _selectedNodeDisplayNameLabel;
    private LineEdit? _selectedNodeDisplayNameEdit;
    private Label? _selectedNodeDescriptionLabel;
    private TextEdit? _selectedNodeDescriptionEdit;
    private Label? _selectedNodePropertiesLabel;
    private VBoxContainer? _selectedNodePropertyHost;

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

    public void SetSelectedNode(BehaviorGraphNodeDefinition? node)
    {
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
    }

    public void SetSelectedNodeProperties(IReadOnlyDictionary<string, string> properties)
    {
        if (_selectedNodePropertyHost == null)
        {
            return;
        }

        _propertyEditors.Clear();
        foreach (var child in _selectedNodePropertyHost.GetChildren())
        {
            child.QueueFree();
        }

        if (properties.Count == 0)
        {
            _selectedNodePropertyHost.AddChild(MakeLabel(Dual("当前节点没有额外属性。", "The selected node has no extra properties."), true));
            return;
        }

        foreach (var pair in properties.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            var row = new VBoxContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            row.AddThemeConstantOverride("separation", 4);
            row.AddChild(MakeLabel(ModStudioFieldDisplayNames.Get(pair.Key), true));

            var editor = BuildPropertyEditor(pair.Key, pair.Value ?? string.Empty);
            row.AddChild(editor);
            _propertyEditors[pair.Key] = editor;
            _selectedNodePropertyHost.AddChild(row);
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
            _runtimeAssetsTabButton.Text = Dual("游戏内素材", "Game Assets");
        }

        if (_importedAssetsTabButton != null)
        {
            _importedAssetsTabButton.Text = Dual("导入的外部素材", "Imported Assets");
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
            _selectedNodePropertiesLabel.Text = Dual("节点属性", "Node Properties");
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
        var page = new VBoxContainer
        {
            Name = "GraphTab",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        page.AddThemeConstantOverride("separation", 8);

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

        _selectedNodePropertiesLabel = MakeLabel(string.Empty, true);
        page.AddChild(_selectedNodePropertiesLabel);

        var propertyScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        _selectedNodePropertyHost = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        _selectedNodePropertyHost.AddThemeConstantOverride("separation", 6);
        propertyScroll.AddChild(_selectedNodePropertyHost);
        page.AddChild(propertyScroll);

        return page;
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
        if (TryCreateChoiceEditor(propertyKey, propertyValue, out var choiceEditor))
        {
            return choiceEditor;
        }

        if (IsIntegerProperty(propertyKey))
        {
            var spinBox = new SpinBox
            {
                MinValue = -9999,
                MaxValue = 9999,
                Step = 1,
                Rounded = true,
                AllowGreater = true,
                AllowLesser = true,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            if (double.TryParse(propertyValue, out var numericValue))
            {
                spinBox.Value = numericValue;
            }
            spinBox.ValueChanged += value => NodePropertyChanged?.Invoke(propertyKey, Math.Round(value).ToString());
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

    private bool TryCreateChoiceEditor(string propertyKey, string propertyValue, out Control editor)
    {
        editor = null!;
        if (!TryGetChoiceOptions(propertyKey, out var choices))
        {
            return false;
        }

        var optionButton = new OptionButton
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };

        var selectedIndex = -1;
        var index = 0;
        foreach (var choice in choices)
        {
            optionButton.AddItem(choice.DisplayText);
            optionButton.SetItemMetadata(index, choice.Value);
            if (string.Equals(choice.Value, propertyValue, StringComparison.Ordinal))
            {
                selectedIndex = index;
            }
            index++;
        }

        if (selectedIndex < 0 && !string.IsNullOrWhiteSpace(propertyValue))
        {
            optionButton.AddItem($"{ModStudioFieldDisplayNames.FormatGraphPropertyValue(propertyKey, propertyValue)} [{propertyValue}]");
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

        optionButton.ItemSelected += itemIndex =>
        {
            var selectedValue = optionButton.GetItemMetadata((int)itemIndex).AsString();
            NodePropertyChanged?.Invoke(propertyKey, selectedValue);
        };

        editor = optionButton;
        return true;
    }

    private static bool TryGetChoiceOptions(string propertyKey, out IReadOnlyList<PropertyChoice> choices)
    {
        choices = Array.Empty<PropertyChoice>();
        switch (propertyKey)
        {
            case "target":
                choices =
                [
                    new PropertyChoice("self", Dual("自身", "Self")),
                    new PropertyChoice("current_target", Dual("当前目标", "Current Target")),
                    new PropertyChoice("all_enemies", Dual("全体敌人", "All Enemies")),
                    new PropertyChoice("all_allies", Dual("全体友方", "All Allies")),
                    new PropertyChoice("all_targets", Dual("所有目标", "All Targets"))
                ];
                return true;
            case "props":
            {
                var results = new List<PropertyChoice>
                {
                    new("none", Dual("无", "None"))
                };
                results.AddRange(Enum.GetNames<ValueProp>()
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .Select(name => new PropertyChoice(name, ModStudioFieldDisplayNames.FormatGraphPropertyValue(propertyKey, name))));
                choices = results;
                return true;
            }
            default:
                return false;
        }
    }

    private static bool IsIntegerProperty(string propertyKey)
    {
        return propertyKey is "amount";
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
