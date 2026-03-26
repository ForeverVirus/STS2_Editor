using Godot;
using STS2_Editor.Scripts.Editor.AI;
using static STS2_Editor.Scripts.Editor.UI.ModStudioUiFactory;

namespace STS2_Editor.Scripts.Editor.UI;

internal sealed partial class ModStudioAiConfigDialog : Control
{
    private LineEdit? _baseUrlEdit;
    private LineEdit? _apiKeyEdit;
    private LineEdit? _modelEdit;
    private Label? _titleLabel;
    private Label? _baseUrlLabel;
    private Label? _apiKeyLabel;
    private Label? _modelLabel;
    private Button? _saveButton;
    private Button? _cancelButton;
    private RichTextLabel? _hintLabel;

    public event Action<AiClientSettings>? SaveRequested;

    public override void _Ready()
    {
        BuildUi();
        RefreshTexts();
        Hide();
    }

    public void ShowDialog(AiClientSettings settings)
    {
        BuildUi();
        _baseUrlEdit!.Text = settings.BaseUrl ?? string.Empty;
        _apiKeyEdit!.Text = settings.ApiKey ?? string.Empty;
        _modelEdit!.Text = settings.Model ?? string.Empty;
        Show();
        _baseUrlEdit.GrabFocus();
    }

    public void RefreshTexts()
    {
        if (_titleLabel != null)
        {
            _titleLabel.Text = Dual("AI 设置", "AI Settings");
        }

        if (_baseUrlLabel != null)
        {
            _baseUrlLabel.Text = "Base URL";
        }

        if (_apiKeyLabel != null)
        {
            _apiKeyLabel.Text = "API Key";
        }

        if (_modelLabel != null)
        {
            _modelLabel.Text = Dual("模型", "Model");
        }

        if (_saveButton != null)
        {
            _saveButton.Text = Dual("保存", "Save");
        }

        if (_cancelButton != null)
        {
            _cancelButton.Text = Dual("关闭", "Close");
        }

        if (_hintLabel != null)
        {
            _hintLabel.Text = Dual(
                "请填写 OpenAI 兼容接口配置。只有点击“关闭”按钮才会关闭这个窗口。",
                "Configure an OpenAI-compatible API endpoint. This dialog closes only when you click the Close button.");
        }
    }

    public new void Show()
    {
        Visible = true;
    }

    public new void Hide()
    {
        Visible = false;
    }

    private void BuildUi()
    {
        if (GetChildCount() > 0)
        {
            return;
        }

        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        ZIndex = 3000;

        AddChild(new ColorRect
        {
            AnchorRight = 1f,
            AnchorBottom = 1f,
            Color = new Color(0f, 0f, 0f, 0.68f),
            MouseFilter = MouseFilterEnum.Stop
        });

        var center = new CenterContainer
        {
            AnchorRight = 1f,
            AnchorBottom = 1f,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Stop
        };
        AddChild(center);

        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(720f, 0f),
            MouseFilter = MouseFilterEnum.Stop
        };
        var panelStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.09f, 0.11f, 0.98f),
            BorderColor = new Color(0.42f, 0.48f, 0.58f, 1f),
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10,
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1
        };
        panel.AddThemeStyleboxOverride("panel", panelStyle);
        center.AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 18);
        margin.AddThemeConstantOverride("margin_top", 18);
        margin.AddThemeConstantOverride("margin_right", 18);
        margin.AddThemeConstantOverride("margin_bottom", 18);
        panel.AddChild(margin);

        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", 10);
        margin.AddChild(root);

        _titleLabel = MakeLabel(string.Empty, true);
        _titleLabel.AddThemeColorOverride("font_color", new Color(0.94f, 0.96f, 1f, 1f));
        root.AddChild(_titleLabel);

        _hintLabel = MakeDetails(string.Empty, scrollActive: false, fitContent: true, minHeight: 56f);
        root.AddChild(_hintLabel);

        root.AddChild(BuildFieldRow(out _baseUrlLabel, out _baseUrlEdit, secret: false));
        root.AddChild(BuildFieldRow(out _apiKeyLabel, out _apiKeyEdit, secret: true));
        root.AddChild(BuildFieldRow(out _modelLabel, out _modelEdit, secret: false));

        var actions = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        actions.AddThemeConstantOverride("separation", 8);
        root.AddChild(actions);

        _saveButton = MakeButton(string.Empty, HandleSave);
        _cancelButton = MakeButton(string.Empty, Hide);
        actions.AddChild(_saveButton);
        actions.AddChild(_cancelButton);
    }

    private Control BuildFieldRow(out Label label, out LineEdit lineEdit, bool secret)
    {
        var row = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        row.AddThemeConstantOverride("separation", 4);

        label = MakeLabel(string.Empty, true);
        label.AddThemeColorOverride("font_color", new Color(0.86f, 0.90f, 0.96f, 1f));
        row.AddChild(label);

        lineEdit = new LineEdit
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            Secret = secret,
            PlaceholderText = secret ? "sk-..." : string.Empty
        };
        row.AddChild(lineEdit);
        return row;
    }

    private void HandleSave()
    {
        var settings = new AiClientSettings
        {
            BaseUrl = _baseUrlEdit?.Text ?? string.Empty,
            ApiKey = _apiKeyEdit?.Text ?? string.Empty,
            Model = _modelEdit?.Text ?? string.Empty
        };
        settings.Normalize();

        if (!settings.IsConfigured)
        {
            OS.Alert(
                Dual("请完整填写 Base URL、API Key 和模型名。", "Fill in Base URL, API Key, and model."),
                Dual("配置不完整", "Incomplete Settings"));
            return;
        }

        SaveRequested?.Invoke(settings);
        Hide();
    }
}
