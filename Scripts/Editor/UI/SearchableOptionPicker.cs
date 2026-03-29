using Godot;
using static STS2_Editor.Scripts.Editor.UI.ModStudioUiFactory;

namespace STS2_Editor.Scripts.Editor.UI;

internal sealed partial class SearchableOptionPicker : HBoxContainer
{
    private readonly bool _allowEmptySelection;
    private readonly string _emptyDisplayZh;
    private readonly string _emptyDisplayEn;
    private readonly Func<string, string>? _fallbackDisplayFactory;
    private readonly bool _useSearchDialog;
    private readonly OptionButton? _optionButton;
    private readonly Button? _selectButton;
    private readonly ChoiceSearchDialog? _searchDialog;

    private List<(string Value, string Display)> _choices = new();
    private string _currentValue = string.Empty;
    private bool _suppressChanges;

    public SearchableOptionPicker(
        IReadOnlyList<(string Value, string Display)> choices,
        string? value = null,
        bool allowEmptySelection = false,
        string emptyDisplayZh = "未选择",
        string emptyDisplayEn = "Unselected",
        Func<string, string>? fallbackDisplayFactory = null,
        int searchThreshold = 18)
    {
        _allowEmptySelection = allowEmptySelection;
        _emptyDisplayZh = emptyDisplayZh;
        _emptyDisplayEn = emptyDisplayEn;
        _fallbackDisplayFactory = fallbackDisplayFactory;
        _useSearchDialog = choices.Count >= searchThreshold;

        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        if (_useSearchDialog)
        {
            _selectButton = new Button
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                FocusMode = FocusModeEnum.All,
                CustomMinimumSize = new Vector2(0f, 30f),
                Alignment = HorizontalAlignment.Left,
                TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis
            };
            _selectButton.Pressed += ShowSearchDialog;
            AddChild(_selectButton);

            _searchDialog = new ChoiceSearchDialog();
            _searchDialog.ChoiceConfirmed += HandleDialogChoiceConfirmed;
            AddChild(_searchDialog);
        }
        else
        {
            _optionButton = new OptionButton
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            _optionButton.ItemSelected += HandleOptionSelected;
            AddChild(_optionButton);
        }

        SetChoices(choices);
        SetValue(value ?? string.Empty);
    }

    public event Action<string>? ValueChanged;

    public string GetValue() => _currentValue;

    public void SetChoices(IReadOnlyList<(string Value, string Display)> choices)
    {
        _choices = choices
            .Where(choice => !string.IsNullOrWhiteSpace(choice.Value))
            .GroupBy(choice => choice.Value, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();
        _currentValue = NormalizeValue(_currentValue);
        RefreshSelector();
    }

    public void SetValue(string? value, bool emitChanged = false)
    {
        var normalized = NormalizeValue(value ?? string.Empty);
        var changed = !string.Equals(_currentValue, normalized, StringComparison.Ordinal);
        _currentValue = normalized;
        RefreshSelector();

        if (emitChanged && changed)
        {
            ValueChanged?.Invoke(_currentValue);
        }
    }

    public void RefreshTexts()
    {
        RefreshSelector();
    }

    private void RefreshSelector()
    {
        var effectiveChoices = BuildEffectiveChoices();
        var selectedIndex = ResolveSelectedIndex(effectiveChoices);

        if (_useSearchDialog)
        {
            if (_selectButton != null)
            {
                _selectButton.Text = selectedIndex >= 0
                    ? effectiveChoices[selectedIndex].Display
                    : CurrentEmptyDisplay;
            }

            _searchDialog?.SetChoices(effectiveChoices, _currentValue);
        }
        else if (_optionButton != null)
        {
            _suppressChanges = true;
            try
            {
                _optionButton.Clear();
                foreach (var choice in effectiveChoices)
                {
                    _optionButton.AddItem(choice.Display);
                    _optionButton.SetItemMetadata(_optionButton.ItemCount - 1, choice.Value);
                }

                if (selectedIndex >= 0)
                {
                    _optionButton.Select(selectedIndex);
                }
                else if (_optionButton.ItemCount > 0)
                {
                    _optionButton.Select(0);
                }
            }
            finally
            {
                _suppressChanges = false;
            }
        }
    }

    private List<(string Value, string Display)> BuildEffectiveChoices()
    {
        var effective = new List<(string Value, string Display)>();
        if (_allowEmptySelection)
        {
            effective.Add((string.Empty, CurrentEmptyDisplay));
        }

        effective.AddRange(_choices);

        if (!string.IsNullOrWhiteSpace(_currentValue) &&
            effective.All(choice => !string.Equals(choice.Value, _currentValue, StringComparison.Ordinal)))
        {
            effective.Add((_currentValue, BuildFallbackDisplay(_currentValue)));
        }

        return effective;
    }

    private int ResolveSelectedIndex(IReadOnlyList<(string Value, string Display)> effectiveChoices)
    {
        for (var index = 0; index < effectiveChoices.Count; index++)
        {
            if (string.Equals(effectiveChoices[index].Value, _currentValue, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private string NormalizeValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            if (_allowEmptySelection)
            {
                return string.Empty;
            }

            return _choices.FirstOrDefault().Value ?? string.Empty;
        }

        return value;
    }

    private string BuildFallbackDisplay(string value)
    {
        if (_fallbackDisplayFactory != null)
        {
            return _fallbackDisplayFactory(value);
        }

        return value;
    }

    private string CurrentEmptyDisplay => Dual(_emptyDisplayZh, _emptyDisplayEn);

    private void ShowSearchDialog()
    {
        _searchDialog?.ShowDialog();
    }

    private void HandleDialogChoiceConfirmed(string value)
    {
        if (string.Equals(_currentValue, value, StringComparison.Ordinal))
        {
            RefreshSelector();
            return;
        }

        _currentValue = value;
        RefreshSelector();
        ValueChanged?.Invoke(_currentValue);
    }

    private void HandleOptionSelected(long index)
    {
        if (_suppressChanges || _optionButton == null || index < 0 || index >= _optionButton.ItemCount)
        {
            return;
        }

        var value = _optionButton.GetItemMetadata((int)index).AsString();
        if (string.Equals(_currentValue, value, StringComparison.Ordinal))
        {
            return;
        }

        _currentValue = value;
        ValueChanged?.Invoke(_currentValue);
    }

    private sealed partial class ChoiceSearchDialog : PopupPanel
    {
        private readonly List<(string Value, string Display)> _choices = new();
        private readonly List<(string Value, string Display)> _visibleChoices = new();

        private LineEdit? _searchEdit;
        private ItemList? _itemList;
        private Button? _confirmButton;
        private string _selectedValue = string.Empty;

        public event Action<string>? ChoiceConfirmed;

        public void SetChoices(IReadOnlyList<(string Value, string Display)> choices, string selectedValue)
        {
            _choices.Clear();
            _choices.AddRange(choices);
            _selectedValue = selectedValue ?? string.Empty;

            if (GetChildCount() > 0)
            {
                RefreshList();
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

            root.AddChild(MakeLabel(Dual("搜索并选择项目", "Search and choose an item"), true));

            _searchEdit = new LineEdit
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                ClearButtonEnabled = true
            };
            _searchEdit.TextChanged += _ => RefreshList();
            _searchEdit.TextSubmitted += _ => ConfirmSelection();
            root.AddChild(_searchEdit);

            _itemList = new ItemList
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                SelectMode = ItemList.SelectModeEnum.Single
            };
            _itemList.ItemActivated += _ => ConfirmSelection();
            root.AddChild(_itemList);

            var actions = new HBoxContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            actions.AddThemeConstantOverride("separation", 8);
            root.AddChild(actions);

            actions.AddChild(MakeButton(Dual("取消", "Cancel"), Hide));
            _confirmButton = MakeButton(Dual("选择", "Select"), ConfirmSelection);
            actions.AddChild(_confirmButton);
        }

        private void RefreshTexts()
        {
            if (_searchEdit != null)
            {
                _searchEdit.PlaceholderText = Dual("搜索名称或 ID", "Search by display or id");
            }

            if (_confirmButton != null)
            {
                _confirmButton.Text = Dual("选择", "Select");
            }
        }

        private void RefreshList()
        {
            if (_itemList == null)
            {
                return;
            }

            _visibleChoices.Clear();
            _itemList.Clear();

            var search = _searchEdit?.Text?.Trim() ?? string.Empty;
            foreach (var choice in _choices.Where(choice =>
                         string.IsNullOrWhiteSpace(search) ||
                         choice.Display.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                         choice.Value.Contains(search, StringComparison.OrdinalIgnoreCase)))
            {
                _visibleChoices.Add(choice);
                _itemList.AddItem(choice.Display);
            }

            var selectedIndex = _visibleChoices.FindIndex(choice =>
                string.Equals(choice.Value, _selectedValue, StringComparison.Ordinal));

            if (selectedIndex >= 0)
            {
                _itemList.Select(selectedIndex);
            }
            else if (_itemList.ItemCount > 0)
            {
                _itemList.Select(0);
            }
        }

        private void ConfirmSelection()
        {
            if (_itemList == null)
            {
                return;
            }

            var selectedItems = _itemList.GetSelectedItems();
            if (selectedItems.Length == 0)
            {
                return;
            }

            var index = selectedItems[0];
            if (index < 0 || index >= _visibleChoices.Count)
            {
                return;
            }

            Hide();
            ChoiceConfirmed?.Invoke(_visibleChoices[index].Value);
        }
    }
}
