using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using Godot;
using STS2_Editor.Scripts.Editor.Core.Models;
using STS2_Editor.Scripts.Editor.Core.Utilities;
using static STS2_Editor.Scripts.Editor.UI.ModStudioUiFactory;

namespace STS2_Editor.Scripts.Editor.UI;

internal sealed partial class ModStudioBasicEditor : MarginContainer
{
    private static readonly Dictionary<string, IReadOnlyList<FieldOption>> ListOptionCache = new(StringComparer.OrdinalIgnoreCase);
    private static bool? _cachedLanguageIsChinese;

    private readonly Dictionary<string, Control> _fieldControls = new(StringComparer.OrdinalIgnoreCase);
    private VBoxContainer? _fieldHost;
    private Label? _titleLabel;
    private Button? _saveButton;
    private Button? _revertButton;
    private bool _suppressFieldChanged;
    private ModStudioEntityKind _boundKind;
    private string _fieldLayoutSignature = string.Empty;

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

    public void BindMetadata(string title, ModStudioEntityKind kind, IReadOnlyDictionary<string, string> metadata)
    {
        _boundKind = kind;
        if (_titleLabel != null)
        {
            _titleLabel.Text = title;
        }

        if (_fieldHost == null)
        {
            return;
        }

        var signature = string.Join("|", metadata.Keys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase));
        if (string.Equals(_fieldLayoutSignature, signature, StringComparison.Ordinal))
        {
            ApplyFieldValues(metadata);
            return;
        }

        _fieldLayoutSignature = signature;
        _fieldControls.Clear();
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

    private bool TryCreateListFieldEditor(string key, string value, out ListFieldEditor editor)
    {
        editor = null!;
        if (!TryGetListOptions(key, out var options))
        {
            return false;
        }

        editor = new ListFieldEditor(options, ParseListValue(value));
        return true;
    }

    private bool TryCreateChoiceFieldEditor(string key, string value, out ChoiceFieldEditor editor)
    {
        editor = null!;
        var options = FieldChoiceProvider.GetBasicChoices(_boundKind, key);
        if (options.Count == 0)
        {
            return false;
        }

        editor = new ChoiceFieldEditor(_boundKind, key, options, value);
        return true;
    }

    private static bool TryGetListOptions(string key, out IReadOnlyList<FieldOption> options)
    {
        EnsureListOptionCacheLanguage();
        if (ListOptionCache.TryGetValue(key, out options!))
        {
            return true;
        }

        options = Array.Empty<FieldOption>();
        switch (key)
        {
            case "starting_deck_ids":
            {
                var cardOptions = ModelDb.AllCards
                    .Select(card => new FieldOption(card.Id.Entry, BuildOptionDisplay(card.Title, card.Id.Entry)))
                    .ToList();
                AppendProjectFieldOptions(cardOptions, ModStudioEntityKind.Card);
                options = cardOptions.OrderBy(o => o.DisplayText, StringComparer.OrdinalIgnoreCase).ThenBy(o => o.Id, StringComparer.OrdinalIgnoreCase).ToList();
                break;
            }
            case "starting_relic_ids":
            {
                var relicOptions = ModelDb.AllRelics
                    .Select(relic => new FieldOption(relic.Id.Entry, BuildOptionDisplay(SafeLocText(relic.Title), relic.Id.Entry)))
                    .ToList();
                AppendProjectFieldOptions(relicOptions, ModStudioEntityKind.Relic);
                options = relicOptions.OrderBy(o => o.DisplayText, StringComparer.OrdinalIgnoreCase).ThenBy(o => o.Id, StringComparer.OrdinalIgnoreCase).ToList();
                break;
            }
            case "starting_potion_ids":
            {
                var potionOptions = ModelDb.AllPotions
                    .Select(potion => new FieldOption(potion.Id.Entry, BuildOptionDisplay(SafeLocText(potion.Title), potion.Id.Entry)))
                    .ToList();
                AppendProjectFieldOptions(potionOptions, ModStudioEntityKind.Potion);
                options = potionOptions.OrderBy(o => o.DisplayText, StringComparer.OrdinalIgnoreCase).ThenBy(o => o.Id, StringComparer.OrdinalIgnoreCase).ToList();
                break;
            }
            default:
                return false;
        }

        ListOptionCache[key] = options;
        return true;
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

    private void ApplyFieldValues(IReadOnlyDictionary<string, string> metadata)
    {
        WithSuppressedFieldChanged(() =>
        {
            foreach (var pair in metadata)
            {
                if (!_fieldControls.TryGetValue(pair.Key, out var control))
                {
                    continue;
                }

                switch (control)
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
                    case ChoiceFieldEditor choiceFieldEditor:
                        choiceFieldEditor.SetValue(pair.Value ?? string.Empty);
                        break;
                    case ListFieldEditor listFieldEditor:
                        listFieldEditor.SetValues(ParseListValue(pair.Value));
                        break;
                }
            }
        });
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

    public static void InvalidateListOptionCache()
    {
        ListOptionCache.Clear();
    }

    private static void EnsureListOptionCacheLanguage()
    {
        if (_cachedLanguageIsChinese == ModStudioLocalization.IsChinese)
        {
            return;
        }

        _cachedLanguageIsChinese = ModStudioLocalization.IsChinese;
        ListOptionCache.Clear();
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

        public void SetValues(IReadOnlyList<string> selectedIds)
        {
            var normalized = selectedIds.Count == 0 ? new[] { string.Empty } : selectedIds.ToArray();

            while (_rows.Count < normalized.Length)
            {
                AddRow(null);
            }

            while (_rows.Count > normalized.Length && _rows.Count > 1)
            {
                var last = _rows[^1];
                _rows.RemoveAt(_rows.Count - 1);
                _rowsHost.RemoveChild(last);
                last.QueueFree();
            }

            for (var index = 0; index < _rows.Count; index++)
            {
                var value = index < normalized.Length ? normalized[index] : string.Empty;
                _rows[index].SetSelectedId(value);
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
        private readonly ModStudioEntityKind _kind;
        private readonly string _key;
        private readonly SearchableOptionPicker _picker;
        private string _currentValue;

        public ChoiceFieldEditor(ModStudioEntityKind kind, string key, IReadOnlyList<(string Value, string Display)> options, string value)
        {
            _kind = kind;
            _key = key;
            _currentValue = value ?? string.Empty;
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            _picker = new SearchableOptionPicker(
                options,
                value,
                fallbackDisplayFactory: unknown => BuildDisplayText(unknown, unknown));
            _picker.ValueChanged += OnValueChanged;
            AddChild(_picker);

            RefreshTexts();
        }

        public event Action? ValueChanged;

        public string GetSerializedValue()
        {
            return _picker.GetValue();
        }

        public void SetValue(string value)
        {
            _currentValue = value ?? string.Empty;
            _picker.SetValue(_currentValue);
        }

        public void RefreshTexts()
        {
            var options = FieldChoiceProvider.GetBasicChoices(_kind, _key);
            _picker.SetChoices(options);
            _picker.RefreshTexts();
        }

        private void OnValueChanged(string value)
        {
            _currentValue = value;
            ValueChanged?.Invoke();
        }

        private static string BuildDisplayText(string value, string fallback)
        {
            var display = ModStudioFieldDisplayNames.FormatPropertyValue(string.Empty, value);
            if (string.IsNullOrWhiteSpace(display))
            {
                display = fallback;
            }

            return display == value ? display : $"{display} [{value}]";
        }
    }

    private sealed partial class ListRow : HBoxContainer
    {
        private readonly SearchableOptionPicker _picker;
        private readonly Button _removeButton;

        public ListRow(IReadOnlyList<FieldOption> options, string? selectedId)
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            AddThemeConstantOverride("separation", 6);

            _picker = new SearchableOptionPicker(
                options.Select(option => (option.Id, option.DisplayText)).ToList(),
                selectedId,
                allowEmptySelection: true,
                fallbackDisplayFactory: unknown => unknown);
            _picker.ValueChanged += _ => SelectionChanged?.Invoke();
            AddChild(_picker);

            _removeButton = MakeButton(string.Empty, () => RemoveRequested?.Invoke());
            _removeButton.CustomMinimumSize = new Vector2(84f, 30f);
            AddChild(_removeButton);
        }

        public event Action? SelectionChanged;
        public event Action? RemoveRequested;

        public string GetSelectedId()
        {
            return _picker.GetValue();
        }

        public void ClearSelection()
        {
            _picker.SetValue(string.Empty);
        }

        public void SetSelectedId(string? selectedId)
        {
            _picker.SetValue(selectedId ?? string.Empty);
        }

        public void RefreshTexts()
        {
            _picker.RefreshTexts();
            _removeButton.Text = Dual("删除", "Remove");
        }
    }

    private static void AppendProjectFieldOptions(List<FieldOption> items, ModStudioEntityKind kind)
    {
        var project = FieldChoiceProvider.CurrentProject;
        if (project == null)
        {
            return;
        }

        var existingIds = new HashSet<string>(items.Select(i => i.Id), StringComparer.OrdinalIgnoreCase);

        foreach (var envelope in project.Overrides)
        {
            if (envelope.EntityKind != kind || existingIds.Contains(envelope.EntityId))
            {
                continue;
            }

            var title = envelope.Metadata.TryGetValue("title", out var t) ? t : envelope.EntityId;
            items.Add(new FieldOption(envelope.EntityId, BuildOptionDisplay(title, envelope.EntityId)));
        }
    }

    private sealed record FieldOption(string Id, string DisplayText);
}
