using HarmonyLib;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace STS2_Editor.Scripts.Editor.Runtime;

[HarmonyPatch]
internal static class RuntimeEventTemplatePersistencePatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CombatRoom), nameof(CombatRoom.ToSerializable))]
    private static void CombatRoom_ToSerializable_Postfix(CombatRoom __instance, ref SerializableRoom __result)
    {
        RuntimeEventTemplateSupport.TryWriteCombatRoomState(__instance, __result);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CombatRoom), nameof(CombatRoom.FromSerializable))]
    private static void CombatRoom_FromSerializable_Postfix(SerializableRoom serializableRoom, IRunState? runState, ref CombatRoom __result)
    {
        RuntimeEventTemplateSupport.TryReadCombatRoomState(serializableRoom);
    }
}
