using Godot;
using STS2_Editor.Scripts.Editor.Core.Utilities;
using static STS2_Editor.Scripts.Editor.UI.ModStudioUiFactory;

namespace STS2_Editor.Scripts.Editor.UI;

internal sealed partial class ModStudioProjectMenuBar : PanelContainer
{
    private const bool AiEntryEnabled = false;
    private const int FileNewProjectId = 1;
    private const int FileOpenProjectId = 2;
    private const int FileSaveProjectId = 3;
    private const int FileExportPackageId = 4;
    private const int FileCloseId = 5;

    private const int EditSaveCurrentId = 10;
    private const int EditRevertCurrentId = 11;

    private const int ModeSwitchId = 20;

    private const int LanguageChineseId = 30;
    private const int LanguageEnglishId = 31;

    private const int HelpPresetKeyGuideId = 40;

    private const int AboutGithubId = 45;
    private const int AboutUsageGuideId = 46;

    private const int AiAssistantId = 50;
    private const int AiSettingsId = 51;

    private MenuButton? _fileButton;
    private MenuButton? _editButton;
    private MenuButton? _modeButton;
    private MenuButton? _languageButton;
    private MenuButton? _helpButton;
    private MenuButton? _aboutButton;
    private MenuButton? _aiButton;
    private LinkButton? _authorLinkButton;
    private Button? _exitButton;
    private Label? _projectStateLabel;

    public event Action? NewProjectRequested;
    public event Action? OpenProjectRequested;
    public event Action? SaveProjectRequested;
    public event Action? ExportPackageRequested;
    public event Action? RevertRequested;
    public event Action? SwitchModeRequested;
    public event Action? PresetStateKeysGuideRequested;
    public event Action? AiAssistantRequested;
    public event Action? AiSettingsRequested;
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
            SetMenuItems(_fileButton, new[]
            {
                (FileNewProjectId, Dual("新建项目", "New Project")),
                (FileOpenProjectId, Dual("打开项目", "Open Project")),
                (FileSaveProjectId, Dual("保存项目", "Save Project")),
                (FileExportPackageId, Dual("导出包", "Export Package")),
                (FileCloseId, Dual("关闭", "Close"))
            });
        }

        if (_editButton != null)
        {
            _editButton.Text = Dual("编辑", "Edit");
            SetMenuItems(_editButton, new[]
            {
                (EditSaveCurrentId, Dual("保存当前", "Save Current")),
                (EditRevertCurrentId, Dual("还原当前", "Revert Current"))
            });
        }

        if (_modeButton != null)
        {
            _modeButton.Text = Dual("模式", "Mode");
            SetMenuItems(_modeButton, new[]
            {
                (ModeSwitchId, Dual("切换模式", "Switch Mode"))
            });
        }

        if (_languageButton != null)
        {
            _languageButton.Text = Dual("语言", "Language");
            SetMenuItems(_languageButton, new[]
            {
                (LanguageChineseId, Dual("中文", "Chinese")),
                (LanguageEnglishId, Dual("English", "English"))
            });
        }

        if (_helpButton != null)
        {
            _helpButton.Text = Dual("说明", "Help");
            SetMenuItems(_helpButton, new[]
            {
                (HelpPresetKeyGuideId, Dual("预制键说明", "Preset Key Guide"))
            });
        }

        if (_aboutButton != null)
        {
            _aboutButton.Text = Dual("关于", "About");
            SetMenuItems(_aboutButton, new[]
            {
                (AboutGithubId, Dual("github地址", "GitHub")),
                (AboutUsageGuideId, Dual("使用说明", "User Guide"))
            });
        }

        if (AiEntryEnabled && _aiButton != null)
        {
            _aiButton.Text = "AI";
            SetMenuItems(_aiButton, new[]
            {
                (AiAssistantId, Dual("AI 助手", "AI Assistant")),
                (AiSettingsId, Dual("AI 设置", "AI Settings"))
            });
        }

        if (_authorLinkButton != null)
        {
            _authorLinkButton.Text = Dual("作者：禽兽-云轩", "Author: 禽兽-云轩");
            _authorLinkButton.TooltipText = ModStudioExternalLinks.AuthorProfileUrl;
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
        _helpButton = MakeMenuButton(Dual("说明", "Help"));
        _aboutButton = MakeMenuButton(Dual("关于", "About"));
        if (AiEntryEnabled)
        {
            _aiButton = MakeMenuButton("AI");
        }
        _projectStateLabel = MakeLabel(string.Empty, true);
        _projectStateLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _authorLinkButton = new LinkButton
        {
            Text = Dual("作者：禽兽-云轩", "Author: 禽兽-云轩"),
            FocusMode = Control.FocusModeEnum.All,
            CustomMinimumSize = new Vector2(168f, 30f),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
            TooltipText = ModStudioExternalLinks.AuthorProfileUrl
        };
        _authorLinkButton.Pressed += () => OS.ShellOpen(ModStudioExternalLinks.AuthorProfileUrl);
        _exitButton = MakeButton(Dual("退出", "Exit"), () => ExitRequested?.Invoke());
        _exitButton.CustomMinimumSize = new Vector2(100f, 30f);

        row.AddChild(_fileButton);
        row.AddChild(_editButton);
        row.AddChild(_modeButton);
        row.AddChild(_languageButton);
        row.AddChild(_helpButton);
        row.AddChild(_aboutButton);
        if (_aiButton != null)
        {
            row.AddChild(_aiButton);
        }
        row.AddChild(_projectStateLabel);
        row.AddChild(_authorLinkButton);
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
                    case FileNewProjectId:
                        NewProjectRequested?.Invoke();
                        break;
                    case FileOpenProjectId:
                        OpenProjectRequested?.Invoke();
                        break;
                    case FileSaveProjectId:
                        SaveProjectRequested?.Invoke();
                        break;
                    case FileExportPackageId:
                        ExportPackageRequested?.Invoke();
                        break;
                    case FileCloseId:
                        ExitRequested?.Invoke();
                        break;
                }
            };
        }

        if (_editButton?.GetPopup() is PopupMenu editPopup)
        {
            editPopup.IdPressed += id =>
            {
                switch (id)
                {
                    case EditSaveCurrentId:
                        SaveProjectRequested?.Invoke();
                        break;
                    case EditRevertCurrentId:
                        RevertRequested?.Invoke();
                        break;
                }
            };
        }

        if (_modeButton?.GetPopup() is PopupMenu modePopup)
        {
            modePopup.IdPressed += id =>
            {
                if (id == ModeSwitchId)
                {
                    SwitchModeRequested?.Invoke();
                }
            };
        }

        if (_languageButton?.GetPopup() is PopupMenu languagePopup)
        {
            languagePopup.IdPressed += id =>
            {
                if (id == LanguageChineseId)
                {
                    LanguageChanged?.Invoke(ModStudioLocalization.ChineseLanguageCode);
                }
                else if (id == LanguageEnglishId)
                {
                    LanguageChanged?.Invoke(ModStudioLocalization.EnglishLanguageCode);
                }
            };
        }

        if (_helpButton?.GetPopup() is PopupMenu helpPopup)
        {
            helpPopup.IdPressed += id =>
            {
                if (id == HelpPresetKeyGuideId)
                {
                    PresetStateKeysGuideRequested?.Invoke();
                }
            };
        }

        if (_aboutButton?.GetPopup() is PopupMenu aboutPopup)
        {
            aboutPopup.IdPressed += id =>
            {
                switch (id)
                {
                    case AboutGithubId:
                        OS.ShellOpen(ModStudioExternalLinks.RepositoryUrl);
                        break;
                    case AboutUsageGuideId:
                        OpenUsageGuide();
                        break;
                }
            };
        }

        if (AiEntryEnabled && _aiButton?.GetPopup() is PopupMenu aiPopup)
        {
            aiPopup.IdPressed += id =>
            {
                switch (id)
                {
                    case AiAssistantId:
                        AiAssistantRequested?.Invoke();
                        break;
                    case AiSettingsId:
                        AiSettingsRequested?.Invoke();
                        break;
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

    private static void OpenUsageGuide()
    {
        var guidePath = ResolveUsageGuidePath();
        if (string.IsNullOrWhiteSpace(guidePath) || !File.Exists(guidePath))
        {
            OS.Alert(
                Dual("未找到“使用说明”文件。请先重新编译并同步当前模组目录。", "The user guide file was not found. Rebuild and sync the current mod directory first."),
                Dual("说明文件缺失", "Guide Missing"));
            return;
        }

        OS.ShellOpen(guidePath);
    }

    private static string ResolveUsageGuidePath()
    {
        var candidates = new[]
        {
            ModStudioPaths.UserGuidePath,
            Path.Combine(AppContext.BaseDirectory, "docs", ModStudioExternalLinks.UserGuideFileName)
        };

        return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
    }
}
