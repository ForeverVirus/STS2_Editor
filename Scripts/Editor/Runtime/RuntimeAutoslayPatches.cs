using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;

namespace STS2_Editor.Scripts.Editor.Runtime;

[HarmonyPatch]
internal static class RuntimeAutoslayPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(NGame), nameof(NGame.IsReleaseGame))]
    private static void NGame_IsReleaseGame_Postfix(ref bool __result)
    {
        if (CommandLineHelper.HasArg("autoslay"))
        {
            __result = false;
        }
    }
}
