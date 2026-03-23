using Godot;
using static STS2_Editor.Scripts.Editor.UI.ModStudioUiFactory;

namespace STS2_Editor.Scripts.Editor.UI;

internal sealed partial class ModStudioAssetEditor : MarginContainer
{
    private Label? _titleLabel;
    private Label? _originalPathLabel;
    private Label? _candidatePathLabel;
    private RichTextLabel? _detailsLabel;
    private Button? _saveButton;
    private Button? _revertButton;
    private TextureRect? _originalPreview;
    private TextureRect? _candidatePreview;

    public TextureRect OriginalPreview
    {
        get
        {
            EnsureBuilt();
            return _originalPreview!;
        }
    }

    public TextureRect CandidatePreview
    {
        get
        {
            EnsureBuilt();
            return _candidatePreview!;
        }
    }

    public event Action? SaveRequested;
    public event Action? RevertRequested;

    public override void _Ready()
    {
        EnsureBuilt();
    }

    public void EnsureBuilt()
    {
        BuildUi();
        RefreshTexts();
    }

    public void SetPreviews(Texture2D? original, string originalPath, Texture2D? candidate, string candidatePath, string details)
    {
        if (_originalPathLabel != null)
        {
            _originalPathLabel.Text = originalPath;
        }

        if (_candidatePathLabel != null)
        {
            _candidatePathLabel.Text = candidatePath;
        }

        if (_detailsLabel != null)
        {
            _detailsLabel.Text = details;
        }

        if (_originalPreview != null)
        {
            _originalPreview.Texture = original;
        }

        if (_candidatePreview != null)
        {
            _candidatePreview.Texture = candidate;
        }
    }

    public void RefreshTexts()
    {
        if (_titleLabel != null)
        {
            _titleLabel.Text = Dual("资源预览", "Asset Preview");
        }

        if (_saveButton != null)
        {
            _saveButton.Text = Dual("保存应用", "Save Apply");
        }

        if (_revertButton != null)
        {
            _revertButton.Text = Dual("还原", "Revert");
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

        _titleLabel = MakeLabel(string.Empty, true);
        root.AddChild(_titleLabel);

        var compareRow = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        compareRow.AddThemeConstantOverride("separation", 10);
        root.AddChild(compareRow);

        compareRow.AddChild(BuildPreviewPane(Dual("当前预览", "Current"), out _originalPreview, out _originalPathLabel));
        compareRow.AddChild(BuildPreviewPane(Dual("候选预览", "Candidate"), out _candidatePreview, out _candidatePathLabel));

        _detailsLabel = MakeDetails(string.Empty, scrollActive: false, fitContent: true, minHeight: 72f);
        root.AddChild(_detailsLabel);

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
    }

    private static Control BuildPreviewPane(string title, out TextureRect textureRect, out Label pathLabel)
    {
        var panel = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsStretchRatio = 1f
        };
        panel.AddThemeConstantOverride("separation", 6);

        panel.AddChild(MakeLabel(title, true));
        pathLabel = MakeLabel(string.Empty, true);
        panel.AddChild(pathLabel);

        var previewPanel = new PanelContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        panel.AddChild(previewPanel);

        textureRect = new TextureRect
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            CustomMinimumSize = new Vector2(0f, 340f)
        };
        previewPanel.AddChild(textureRect);
        return panel;
    }
}
