using Godot;
using STS2_Editor.Scripts.Editor.Core.Utilities;

namespace STS2_Editor.Scripts.Editor.UI;

internal static class ModStudioPackageUi
{
    public static string T(string zh, string en) => ModStudioLocalization.IsChinese ? zh : en;

    public static Label MakeLabel(string zh, string en, bool expand = false)
    {
        return new Label
        {
            Text = T(zh, en),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = expand ? Control.SizeFlags.ExpandFill : Control.SizeFlags.Fill
        };
    }

    public static RichTextLabel MakeRichText(string zh, string en, bool fitContent = false, float minHeight = 120f)
    {
        return new RichTextLabel
        {
            Text = T(zh, en),
            BbcodeEnabled = false,
            ScrollActive = true,
            FitContent = fitContent,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0f, minHeight)
        };
    }

    public static Button MakeButton(string zh, string en, Action callback, bool toggle = false)
    {
        var button = new Button
        {
            Text = T(zh, en),
            ToggleMode = toggle,
            FocusMode = Control.FocusModeEnum.All,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0f, 30f)
        };
        button.Pressed += callback;
        return button;
    }

    public static LineEdit MakeSearchBox(string zh, string en)
    {
        return new LineEdit
        {
            PlaceholderText = T(zh, en),
            ClearButtonEnabled = true,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0f, 30f)
        };
    }

    public static void ClearChildren(Node? node)
    {
        if (node == null)
        {
            return;
        }

        foreach (var child in node.GetChildren())
        {
            child.QueueFree();
        }
    }
}
