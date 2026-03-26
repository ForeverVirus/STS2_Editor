using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using STS2_Editor.Scripts.Editor.Core.Utilities;

namespace STS2_Editor.Scripts.Editor.UI;

[HarmonyPatch]
internal static class ModStudioUiPatches
{
    private const string MenuButtonName = "ModStudioButton";

    private static readonly Dictionary<ulong, NModStudioModeChooserDialog> ChooserCache = new();
    private static readonly Dictionary<ulong, NModStudioProjectWindow> ProjectCache = new();
    private static readonly Dictionary<ulong, NModStudioPackageWindow> PackageCache = new();
    private static readonly Dictionary<ulong, NMainMenuTextButton> ButtonCache = new();
    private static readonly FieldInfo? LastHitButtonField = AccessTools.Field(typeof(NMainMenu), "_lastHitButton");
    private static readonly MethodInfo? MainMenuButtonFocusedMethod = AccessTools.Method(typeof(NMainMenu), "MainMenuButtonFocused");
    private static readonly MethodInfo? MainMenuButtonUnfocusedMethod = AccessTools.Method(typeof(NMainMenu), "MainMenuButtonUnfocused");

    static ModStudioUiPatches()
    {
        ModStudioLocalization.LanguageChanged += RefreshMenuButtonTexts;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(NMainMenu), nameof(NMainMenu._Ready))]
    private static void AddModStudioButton(NMainMenu __instance)
    {
        var container = __instance.GetNodeOrNull<VBoxContainer>("MainMenuTextButtons");
        if (container == null || container.GetNodeOrNull<NMainMenuTextButton>(MenuButtonName) != null)
        {
            return;
        }

        var quitButton = __instance.GetNodeOrNull<NMainMenuTextButton>("MainMenuTextButtons/QuitButton");
        var compendiumButton = __instance.GetNodeOrNull<NMainMenuTextButton>("MainMenuTextButtons/CompendiumButton") ?? quitButton;
        if (quitButton == null || compendiumButton == null)
        {
            Log.Warn("Mod Studio button could not be attached because the main menu button container is incomplete.");
            return;
        }

        var modStudioButton = CreateMenuButton(compendiumButton, quitButton);
        modStudioButton.Name = MenuButtonName;
        modStudioButton.Connect(NClickableControl.SignalName.Released, Callable.From<NButton>(button => OpenModStudio(__instance, button)));
        modStudioButton.Connect(NClickableControl.SignalName.Focused, Callable.From<NClickableControl>(button => HandleFocus(__instance, button as NMainMenuTextButton)));
        modStudioButton.Connect(NClickableControl.SignalName.Unfocused, Callable.From<NClickableControl>(button => HandleUnfocus(__instance, button as NMainMenuTextButton)));
        container.AddChild(modStudioButton);
        container.MoveChild(modStudioButton, quitButton.GetIndex());
        ButtonCache[__instance.GetInstanceId()] = modStudioButton;

        modStudioButton.FocusNeighborTop = compendiumButton.GetPath();
        modStudioButton.FocusNeighborBottom = quitButton.GetPath();
        compendiumButton.FocusNeighborBottom = modStudioButton.GetPath();
        quitButton.FocusNeighborTop = modStudioButton.GetPath();

        ApplyButtonText(modStudioButton);
        Log.Info("Mod Studio main menu entry attached.");
    }

    public static void RefreshMenuButtonTexts()
    {
        foreach (var pair in ButtonCache.ToList())
        {
            if (!GodotObject.IsInstanceValid(pair.Value))
            {
                ButtonCache.Remove(pair.Key);
                continue;
            }

            ApplyButtonText(pair.Value);
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NMainMenuSubmenuStack), nameof(NMainMenuSubmenuStack.GetSubmenuType), new[] { typeof(Type) })]
    private static bool AddModStudioSubmenu(NMainMenuSubmenuStack __instance, Type type, ref NSubmenu __result)
    {
        if (type != typeof(NModStudioModeChooserDialog) && type != typeof(NModStudioProjectWindow) && type != typeof(NModStudioPackageWindow))
        {
            return true;
        }

        var stackId = __instance.GetInstanceId();
        if (type == typeof(NModStudioModeChooserDialog))
        {
            if (!ChooserCache.TryGetValue(stackId, out var chooser) || !GodotObject.IsInstanceValid(chooser))
            {
                chooser = NModStudioModeChooserDialog.Create();
                chooser.Visible = false;
                __instance.AddChildSafely(chooser);
                ChooserCache[stackId] = chooser;
            }

            __result = chooser;
            return false;
        }

        if (type == typeof(NModStudioProjectWindow))
        {
            if (!ProjectCache.TryGetValue(stackId, out var projectWindow) || !GodotObject.IsInstanceValid(projectWindow))
            {
                projectWindow = NModStudioProjectWindow.Create();
                projectWindow.Visible = false;
                __instance.AddChildSafely(projectWindow);
                ProjectCache[stackId] = projectWindow;
            }

            __result = projectWindow;
            return false;
        }

        if (!PackageCache.TryGetValue(stackId, out var window) || !GodotObject.IsInstanceValid(window))
        {
            window = NModStudioPackageWindow.Create();
            window.Visible = false;
            __instance.AddChildSafely(window);
            PackageCache[stackId] = window;
        }

        __result = window;
        return false;
    }

    private static NMainMenuTextButton CreateMenuButton(NMainMenuTextButton primaryTemplate, NMainMenuTextButton fallbackTemplate)
    {
        var duplicateFlags = (int)(Node.DuplicateFlags.Groups | Node.DuplicateFlags.Scripts | Node.DuplicateFlags.UseInstantiation);
        if (primaryTemplate.Duplicate(duplicateFlags) is NMainMenuTextButton duplicated)
        {
            duplicated.FocusMode = Control.FocusModeEnum.All;
            return duplicated;
        }

        var templateLabel = primaryTemplate.GetNodeOrNull<MegaCrit.Sts2.addons.mega_text.MegaLabel>("Label")
            ?? fallbackTemplate.GetNodeOrNull<MegaCrit.Sts2.addons.mega_text.MegaLabel>("Label");
        var button = new NMainMenuTextButton
        {
            FocusMode = Control.FocusModeEnum.All,
            CustomMinimumSize = primaryTemplate.CustomMinimumSize,
            SizeFlagsHorizontal = primaryTemplate.SizeFlagsHorizontal,
            SizeFlagsVertical = primaryTemplate.SizeFlagsVertical,
            Theme = primaryTemplate.Theme,
            MouseFilter = primaryTemplate.MouseFilter,
            Scale = primaryTemplate.Scale,
            PivotOffset = primaryTemplate.PivotOffset
        };

        var label = new MegaCrit.Sts2.addons.mega_text.MegaLabel
        {
            Name = "Label",
            Theme = ResourceLoader.Load<Theme>("res://themes/main_menu_text_button.tres"),
            Text = ModStudioPackageUi.T("\u6a21\u7ec4\u5de5\u574a", "Mod Studio"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutoSizeEnabled = false,
            MinFontSize = 18,
            MaxFontSize = 32
        };
        if (templateLabel != null)
        {
            var themeFont = templateLabel.GetThemeFont("font");
            if (themeFont != null)
            {
                label.AddThemeFontOverride("font", themeFont);
            }

            var fontSize = templateLabel.GetThemeFontSize("font_size");
            if (fontSize > 0)
            {
                label.AddThemeFontSizeOverride("font_size", fontSize);
            }

            label.SelfModulate = templateLabel.SelfModulate;
        }

        button.AddChild(label);
        return button;
    }

    private static void ApplyButtonText(NMainMenuTextButton button)
    {
        var label = button.GetNodeOrNull<MegaCrit.Sts2.addons.mega_text.MegaLabel>("Label");
        if (label != null)
        {
            label.Text = ModStudioPackageUi.T("\u6a21\u7ec4\u5de5\u574a", "Mod Studio");
        }
    }

    private static void OpenModStudio(NMainMenu menu, NButton button)
    {
        LastHitButtonField?.SetValue(menu, button as NMainMenuTextButton);
        menu.SubmenuStack.PushSubmenuType<NModStudioModeChooserDialog>();
    }

    private static void HandleFocus(NMainMenu menu, NMainMenuTextButton? button)
    {
        if (button == null)
        {
            return;
        }

        MainMenuButtonFocusedMethod?.Invoke(menu, new object[] { button });
    }

    private static void HandleUnfocus(NMainMenu menu, NMainMenuTextButton? button)
    {
        if (button == null)
        {
            return;
        }

        MainMenuButtonUnfocusedMethod?.Invoke(menu, new object[] { button });
    }
}
