using Godot;
using STS2_Editor.Scripts.Editor.Core.Models;
using static STS2_Editor.Scripts.Editor.UI.ModStudioUiFactory;

namespace STS2_Editor.Scripts.Editor.UI;

internal sealed partial class ModStudioEntityBrowserPanel : PanelContainer
{
    private readonly Dictionary<ModStudioEntityKind, Button> _kindButtons = new();
    private readonly List<EntityBrowserItem> _allItems = new();
    private VBoxContainer? _itemsHost;
    private LineEdit? _searchEdit;
    private Button? _allScopeButton;
    private Button? _modifiedScopeButton;
    private Button? _newScopeButton;
    private Button? _newEntryButton;
    private Button? _refreshButton;
    private ModStudioEntityKind _selectedKind = ModStudioEntityKind.Character;
    private string _scope = "all";
    private string _searchText = string.Empty;
    private string? _selectedEntityId;

    public event Action<ModStudioEntityKind>? KindChanged;
    public event Action<string>? SearchChanged;
    public event Action<string>? ScopeChanged;
    public event Action<EntityBrowserItem>? ItemSelected;
    public event Action? CreateEntryRequested;

    public override void _Ready()
    {
        BuildUi();
        RefreshTexts();
    }

    public void SetSelection(ModStudioEntityKind kind, string? entityId = null)
    {
        _selectedKind = kind;
        _selectedEntityId = entityId;
        foreach (var pair in _kindButtons)
        {
            pair.Value.ButtonPressed = pair.Key == kind;
        }

        RefreshItemList();
    }

    public void BindItems(IEnumerable<EntityBrowserItem> items)
    {
        _allItems.Clear();
        _allItems.AddRange(items);
        RefreshItemList();
    }

    public void SetSearchText(string text)
    {
        _searchText = text ?? string.Empty;
        if (_searchEdit != null && _searchEdit.Text != _searchText)
        {
            _searchEdit.Text = _searchText;
        }
        RefreshItemList();
    }

    public void RefreshTexts()
    {
        foreach (var pair in _kindButtons)
        {
            pair.Value.Text = pair.Key switch
            {
                ModStudioEntityKind.Character => Dual("角色", "Characters"),
                ModStudioEntityKind.Card => Dual("卡牌", "Cards"),
                ModStudioEntityKind.Relic => Dual("遗物", "Relics"),
                ModStudioEntityKind.Potion => Dual("药水", "Potions"),
                ModStudioEntityKind.Event => Dual("事件", "Events"),
                ModStudioEntityKind.Enchantment => Dual("附魔", "Enchantments"),
                _ => pair.Key.ToString()
            };
        }

        if (_searchEdit != null)
        {
            _searchEdit.PlaceholderText = Dual("搜索名称包含...", "Search contains...");
        }

        if (_allScopeButton != null) _allScopeButton.Text = Dual("全部", "All");
        if (_modifiedScopeButton != null) _modifiedScopeButton.Text = Dual("已修改", "Modified");
        if (_newScopeButton != null) _newScopeButton.Text = Dual("项目新建", "Project New");
        if (_newEntryButton != null) _newEntryButton.Text = Dual("新建条目", "New Entry");
        if (_refreshButton != null) _refreshButton.Text = Dual("刷新", "Refresh");

        RefreshItemList();
    }

    private void BuildUi()
    {
        if (GetChildCount() > 0)
        {
            return;
        }

        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", 8);
        AddChild(root);

        root.AddChild(MakeLabel(Dual("实体列表", "Entity Browser"), true));

        var kindRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        kindRow.AddThemeConstantOverride("separation", 4);
        root.AddChild(kindRow);

        foreach (var kind in new[] { ModStudioEntityKind.Character, ModStudioEntityKind.Card, ModStudioEntityKind.Relic, ModStudioEntityKind.Potion, ModStudioEntityKind.Event, ModStudioEntityKind.Enchantment })
        {
            var button = MakeButton(string.Empty, () => SelectKind(kind), toggle: true);
            button.CustomMinimumSize = new Vector2(0f, 34f);
            _kindButtons[kind] = button;
            kindRow.AddChild(button);
        }

        _searchEdit = new LineEdit { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _searchEdit.PlaceholderText = Dual("搜索名称包含...", "Search contains...");
        _searchEdit.TextChanged += OnSearchChanged;
        root.AddChild(_searchEdit);

        var scopeRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scopeRow.AddThemeConstantOverride("separation", 4);
        root.AddChild(scopeRow);

        _allScopeButton = MakeButton(string.Empty, () => SetScope("all"), toggle: true);
        _modifiedScopeButton = MakeButton(string.Empty, () => SetScope("modified"), toggle: true);
        _newScopeButton = MakeButton(string.Empty, () => SetScope("new"), toggle: true);
        scopeRow.AddChild(_allScopeButton);
        scopeRow.AddChild(_modifiedScopeButton);
        scopeRow.AddChild(_newScopeButton);

        var actionRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        actionRow.AddThemeConstantOverride("separation", 4);
        root.AddChild(actionRow);
        _newEntryButton = MakeButton(string.Empty, () => CreateEntryRequested?.Invoke());
        _refreshButton = MakeButton(string.Empty, RefreshItemList);
        actionRow.AddChild(_newEntryButton);
        actionRow.AddChild(_refreshButton);

        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        root.AddChild(scroll);

        _itemsHost = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        _itemsHost.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(_itemsHost);

        SetSelection(_selectedKind);
    }

    private void SelectKind(ModStudioEntityKind kind)
    {
        if (_selectedKind == kind)
        {
            return;
        }

        _selectedKind = kind;
        _selectedEntityId = null;
        foreach (var pair in _kindButtons)
        {
            pair.Value.ButtonPressed = pair.Key == kind;
        }

        KindChanged?.Invoke(kind);
        RefreshItemList();
    }

    private void SetScope(string scope)
    {
        _scope = scope;
        if (_allScopeButton != null) _allScopeButton.ButtonPressed = scope == "all";
        if (_modifiedScopeButton != null) _modifiedScopeButton.ButtonPressed = scope == "modified";
        if (_newScopeButton != null) _newScopeButton.ButtonPressed = scope == "new";
        ScopeChanged?.Invoke(scope);
        RefreshItemList();
    }

    private void OnSearchChanged(string text)
    {
        _searchText = text ?? string.Empty;
        SearchChanged?.Invoke(_searchText);
        RefreshItemList();
    }

    private void RefreshItemList()
    {
        if (_itemsHost == null)
        {
            return;
        }

        foreach (var child in _itemsHost.GetChildren())
        {
            child.QueueFree();
        }

        var filtered = _allItems.Where(item => item.Kind == _selectedKind);
        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            filtered = filtered.Where(item =>
                Contains(item.Title, _searchText) ||
                Contains(item.EntityId, _searchText) ||
                Contains(item.Summary, _searchText) ||
                Contains(item.DetailText, _searchText));
        }

        filtered = _scope switch
        {
            "modified" => filtered.Where(item => item.IsProjectOnly || item.DetailText.Contains("override", StringComparison.OrdinalIgnoreCase)),
            "new" => filtered.Where(item => item.IsProjectOnly),
            _ => filtered
        };

        foreach (var item in filtered.OrderBy(item => item.Title, StringComparer.OrdinalIgnoreCase))
        {
            var button = MakeButton($"{item.Title}\n{item.Summary}", () => ItemSelected?.Invoke(item), toggle: true);
            button.ButtonPressed = string.Equals(item.EntityId, _selectedEntityId, StringComparison.OrdinalIgnoreCase);
            button.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            button.CustomMinimumSize = new Vector2(0f, 52f);
            _itemsHost.AddChild(button);
        }
    }

    private static bool Contains(string? source, string? value)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return source.Contains(value, StringComparison.OrdinalIgnoreCase);
    }
}
