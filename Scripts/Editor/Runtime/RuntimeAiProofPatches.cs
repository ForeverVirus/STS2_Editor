using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace STS2_Editor.Scripts.Editor.Runtime;

[HarmonyPatch]
internal static class RuntimeAiProofPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(NMainMenu), nameof(NMainMenu._Ready))]
    private static void NMainMenu_Ready_Postfix()
    {
        RuntimeAiProofHarness.NotifyMainMenuReady();
    }
}
