using Godot;
using STS2_Editor.Scripts.Editor.Core.Models;
using STS2_Editor.Scripts.Editor.Core.Utilities;
using static STS2_Editor.Scripts.Editor.UI.ModStudioUiFactory;

namespace STS2_Editor.Scripts.Editor.UI;

internal sealed partial class ModStudioMonsterAiEditor : MarginContainer
{
    private MonsterAiDefinition _definition = new();
    private TabContainer? _tabs;
    private VBoxContainer? _structureHost;
    private VBoxContainer? _stateHost;
    private VBoxContainer? _hooksHost;
    private Button? _saveButton;
    private Button? _revertButton;
    private Button? _addOpeningTurnButton;
    private Button? _addTurnButton;
    private Button? _addPhaseButton;
    private Button? _addStateVariableButton;
    private Button? _addLifecycleHookButton;
    private Button? _addEventTriggerButton;
    private Label? _titleLabel;

    public event Action? SaveRequested;
    public event Action? RevertRequested;
    public event Action? Changed;
    public event Action<MonsterMoveDefinition>? MoveGraphEditRequested;

    public override void _Ready()
    {
        EnsureBuilt();
    }

    public void EnsureBuilt()
    {
        BuildUi();
        RefreshTexts();
    }

    public void RefreshTexts()
    {
        if (_titleLabel != null) _titleLabel.Text = Dual("Monster AI", "Monster AI");
        if (_saveButton != null) _saveButton.Text = Dual("保存 Monster AI", "Save Monster AI");
        if (_revertButton != null) _revertButton.Text = Dual("还原", "Revert");
        if (_tabs != null)
        {
            _tabs.SetTabTitle(0, Dual("AI 结构", "AI Structure"));
            _tabs.SetTabTitle(1, Dual("状态", "State"));
            _tabs.SetTabTitle(2, Dual("Hooks", "Hooks"));
        }
        if (_addOpeningTurnButton != null) _addOpeningTurnButton.Text = Dual("新增 Opening Turn", "Add Opening Turn");
        if (_addTurnButton != null) _addTurnButton.Text = Dual("新增 Turn", "Add Turn");
        if (_addPhaseButton != null) _addPhaseButton.Text = Dual("新增 Loop Phase", "Add Loop Phase");
        if (_addStateVariableButton != null) _addStateVariableButton.Text = Dual("新增状态变量", "Add State Variable");
        if (_addLifecycleHookButton != null) _addLifecycleHookButton.Text = Dual("新增生命周期 Hook", "Add Lifecycle Hook");
        if (_addEventTriggerButton != null) _addEventTriggerButton.Text = Dual("新增事件触发器", "Add Event Trigger");
    }

    public void BindMonsterAi(MonsterAiDefinition definition)
    {
        _definition = definition?.Clone() ?? new MonsterAiDefinition();
        EnsureBuilt();
        RebuildAll();
    }

    public MonsterAiDefinition GetMonsterAiDefinition()
    {
        return _definition.Clone();
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

        var actions = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        actions.AddThemeConstantOverride("separation", 6);
        _saveButton = MakeButton(string.Empty, () => SaveRequested?.Invoke());
        _revertButton = MakeButton(string.Empty, () => RevertRequested?.Invoke());
        actions.AddChild(_saveButton);
        actions.AddChild(_revertButton);
        root.AddChild(actions);

        _titleLabel = MakeLabel(string.Empty, true);
        root.AddChild(_titleLabel);

        _tabs = new TabContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        root.AddChild(_tabs);

        _structureHost = CreateTabHost("StructureTab");
        _stateHost = CreateTabHost("StateTab");
        _hooksHost = CreateTabHost("HooksTab");
    }

    private VBoxContainer CreateTabHost(string name)
    {
        var host = new VBoxContainer
        {
            Name = name,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        host.AddThemeConstantOverride("separation", 8);
        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        scroll.AddChild(host);
        _tabs!.AddChild(scroll);
        return host;
    }

    private void RebuildAll()
    {
        RebuildStructureTab();
        RebuildStateTab();
        RebuildHooksTab();
    }

    private void RebuildStructureTab()
    {
        if (_structureHost == null)
        {
            return;
        }

        ClearChildren(_structureHost);
        _structureHost.AddChild(MakeLabel(Dual("Opening Turns", "Opening Turns"), true));
        foreach (var turn in _definition.OpeningTurns)
        {
            var editor = new ModStudioMonsterTurnEditor();
            editor.BindTurn(turn);
            editor.Changed += RaiseChanged;
            editor.RemoveRequested += removed =>
            {
                _definition.OpeningTurns.Remove(removed);
                RebuildStructureTab();
                RaiseChanged();
            };
            editor.MoveGraphEditRequested += move => MoveGraphEditRequested?.Invoke(move);
            _structureHost.AddChild(editor);
        }

        _addOpeningTurnButton = MakeButton(string.Empty, () =>
        {
            _definition.OpeningTurns.Add(new MonsterTurnDefinition
            {
                TurnId = $"turn_{_definition.OpeningTurns.Count + 1:00}",
                DisplayName = Dual("新回合", "New Turn")
            });
            RebuildStructureTab();
            RaiseChanged();
        });
        _structureHost.AddChild(_addOpeningTurnButton);

        _structureHost.AddChild(MakeLabel(Dual("Turns", "Turns"), true));
        foreach (var turn in _definition.Turns)
        {
            var editor = new ModStudioMonsterTurnEditor();
            editor.BindTurn(turn);
            editor.Changed += RaiseChanged;
            editor.RemoveRequested += removed =>
            {
                _definition.Turns.Remove(removed);
                RebuildStructureTab();
                RaiseChanged();
            };
            editor.MoveGraphEditRequested += move => MoveGraphEditRequested?.Invoke(move);
            _structureHost.AddChild(editor);
        }

        _addTurnButton = MakeButton(string.Empty, () =>
        {
            _definition.Turns.Add(new MonsterTurnDefinition
            {
                TurnId = $"turn_{_definition.Turns.Count + 1:00}",
                DisplayName = Dual("新回合", "New Turn")
            });
            RebuildStructureTab();
            RaiseChanged();
        });
        _structureHost.AddChild(_addTurnButton);

        _structureHost.AddChild(MakeLabel(Dual("Loop Phases", "Loop Phases"), true));
        foreach (var phase in _definition.LoopPhases)
        {
            var editor = new ModStudioMonsterPhaseEditor();
            editor.BindPhase(phase);
            editor.Changed += RaiseChanged;
            editor.RemoveRequested += removed =>
            {
                _definition.LoopPhases.Remove(removed);
                RebuildStructureTab();
                RaiseChanged();
            };
            _structureHost.AddChild(editor);
        }

        _addPhaseButton = MakeButton(string.Empty, () =>
        {
            _definition.LoopPhases.Add(new MonsterPhaseDefinition
            {
                PhaseId = $"phase_{_definition.LoopPhases.Count + 1:00}",
                DisplayName = Dual("新阶段", "New Phase")
            });
            RebuildStructureTab();
            RaiseChanged();
        });
        _structureHost.AddChild(_addPhaseButton);
        RefreshTexts();
    }

    private void RebuildStateTab()
    {
        if (_stateHost == null)
        {
            return;
        }

        ClearChildren(_stateHost);
        foreach (var variable in _definition.StateVariables)
        {
            _stateHost.AddChild(BuildStateVariableRow(variable));
        }

        _addStateVariableButton = MakeButton(string.Empty, () =>
        {
            _definition.StateVariables.Add(new MonsterStateVariableDefinition
            {
                Name = $"state_{_definition.StateVariables.Count + 1:00}"
            });
            RebuildStateTab();
            RaiseChanged();
        });
        _stateHost.AddChild(_addStateVariableButton);
        RefreshTexts();
    }

    private void RebuildHooksTab()
    {
        if (_hooksHost == null)
        {
            return;
        }

        ClearChildren(_hooksHost);
        _hooksHost.AddChild(MakeLabel(Dual("Lifecycle Hooks", "Lifecycle Hooks"), true));
        foreach (var hook in _definition.LifecycleHooks)
        {
            _hooksHost.AddChild(BuildLifecycleHookRow(hook));
        }

        _addLifecycleHookButton = MakeButton(string.Empty, () =>
        {
            _definition.LifecycleHooks.Add(new MonsterLifecycleHookDefinition());
            RebuildHooksTab();
            RaiseChanged();
        });
        _hooksHost.AddChild(_addLifecycleHookButton);

        _hooksHost.AddChild(MakeLabel(Dual("Event Triggers", "Event Triggers"), true));
        foreach (var trigger in _definition.EventTriggers)
        {
            _hooksHost.AddChild(BuildEventTriggerRow(trigger));
        }

        _addEventTriggerButton = MakeButton(string.Empty, () =>
        {
            _definition.EventTriggers.Add(new MonsterEventTriggerDefinition());
            RebuildHooksTab();
            RaiseChanged();
        });
        _hooksHost.AddChild(_addEventTriggerButton);
        RefreshTexts();
    }

    private Control BuildStateVariableRow(MonsterStateVariableDefinition variable)
    {
        var root = CreateSectionRoot();
        root.AddChild(BuildLabeledLineEdit("key", variable.Name, value =>
        {
            variable.Name = value;
            RaiseChanged();
        }));
        root.AddChild(BuildEnumPicker("state_variable_type", variable.Type.ToString(), value =>
        {
            if (Enum.TryParse<MonsterStateVariableType>(value, true, out var parsed))
            {
                variable.Type = parsed;
                RaiseChanged();
            }
        }));
        root.AddChild(BuildLabeledLineEdit("initial_value", variable.InitialValue, value =>
        {
            variable.InitialValue = value;
            RaiseChanged();
        }));
        root.AddChild(MakeButton(Dual("删除变量", "Remove Variable"), () =>
        {
            _definition.StateVariables.Remove(variable);
            RebuildStateTab();
            RaiseChanged();
        }));
        return root;
    }

    private Control BuildLifecycleHookRow(MonsterLifecycleHookDefinition hook)
    {
        var root = CreateSectionRoot();
        root.AddChild(BuildEnumPicker("hook_type", hook.HookType.ToString(), value =>
        {
            if (Enum.TryParse<MonsterLifecycleHookType>(value, true, out var parsed))
            {
                hook.HookType = parsed;
                RaiseChanged();
            }
        }));
        root.AddChild(BuildLabeledLineEdit("graph_id", hook.GraphId, value =>
        {
            hook.GraphId = value;
            RaiseChanged();
        }));
        root.AddChild(MakeButton(Dual("删除 Hook", "Remove Hook"), () =>
        {
            _definition.LifecycleHooks.Remove(hook);
            RebuildHooksTab();
            RaiseChanged();
        }));
        return root;
    }

    private Control BuildEventTriggerRow(MonsterEventTriggerDefinition trigger)
    {
        var root = CreateSectionRoot();
        root.AddChild(BuildEnumPicker("event_kind", trigger.EventKind.ToString(), value =>
        {
            if (Enum.TryParse<MonsterEventTriggerKind>(value, true, out var parsed))
            {
                trigger.EventKind = parsed;
                RaiseChanged();
            }
        }));
        root.AddChild(BuildLabeledLineEdit("filter_monster_id", trigger.FilterMonsterId, value =>
        {
            trigger.FilterMonsterId = value;
            RaiseChanged();
        }));
        root.AddChild(BuildLabeledLineEdit("target_phase_id", trigger.TargetPhaseId, value =>
        {
            trigger.TargetPhaseId = value;
            RaiseChanged();
        }));
        root.AddChild(BuildLabeledLineEdit("graph_id", trigger.GraphId, value =>
        {
            trigger.GraphId = value;
            RaiseChanged();
        }));
        root.AddChild(MakeButton(Dual("删除触发器", "Remove Trigger"), () =>
        {
            _definition.EventTriggers.Remove(trigger);
            RebuildHooksTab();
            RaiseChanged();
        }));
        return root;
    }

    private static VBoxContainer CreateSectionRoot()
    {
        var root = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        root.AddThemeConstantOverride("separation", 4);
        return root;
    }

    private Control BuildLabeledLineEdit(string key, string value, Action<string> onChanged)
    {
        var root = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        root.AddThemeConstantOverride("separation", 2);
        root.AddChild(MakeLabel(ModStudioFieldDisplayNames.Get(key), true));
        var edit = new LineEdit
        {
            Text = value ?? string.Empty,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        edit.TextChanged += value => onChanged(value);
        root.AddChild(edit);
        return root;
    }

    private Control BuildEnumPicker(string key, string selectedValue, Action<string> onChanged)
    {
        var root = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        root.AddThemeConstantOverride("separation", 2);
        root.AddChild(MakeLabel(ModStudioFieldDisplayNames.Get(key), true));

        var options = FieldChoiceProvider.GetBasicChoices(ModStudioEntityKind.Monster, key);
        var picker = new OptionButton
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };

        var selectedIndex = 0;
        for (var index = 0; index < options.Count; index++)
        {
            picker.AddItem(options[index].Display);
            picker.SetItemMetadata(index, options[index].Value);
            if (string.Equals(options[index].Value, selectedValue, StringComparison.Ordinal))
            {
                selectedIndex = index;
            }
        }

        if (picker.ItemCount > 0)
        {
            picker.Select(selectedIndex);
        }

        picker.ItemSelected += index => onChanged(picker.GetItemMetadata((int)index).AsString());
        root.AddChild(picker);
        return root;
    }

    private static void ClearChildren(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            child.QueueFree();
        }
    }

    private void RaiseChanged()
    {
        Changed?.Invoke();
    }

    private static string Dual(string zh, string en)
    {
        return ModStudioLocalization.IsChinese ? zh : en;
    }
}
