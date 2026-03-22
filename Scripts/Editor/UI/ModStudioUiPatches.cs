using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using STS2_Editor.Scripts.Editor.Core.Utilities;

namespace STS2_Editor.Scripts.Editor.UI;

[HarmonyPatch]
internal static class ModStudioUiPatches
{
    private const string MenuButtonName = "ModStudioButton";

    private static readonly Dictionary<ulong, NModStudioScreen> _screenCache = new();
    private static readonly Dictionary<ulong, NMainMenuTextButton> _buttonCache = new();
    private static readonly FieldInfo? _lastHitButtonField = AccessTools.Field(typeof(NMainMenu), "_lastHitButton");
    private static readonly MethodInfo? _mainMenuButtonFocusedMethod = AccessTools.Method(typeof(NMainMenu), "MainMenuButtonFocused");
    private static readonly MethodInfo? _mainMenuButtonUnfocusedMethod = AccessTools.Method(typeof(NMainMenu), "MainMenuButtonUnfocused");

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

        var templateLabel = compendiumButton.GetNodeOrNull<MegaCrit.Sts2.addons.mega_text.MegaLabel>("Label")
            ?? quitButton.GetNodeOrNull<MegaCrit.Sts2.addons.mega_text.MegaLabel>("Label");
        var modStudioButton = CreateMenuButton(templateLabel);
        modStudioButton.Name = MenuButtonName;
        modStudioButton.Connect(NClickableControl.SignalName.Released, Callable.From<NButton>(button => OpenModStudio(__instance, button)));
        modStudioButton.Connect(NClickableControl.SignalName.Focused, Callable.From<NClickableControl>(button => HandleFocus(__instance, button as NMainMenuTextButton)));
        modStudioButton.Connect(NClickableControl.SignalName.Unfocused, Callable.From<NClickableControl>(button => HandleUnfocus(__instance, button as NMainMenuTextButton)));
        container.AddChild(modStudioButton);
        container.MoveChild(modStudioButton, quitButton.GetIndex());
        _buttonCache[__instance.GetInstanceId()] = modStudioButton;

        var quitIndex = quitButton.GetIndex();
        if (quitIndex > 0)
        {
            modStudioButton.FocusNeighborTop = compendiumButton.GetPath();
            modStudioButton.FocusNeighborBottom = quitButton.GetPath();
            compendiumButton.FocusNeighborBottom = modStudioButton.GetPath();
            quitButton.FocusNeighborTop = modStudioButton.GetPath();
        }

        Log.Info("Mod Studio main menu entry attached.");
    }

    public static void RefreshMenuButtonTexts()
    {
        foreach (var pair in _buttonCache.ToList())
        {
            if (!GodotObject.IsInstanceValid(pair.Value))
            {
                _buttonCache.Remove(pair.Key);
                continue;
            }

            ApplyButtonText(pair.Value);
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NMainMenuSubmenuStack), nameof(NMainMenuSubmenuStack.GetSubmenuType), new[] { typeof(Type) })]
    private static bool AddModStudioSubmenu(NMainMenuSubmenuStack __instance, Type type, ref NSubmenu __result)
    {
        if (type != typeof(NModStudioScreen))
        {
            return true;
        }

        var stackId = __instance.GetInstanceId();
        if (!_screenCache.TryGetValue(stackId, out var screen) || !GodotObject.IsInstanceValid(screen))
        {
            screen = NModStudioScreen.Create();
            screen.Visible = false;
            __instance.AddChild(screen);
            _screenCache[stackId] = screen;
        }

        __result = screen;
        return false;
    }

    private static NMainMenuTextButton CreateMenuButton(MegaCrit.Sts2.addons.mega_text.MegaLabel? templateLabel)
    {
        var button = new NMainMenuTextButton
        {
            FocusMode = Control.FocusModeEnum.All,
            CustomMinimumSize = new Vector2(200f, 50f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };

        var label = new MegaCrit.Sts2.addons.mega_text.MegaLabel
        {
            Name = "Label",
            Theme = ResourceLoader.Load<Theme>("res://themes/main_menu_text_button.tres"),
            Text = ModStudioLocalization.T("mod_studio.title"),
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
        if (label == null)
        {
            return;
        }

        label.Text = ModStudioLocalization.T("mod_studio.title");
    }

    private static void OpenModStudio(NMainMenu menu, NButton button)
    {
        _lastHitButtonField?.SetValue(menu, button as NMainMenuTextButton);
        menu.SubmenuStack.PushSubmenuType<NModStudioScreen>();
    }

    private static void HandleFocus(NMainMenu menu, NMainMenuTextButton? button)
    {
        if (button == null)
        {
            return;
        }

        _mainMenuButtonFocusedMethod?.Invoke(menu, new object[] { button });
    }

    private static void HandleUnfocus(NMainMenu menu, NMainMenuTextButton? button)
    {
        if (button == null)
        {
            return;
        }

        _mainMenuButtonUnfocusedMethod?.Invoke(menu, new object[] { button });
    }
}
