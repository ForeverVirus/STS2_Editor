using Godot;
using STS2_Editor.Scripts.Editor.Core.Models;
using static STS2_Editor.Scripts.Editor.UI.ModStudioUiFactory;

namespace STS2_Editor.Scripts.Editor.UI;

internal sealed class ModStudioGraphImportCandidate
{
    public string EntityId { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public bool IsProjectOnly { get; init; }

    public bool IsCurrentEntity { get; init; }

    public EntityBrowserItem? SourceItem { get; init; }
}

internal sealed partial class ModStudioGraphImportPickerDialog : PopupPanel
{
    private readonly List<ModStudioGraphImportCandidate> _candidates = new();
    private readonly List<ModStudioGraphImportCandidate> _visibleCandidates = new();

    private LineEdit? _searchEdit;
    private ItemList? _itemList;
    private RichTextLabel? _detailsLabel;
    private Button? _importButton;

    public event Action<ModStudioGraphImportCandidate>? CandidateConfirmed;

    public override void _Ready()
    {
        BuildUi();
        RefreshTexts();
    }

    public void SetCandidates(IEnumerable<ModStudioGraphImportCandidate> candidates)
    {
        _candidates.Clear();
        _candidates.AddRange(candidates);
        RefreshList();
    }

    public void RefreshTexts()
    {
        if (_searchEdit != null)
        {
            _searchEdit.PlaceholderText = Dual("搜索名称包含...", "Search contains...");
        }

        if (_importButton != null)
        {
            _importButton.Text = Dual("导入所选 Graph", "Import Selected Graph");
        }
    }

    public void ShowDialog()
    {
        BuildUi();
        RefreshTexts();
        RefreshList();
        PopupCentered(new Vector2I(760, 560));
        _searchEdit?.GrabFocus();
    }

    private void BuildUi()
    {
        if (GetChildCount() > 0)
        {
            return;
        }

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_top", 12);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_bottom", 12);
        AddChild(margin);

        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", 8);
        margin.AddChild(root);

        root.AddChild(MakeLabel(Dual("选择要导入 Graph 的对象", "Choose a source graph to import"), true));

        _searchEdit = new LineEdit
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _searchEdit.TextChanged += _ => RefreshList();
        root.AddChild(_searchEdit);

        var body = new HSplitContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SplitOffset = 360
        };
        root.AddChild(body);

        _itemList = new ItemList
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SelectMode = ItemList.SelectModeEnum.Single
        };
        _itemList.ItemSelected += _ => RefreshDetails();
        _itemList.ItemActivated += _ => ConfirmSelection();
        body.AddChild(_itemList);

        _detailsLabel = MakeDetails(string.Empty, scrollActive: true, fitContent: false, minHeight: 260f);
        body.AddChild(_detailsLabel);

        var actions = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        actions.AddThemeConstantOverride("separation", 8);
        root.AddChild(actions);

        actions.AddChild(MakeButton(Dual("取消", "Cancel"), Hide));
        _importButton = MakeButton(string.Empty, ConfirmSelection);
        actions.AddChild(_importButton);
    }

    private void RefreshList()
    {
        if (_itemList == null)
        {
            return;
        }

        _visibleCandidates.Clear();
        _itemList.Clear();

        var search = _searchEdit?.Text ?? string.Empty;
        foreach (var candidate in _candidates
                     .Where(candidate =>
                         string.IsNullOrWhiteSpace(search) ||
                         candidate.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                         candidate.EntityId.Contains(search, StringComparison.OrdinalIgnoreCase))
                     .OrderByDescending(candidate => candidate.IsProjectOnly)
                     .ThenBy(candidate => candidate.Title, StringComparer.OrdinalIgnoreCase))
        {
            _visibleCandidates.Add(candidate);

            var tags = new List<string>();
            tags.Add(candidate.IsProjectOnly ? Dual("项目", "Project") : Dual("游戏", "Game"));
            if (candidate.IsCurrentEntity)
            {
                tags.Add(Dual("当前", "Current"));
            }

            var display = $"{candidate.Title} [{candidate.EntityId}]";
            if (tags.Count > 0)
            {
                display = $"{display}  ·  {string.Join(" / ", tags)}";
            }

            _itemList.AddItem(display);
        }

        if (_itemList.ItemCount > 0 && _itemList.GetSelectedItems().Length == 0)
        {
            _itemList.Select(0);
        }

        RefreshDetails();
    }

    private void RefreshDetails()
    {
        if (_detailsLabel == null || _itemList == null)
        {
            return;
        }

        var candidate = GetSelectedCandidate();
        if (candidate == null)
        {
            _detailsLabel.Text = Dual("请选择一个对象作为 Graph 导入源。", "Choose an entry to use as the graph import source.");
            return;
        }

        _detailsLabel.Text = string.Join(System.Environment.NewLine, new[]
        {
            $"{Dual("标题", "Title")}: {candidate.Title}",
            $"{Dual("实体 ID", "Entity Id")}: {candidate.EntityId}",
            $"{Dual("来源", "Source")}: {(candidate.IsProjectOnly ? Dual("项目条目", "Project Entry") : Dual("游戏内条目", "Game Entry"))}",
            $"{Dual("说明", "Summary")}: {candidate.Summary}"
        });
    }

    private void ConfirmSelection()
    {
        var candidate = GetSelectedCandidate();
        if (candidate == null)
        {
            return;
        }

        Hide();
        CandidateConfirmed?.Invoke(candidate);
    }

    private ModStudioGraphImportCandidate? GetSelectedCandidate()
    {
        if (_itemList == null || _itemList.GetSelectedItems().Length == 0)
        {
            return null;
        }

        var index = _itemList.GetSelectedItems()[0];
        return index >= 0 && index < _visibleCandidates.Count
            ? _visibleCandidates[index]
            : null;
    }
}
