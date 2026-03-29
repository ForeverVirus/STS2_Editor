using Godot;
using MegaCrit.Sts2.Core.MonsterMoves;
using STS2_Editor.Scripts.Editor.Core.Models;
using STS2_Editor.Scripts.Editor.Core.Utilities;
using static STS2_Editor.Scripts.Editor.UI.ModStudioUiFactory;

namespace STS2_Editor.Scripts.Editor.UI;

internal sealed partial class ModStudioMonsterPhaseEditor : VBoxContainer
{
    private MonsterPhaseDefinition? _phase;
    private VBoxContainer? _branchesHost;
    private Button? _addBranchButton;
    private Button? _removePhaseButton;

    public event Action? Changed;
    public event Action<MonsterPhaseDefinition>? RemoveRequested;

    public override void _Ready()
    {
        EnsureBuilt();
    }

    public void EnsureBuilt()
    {
        BuildUi();
        RefreshTexts();
    }

    public void BindPhase(MonsterPhaseDefinition phase)
    {
        _phase = phase;
        EnsureBuilt();
        RebuildRows();
    }

    public void RefreshTexts()
    {
        if (_addBranchButton != null) _addBranchButton.Text = Dual("新增分支", "Add Branch");
        if (_removePhaseButton != null) _removePhaseButton.Text = Dual("删除阶段", "Remove Phase");
    }

    private void BuildUi()
    {
        if (GetChildCount() > 0)
        {
            return;
        }

        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        AddThemeConstantOverride("separation", 8);

        _removePhaseButton = MakeButton(string.Empty, () =>
        {
            if (_phase != null)
            {
                RemoveRequested?.Invoke(_phase);
            }
        });
        AddChild(_removePhaseButton);

        _branchesHost = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _branchesHost.AddThemeConstantOverride("separation", 6);
        AddChild(_branchesHost);

        _addBranchButton = MakeButton(string.Empty, AddBranch);
        AddChild(_addBranchButton);
    }

    private void RebuildRows()
    {
        if (_phase == null || _branchesHost == null)
        {
            return;
        }

        foreach (var child in _branchesHost.GetChildren())
        {
            child.QueueFree();
        }

        _branchesHost.AddChild(BuildPhaseHeader());
        _branchesHost.AddChild(MakeLabel(Dual("分支列表", "Branches"), true));
        foreach (var branch in _phase.Branches)
        {
            _branchesHost.AddChild(BuildBranchRow(branch));
        }
    }

    private Control BuildPhaseHeader()
    {
        var root = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        root.AddThemeConstantOverride("separation", 4);
        root.AddChild(MakeLabel(Dual("阶段信息", "Phase"), true));
        root.AddChild(BuildLabeledLineEdit("phase_id", _phase!.PhaseId, value =>
        {
            _phase.PhaseId = value;
            RaiseChanged();
        }));
        root.AddChild(BuildLabeledLineEdit("title", _phase.DisplayName, value =>
        {
            _phase.DisplayName = value;
            RaiseChanged();
        }));
        root.AddChild(BuildEnumPicker("phase_kind", _phase.PhaseKind.ToString(), value =>
        {
            if (Enum.TryParse<MonsterPhaseKind>(value, true, out var parsed))
            {
                _phase.PhaseKind = parsed;
                RaiseChanged();
            }
        }));
        return root;
    }

    private Control BuildBranchRow(MonsterPhaseBranch branch)
    {
        var root = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        root.AddThemeConstantOverride("separation", 4);
        root.AddChild(BuildLabeledLineEdit("branch_id", branch.BranchId, value =>
        {
            branch.BranchId = value;
            RaiseChanged();
        }));
        root.AddChild(BuildLabeledLineEdit("turn_id", branch.TargetTurnId ?? string.Empty, value =>
        {
            branch.TargetTurnId = value;
            RaiseChanged();
        }));
        root.AddChild(BuildLabeledLineEdit("target_phase_id", branch.TargetPhaseId ?? string.Empty, value =>
        {
            branch.TargetPhaseId = value;
            RaiseChanged();
        }));
        root.AddChild(BuildLabeledLineEdit("condition", branch.Condition, value =>
        {
            branch.Condition = value;
            RaiseChanged();
        }));
        root.AddChild(BuildLabeledLineEdit("weight", branch.Weight.ToString(System.Globalization.CultureInfo.InvariantCulture), value =>
        {
            if (float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
            {
                branch.Weight = parsed;
                RaiseChanged();
            }
        }));
        root.AddChild(BuildEnumPicker("repeat_type", branch.RepeatType.ToString(), value =>
        {
            if (Enum.TryParse<MoveRepeatType>(value, true, out var parsed))
            {
                branch.RepeatType = parsed;
                RaiseChanged();
            }
        }));
        root.AddChild(BuildLabeledLineEdit("max_repeats", branch.MaxRepeats.ToString(), value =>
        {
            if (int.TryParse(value, out var parsed))
            {
                branch.MaxRepeats = parsed;
                RaiseChanged();
            }
        }));
        root.AddChild(BuildLabeledLineEdit("cooldown", branch.Cooldown.ToString(), value =>
        {
            if (int.TryParse(value, out var parsed))
            {
                branch.Cooldown = parsed;
                RaiseChanged();
            }
        }));
        root.AddChild(MakeButton(Dual("删除分支", "Remove Branch"), () =>
        {
            _phase?.Branches.Remove(branch);
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

    private void AddBranch()
    {
        if (_phase == null)
        {
            return;
        }

        _phase.Branches.Add(new MonsterPhaseBranch
        {
            BranchId = $"branch_{_phase.Branches.Count + 1:00}"
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
