using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace STS2_Editor.Scripts.Editor.Runtime;

[HarmonyPatch]
internal static class RuntimeMonsterPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(MonsterModel), nameof(MonsterModel.SetUpForCombat))]
    private static bool MonsterModel_SetUpForCombat_Prefix(MonsterModel __instance)
    {
        if (!RuntimeMonsterDispatcher.ShouldUseOverride(__instance))
        {
            return true;
        }

        RuntimeMonsterDispatcher.ExecuteSetUp(__instance);
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(MonsterModel), nameof(MonsterModel.RollMove))]
    private static bool MonsterModel_RollMove_Prefix(MonsterModel __instance, IEnumerable<Creature> targets)
    {
        if (!RuntimeMonsterDispatcher.ShouldUseOverride(__instance))
        {
            return true;
        }

        RuntimeMonsterDispatcher.ExecuteRollMove(__instance, targets);
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(MonsterModel), nameof(MonsterModel.PerformMove))]
    private static bool MonsterModel_PerformMove_Prefix(MonsterModel __instance, ref Task __result)
    {
        if (!RuntimeMonsterDispatcher.ShouldUseOverride(__instance))
        {
            return true;
        }

        if (string.Equals(__instance.NextMove.Id, MonsterModel.stunnedMoveId, StringComparison.Ordinal))
        {
            return true;
        }

        __result = ExecuteSafelyAsync(
            () => RuntimeMonsterDispatcher.ExecutePerformMoveAsync(__instance),
            $"monster override '{__instance.Id.Entry}'");
        return false;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CombatManager), nameof(CombatManager.AfterCreatureAdded))]
    private static void CombatManager_AfterCreatureAdded_Postfix(Creature creature, ref Task __result)
    {
        if (creature.Monster == null || !RuntimeMonsterDispatcher.ShouldUseOverride(creature.Monster))
        {
            return;
        }

        __result = ContinueAfterCreatureAddedAsync(__result, creature);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(CombatManager), nameof(CombatManager.RemoveCreature))]
    private static void CombatManager_RemoveCreature_Prefix(Creature creature)
    {
        RuntimeMonsterDispatcher.ExecuteBeforeRemovedAsync(creature).GetAwaiter().GetResult();
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Creature), nameof(Creature.InvokeDiedEvent))]
    private static void Creature_InvokeDiedEvent_Prefix(Creature __instance)
    {
        RuntimeMonsterDispatcher.ExecuteBeforeDeathAsync(__instance).GetAwaiter().GetResult();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Creature), nameof(Creature.InvokeDiedEvent))]
    private static void Creature_InvokeDiedEvent_Postfix(Creature __instance)
    {
        MonsterLifecycleManager.ExecuteAllyDiedAsync(__instance).GetAwaiter().GetResult();
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(MonsterModel), nameof(MonsterModel.OnDieToDoom))]
    private static bool MonsterModel_OnDieToDoom_Prefix(MonsterModel __instance)
    {
        if (!RuntimeMonsterDispatcher.ShouldUseOverride(__instance))
        {
            return true;
        }

        RuntimeMonsterDispatcher.ExecuteOnDieToDoom(__instance);
        return false;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterCurrentHpChanged), new[] { typeof(MegaCrit.Sts2.Core.Runs.IRunState), typeof(CombatState), typeof(Creature), typeof(decimal) })]
    private static void Hook_AfterCurrentHpChanged_Postfix(Creature creature, decimal delta, ref Task __result)
    {
        if (creature.CombatState == null)
        {
            return;
        }

        __result = ContinueAfterCurrentHpChangedAsync(__result, creature, delta);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Creature), "StunInternal")]
    private static void Creature_StunInternal_Prefix(Creature __instance, ref string? nextMoveId)
    {
        if (__instance.Monster == null || !RuntimeMonsterDispatcher.ShouldUseOverride(__instance.Monster))
        {
            return;
        }

        nextMoveId = string.IsNullOrWhiteSpace(nextMoveId)
            ? RuntimeMonsterDispatcher.ResolveResumeTurnId(__instance.Monster)
            : nextMoveId;
        if (string.IsNullOrWhiteSpace(nextMoveId))
        {
            return;
        }

        if (RuntimeMonsterDispatcher.TryGetStateForPatch(__instance.Monster, out var runtimeState))
        {
            runtimeState.ResumeTurnId = nextMoveId;
        }
    }

    private static async Task ContinueAfterCreatureAddedAsync(Task original, Creature creature)
    {
        await original;
        await ExecuteSafelyAsync(
            () => RuntimeMonsterDispatcher.ExecuteAfterCreatureAddedAsync(creature),
            $"monster after-added override '{creature.Monster?.Id.Entry ?? creature.ModelId.Entry}'");
    }

    private static async Task ContinueAfterCurrentHpChangedAsync(Task original, Creature creature, decimal delta)
    {
        await original;
        await ExecuteSafelyAsync(
            () => RuntimeMonsterDispatcher.ExecuteAfterCurrentHpChangedAsync(creature, delta),
            $"monster hp-changed override '{creature.Monster?.Id.Entry ?? creature.ModelId.Entry}'");
    }

    private static async Task ExecuteSafelyAsync(Func<Task> work, string label)
    {
        try
        {
            await work();
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed during {label}: {ex.Message}");
            throw;
        }
    }
}
