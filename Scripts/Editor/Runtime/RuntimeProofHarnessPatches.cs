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
        if (!RuntimeProofHarness.TryPeekForcedEventId(out var eventId))
        {
            return true;
        }

        __result = HandleForcedEventAsync(eventId);
        return false;
    }

    private static async Task HandleForcedEventAsync(string eventId)
    {
        if (await RuntimeProofHarness.TryEnterForcedEventAsync(eventId))
        {
            RuntimeProofHarness.MarkForcedEventConsumed();
        }
    }
}
