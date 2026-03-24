using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2_Editor.Scripts.Editor.Runtime;

[HarmonyPatch]
internal static class RuntimeGraphPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(CardModel), nameof(CardModel.OnPlayWrapper))]
    private static bool CardModel_OnPlayWrapper_Prefix(
        CardModel __instance,
        PlayerChoiceContext choiceContext,
        Creature? target,
        bool isAutoPlay,
        ResourceInfo resources,
        bool skipCardPileVisuals,
        ref Task __result)
    {
        if (!RuntimeGraphDispatcher.ShouldUseGraphForCard(__instance))
        {
            return true;
        }

        __result = ExecuteSafelyAsync(
            () => RuntimeGraphDispatcher.ExecuteCardPlayWrapperAsync(__instance, choiceContext, target, isAutoPlay, resources, skipCardPileVisuals),
            $"card graph override '{__instance.Id.Entry}'");
        return false;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CardModel), nameof(CardModel.GetDescriptionForPile), new[] { typeof(PileType), typeof(Creature) })]
    private static void CardModel_GetDescriptionForPile_Postfix(CardModel __instance, Creature? target, ref string __result)
    {
        if (RuntimeGraphDescriptionService.TryGetCardDescription(__instance, target, upgradedPreview: false, out var graphDescription))
        {
            __result = graphDescription;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CardModel), nameof(CardModel.GetDescriptionForUpgradePreview))]
    private static void CardModel_GetDescriptionForUpgradePreview_Postfix(CardModel __instance, ref string __result)
    {
        if (RuntimeGraphDescriptionService.TryGetCardDescription(__instance, null, upgradedPreview: true, out var graphDescription))
        {
            __result = graphDescription;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CardModel), nameof(CardModel.UpdateDynamicVarPreview))]
    private static void CardModel_UpdateDynamicVarPreview_Postfix(
        CardModel __instance,
        CardPreviewMode previewMode,
        Creature? target,
        DynamicVarSet dynamicVarSet)
    {
        RuntimeGraphPreviewService.ApplyCardPreview(__instance, previewMode, target, dynamicVarSet);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(PotionModel), nameof(PotionModel.OnUseWrapper))]
    private static bool PotionModel_OnUseWrapper_Prefix(
        PotionModel __instance,
        PlayerChoiceContext choiceContext,
        Creature? target,
        ref Task __result)
    {
        if (!RuntimeGraphDispatcher.ShouldUseGraphForPotion(__instance))
        {
            return true;
        }

        __result = ExecuteSafelyAsync(
            () => RuntimeGraphDispatcher.ExecutePotionOnUseWrapperAsync(__instance, choiceContext, target),
            $"potion graph override '{__instance.Id.Entry}'");
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Hook), nameof(Hook.BeforeCardPlayed), new[] { typeof(CombatState), typeof(CardPlay) })]
    private static bool Hook_BeforeCardPlayed_Prefix(CombatState combatState, CardPlay cardPlay, ref Task __result)
    {
        __result = ExecuteSafelyAsync(async () =>
        {
            foreach (var model in combatState.IterateHookListeners())
            {
                await RuntimeGraphDispatcher.ExecuteBeforeCardPlayedHookAsync(model, cardPlay);
                model.InvokeExecutionFinished();
            }
        }, "before-card-played graph hook dispatch");
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterCardPlayed), new[] { typeof(CombatState), typeof(PlayerChoiceContext), typeof(CardPlay) })]
    private static bool Hook_AfterCardPlayed_Prefix(CombatState combatState, PlayerChoiceContext choiceContext, CardPlay cardPlay, ref Task __result)
    {
        __result = ExecuteSafelyAsync(async () =>
        {
            foreach (var model in combatState.IterateHookListeners())
            {
                choiceContext.PushModel(model);
                try
                {
                    await RuntimeGraphDispatcher.ExecuteAfterCardPlayedHookAsync(model, choiceContext, cardPlay);
                    model.InvokeExecutionFinished();
                }
                finally
                {
                    if (choiceContext.LastInvolvedModel == model)
                    {
                        choiceContext.PopModel(model);
                    }
                }
            }

            foreach (var model in combatState.IterateHookListeners())
            {
                choiceContext.PushModel(model);
                try
                {
                    await RuntimeGraphDispatcher.ExecuteAfterCardPlayedLateHookAsync(model, choiceContext, cardPlay);
                    model.InvokeExecutionFinished();
                }
                finally
                {
                    if (choiceContext.LastInvolvedModel == model)
                    {
                        choiceContext.PopModel(model);
                    }
                }
            }
        }, "after-card-played graph hook dispatch");
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Hook), nameof(Hook.BeforePotionUsed), new[] { typeof(IRunState), typeof(CombatState), typeof(PotionModel), typeof(Creature) })]
    private static bool Hook_BeforePotionUsed_Prefix(IRunState runState, CombatState? combatState, PotionModel potion, Creature? target, ref Task __result)
    {
        __result = ExecuteSafelyAsync(async () =>
        {
            foreach (var model in runState.IterateHookListeners(combatState))
            {
                await RuntimeGraphDispatcher.ExecuteBeforePotionUsedHookAsync(model, potion, target);
                model.InvokeExecutionFinished();
            }
        }, "before-potion-used graph hook dispatch");
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterPotionUsed), new[] { typeof(IRunState), typeof(CombatState), typeof(PotionModel), typeof(Creature) })]
    private static bool Hook_AfterPotionUsed_Prefix(IRunState runState, CombatState? combatState, PotionModel potion, Creature? target, ref Task __result)
    {
        __result = ExecuteSafelyAsync(async () =>
        {
            foreach (var model in runState.IterateHookListeners(combatState))
            {
                await RuntimeGraphDispatcher.ExecuteAfterPotionUsedHookAsync(model, potion, target);
                model.InvokeExecutionFinished();
            }
        }, "after-potion-used graph hook dispatch");
        return false;
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
