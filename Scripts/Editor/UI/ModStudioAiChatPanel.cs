using Godot;
using STS2_Editor.Scripts.Editor.AI;
using static STS2_Editor.Scripts.Editor.UI.ModStudioUiFactory;

namespace STS2_Editor.Scripts.Editor.UI;

internal sealed partial class ModStudioAiChatPanel : PanelContainer
{
    private Label? _titleLabel;
    private Label? _statusLabel;
    private Button? _closeButton;
    private RichTextLabel? _transcriptLabel;
    private VBoxContainer? _streamingHost;
    private Label? _streamingTitleLabel;
    private RichTextLabel? _streamingLabel;
    private VBoxContainer? _previewHost;
    private Label? _previewTitleLabel;
    private RichTextLabel? _previewLabel;
    private Button? _applyPreviewButton;
    private Button? _discardPreviewButton;
    private TextEdit? _inputEdit;
    private Button? _sendButton;

    public event Action<string>? SendRequested;
    public event Action? Closed;
    public event Action? ApplyPreviewRequested;
    public event Action? DiscardPreviewRequested;

    public override void _Ready()
    {
        BuildUi();
        RefreshTexts();
        Visible = false;
    }

    public void RefreshTexts()
    {
        if (_titleLabel != null)
        {
            _titleLabel.Text = Dual("AI 助手", "AI Assistant");
        }

        if (_closeButton != null)
        {
            _closeButton.Text = Dual("关闭", "Close");
        }

        if (_previewTitleLabel != null)
        {
            _previewTitleLabel.Text = Dual("待应用预览", "Pending Preview");
        }

        if (_streamingTitleLabel != null)
        {
            _streamingTitleLabel.Text = Dual("实时输出", "Live Output");
        }

        if (_applyPreviewButton != null)
        {
            _applyPreviewButton.Text = Dual("应用", "Apply");
        }

        if (_discardPreviewButton != null)
        {
            _discardPreviewButton.Text = Dual("丢弃预览", "Discard Preview");
        }

        if (_sendButton != null)
        {
            _sendButton.Text = Dual("发送", "Send");
        }

        if (_inputEdit != null)
        {
            _inputEdit.PlaceholderText = Dual("输入你的编辑需求。输入 /new 开启新会话。", "Describe the edit you want. Type /new to start a new session.");
        }
    }

    public void ShowPanel()
    {
        Visible = true;
        TopLevel = true;
        ZIndex = 2500;
        _inputEdit?.GrabFocus();
    }

    public void UpdateLayout(Vector2 viewportSize)
    {
        var width = Math.Min(960f, Math.Max(720f, viewportSize.X * 0.72f));
        var height = Math.Min(520f, Math.Max(380f, viewportSize.Y * 0.42f));
        CustomMinimumSize = new Vector2(width, height);
        Size = new Vector2(width, height);
        AnchorLeft = 0f;
        AnchorTop = 0f;
        AnchorRight = 0f;
        AnchorBottom = 0f;
        Position = new Vector2((viewportSize.X - width) * 0.5f, Math.Max(56f, viewportSize.Y - height - 24f));
    }

    public void SetBusy(bool busy, string statusText)
    {
        if (_statusLabel != null)
        {
            _statusLabel.Text = statusText ?? string.Empty;
        }

        if (_sendButton != null)
        {
            _sendButton.Disabled = busy;
        }

        if (_inputEdit != null)
        {
            _inputEdit.Editable = !busy;
        }
    }

    public void SetMessages(IReadOnlyList<AiChatMessage> messages)
    {
        if (_transcriptLabel == null)
        {
            return;
        }

        var lines = new List<string>();
        foreach (var message in messages.Where(message => message.IsVisibleInTranscript))
        {
            var speaker = message.Role switch
            {
                "assistant" => Dual("AI", "AI"),
                "system" => Dual("系统", "System"),
                _ => Dual("你", "You")
            };
            lines.Add($"[{speaker}]");
            lines.Add(message.Content ?? string.Empty);
            lines.Add(string.Empty);
        }

        _transcriptLabel.Text = string.Join(System.Environment.NewLine, lines);
    }

    public void SetPendingPreview(AiPlanPreview? preview)
    {
        if (_previewHost == null || _previewLabel == null || _applyPreviewButton == null || _discardPreviewButton == null)
        {
            return;
        }

        if (preview == null)
        {
            _previewHost.Visible = false;
            _previewLabel.Text = string.Empty;
            _applyPreviewButton.Disabled = true;
            return;
        }

        var lines = new List<string>();
        foreach (var line in preview.SummaryLines)
        {
            lines.Add($"- {line}");
        }

        foreach (var warning in preview.WarningLines)
        {
            lines.Add($"{Dual("警告", "Warning")}: {warning}");
        }

        foreach (var error in preview.ErrorLines)
        {
            lines.Add($"{Dual("错误", "Error")}: {error}");
        }

        _previewHost.Visible = true;
        _previewLabel.Text = string.Join(System.Environment.NewLine, lines);
        _applyPreviewButton.Disabled = !preview.IsValid;
        _discardPreviewButton.Disabled = false;
    }

    public void SetStreamingPreview(string reasoningText, string contentText)
    {
        if (_streamingHost == null || _streamingLabel == null)
        {
            return;
        }

        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(reasoningText))
        {
            lines.Add(Dual("[思考]", "[Reasoning]"));
            lines.Add(reasoningText);
            lines.Add(string.Empty);
        }

        if (!string.IsNullOrWhiteSpace(contentText))
        {
            lines.Add(Dual("[输出]", "[Output]"));
            lines.Add(contentText);
        }

        _streamingHost.Visible = lines.Count > 0;
        _streamingLabel.Text = string.Join(System.Environment.NewLine, lines);
    }

    public void ClearStreamingPreview()
    {
        if (_streamingHost != null)
        {
            _streamingHost.Visible = false;
        }

        if (_streamingLabel != null)
        {
            _streamingLabel.Text = string.Empty;
        }
    }

    public void ClearInput()
    {
        if (_inputEdit != null)
        {
            _inputEdit.Text = string.Empty;
        }
    }

    private void BuildUi()
    {
        if (GetChildCount() > 0)
        {
            return;
        }

        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;
        TopLevel = true;
        ZIndex = 2500;

        var panelStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.06f, 0.07f, 0.09f, 0.985f),
            BorderColor = new Color(0.38f, 0.46f, 0.60f, 1f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10,
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            ShadowColor = new Color(0f, 0f, 0f, 0.35f),
            ShadowSize = 10
        };
        AddThemeStyleboxOverride("panel", panelStyle);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        AddChild(margin);

        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", 8);
        margin.AddChild(root);

        var header = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        header.AddThemeConstantOverride("separation", 8);
        root.AddChild(header);

        _titleLabel = MakeLabel(string.Empty, true);
        _statusLabel = MakeLabel(string.Empty, true);
        _statusLabel.HorizontalAlignment = HorizontalAlignment.Right;
        _closeButton = MakeButton(string.Empty, () =>
        {
            Visible = false;
            Closed?.Invoke();
        });
        _closeButton.CustomMinimumSize = new Vector2(88f, 30f);
        header.AddChild(_titleLabel);
        header.AddChild(_statusLabel);
        header.AddChild(_closeButton);

        _transcriptLabel = MakeDetails(string.Empty, scrollActive: true, fitContent: false, minHeight: 120f);
        _transcriptLabel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        root.AddChild(_transcriptLabel);

        _streamingHost = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            Visible = false
        };
        _streamingHost.AddThemeConstantOverride("separation", 6);
        root.AddChild(_streamingHost);

        _streamingTitleLabel = MakeLabel(string.Empty, true);
        _streamingHost.AddChild(_streamingTitleLabel);

        _streamingLabel = MakeDetails(string.Empty, scrollActive: true, fitContent: false, minHeight: 90f);
        _streamingHost.AddChild(_streamingLabel);

        _previewHost = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            Visible = false
        };
        _previewHost.AddThemeConstantOverride("separation", 6);
        root.AddChild(_previewHost);

        _previewTitleLabel = MakeLabel(string.Empty, true);
        _previewHost.AddChild(_previewTitleLabel);

        _previewLabel = MakeDetails(string.Empty, scrollActive: true, fitContent: false, minHeight: 90f);
        _previewHost.AddChild(_previewLabel);

        var previewActions = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        previewActions.AddThemeConstantOverride("separation", 8);
        _previewHost.AddChild(previewActions);

        _applyPreviewButton = MakeButton(string.Empty, () => ApplyPreviewRequested?.Invoke());
        _discardPreviewButton = MakeButton(string.Empty, () => DiscardPreviewRequested?.Invoke());
        previewActions.AddChild(_applyPreviewButton);
        previewActions.AddChild(_discardPreviewButton);

        _inputEdit = new TextEdit
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0f, 84f),
            WrapMode = TextEdit.LineWrappingMode.Boundary
        };
        root.AddChild(_inputEdit);

        var inputActions = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        inputActions.AddThemeConstantOverride("separation", 8);
        root.AddChild(inputActions);

        _sendButton = MakeButton(string.Empty, HandleSend);
        inputActions.AddChild(_sendButton);
    }

    private void HandleSend()
    {
        var text = _inputEdit?.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        SendRequested?.Invoke(text.Trim());
    }
}
