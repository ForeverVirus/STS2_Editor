using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using STS2_Editor.Scripts.Editor.Core.Utilities;
using static STS2_Editor.Scripts.Editor.UI.ModStudioUiFactory;

namespace STS2_Editor.Scripts.Editor.UI;

public sealed partial class NModStudioModeChooserDialog : NSubmenu
{
    private const string BackButtonScenePath = "res://scenes/ui/back_button.tscn";

    private bool _uiBuilt;
    private Label? _titleLabel;
    private RichTextLabel? _descriptionLabel;
    private Button? _projectButton;
    private Button? _packageButton;
    private Button? _closeButton;

    protected override Control? InitialFocusedControl => _projectButton;

    public static IEnumerable<string> AssetPaths => new[] { BackButtonScenePath };

    public static NModStudioModeChooserDialog Create() => new();

    public override void _Ready()
    {
        BuildUi();
        base.ConnectSignals();
        base.HideBackButtonImmediately();

        ModStudioLocalization.LanguageChanged += RefreshTexts;
        RefreshTexts();
    }

    public override void OnSubmenuOpened()
    {
        base.HideBackButtonImmediately();
    }

    protected override void OnSubmenuShown()
    {
        base.OnSubmenuShown();
        base.HideBackButtonImmediately();
    }

    public override void OnSubmenuClosed()
    {
        base.OnSubmenuClosed();
    }

    private void BuildUi()
    {
        if (_uiBuilt)
        {
            return;
        }

        _uiBuilt = true;
        SetAnchorsPreset(LayoutPreset.FullRect);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        MouseFilter = MouseFilterEnum.Stop;

        AddBackButton();

        AddChild(new ColorRect
        {
            AnchorRight = 1f,
            AnchorBottom = 1f,
            Color = new Color(0.02f, 0.03f, 0.05f, 0.72f),
            MouseFilter = MouseFilterEnum.Stop
        });

        var center = new CenterContainer
        {
            AnchorRight = 1f,
            AnchorBottom = 1f,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        AddChild(center);

        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(560f, 0f),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
            SizeFlagsVertical = SizeFlags.ShrinkCenter
        };
        center.AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 20);
        margin.AddThemeConstantOverride("margin_top", 20);
        margin.AddThemeConstantOverride("margin_right", 20);
        margin.AddThemeConstantOverride("margin_bottom", 20);
        panel.AddChild(margin);

        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", 14);
        margin.AddChild(root);

        _titleLabel = MakeLabel(string.Empty, true);
        _titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
        root.AddChild(_titleLabel);

        _descriptionLabel = MakeDetails(string.Empty, scrollActive: false, fitContent: true, minHeight: 84f);
        root.AddChild(_descriptionLabel);

        _projectButton = MakeButton(string.Empty, OpenProjectMode);
        _projectButton.CustomMinimumSize = new Vector2(0f, 42f);
        root.AddChild(_projectButton);

        _packageButton = MakeButton(string.Empty, OpenPackageMode);
        _packageButton.CustomMinimumSize = new Vector2(0f, 42f);
        root.AddChild(_packageButton);

        _closeButton = MakeButton(string.Empty, CloseChooser);
        _closeButton.CustomMinimumSize = new Vector2(0f, 38f);
        root.AddChild(_closeButton);
    }

    private void RefreshTexts()
    {
        if (_titleLabel != null)
        {
            _titleLabel.Text = Dual("选择 Mod Studio 模式", "Choose a Mod Studio mode");
        }

        if (_descriptionLabel != null)
        {
            _descriptionLabel.Text = Dual(
                "Project Mode 用于创建和编辑工程，Package Mode 用于管理已经导出的 .sts2pack 模组包。",
                "Project Mode is for creating and editing projects. Package Mode manages exported .sts2pack packages.");
        }

        if (_projectButton != null)
        {
            _projectButton.Text = Dual("进入 Project Mode", "Open Project Mode");
        }

        if (_packageButton != null)
        {
            _packageButton.Text = Dual("进入 Package Mode", "Open Package Mode");
        }

        if (_closeButton != null)
        {
            _closeButton.Text = Dual("关闭", "Close");
        }
    }

    private void OpenProjectMode()
    {
        _stack.PushSubmenuType<NModStudioProjectWindow>();
    }

    private void OpenPackageMode()
    {
        _stack.PushSubmenuType<NModStudioPackageWindow>();
    }

    private void CloseChooser()
    {
        _stack.Pop();
    }

    private void AddBackButton()
    {
        var scene = ResourceLoader.Load<PackedScene>(BackButtonScenePath);
        if (scene == null)
        {
            return;
        }

        var backButton = scene.Instantiate<NBackButton>(PackedScene.GenEditState.Disabled);
        backButton.Name = "BackButton";
        AddChild(backButton);
    }
}
