using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using STS2_Editor.Scripts.Editor;

namespace STS2_Editor.Scripts.Editor.Runtime;

[HarmonyPatch]
internal static class RuntimeCoreInitializationPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ModelDb), nameof(ModelDb.InitIds))]
    private static void ModelDb_InitIds_Postfix()
    {
        ModStudioBootstrap.EnsureRuntimeInitialized();
    }
}
