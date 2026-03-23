using Godot;
using static STS2_Editor.Scripts.Editor.UI.ModStudioUiFactory;

namespace STS2_Editor.Scripts.Editor.UI;

internal sealed partial class ModStudioUnsavedDialog : Control
{
    private Label? _title;
    private RichTextLabel? _body;
    private Button? _saveButton;
    private Button? _discardButton;
    private Button? _cancelButton;

    public event Action? SaveAndExit;
    public event Action? DiscardAndExit;
    public event Action? Cancel;

    public override void _Ready()
    {
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsPreset(LayoutPreset.FullRect);

        AddChild(new ColorRect
        {
            AnchorRight = 1f,
            AnchorBottom = 1f,
            Color = new Color(0f, 0f, 0f, 0.62f),
            MouseFilter = MouseFilterEnum.Stop
        });

        var center = new CenterContainer
        {
            AnchorRight = 1f,
            AnchorBottom = 1f,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        AddChild(center);

        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(520f, 0f)
        };
        center.AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 18);
        margin.AddThemeConstantOverride("margin_top", 18);
        margin.AddThemeConstantOverride("margin_right", 18);
        margin.AddThemeConstantOverride("margin_bottom", 18);
        panel.AddChild(margin);

        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 12);
        margin.AddChild(body);

        _title = MakeLabel(Dual("有未保存的修改", "There are unsaved changes"), true);
        body.AddChild(_title);

        _body = MakeDetails(Dual("退出前是否保存当前项目的修改？", "Would you like to save the current changes before exiting?"), scrollActive: false, fitContent: true, minHeight: 72f);
        body.AddChild(_body);

        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 8);
        body.AddChild(actions);

        _saveButton = MakeButton(Dual("保存并退出", "Save and Exit"), () => SaveAndExit?.Invoke());
        _discardButton = MakeButton(Dual("不保存", "Discard"), () => DiscardAndExit?.Invoke());
        _cancelButton = MakeButton(Dual("取消", "Cancel"), () => Cancel?.Invoke());
        actions.AddChild(_saveButton);
        actions.AddChild(_discardButton);
        actions.AddChild(_cancelButton);
    }

    public void RefreshTexts()
    {
        if (_title != null)
        {
            _title.Text = Dual("有未保存的修改", "There are unsaved changes");
        }

        if (_body != null)
        {
            _body.Text = Dual("退出前是否保存当前项目的修改？", "Would you like to save the current changes before exiting?");
        }

        if (_saveButton != null)
        {
            _saveButton.Text = Dual("保存并退出", "Save and Exit");
        }

        if (_discardButton != null)
        {
            _discardButton.Text = Dual("不保存", "Discard");
        }

        if (_cancelButton != null)
        {
            _cancelButton.Text = Dual("取消", "Cancel");
        }
    }

    public new void Show()
    {
        Visible = true;
        RefreshTexts();
    }

    public new void Hide()
    {
        Visible = false;
    }
}
