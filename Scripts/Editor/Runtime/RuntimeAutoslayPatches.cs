using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
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

    [HarmonyPrefix]
    [HarmonyPatch(typeof(CardCmd), nameof(CardCmd.AutoPlay))]
    private static bool CardCmd_AutoPlay_Prefix(CardModel card, ref Task __result)
    {
        if (!RuntimeMonsterProofHarness.ShouldDelayPlayerActions() || card.Owner?.Creature?.IsPlayer != true)
        {
            return true;
        }

        __result = Task.CompletedTask;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(PotionModel), nameof(PotionModel.EnqueueManualUse))]
    private static bool PotionModel_EnqueueManualUse_Prefix(PotionModel __instance)
    {
        if (!RuntimeMonsterProofHarness.ShouldDelayPlayerActions() || __instance.Owner?.Creature?.IsPlayer != true)
        {
            return true;
        }

        return false;
    }
}
