using HarmonyLib;
using MegaCrit.Sts2.Core.AutoSlay.Handlers.Screens;

namespace STS2_Editor.Scripts.Editor.Runtime;

[HarmonyPatch]
internal static class RuntimeProofHarnessPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(MapScreenHandler), nameof(MapScreenHandler.HandleAsync))]
    private static bool MapScreenHandler_HandleAsync_Prefix(ref Task __result)
    {
        if (RuntimeProofHarness.TryPeekForcedEventId(out var eventId))
        {
            __result = HandleForcedEventAsync(eventId);
            return false;
        }

        if (RuntimeMonsterProofHarness.TryPeekForcedMonsterId(out var monsterId))
        {
            __result = HandleForcedMonsterAsync(monsterId);
            return false;
        }

        if (!RuntimeMonsterProofHarness.TryPeekForcedEncounterId(out var encounterId))
        {
            return true;
        }

        __result = HandleForcedEncounterAsync(encounterId);
        return false;
    }

    private static async Task HandleForcedEventAsync(string eventId)
    {
        if (await RuntimeProofHarness.TryEnterForcedEventAsync(eventId))
        {
            RuntimeProofHarness.MarkForcedEventConsumed();
        }
    }

    private static async Task HandleForcedEncounterAsync(string encounterId)
    {
        if (await RuntimeMonsterProofHarness.TryEnterForcedEncounterAsync(encounterId))
        {
            RuntimeMonsterProofHarness.MarkForcedEncounterConsumed();
        }
    }

    private static async Task HandleForcedMonsterAsync(string monsterId)
    {
        if (await RuntimeMonsterProofHarness.TryEnterForcedMonsterAsync(monsterId))
        {
            RuntimeMonsterProofHarness.MarkForcedEncounterConsumed();
        }
    }
}
