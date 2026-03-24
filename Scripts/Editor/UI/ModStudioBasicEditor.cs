using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using Godot;
using static STS2_Editor.Scripts.Editor.UI.ModStudioUiFactory;

namespace STS2_Editor.Scripts.Editor.UI;

internal sealed partial class ModStudioBasicEditor : MarginContainer
{
    private readonly Dictionary<string, Control> _fieldControls = new(StringComparer.OrdinalIgnoreCase);
    private VBoxContainer? _fieldHost;
    private Label? _titleLabel;
    private Button? _saveButton;
    private Button? _revertButton;
    private bool _suppressFieldChanged;

    public event Action? SaveRequested;
    public event Action? RevertRequested;
    public event Action? FieldChanged;

    public override void _Ready()
    {
        EnsureBuilt();
    }

    public void EnsureBuilt()
    {
        BuildUi();
        RefreshTexts();
    }

    public void BindMetadata(string title, IReadOnlyDictionary<string, string> metadata)
    {
        if (_titleLabel != null)
        {
            _titleLabel.Text = title;
        }

        _fieldControls.Clear();
        if (_fieldHost == null)
        {
            return;
        }

        foreach (var child in _fieldHost.GetChildren())
        {
            child.QueueFree();
        }

        foreach (var pair in metadata.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            _fieldHost.AddChild(BuildFieldRow(pair.Key, pair.Value ?? string.Empty));
        }
    }

    public Dictionary<string, string> GetFieldValues()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in _fieldControls)
        {
            values[pair.Key] = pair.Value switch
            {
                LineEdit lineEdit => lineEdit.Text ?? string.Empty,
                TextEdit textEdit => textEdit.Text ?? string.Empty,
                CheckBox checkBox => checkBox.ButtonPressed.ToString(),
                ChoiceFieldEditor choiceFieldEditor => choiceFieldEditor.GetSerializedValue(),
                ListFieldEditor listFieldEditor => listFieldEditor.GetSerializedValue(),
                _ => string.Empty
            };
        }

        return values;
    }

    public bool TryGetFieldValue(string key, out string value)
    {
        value = string.Empty;
        if (!_fieldControls.TryGetValue(key, out var control))
        {
            return false;
        }

        value = control switch
        {
            LineEdit lineEdit => lineEdit.Text ?? string.Empty,
            TextEdit textEdit => textEdit.Text ?? string.Empty,
            CheckBox checkBox => checkBox.ButtonPressed.ToString(),
            ChoiceFieldEditor choiceFieldEditor => choiceFieldEditor.GetSerializedValue(),
            ListFieldEditor listFieldEditor => listFieldEditor.GetSerializedValue(),
            _ => string.Empty
        };
        return true;
    }

    public bool TrySetFieldValue(string key, string value, bool raiseChanged = false)
    {
        if (!_fieldControls.TryGetValue(key, out var control))
        {
            return false;
        }

        WithSuppressedFieldChanged(() =>
        {
            switch (control)
            {
                case LineEdit lineEdit:
                    lineEdit.Text = value ?? string.Empty;
                    break;
                case TextEdit textEdit:
                    textEdit.Text = value ?? string.Empty;
                    break;
                case CheckBox checkBox when bool.TryParse(value, out var boolValue):
                    checkBox.ButtonPressed = boolValue;
                    break;
                case ChoiceFieldEditor choiceFieldEditor:
                    choiceFieldEditor.SetValue(value ?? string.Empty);
                    break;
            }
        });

        if (raiseChanged)
        {
            RaiseFieldChanged();
        }

        return true;
    }

    public void RefreshTexts()
    {
        if (_saveButton != null)
        {
            _saveButton.Text = Dual("保存", "Save");
        }

        if (_revertButton != null)
        {
            _revertButton.Text = Dual("还原", "Revert");
        }

        foreach (var listEditor in _fieldControls.Values.OfType<ListFieldEditor>())
        {
            listEditor.RefreshTexts();
        }

        foreach (var choiceEditor in _fieldControls.Values.OfType<ChoiceFieldEditor>())
        {
            choiceEditor.RefreshTexts();
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

        var actions = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        actions.AddThemeConstantOverride("separation", 8);
        _saveButton = MakeButton(string.Empty, () => SaveRequested?.Invoke());
        _revertButton = MakeButton(string.Empty, () => RevertRequested?.Invoke());
        actions.AddChild(_saveButton);
        actions.AddChild(_revertButton);
        root.AddChild(actions);

        _titleLabel = MakeLabel(Dual("基础字段", "Basic Fields"), true);
        root.AddChild(_titleLabel);

        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        root.AddChild(scroll);

        _fieldHost = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        _fieldHost.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(_fieldHost);
    }

    private Control BuildFieldRow(string key, string value)
    {
        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", 4);

        var label = MakeLabel(ModStudioFieldDisplayNames.Get(key), true);
        root.AddChild(label);

        var control = CreateFieldControl(key, value);
        _fieldControls[key] = control;
        root.AddChild(control);
        return root;
    }

    private Control CreateFieldControl(string key, string value)
    {
        if (TryCreateListFieldEditor(key, value, out var listFieldEditor))
        {
            listFieldEditor.ValueChanged += RaiseFieldChanged;
            return listFieldEditor;
        }

        if (TryCreateChoiceFieldEditor(key, value, out var choiceFieldEditor))
        {
            choiceFieldEditor.ValueChanged += RaiseFieldChanged;
            return choiceFieldEditor;
        }

        if (bool.TryParse(value, out var boolValue))
        {
            var checkBox = new CheckBox
            {
                ButtonPressed = boolValue,
                Text = Dual("启用", "Enabled"),
                SizeFlagsHorizontal = Control.SizeFlags.Fill
            };
            checkBox.Toggled += _ => RaiseFieldChanged();
            return checkBox;
        }

        if (IsMultilineField(key))
        {
            var textEdit = new TextEdit
            {
                Text = value,
                CustomMinimumSize = new Vector2(0f, 92f),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.Fill
            };
            textEdit.TextChanged += RaiseFieldChanged;
            return textEdit;
        }

        var lineEdit = new LineEdit
        {
            Text = value,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        lineEdit.TextChanged += _ => RaiseFieldChanged();
        return lineEdit;
    }

    private static bool TryCreateListFieldEditor(string key, string value, out ListFieldEditor editor)
    {
        editor = null!;
        if (!TryGetListOptions(key, out var options))
        {
            return false;
        }

        editor = new ListFieldEditor(options, ParseListValue(value));
        return true;
    }

    private static bool TryCreateChoiceFieldEditor(string key, string value, out ChoiceFieldEditor editor)
    {
        editor = null!;
        var options = FieldChoiceProvider.GetBasicChoices(key);
        if (options.Count == 0)
        {
            return false;
        }

        editor = new ChoiceFieldEditor(key, options, value);
        return true;
    }

    private static bool TryGetListOptions(string key, out IReadOnlyList<FieldOption> options)
    {
        options = Array.Empty<FieldOption>();
        switch (key)
        {
            case "starting_deck_ids":
                options = ModelDb.AllCards
                    .OrderBy(card => card.Title, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(card => card.Id.Entry, StringComparer.OrdinalIgnoreCase)
                    .Select(card => new FieldOption(card.Id.Entry, BuildOptionDisplay(card.Title, card.Id.Entry)))
                    .ToList();
                return true;
            case "starting_relic_ids":
                options = ModelDb.AllRelics
                    .OrderBy(relic => SafeLocText(relic.Title), StringComparer.OrdinalIgnoreCase)
                    .ThenBy(relic => relic.Id.Entry, StringComparer.OrdinalIgnoreCase)
                    .Select(relic => new FieldOption(relic.Id.Entry, BuildOptionDisplay(SafeLocText(relic.Title), relic.Id.Entry)))
                    .ToList();
                return true;
            case "starting_potion_ids":
                options = ModelDb.AllPotions
                    .OrderBy(potion => SafeLocText(potion.Title), StringComparer.OrdinalIgnoreCase)
                    .ThenBy(potion => potion.Id.Entry, StringComparer.OrdinalIgnoreCase)
                    .Select(potion => new FieldOption(potion.Id.Entry, BuildOptionDisplay(SafeLocText(potion.Title), potion.Id.Entry)))
                    .ToList();
                return true;
            default:
                return false;
        }
    }

    private static IReadOnlyList<string> ParseListValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        return value
            .Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(entry => entry.Trim())
            .Where(entry => !string.IsNullOrWhiteSpace(entry))
            .ToList();
    }

    private static string BuildOptionDisplay(string title, string id)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return id;
        }

        return string.Equals(title, id, StringComparison.OrdinalIgnoreCase)
            ? title
            : $"{title} [{id}]";
    }

    private static string SafeLocText(LocString? locString)
    {
        if (locString == null)
        {
            return string.Empty;
        }

        try
        {
            return locString.GetRawText();
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool IsMultilineField(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        return key.Contains("description", StringComparison.OrdinalIgnoreCase) ||
               key.Contains("notes", StringComparison.OrdinalIgnoreCase) ||
               key.Contains("text", StringComparison.OrdinalIgnoreCase);
    }

    private void RaiseFieldChanged()
    {
        if (!_suppressFieldChanged)
        {
            FieldChanged?.Invoke();
        }
    }

    private void WithSuppressedFieldChanged(Action action)
    {
        var previous = _suppressFieldChanged;
        _suppressFieldChanged = true;
        try
        {
            action();
        }
        finally
        {
            _suppressFieldChanged = previous;
        }
    }

    private sealed partial class ListFieldEditor : VBoxContainer
    {
        private readonly IReadOnlyList<FieldOption> _options;
        private readonly List<ListRow> _rows = new();
        private readonly VBoxContainer _rowsHost;
        private readonly Button _addButton;

        public ListFieldEditor(IReadOnlyList<FieldOption> options, IReadOnlyList<string> selectedIds)
        {
            _options = options;
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            AddThemeConstantOverride("separation", 6);

            _rowsHost = new VBoxContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            _rowsHost.AddThemeConstantOverride("separation", 4);
            AddChild(_rowsHost);

            _addButton = MakeButton(string.Empty, () => AddRow(null));
            AddChild(_addButton);

            if (selectedIds.Count == 0)
            {
                AddRow(null);
            }
            else
            {
                foreach (var selectedId in selectedIds)
                {
                    AddRow(selectedId);
                }
            }

            RefreshTexts();
        }

        public event Action? ValueChanged;

        public string GetSerializedValue()
        {
            return string.Join(", ", _rows
                .Select(row => row.GetSelectedId())
                .Where(value => !string.IsNullOrWhiteSpace(value)));
        }

        public void RefreshTexts()
        {
            _addButton.Text = Dual("新增一项", "Add Item");
            foreach (var row in _rows)
            {
                row.RefreshTexts();
            }
        }

        private void AddRow(string? selectedId)
        {
            var row = new ListRow(_options, selectedId);
            row.SelectionChanged += () => ValueChanged?.Invoke();
            row.RemoveRequested += () => RemoveRow(row);
            _rows.Add(row);
            _rowsHost.AddChild(row);
            RefreshTexts();
            ValueChanged?.Invoke();
        }

        private void RemoveRow(ListRow row)
        {
            if (_rows.Count <= 1)
            {
                row.ClearSelection();
                ValueChanged?.Invoke();
                return;
            }

            _rows.Remove(row);
            _rowsHost.RemoveChild(row);
            row.QueueFree();
            ValueChanged?.Invoke();
        }
    }

    private sealed partial class ChoiceFieldEditor : VBoxContainer
    {
        private readonly string _key;
        private readonly OptionButton _picker;
        private string _currentValue;

        public ChoiceFieldEditor(string key, IReadOnlyList<(string Value, string Display)> options, string value)
        {
            _key = key;
            _currentValue = value ?? string.Empty;
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            _picker = new OptionButton
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            _picker.ItemSelected += OnItemSelected;
            AddChild(_picker);

            RefreshTexts(options);
            SetValue(_currentValue);
        }

        public event Action? ValueChanged;

        public string GetSerializedValue()
        {
            if (_picker.Selected < 0 || _picker.Selected >= _picker.ItemCount)
            {
                return _currentValue;
            }

            return _picker.GetItemMetadata(_picker.Selected).AsString();
        }

        public void SetValue(string value)
        {
            _currentValue = value ?? string.Empty;
            for (var index = 0; index < _picker.ItemCount; index++)
            {
                if (string.Equals(_picker.GetItemMetadata(index).AsString(), _currentValue, StringComparison.Ordinal))
                {
                    _picker.Select(index);
                    return;
                }
            }

            if (!string.IsNullOrWhiteSpace(_currentValue))
            {
                _picker.AddItem(BuildDisplayText(_currentValue, _currentValue));
                _picker.SetItemMetadata(_picker.ItemCount - 1, _currentValue);
                _picker.Select(_picker.ItemCount - 1);
            }
            else if (_picker.ItemCount > 0)
            {
                _picker.Select(0);
            }
        }

        public void RefreshTexts()
        {
            RefreshTexts(FieldChoiceProvider.GetBasicChoices(_key));
        }

        private void RefreshTexts(IReadOnlyList<(string Value, string Display)> options)
        {
            var selected = GetSerializedValue();
            _picker.Clear();

            foreach (var option in options)
            {
                _picker.AddItem(option.Display);
                _picker.SetItemMetadata(_picker.ItemCount - 1, option.Value);
            }

            if (!string.IsNullOrWhiteSpace(selected))
            {
                SetValue(selected);
                return;
            }

            if (_picker.ItemCount > 0)
            {
                _picker.Select(0);
            }
        }

        private void OnItemSelected(long index)
        {
            if (index < 0 || index >= _picker.ItemCount)
            {
                return;
            }

            _currentValue = _picker.GetItemMetadata((int)index).AsString();
            ValueChanged?.Invoke();
        }

        private static string BuildDisplayText(string value, string fallback)
        {
            var display = ModStudioFieldDisplayNames.FormatValue(value);
            if (string.IsNullOrWhiteSpace(display))
            {
                display = fallback;
            }

            return display == value ? display : $"{display} [{value}]";
        }
    }

    private sealed partial class ListRow : HBoxContainer
    {
        private readonly OptionButton _picker;
        private readonly Button _removeButton;

        public ListRow(IReadOnlyList<FieldOption> options, string? selectedId)
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            AddThemeConstantOverride("separation", 6);

            _picker = new OptionButton
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };

            _picker.AddItem(Dual("未选择", "Unselected"));
            _picker.SetItemMetadata(0, string.Empty);

            foreach (var option in options)
            {
                _picker.AddItem(option.DisplayText);
                _picker.SetItemMetadata(_picker.ItemCount - 1, option.Id);
            }

            if (!string.IsNullOrWhiteSpace(selectedId))
            {
                var found = false;
                for (var index = 0; index < _picker.ItemCount; index++)
                {
                    if (string.Equals(_picker.GetItemMetadata(index).AsString(), selectedId, StringComparison.Ordinal))
                    {
                        _picker.Select(index);
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    _picker.AddItem(selectedId);
                    _picker.SetItemMetadata(_picker.ItemCount - 1, selectedId);
                    _picker.Select(_picker.ItemCount - 1);
                }
            }
            else
            {
                _picker.Select(0);
            }

            _picker.ItemSelected += _ => SelectionChanged?.Invoke();
            AddChild(_picker);

            _removeButton = MakeButton(string.Empty, () => RemoveRequested?.Invoke());
            _removeButton.CustomMinimumSize = new Vector2(84f, 30f);
            AddChild(_removeButton);
        }

        public event Action? SelectionChanged;
        public event Action? RemoveRequested;

        public string GetSelectedId()
        {
            if (_picker.Selected < 0 || _picker.Selected >= _picker.ItemCount)
            {
                return string.Empty;
            }

            return _picker.GetItemMetadata(_picker.Selected).AsString();
        }

        public void ClearSelection()
        {
            _picker.Select(0);
        }

        public void RefreshTexts()
        {
            _picker.SetItemText(0, Dual("未选择", "Unselected"));
            _removeButton.Text = Dual("删除", "Remove");
        }
    }

    private sealed record FieldOption(string Id, string DisplayText);
}
