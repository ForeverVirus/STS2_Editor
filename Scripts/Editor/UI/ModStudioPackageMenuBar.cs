using Godot;

namespace STS2_Editor.Scripts.Editor.UI;

internal sealed partial class ModStudioPackageMenuBar : PanelContainer
{
    private Label? _titleLabel;
    private Label? _pathLabel;
    private Label? _stateLabel;
    private Button? _hotReloadButton;
    private Button? _closeButton;

    public event Action? HotReloadRequested;
    public event Action? CloseRequested;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(0f, 40f);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;

        AddChild(new ColorRect
        {
            AnchorRight = 1f,
            AnchorBottom = 1f,
            Color = new Color(0.08f, 0.10f, 0.12f, 0.98f),
            MouseFilter = MouseFilterEnum.Ignore
        });

        var row = new HBoxContainer
        {
            AnchorRight = 1f,
            AnchorBottom = 1f,
            OffsetLeft = 10f,
            OffsetTop = 6f,
            OffsetRight = -10f,
            OffsetBottom = -6f,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        row.AddThemeConstantOverride("separation", 8);
        AddChild(row);

        _titleLabel = ModStudioPackageUi.MakeLabel("\u6a21\u7ec4\u7ba1\u7406\u5668", "Package Manager");
        _titleLabel.CustomMinimumSize = new Vector2(110f, 0f);
        row.AddChild(_titleLabel);

        row.AddChild(new VSeparator());

        _pathLabel = ModStudioPackageUi.MakeLabel(string.Empty, string.Empty, true);
        _pathLabel.AutowrapMode = TextServer.AutowrapMode.Off;
        _pathLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(_pathLabel);

        _stateLabel = ModStudioPackageUi.MakeLabel(string.Empty, string.Empty, true);
        _stateLabel.HorizontalAlignment = HorizontalAlignment.Right;
        _stateLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(_stateLabel);

        _hotReloadButton = ModStudioPackageUi.MakeButton("\u70ed\u91cd\u8f7d", "Hot Reload", () => HotReloadRequested?.Invoke());
        _closeButton = ModStudioPackageUi.MakeButton("\u9000\u51fa", "Exit", () => CloseRequested?.Invoke());
        row.AddChild(_hotReloadButton);
        row.AddChild(_closeButton);

        RefreshLocalizedText();
    }

    public void SetStateText(string text)
    {
        if (_stateLabel != null)
        {
            _stateLabel.Text = text;
        }
    }

    public void SetPublishedRootPath(string path)
    {
        if (_pathLabel != null)
        {
            _pathLabel.Text = path;
        }
    }

    public void RefreshLocalizedText()
    {
        if (_titleLabel != null)
        {
            _titleLabel.Text = ModStudioPackageUi.T("\u6a21\u7ec4\u7ba1\u7406\u5668", "Package Manager");
        }

        if (_pathLabel != null && string.IsNullOrWhiteSpace(_pathLabel.Text))
        {
            _pathLabel.Text = ModStudioPackageUi.T("\u6b63\u5728\u5b9a\u4f4d\u53d1\u5e03\u5305\u76ee\u5f55", "Locating published package root...");
        }

        if (_hotReloadButton != null)
        {
            _hotReloadButton.Text = ModStudioPackageUi.T("\u70ed\u91cd\u8f7d", "Hot Reload");
        }

        if (_closeButton != null)
        {
            _closeButton.Text = ModStudioPackageUi.T("\u9000\u51fa", "Exit");
        }
    }
}
