using Godot;
using STS2_Editor.Scripts.Editor.Core.Models;
using STS2_Editor.Scripts.Editor.Core.Utilities;
using static STS2_Editor.Scripts.Editor.UI.ModStudioUiFactory;

namespace STS2_Editor.Scripts.Editor.UI;

internal sealed partial class ModStudioMonsterTurnEditor : VBoxContainer
{
    private MonsterTurnDefinition? _turn;
    private VBoxContainer? _movesHost;
    private VBoxContainer? _intentsHost;
    private Button? _addMoveButton;
    private Button? _addIntentButton;
    private Button? _removeTurnButton;

    public event Action? Changed;
    public event Action<MonsterTurnDefinition>? RemoveRequested;
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

    public void BindTurn(MonsterTurnDefinition turn)
    {
        _turn = turn;
        EnsureBuilt();
        RebuildRows();
    }

    public void RefreshTexts()
    {
        if (_addMoveButton != null) _addMoveButton.Text = Dual("新增招式", "Add Move");
        if (_addIntentButton != null) _addIntentButton.Text = Dual("新增意图", "Add Intent");
        if (_removeTurnButton != null) _removeTurnButton.Text = Dual("删除回合", "Remove Turn");
    }

    private void BuildUi()
    {
        if (GetChildCount() > 0)
        {
            return;
        }

        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        AddThemeConstantOverride("separation", 8);

        _removeTurnButton = MakeButton(string.Empty, () =>
        {
            if (_turn != null)
            {
                RemoveRequested?.Invoke(_turn);
            }
        });
        AddChild(_removeTurnButton);

        _movesHost = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _movesHost.AddThemeConstantOverride("separation", 6);
        AddChild(_movesHost);

        _addMoveButton = MakeButton(string.Empty, AddMove);
        AddChild(_addMoveButton);

        _intentsHost = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _intentsHost.AddThemeConstantOverride("separation", 6);
        AddChild(_intentsHost);

        _addIntentButton = MakeButton(string.Empty, AddIntent);
        AddChild(_addIntentButton);
    }

    private void RebuildRows()
    {
        if (_turn == null || _movesHost == null || _intentsHost == null)
        {
            return;
        }

        foreach (var child in _movesHost.GetChildren())
        {
            child.QueueFree();
        }

        foreach (var child in _intentsHost.GetChildren())
        {
            child.QueueFree();
        }

        _movesHost.AddChild(BuildTurnHeader());
        _movesHost.AddChild(MakeLabel(Dual("招式列表", "Moves"), true));
        foreach (var move in _turn.Moves)
        {
            _movesHost.AddChild(BuildMoveRow(move));
        }

        _intentsHost.AddChild(MakeLabel(Dual("意图列表", "Intents"), true));
        foreach (var intent in _turn.Intents)
        {
            _intentsHost.AddChild(BuildIntentRow(intent));
        }
    }

    private Control BuildTurnHeader()
    {
        var root = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        root.AddThemeConstantOverride("separation", 4);
        root.AddChild(MakeLabel(Dual("回合信息", "Turn"), true));
        root.AddChild(BuildLabeledLineEdit("turn_id", _turn!.TurnId, value =>
        {
            _turn.TurnId = value;
            RaiseChanged();
        }));
        root.AddChild(BuildLabeledLineEdit("title", _turn.DisplayName, value =>
        {
            _turn.DisplayName = value;
            RaiseChanged();
        }));

        var check = new CheckBox
        {
            Text = Dual("必须至少执行一次", "Must Perform Once"),
            ButtonPressed = _turn.MustPerformOnceBeforeTransitioning,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        check.Toggled += pressed =>
        {
            _turn.MustPerformOnceBeforeTransitioning = pressed;
            RaiseChanged();
        };
        root.AddChild(check);
        return root;
    }

    private Control BuildMoveRow(MonsterMoveDefinition move)
    {
        var root = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        root.AddThemeConstantOverride("separation", 4);
        root.AddChild(BuildLabeledLineEdit("move_id", move.MoveId, value =>
        {
            move.MoveId = value;
            RaiseChanged();
        }));
        root.AddChild(BuildLabeledLineEdit("title", move.DisplayName, value =>
        {
            move.DisplayName = value;
            RaiseChanged();
        }));

        var actions = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        actions.AddThemeConstantOverride("separation", 6);
        var editButton = MakeButton(Dual("编辑 Graph", "Edit Graph"), () => MoveGraphEditRequested?.Invoke(move));
        var removeButton = MakeButton(Dual("删除招式", "Remove Move"), () =>
        {
            _turn?.Moves.Remove(move);
            RebuildRows();
            RaiseChanged();
        });
        actions.AddChild(editButton);
        actions.AddChild(removeButton);
        root.AddChild(actions);
        return root;
    }

    private Control BuildIntentRow(MonsterIntentDeclaration intent)
    {
        var root = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        root.AddThemeConstantOverride("separation", 4);

        root.AddChild(BuildEnumPicker("intent_type", intent.IntentType.ToString(), value =>
        {
            if (Enum.TryParse<MonsterIntentType>(value, true, out var parsed))
            {
                intent.IntentType = parsed;
                RaiseChanged();
            }
        }));

        root.AddChild(BuildLabeledLineEdit("amount", intent.Parameters.TryGetValue("amount", out var amount) ? amount : string.Empty, value =>
        {
            intent.Parameters["amount"] = value;
            RaiseChanged();
        }));
        root.AddChild(BuildLabeledLineEdit("count", intent.Parameters.TryGetValue("count", out var count) ? count : string.Empty, value =>
        {
            intent.Parameters["count"] = value;
            RaiseChanged();
        }));

        root.AddChild(MakeButton(Dual("删除意图", "Remove Intent"), () =>
        {
            _turn?.Intents.Remove(intent);
            RebuildRows();
            RaiseChanged();
        }));
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

    private void AddMove()
    {
        if (_turn == null)
        {
            return;
        }

        _turn.Moves.Add(new MonsterMoveDefinition
        {
            MoveId = $"move_{_turn.Moves.Count + 1:00}",
            DisplayName = Dual("新招式", "New Move")
        });
        RebuildRows();
        RaiseChanged();
    }

    private void AddIntent()
    {
        if (_turn == null)
        {
            return;
        }

        _turn.Intents.Add(new MonsterIntentDeclaration
        {
            IntentType = MonsterIntentType.SingleAttack
        });
        RebuildRows();
        RaiseChanged();
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
