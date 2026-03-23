using Godot;
using STS2_Editor.Scripts.Editor.Core.Utilities;

namespace STS2_Editor.Scripts.Editor.UI;

internal static class ModStudioUiFactory
{
    public static string Dual(string zh, string en) => ModStudioLocalization.IsChinese ? zh : en;

    public static Button MakeButton(string text, Action onPressed, bool toggle = false)
    {
        var button = new Button
        {
            Text = text,
            ToggleMode = toggle,
            FocusMode = Control.FocusModeEnum.All,
            SizeFlagsHorizontal = Control.SizeFlags.Fill,
            CustomMinimumSize = new Vector2(0f, 30f)
        };
        button.Pressed += onPressed;
        return button;
    }

    public static Label MakeLabel(string text, bool expand = false)
    {
        return new Label
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = expand ? Control.SizeFlags.ExpandFill : Control.SizeFlags.Fill
        };
    }

    public static RichTextLabel MakeDetails(string text, bool scrollActive = true, bool fitContent = false, float minHeight = 120f)
    {
        return new RichTextLabel
        {
            BbcodeEnabled = false,
            ScrollActive = scrollActive,
            FitContent = fitContent,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0f, minHeight),
            Text = text
        };
    }

    public static ScrollContainer MakeScrollList(string name, out VBoxContainer list)
    {
        var scroll = new ScrollContainer
        {
            Name = $"{name}Scroll",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };

        list = new VBoxContainer
        {
            Name = name,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        list.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(list);
        return scroll;
    }

    public static PanelContainer MakePanel(Control content, int margin = 10)
    {
        var panel = new PanelContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };

        var body = new MarginContainer
        {
            AnchorRight = 1f,
            AnchorBottom = 1f,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        body.AddThemeConstantOverride("margin_left", margin);
        body.AddThemeConstantOverride("margin_top", margin);
        body.AddThemeConstantOverride("margin_right", margin);
        body.AddThemeConstantOverride("margin_bottom", margin);
        body.AddChild(content);
        panel.AddChild(body);
        return panel;
    }

    public static HBoxContainer MakeActionRow(params (string Text, Action Callback)[] actions)
    {
        var row = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        row.AddThemeConstantOverride("separation", 8);
        foreach (var (text, callback) in actions)
        {
            row.AddChild(MakeButton(text, callback));
        }
        return row;
    }

    public static void ApplyLanguageToNode(Node node, Action? refresh)
    {
        if (node == null)
        {
            return;
        }

        refresh?.Invoke();
        foreach (var child in node.GetChildren())
        {
            if (child is Node childNode)
            {
                ApplyLanguageToNode(childNode, refresh);
            }
        }
    }
}
