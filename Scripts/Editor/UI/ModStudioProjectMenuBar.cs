using Godot;
using STS2_Editor.Scripts.Editor.Core.Utilities;
using static STS2_Editor.Scripts.Editor.UI.ModStudioUiFactory;

namespace STS2_Editor.Scripts.Editor.UI;

internal sealed partial class ModStudioProjectMenuBar : PanelContainer
{
    private MenuButton? _fileButton;
    private MenuButton? _editButton;
    private MenuButton? _modeButton;
    private MenuButton? _languageButton;
    private Button? _exitButton;
    private Label? _projectStateLabel;

    public event Action? NewProjectRequested;
    public event Action? OpenProjectRequested;
    public event Action? SaveProjectRequested;
    public event Action? ExportPackageRequested;
    public event Action? RevertRequested;
    public event Action? SwitchModeRequested;
    public event Action? ExitRequested;
    public event Action<string>? LanguageChanged;

    public override void _Ready()
    {
        BuildUi();
        RefreshTexts();
    }

    public void SetProjectState(string title, bool dirty)
    {
        if (_projectStateLabel == null)
        {
            return;
        }

        var dirtyText = dirty ? Dual("未保存", "Unsaved") : Dual("已保存", "Saved");
        _projectStateLabel.Text = string.IsNullOrWhiteSpace(title) ? dirtyText : $"{title}   {dirtyText}";
    }

    public void RefreshTexts()
    {
        if (_fileButton != null)
        {
            _fileButton.Text = Dual("文件", "File");
            SetMenuItems(_fileButton, new (int, string)[]
            {
                (1, Dual("新建项目", "New Project")),
                (2, Dual("打开项目", "Open Project")),
                (3, Dual("保存项目", "Save Project")),
                (4, Dual("导出包", "Export Package")),
                (5, Dual("关闭", "Close"))
            });
        }

        if (_editButton != null)
        {
            _editButton.Text = Dual("编辑", "Edit");
            SetMenuItems(_editButton, new (int, string)[]
            {
                (10, Dual("保存当前", "Save Current")),
                (11, Dual("还原当前", "Revert Current"))
            });
        }

        if (_modeButton != null)
        {
            _modeButton.Text = Dual("模式", "Mode");
            SetMenuItems(_modeButton, new (int, string)[] { (20, Dual("切换模式", "Switch Mode")) });
        }

        if (_languageButton != null)
        {
            _languageButton.Text = Dual("语言", "Language");
            SetMenuItems(_languageButton, new (int, string)[]
            {
                (30, Dual("中文", "Chinese")),
                (31, Dual("English", "English"))
            });
        }

        if (_exitButton != null)
        {
            _exitButton.Text = Dual("退出", "Exit");
        }
    }

    private void BuildUi()
    {
        if (GetChildCount() > 0)
        {
            return;
        }

        CustomMinimumSize = new Vector2(0f, 42f);
        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        var row = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.Fill
        };
        row.AddThemeConstantOverride("separation", 6);
        AddChild(row);

        _fileButton = MakeMenuButton(Dual("文件", "File"));
        _editButton = MakeMenuButton(Dual("编辑", "Edit"));
        _modeButton = MakeMenuButton(Dual("模式", "Mode"));
        _languageButton = MakeMenuButton(Dual("语言", "Language"));
        _projectStateLabel = MakeLabel(string.Empty, true);
        _projectStateLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _exitButton = MakeButton(Dual("退出", "Exit"), () => ExitRequested?.Invoke());
        _exitButton.CustomMinimumSize = new Vector2(100f, 30f);

        row.AddChild(_fileButton);
        row.AddChild(_editButton);
        row.AddChild(_modeButton);
        row.AddChild(_languageButton);
        row.AddChild(_projectStateLabel);
        row.AddChild(_exitButton);

        AttachMenuHandlers();
    }

    private void AttachMenuHandlers()
    {
        if (_fileButton?.GetPopup() is PopupMenu filePopup)
        {
            filePopup.IdPressed += id =>
            {
                switch (id)
                {
                    case 1: NewProjectRequested?.Invoke(); break;
                    case 2: OpenProjectRequested?.Invoke(); break;
                    case 3: SaveProjectRequested?.Invoke(); break;
                    case 4: ExportPackageRequested?.Invoke(); break;
                    case 5: ExitRequested?.Invoke(); break;
                }
            };
        }

        if (_editButton?.GetPopup() is PopupMenu editPopup)
        {
            editPopup.IdPressed += id =>
            {
                switch (id)
                {
                    case 10: SaveProjectRequested?.Invoke(); break;
                    case 11: RevertRequested?.Invoke(); break;
                }
            };
        }

        if (_modeButton?.GetPopup() is PopupMenu modePopup)
        {
            modePopup.IdPressed += id =>
            {
                if (id == 20)
                {
                    SwitchModeRequested?.Invoke();
                }
            };
        }

        if (_languageButton?.GetPopup() is PopupMenu languagePopup)
        {
            languagePopup.IdPressed += id =>
            {
                if (id == 30)
                {
                    LanguageChanged?.Invoke(ModStudioLocalization.ChineseLanguageCode);
                }
                else if (id == 31)
                {
                    LanguageChanged?.Invoke(ModStudioLocalization.EnglishLanguageCode);
                }
            };
        }
    }

    private static MenuButton MakeMenuButton(string title)
    {
        return new MenuButton
        {
            Text = title,
            Flat = true,
            FocusMode = Control.FocusModeEnum.All,
            CustomMinimumSize = new Vector2(88f, 30f),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin
        };
    }

    private static void SetMenuItems(MenuButton button, IEnumerable<(int Id, string Text)> items)
    {
        var popup = button.GetPopup();
        popup.Clear();
        foreach (var (id, text) in items)
        {
            popup.AddItem(text, id);
        }
    }
}
