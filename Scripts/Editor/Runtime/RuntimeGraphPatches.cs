using HarmonyLib;
using MegaCrit.Sts2.Core.Audio.Debug;
using System.Reflection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.ValueProps;

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
    [HarmonyPatch(typeof(EnchantmentModel), nameof(EnchantmentModel.ModifyCard))]
    private static bool EnchantmentModel_ModifyCard_Prefix(EnchantmentModel __instance)
    {
        if (!RuntimeGraphDispatcher.ShouldUseGraphForEnchantmentOnEnchant(__instance))
        {
            return true;
        }

        ExecuteSafelyAsync(async () =>
        {
            if (__instance.Card == null)
            {
                throw new InvalidOperationException("Card must be set before running enchantment graph.");
            }

            await RuntimeGraphDispatcher.ExecuteEnchantmentOnEnchantAsync(__instance);
            __instance.RecalculateValues();
            __instance.Card.DynamicVars.RecalculateForUpgradeOrEnchant();
        }, $"enchantment graph override '{__instance.Id.Entry}' (on_enchant)").GetAwaiter().GetResult();
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Hook), nameof(Hook.BeforeAttack), new[] { typeof(CombatState), typeof(AttackCommand) })]
    private static bool Hook_BeforeAttack_Prefix(CombatState combatState, AttackCommand command, ref Task __result)
    {
        __result = ExecuteSafelyAsync(async () =>
        {
            foreach (var model in combatState.IterateHookListeners())
            {
                await RuntimeGraphDispatcher.ExecuteBeforeAttackHookAsync(model, command);
                model.InvokeExecutionFinished();
            }
        }, "before-attack graph hook dispatch");
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterAttack), new[] { typeof(CombatState), typeof(AttackCommand) })]
    private static bool Hook_AfterAttack_Prefix(CombatState combatState, AttackCommand command, ref Task __result)
    {
        __result = ExecuteSafelyAsync(async () =>
        {
            foreach (var model in combatState.IterateHookListeners())
            {
                await RuntimeGraphDispatcher.ExecuteAfterAttackHookAsync(model, command);
                model.InvokeExecutionFinished();
            }
        }, "after-attack graph hook dispatch");
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterActEntered), new[] { typeof(IRunState) })]
    private static bool Hook_AfterActEntered_Prefix(IRunState runState, ref Task __result)
    {
        __result = ExecuteSafelyAsync(async () =>
        {
            foreach (var model in runState.IterateHookListeners(null))
            {
                await RuntimeGraphDispatcher.ExecuteAfterActEnteredHookAsync(model);
                model.InvokeExecutionFinished();
            }
        }, "after-act-entered graph hook dispatch");
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(RelicCmd), nameof(RelicCmd.Obtain), new[] { typeof(RelicModel), typeof(Player), typeof(int) })]
    private static bool RelicCmd_Obtain_Prefix(RelicModel relic, Player player, int index, ref Task<RelicModel> __result)
    {
        __result = ExecuteSafelyAsync(async () =>
        {
            relic.AssertMutable();
            IRunState runState = player.RunState;
            runState.CurrentMapPointHistoryEntry?.GetEntry(player.NetId).RelicChoices.Add(new MegaCrit.Sts2.Core.Runs.History.ModelChoiceHistoryEntry(relic.Id, wasPicked: true));
            player.AddRelicInternal(relic, index);
            if (!relic.IsStackable)
            {
                player.RelicGrabBag.Remove(relic);
                runState.SharedRelicGrabBag.Remove(relic);
            }
            if (LocalContext.IsMe(player))
            {
                NRun.Instance?.GlobalUi.RelicInventory.AnimateRelic(relic);
                NDebugAudioManager.Instance?.Play("relic_get.mp3");
                SaveManager.Instance.MarkRelicAsSeen(relic);
            }

            relic.FloorAddedToDeck = runState.TotalFloor;
            await RuntimeGraphDispatcher.ExecuteRelicAfterObtainedAsync(relic);
            return relic;
        }, $"relic graph obtain '{relic.Id.Entry}'");
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterCardChangedPiles), new[] { typeof(IRunState), typeof(CombatState), typeof(CardModel), typeof(PileType), typeof(AbstractModel) })]
    private static bool Hook_AfterCardChangedPiles_Prefix(IRunState runState, CombatState? combatState, CardModel card, PileType oldPile, AbstractModel? source, ref Task __result)
    {
        __result = ExecuteSafelyAsync(async () =>
        {
            foreach (var model in runState.IterateHookListeners(combatState))
            {
                await RuntimeGraphDispatcher.ExecuteAfterCardChangedPilesHookAsync(model, card, oldPile, source);
                model.InvokeExecutionFinished();
            }

            foreach (var model in runState.IterateHookListeners(combatState))
            {
                await RuntimeGraphDispatcher.ExecuteAfterCardChangedPilesLateHookAsync(model, card, oldPile, source);
                model.InvokeExecutionFinished();
            }
        }, "after-card-changed-piles graph hook dispatch");
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterCardExhausted), new[] { typeof(CombatState), typeof(PlayerChoiceContext), typeof(CardModel), typeof(bool) })]
    private static bool Hook_AfterCardExhausted_Prefix(CombatState combatState, PlayerChoiceContext choiceContext, CardModel card, bool causedByEthereal, ref Task __result)
    {
        __result = ExecuteSafelyAsync(async () =>
        {
            foreach (var model in combatState.IterateHookListeners())
            {
                choiceContext.PushModel(model);
                try
                {
                    await RuntimeGraphDispatcher.ExecuteAfterCardExhaustedHookAsync(model, choiceContext, card, causedByEthereal);
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
        }, "after-card-exhausted graph hook dispatch");
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterCardDiscarded), new[] { typeof(CombatState), typeof(PlayerChoiceContext), typeof(CardModel) })]
    private static bool Hook_AfterCardDiscarded_Prefix(CombatState combatState, PlayerChoiceContext choiceContext, CardModel card, ref Task __result)
    {
        __result = ExecuteSafelyAsync(async () =>
        {
            foreach (var model in combatState.IterateHookListeners())
            {
                choiceContext.PushModel(model);
                try
                {
                    await RuntimeGraphDispatcher.ExecuteAfterCardDiscardedHookAsync(model, choiceContext, card);
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
        }, "after-card-discarded graph hook dispatch");
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
    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterDamageReceived), new[] { typeof(PlayerChoiceContext), typeof(IRunState), typeof(CombatState), typeof(Creature), typeof(DamageResult), typeof(ValueProp), typeof(Creature), typeof(CardModel) })]
    private static bool Hook_AfterDamageReceived_Prefix(PlayerChoiceContext choiceContext, IRunState runState, CombatState? combatState, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource, ref Task __result)
    {
        __result = ExecuteSafelyAsync(async () =>
        {
            foreach (var model in runState.IterateHookListeners(combatState))
            {
                choiceContext.PushModel(model);
                try
                {
                    await RuntimeGraphDispatcher.ExecuteAfterDamageReceivedHookAsync(model, choiceContext, target, result, props, dealer, cardSource);
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

            foreach (var model in runState.IterateHookListeners(combatState))
            {
                choiceContext.PushModel(model);
                try
                {
                    await RuntimeGraphDispatcher.ExecuteAfterDamageReceivedLateHookAsync(model, choiceContext, target, result, props, dealer, cardSource);
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
        }, "after-damage-received graph hook dispatch");
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterDamageGiven), new[] { typeof(PlayerChoiceContext), typeof(CombatState), typeof(Creature), typeof(DamageResult), typeof(ValueProp), typeof(Creature), typeof(CardModel) })]
    private static bool Hook_AfterDamageGiven_Prefix(PlayerChoiceContext choiceContext, CombatState combatState, Creature? dealer, DamageResult results, ValueProp props, Creature target, CardModel? cardSource, ref Task __result)
    {
        __result = ExecuteSafelyAsync(async () =>
        {
            foreach (var model in combatState.IterateHookListeners())
            {
                choiceContext.PushModel(model);
                try
                {
                    await RuntimeGraphDispatcher.ExecuteAfterDamageGivenHookAsync(model, choiceContext, dealer, results, props, target, cardSource);
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
        }, "after-damage-given graph hook dispatch");
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterGoldGained), new[] { typeof(IRunState), typeof(Player) })]
    private static bool Hook_AfterGoldGained_Prefix(IRunState runState, Player player, ref Task __result)
    {
        __result = ExecuteSafelyAsync(async () =>
        {
            foreach (var model in runState.IterateHookListeners(null))
            {
                await RuntimeGraphDispatcher.ExecuteAfterGoldGainedHookAsync(model, player);
                model.InvokeExecutionFinished();
            }
        }, "after-gold-gained graph hook dispatch");
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterCurrentHpChanged), new[] { typeof(IRunState), typeof(CombatState), typeof(Creature), typeof(decimal) })]
    private static bool Hook_AfterCurrentHpChanged_Prefix(IRunState runState, CombatState? combatState, Creature creature, decimal delta, ref Task __result)
    {
        __result = ExecuteSafelyAsync(async () =>
        {
            foreach (var model in runState.IterateHookListeners(combatState))
            {
                await RuntimeGraphDispatcher.ExecuteAfterCurrentHpChangedHookAsync(model, creature, delta);
                model.InvokeExecutionFinished();
            }
        }, "after-current-hp-changed graph hook dispatch");
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterDeath), new[] { typeof(IRunState), typeof(CombatState), typeof(Creature), typeof(bool), typeof(float) })]
    private static bool Hook_AfterDeath_Prefix(IRunState runState, CombatState? combatState, Creature creature, bool wasRemovalPrevented, float deathAnimLength, ref Task __result)
    {
        __result = ExecuteSafelyAsync(async () =>
        {
            var netId = LocalContext.NetId;
            if (!netId.HasValue)
            {
                return;
            }

            foreach (var model in runState.IterateHookListeners(combatState))
            {
                var hookContext = new HookPlayerChoiceContext(model, netId.Value, creature.CombatState, GameActionType.Combat);
                await RuntimeGraphDispatcher.ExecuteAfterDeathHookAsync(model, hookContext, creature, wasRemovalPrevented, deathAnimLength);
                model.InvokeExecutionFinished();
            }
        }, "after-death graph hook dispatch");
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterPreventingBlockClear), new[] { typeof(CombatState), typeof(AbstractModel), typeof(Creature) })]
    private static bool Hook_AfterPreventingBlockClear_Prefix(CombatState combatState, AbstractModel preventer, Creature creature, ref Task __result)
    {
        _ = combatState;
        __result = ExecuteSafelyAsync(async () =>
        {
            await RuntimeGraphDispatcher.ExecuteAfterPreventingBlockClearHookAsync(preventer, creature);
            preventer.InvokeExecutionFinished();
        }, "after-preventing-block-clear graph hook dispatch");
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterBlockCleared), new[] { typeof(CombatState), typeof(Creature) })]
    private static bool Hook_AfterBlockCleared_Prefix(CombatState combatState, Creature creature, ref Task __result)
    {
        __result = ExecuteSafelyAsync(async () =>
        {
            foreach (var model in combatState.IterateHookListeners())
            {
                await RuntimeGraphDispatcher.ExecuteAfterBlockClearedHookAsync(model, creature);
                model.InvokeExecutionFinished();
            }
        }, "after-block-cleared graph hook dispatch");
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterRestSiteHeal), new[] { typeof(IRunState), typeof(Player), typeof(bool) })]
    private static bool Hook_AfterRestSiteHeal_Prefix(IRunState runState, Player player, bool isMimicked, ref Task __result)
    {
        __result = ExecuteSafelyAsync(async () =>
        {
            foreach (var model in runState.IterateHookListeners(null))
            {
                await RuntimeGraphDispatcher.ExecuteAfterRestSiteHealHookAsync(model, player, isMimicked);
                model.InvokeExecutionFinished();
            }
        }, "after-rest-site-heal graph hook dispatch");
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterDiedToDoom), new[] { typeof(CombatState), typeof(IReadOnlyList<Creature>) })]
    private static bool Hook_AfterDiedToDoom_Prefix(CombatState combatState, IReadOnlyList<Creature> creatures, ref Task __result)
    {
        __result = ExecuteSafelyAsync(async () =>
        {
            var netId = LocalContext.NetId;
            if (!netId.HasValue)
            {
                return;
            }

            foreach (var model in combatState.IterateHookListeners())
            {
                var hookContext = new HookPlayerChoiceContext(model, netId.Value, combatState, GameActionType.Combat);
                await RuntimeGraphDispatcher.ExecuteAfterDiedToDoomHookAsync(model, hookContext, creatures);
                model.InvokeExecutionFinished();
            }
        }, "after-died-to-doom graph hook dispatch");
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterPreventingDeath), new[] { typeof(IRunState), typeof(CombatState), typeof(AbstractModel), typeof(Creature) })]
    private static bool Hook_AfterPreventingDeath_Prefix(IRunState runState, CombatState? combatState, AbstractModel preventer, Creature creature, ref Task __result)
    {
        _ = runState;
        _ = combatState;
        __result = ExecuteSafelyAsync(async () =>
        {
            await RuntimeGraphDispatcher.ExecuteAfterPreventingDeathHookAsync(preventer, creature);
            preventer.InvokeExecutionFinished();
        }, "after-preventing-death graph hook dispatch");
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Hook), nameof(Hook.ModifyRestSiteHealRewards), new[] { typeof(IRunState), typeof(Player), typeof(List<Reward>), typeof(bool) })]
    private static bool Hook_ModifyRestSiteHealRewards_Prefix(IRunState runState, Player player, List<Reward> rewards, bool isMimicked, ref IEnumerable<AbstractModel> __result)
    {
        var changed = new List<AbstractModel>();
        foreach (var model in runState.IterateHookListeners(null))
        {
            if (model is RelicModel relic &&
                RuntimeGraphDispatcher.TryApplyRelicRewardModifier(relic, "relic.modify_rest_site_heal_rewards", player, rewards, room: null, isMimicked, out var modified))
            {
                if (modified)
                {
                    changed.Add(model);
                }

                continue;
            }

            if (model.TryModifyRestSiteHealRewards(player, rewards, isMimicked))
            {
                changed.Add(model);
            }
        }

        __result = changed;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Hook), nameof(Hook.ModifyRewards), new[] { typeof(IRunState), typeof(Player), typeof(List<Reward>), typeof(AbstractRoom) })]
    private static bool Hook_ModifyRewards_Prefix(IRunState runState, Player player, List<Reward> rewards, AbstractRoom? room, ref IEnumerable<AbstractModel> __result)
    {
        var changed = new List<AbstractModel>();
        foreach (var model in runState.IterateHookListeners(null))
        {
            if (model is RelicModel relic &&
                RuntimeGraphDispatcher.TryApplyRelicRewardModifier(relic, "relic.modify_rewards", player, rewards, room, isMimicked: null, out var modified))
            {
                if (modified)
                {
                    changed.Add(model);
                }

                continue;
            }

            if (model.TryModifyRewards(player, rewards, room))
            {
                changed.Add(model);
            }
        }

        foreach (var model in runState.IterateHookListeners(null))
        {
            if (model is RelicModel relic &&
                RuntimeGraphDispatcher.TryApplyRelicRewardModifier(relic, "relic.modify_rewards_late", player, rewards, room, isMimicked: null, out var modified))
            {
                if (modified)
                {
                    changed.Add(model);
                }

                continue;
            }

            if (model.TryModifyRewardsLate(player, rewards, room))
            {
                changed.Add(model);
            }
        }

        __result = changed;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Hook), nameof(Hook.TryModifyCardRewardOptions))]
    private static bool Hook_TryModifyCardRewardOptions_Prefix(
        IRunState runState,
        Player player,
        List<CardCreationResult> cardRewardOptions,
        CardCreationOptions creationOptions,
        ref List<AbstractModel> modifiers,
        ref bool __result)
    {
        var modifiedAny = false;
        modifiers = new List<AbstractModel>();

        foreach (var model in runState.IterateHookListeners(null))
        {
            if (model is RelicModel relic &&
                RuntimeGraphDispatcher.TryApplyRelicCardRewardOptionsModifier(
                    relic,
                    "relic.modify_card_reward_options",
                    player,
                    cardRewardOptions,
                    creationOptions,
                    out var modified))
            {
                modifiedAny |= modified;
                if (modified)
                {
                    modifiers.Add(model);
                }

                continue;
            }

            var flag = model.TryModifyCardRewardOptions(player, cardRewardOptions, creationOptions);
            modifiedAny |= flag;
            if (flag)
            {
                modifiers.Add(model);
            }
        }

        foreach (var model in runState.IterateHookListeners(null))
        {
            if (model is RelicModel relic &&
                RuntimeGraphDispatcher.TryApplyRelicCardRewardOptionsModifier(
                    relic,
                    "relic.modify_card_reward_options_late",
                    player,
                    cardRewardOptions,
                    creationOptions,
                    out var modified))
            {
                modifiedAny |= modified;
                if (modified)
                {
                    modifiers.Add(model);
                }

                continue;
            }

            var flag = model.TryModifyCardRewardOptionsLate(player, cardRewardOptions, creationOptions);
            modifiedAny |= flag;
            if (flag)
            {
                modifiers.Add(model);
            }
        }

        __result = modifiedAny;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Hook), nameof(Hook.ShouldGainGold), new[] { typeof(IRunState), typeof(CombatState), typeof(decimal), typeof(Player) })]
    private static bool Hook_ShouldGainGold_Prefix(IRunState runState, CombatState? combatState, decimal amount, Player player, ref bool __result)
    {
        foreach (var model in runState.IterateHookListeners(combatState))
        {
            if (model is RelicModel relic &&
                RuntimeGraphDispatcher.TryEvaluateRelicBoolHook(
                    relic,
                    "relic.should_gain_gold",
                    player,
                    context => context["GoldAmount"] = amount,
                    out var graphResult))
            {
                if (!graphResult)
                {
                    __result = false;
                    return false;
                }

                continue;
            }

            if (!model.ShouldGainGold(amount, player))
            {
                __result = false;
                return false;
            }
        }

        __result = true;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Hook), nameof(Hook.ShouldPlayerResetEnergy), new[] { typeof(CombatState), typeof(Player) })]
    private static bool Hook_ShouldPlayerResetEnergy_Prefix(CombatState combatState, Player player, ref bool __result)
    {
        foreach (var model in combatState.IterateHookListeners())
        {
            if (model is RelicModel relic &&
                RuntimeGraphDispatcher.TryEvaluateRelicBoolHook(
                    relic,
                    "relic.should_player_reset_energy",
                    player,
                    configureContext: null,
                    out var graphResult))
            {
                if (!graphResult)
                {
                    __result = false;
                    return false;
                }

                continue;
            }

            if (!model.ShouldPlayerResetEnergy(player))
            {
                __result = false;
                return false;
            }
        }

        __result = true;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Hook), nameof(Hook.ShouldFlush), new[] { typeof(CombatState), typeof(Player) })]
    private static bool Hook_ShouldFlush_Prefix(CombatState combatState, Player player, ref bool __result)
    {
        foreach (var model in combatState.IterateHookListeners())
        {
            if (model is RelicModel relic &&
                RuntimeGraphDispatcher.TryEvaluateRelicBoolHook(
                    relic,
                    "relic.should_flush",
                    player,
                    context => context["CombatRound"] = player.Creature.CombatState.RoundNumber,
                    out var graphResult))
            {
                if (!graphResult)
                {
                    __result = false;
                    return false;
                }

                continue;
            }

            if (!model.ShouldFlush(player))
            {
                __result = false;
                return false;
            }
        }

        __result = true;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Hook), nameof(Hook.ShouldDisableRemainingRestSiteOptions), new[] { typeof(IRunState), typeof(Player) })]
    private static bool Hook_ShouldDisableRemainingRestSiteOptions_Prefix(IRunState runState, Player player, ref bool __result)
    {
        foreach (var model in runState.IterateHookListeners(null))
        {
            if (model is RelicModel relic &&
                RuntimeGraphDispatcher.TryEvaluateRelicBoolHook(
                    relic,
                    "relic.should_disable_remaining_rest_site_options",
                    player,
                    configureContext: null,
                    out var graphResult))
            {
                if (!graphResult)
                {
                    __result = false;
                    return false;
                }

                continue;
            }

            if (!model.ShouldDisableRemainingRestSiteOptions(player))
            {
                __result = false;
                return false;
            }
        }

        __result = true;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Hook), nameof(Hook.ShouldForcePotionReward), new[] { typeof(IRunState), typeof(Player), typeof(RoomType) })]
    private static bool Hook_ShouldForcePotionReward_Prefix(IRunState runState, Player player, RoomType roomType, ref bool __result)
    {
        var anyTrue = false;
        foreach (var model in runState.IterateHookListeners(null))
        {
            if (model is RelicModel relic &&
                RuntimeGraphDispatcher.TryEvaluateRelicBoolHook(
                    relic,
                    "relic.should_force_potion_reward",
                    player,
                    context => context["CurrentRoomIsCombat"] = roomType.IsCombatRoom(),
                    out var graphResult))
            {
                anyTrue |= graphResult;
                continue;
            }

            anyTrue |= model.ShouldForcePotionReward(player, roomType);
        }

        __result = anyTrue;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Hook), nameof(Hook.ShouldRefillMerchantEntry), new[] { typeof(IRunState), typeof(MerchantEntry), typeof(Player) })]
    private static bool Hook_ShouldRefillMerchantEntry_Prefix(IRunState runState, MerchantEntry entry, Player player, ref bool __result)
    {
        var anyTrue = false;
        foreach (var model in runState.IterateHookListeners(null))
        {
            if (model is RelicModel relic &&
                RuntimeGraphDispatcher.TryEvaluateRelicBoolHook(
                    relic,
                    "relic.should_refill_merchant_entry",
                    player,
                    context => context["merchant_entry"] = entry,
                    out var graphResult))
            {
                anyTrue |= graphResult;
                continue;
            }

            anyTrue |= model.ShouldRefillMerchantEntry(entry, player);
        }

        __result = anyTrue;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Hook), nameof(Hook.ShouldProcurePotion), new[] { typeof(IRunState), typeof(CombatState), typeof(PotionModel), typeof(Player) })]
    private static bool Hook_ShouldProcurePotion_Prefix(IRunState runState, CombatState? combatState, PotionModel potion, Player player, ref bool __result)
    {
        foreach (var model in runState.IterateHookListeners(combatState))
        {
            if (model is RelicModel relic &&
                RuntimeGraphDispatcher.TryEvaluateRelicBoolHook(
                    relic,
                    "relic.should_procure_potion",
                    player,
                    context => context["HookPotion"] = potion,
                    out var graphResult))
            {
                if (!graphResult)
                {
                    __result = false;
                    return false;
                }

                continue;
            }

            if (!model.ShouldProcurePotion(potion, player))
            {
                __result = false;
                return false;
            }
        }

        __result = true;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Hook), nameof(Hook.ModifyGeneratedMap), new[] { typeof(IRunState), typeof(ActMap), typeof(int) })]
    private static bool Hook_ModifyGeneratedMap_Prefix(IRunState runState, ActMap map, int actIndex, ref ActMap __result)
    {
        var current = map;
        foreach (var model in runState.IterateHookListeners(null))
        {
            if (model is RelicModel relic &&
                RuntimeGraphDispatcher.TryApplyRelicMapModifier(relic, "relic.modify_generated_map", runState, current, actIndex, out var graphMap))
            {
                current = graphMap;
                continue;
            }

            current = model.ModifyGeneratedMap(runState, current, actIndex);
        }

        __result = current;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Hook), nameof(Hook.ModifyGeneratedMapLate), new[] { typeof(IRunState), typeof(ActMap), typeof(int) })]
    private static bool Hook_ModifyGeneratedMapLate_Prefix(IRunState runState, ActMap map, int actIndex, ref ActMap __result)
    {
        var current = map;
        foreach (var model in runState.IterateHookListeners(null))
        {
            if (model is RelicModel relic &&
                RuntimeGraphDispatcher.TryApplyRelicMapModifier(relic, "relic.modify_generated_map_late", runState, current, actIndex, out var graphMap))
            {
                current = graphMap;
                continue;
            }

            current = model.ModifyGeneratedMapLate(runState, current, actIndex);
        }

        __result = current;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Hook), nameof(Hook.BeforeCombatStart), new[] { typeof(IRunState), typeof(CombatState) })]
    private static bool Hook_BeforeCombatStart_Prefix(IRunState runState, CombatState? combatState, ref Task __result)
    {
        __result = ExecuteSafelyAsync(async () =>
        {
            foreach (var model in runState.IterateHookListeners(combatState))
            {
                await RuntimeGraphDispatcher.ExecuteBeforeCombatStartHookAsync(model);
                model.InvokeExecutionFinished();
            }

            foreach (var model in runState.IterateHookListeners(combatState))
            {
                await RuntimeGraphDispatcher.ExecuteBeforeCombatStartLateHookAsync(model);
                model.InvokeExecutionFinished();
            }
        }, "before-combat-start graph hook dispatch");
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

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterPotionDiscarded), new[] { typeof(IRunState), typeof(CombatState), typeof(PotionModel) })]
    private static bool Hook_AfterPotionDiscarded_Prefix(IRunState runState, CombatState? combatState, PotionModel potion, ref Task __result)
    {
        __result = ExecuteSafelyAsync(async () =>
        {
            foreach (var model in runState.IterateHookListeners(combatState))
            {
                await RuntimeGraphDispatcher.ExecuteAfterPotionDiscardedHookAsync(model, potion);
                model.InvokeExecutionFinished();
            }
        }, "after-potion-discarded graph hook dispatch");
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterPotionProcured), new[] { typeof(IRunState), typeof(CombatState), typeof(PotionModel) })]
    private static bool Hook_AfterPotionProcured_Prefix(IRunState runState, CombatState? combatState, PotionModel potion, ref Task __result)
    {
        __result = ExecuteSafelyAsync(async () =>
        {
            foreach (var model in runState.IterateHookListeners(combatState))
            {
                await RuntimeGraphDispatcher.ExecuteAfterPotionProcuredHookAsync(model, potion);
                model.InvokeExecutionFinished();
            }
        }, "after-potion-procured graph hook dispatch");
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterCombatEnd), new[] { typeof(IRunState), typeof(CombatState), typeof(CombatRoom) })]
    private static bool Hook_AfterCombatEnd_Prefix(IRunState runState, CombatState? combatState, CombatRoom room, ref Task __result)
    {
        __result = ExecuteSafelyAsync(async () =>
        {
            foreach (var model in runState.IterateHookListeners(combatState))
            {
                await RuntimeGraphDispatcher.ExecuteAfterCombatEndHookAsync(model, room);
                model.InvokeExecutionFinished();
            }
        }, "after-combat-end graph hook dispatch");
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterCombatVictory), new[] { typeof(IRunState), typeof(CombatState), typeof(CombatRoom) })]
    private static bool Hook_AfterCombatVictory_Prefix(IRunState runState, CombatState? combatState, CombatRoom room, ref Task __result)
    {
        __result = ExecuteSafelyAsync(async () =>
        {
            foreach (var model in runState.IterateHookListeners(combatState))
            {
                await RuntimeGraphDispatcher.ExecuteAfterCombatVictoryEarlyHookAsync(model, room);
                model.InvokeExecutionFinished();
            }

            foreach (var model in runState.IterateHookListeners(combatState))
            {
                await RuntimeGraphDispatcher.ExecuteAfterCombatVictoryHookAsync(model, room);
                model.InvokeExecutionFinished();
            }
        }, "after-combat-victory graph hook dispatch");
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterCardDrawn), new[] { typeof(CombatState), typeof(PlayerChoiceContext), typeof(CardModel), typeof(bool) })]
    private static bool Hook_AfterCardDrawn_Prefix(CombatState combatState, PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw, ref Task __result)
    {
        __result = ExecuteSafelyAsync(async () =>
        {
            foreach (var model in combatState.IterateHookListeners())
            {
                choiceContext.PushModel(model);
                try
                {
                    await RuntimeGraphDispatcher.ExecuteAfterCardDrawnHookAsync(model, choiceContext, card, fromHandDraw);
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
        }, "after-card-drawn graph hook dispatch");
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterCardEnteredCombat), new[] { typeof(CombatState), typeof(CardModel) })]
    private static bool Hook_AfterCardEnteredCombat_Prefix(CombatState combatState, CardModel card, ref Task __result)
    {
        __result = ExecuteSafelyAsync(async () =>
        {
            foreach (var model in combatState.IterateHookListeners())
            {
                await RuntimeGraphDispatcher.ExecuteAfterCardEnteredCombatHookAsync(model, card);
                model.InvokeExecutionFinished();
            }
        }, "after-card-entered-combat graph hook dispatch");
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterHandEmptied), new[] { typeof(CombatState), typeof(PlayerChoiceContext), typeof(Player) })]
    private static bool Hook_AfterHandEmptied_Prefix(CombatState combatState, PlayerChoiceContext choiceContext, Player player, ref Task __result)
    {
        __result = ExecuteSafelyAsync(async () =>
        {
            foreach (var model in combatState.IterateHookListeners())
            {
                choiceContext.PushModel(model);
                try
                {
                    await RuntimeGraphDispatcher.ExecuteAfterHandEmptiedHookAsync(model, choiceContext, player);
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
        }, "after-hand-emptied graph hook dispatch");
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterShuffle), new[] { typeof(CombatState), typeof(PlayerChoiceContext), typeof(Player) })]
    private static bool Hook_AfterShuffle_Prefix(CombatState combatState, PlayerChoiceContext choiceContext, Player shuffler, ref Task __result)
    {
        __result = ExecuteSafelyAsync(async () =>
        {
            foreach (var model in combatState.IterateHookListeners())
            {
                choiceContext.PushModel(model);
                try
                {
                    await RuntimeGraphDispatcher.ExecuteAfterShuffleHookAsync(model, choiceContext, shuffler);
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
        }, "after-shuffle graph hook dispatch");
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterOrbChanneled), new[] { typeof(CombatState), typeof(PlayerChoiceContext), typeof(Player), typeof(OrbModel) })]
    private static bool Hook_AfterOrbChanneled_Prefix(CombatState combatState, PlayerChoiceContext choiceContext, Player player, OrbModel orb, ref Task __result)
    {
        __result = ExecuteSafelyAsync(async () =>
        {
            foreach (var model in combatState.IterateHookListeners())
            {
                choiceContext.PushModel(model);
                try
                {
                    await RuntimeGraphDispatcher.ExecuteAfterOrbChanneledHookAsync(model, choiceContext, player, orb);
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
        }, "after-orb-channeled graph hook dispatch");
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterEnergyReset), new[] { typeof(CombatState), typeof(Player) })]
    private static bool Hook_AfterEnergyReset_Prefix(CombatState combatState, Player player, ref Task __result)
    {
        __result = ExecuteSafelyAsync(async () =>
        {
            foreach (var model in combatState.IterateHookListeners())
            {
                await RuntimeGraphDispatcher.ExecuteAfterEnergyResetHookAsync(model, player);
                model.InvokeExecutionFinished();
            }

            foreach (var model in combatState.IterateHookListeners())
            {
                await RuntimeGraphDispatcher.ExecuteAfterEnergyResetLateHookAsync(model, player);
                model.InvokeExecutionFinished();
            }
        }, "after-energy-reset graph hook dispatch");
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterStarsSpent), new[] { typeof(CombatState), typeof(int), typeof(Player) })]
    private static bool Hook_AfterStarsSpent_Prefix(CombatState combatState, int amount, Player spender, ref Task __result)
    {
        __result = ExecuteSafelyAsync(async () =>
        {
            foreach (var model in combatState.IterateHookListeners())
            {
                await RuntimeGraphDispatcher.ExecuteAfterStarsSpentHookAsync(model, amount, spender);
                model.InvokeExecutionFinished();
            }
        }, "after-stars-spent graph hook dispatch");
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterPlayerTurnStart), new[] { typeof(CombatState), typeof(PlayerChoiceContext), typeof(Player) })]
    private static bool Hook_AfterPlayerTurnStart_Prefix(CombatState combatState, PlayerChoiceContext choiceContext, Player player, ref Task __result)
    {
        __result = ExecuteSafelyAsync(async () =>
        {
            foreach (var model in combatState.IterateHookListeners())
            {
                choiceContext.PushModel(model);
                try
                {
                    await RuntimeGraphDispatcher.ExecuteAfterPlayerTurnStartEarlyHookAsync(model, choiceContext, player);
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
                    await RuntimeGraphDispatcher.ExecuteAfterPlayerTurnStartHookAsync(model, choiceContext, player);
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
                    await RuntimeGraphDispatcher.ExecuteAfterPlayerTurnStartLateHookAsync(model, choiceContext, player);
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
        }, "after-player-turn-start graph hook dispatch");
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterSideTurnStart), new[] { typeof(CombatState), typeof(CombatSide) })]
    private static bool Hook_AfterSideTurnStart_Prefix(CombatState combatState, CombatSide side, ref Task __result)
    {
        __result = ExecuteSafelyAsync(async () =>
        {
            foreach (var model in combatState.IterateHookListeners())
            {
                await RuntimeGraphDispatcher.ExecuteAfterSideTurnStartHookAsync(model, side, combatState);
                model.InvokeExecutionFinished();
            }
        }, "after-side-turn-start graph hook dispatch");
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterRoomEntered), new[] { typeof(IRunState), typeof(AbstractRoom) })]
    private static bool Hook_AfterRoomEntered_Prefix(IRunState runState, AbstractRoom room, ref Task __result)
    {
        __result = ExecuteSafelyAsync(async () =>
        {
            foreach (var model in runState.IterateHookListeners(null))
            {
                await RuntimeGraphDispatcher.ExecuteAfterRoomEnteredHookAsync(model, room);
                model.InvokeExecutionFinished();
            }
        }, "after-room-entered graph hook dispatch");
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Hook), nameof(Hook.BeforeHandDraw), new[] { typeof(CombatState), typeof(Player), typeof(PlayerChoiceContext) })]
    private static bool Hook_BeforeHandDraw_Prefix(CombatState combatState, Player player, PlayerChoiceContext playerChoiceContext, ref Task __result)
    {
        __result = ExecuteSafelyAsync(async () =>
        {
            foreach (var model in combatState.IterateHookListeners())
            {
                playerChoiceContext.PushModel(model);
                try
                {
                    await RuntimeGraphDispatcher.ExecuteBeforeHandDrawHookAsync(model, player, playerChoiceContext, combatState);
                    model.InvokeExecutionFinished();
                }
                finally
                {
                    if (playerChoiceContext.LastInvolvedModel == model)
                    {
                        playerChoiceContext.PopModel(model);
                    }
                }
            }

            foreach (var model in combatState.IterateHookListeners())
            {
                playerChoiceContext.PushModel(model);
                try
                {
                    await RuntimeGraphDispatcher.ExecuteBeforeHandDrawLateHookAsync(model, player, playerChoiceContext, combatState);
                    model.InvokeExecutionFinished();
                }
                finally
                {
                    if (playerChoiceContext.LastInvolvedModel == model)
                    {
                        playerChoiceContext.PopModel(model);
                    }
                }
            }
        }, "before-hand-draw graph hook dispatch");
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Hook), nameof(Hook.BeforeSideTurnStart), new[] { typeof(CombatState), typeof(CombatSide) })]
    private static bool Hook_BeforeSideTurnStart_Prefix(CombatState combatState, CombatSide side, ref Task __result)
    {
        __result = ExecuteSafelyAsync(async () =>
        {
            var netId = LocalContext.NetId;
            if (!netId.HasValue)
            {
                return;
            }

            foreach (var model in combatState.IterateHookListeners())
            {
                var hookContext = new HookPlayerChoiceContext(model, netId.Value, combatState, GameActionType.Combat);
                await RuntimeGraphDispatcher.ExecuteBeforeSideTurnStartHookAsync(model, hookContext, side, combatState);
                model.InvokeExecutionFinished();
            }
        }, "before-side-turn-start graph hook dispatch");
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Hook), nameof(Hook.BeforePlayPhaseStart), new[] { typeof(CombatState), typeof(Player) })]
    private static bool Hook_BeforePlayPhaseStart_Prefix(CombatState combatState, Player player, ref Task __result)
    {
        __result = ExecuteSafelyAsync(async () =>
        {
            var netId = LocalContext.NetId;
            if (!netId.HasValue)
            {
                return;
            }

            foreach (var model in combatState.IterateHookListeners())
            {
                var hookContext = new HookPlayerChoiceContext(model, netId.Value, combatState, GameActionType.Combat);
                await RuntimeGraphDispatcher.ExecuteBeforePlayPhaseStartHookAsync(model, hookContext, player);
                model.InvokeExecutionFinished();
            }
        }, "before-play-phase-start graph hook dispatch");
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Hook), nameof(Hook.BeforeTurnEnd), new[] { typeof(CombatState), typeof(CombatSide) })]
    private static bool Hook_BeforeTurnEnd_Prefix(CombatState combatState, CombatSide side, ref Task __result)
    {
        __result = ExecuteSafelyAsync(async () =>
        {
            var netId = LocalContext.NetId;
            if (!netId.HasValue)
            {
                return;
            }

            foreach (var model in combatState.IterateHookListeners())
            {
                var hookContext = new HookPlayerChoiceContext(model, netId.Value, combatState, GameActionType.Combat);
                await RuntimeGraphDispatcher.ExecuteBeforeTurnEndVeryEarlyHookAsync(model, hookContext, side);
                model.InvokeExecutionFinished();
            }

            foreach (var model in combatState.IterateHookListeners())
            {
                var hookContext = new HookPlayerChoiceContext(model, netId.Value, combatState, GameActionType.Combat);
                await RuntimeGraphDispatcher.ExecuteBeforeTurnEndEarlyHookAsync(model, hookContext, side);
                model.InvokeExecutionFinished();
            }

            foreach (var model in combatState.IterateHookListeners())
            {
                var hookContext = new HookPlayerChoiceContext(model, netId.Value, combatState, GameActionType.Combat);
                await RuntimeGraphDispatcher.ExecuteBeforeTurnEndHookAsync(model, hookContext, side);
                model.InvokeExecutionFinished();
            }
        }, "before-turn-end graph hook dispatch");
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Hook), nameof(Hook.BeforeFlush), new[] { typeof(CombatState), typeof(Player) })]
    private static bool Hook_BeforeFlush_Prefix(CombatState combatState, Player player, ref Task __result)
    {
        __result = ExecuteSafelyAsync(async () =>
        {
            var netId = LocalContext.NetId;
            if (!netId.HasValue)
            {
                return;
            }

            foreach (var model in combatState.IterateHookListeners())
            {
                var hookContext = new HookPlayerChoiceContext(model, netId.Value, combatState, GameActionType.Combat);
                await RuntimeGraphDispatcher.ExecuteBeforeFlushHookAsync(model, hookContext, player);
                model.InvokeExecutionFinished();
            }

            foreach (var model in combatState.IterateHookListeners())
            {
                var hookContext = new HookPlayerChoiceContext(model, netId.Value, combatState, GameActionType.Combat);
                await RuntimeGraphDispatcher.ExecuteBeforeFlushLateHookAsync(model, hookContext, player);
                model.InvokeExecutionFinished();
            }
        }, "before-flush graph hook dispatch");
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterTurnEnd), new[] { typeof(CombatState), typeof(CombatSide) })]
    private static bool Hook_AfterTurnEnd_Prefix(CombatState combatState, CombatSide side, ref Task __result)
    {
        __result = ExecuteSafelyAsync(async () =>
        {
            var netId = LocalContext.NetId;
            if (!netId.HasValue)
            {
                return;
            }

            foreach (var model in combatState.IterateHookListeners())
            {
                var hookContext = new HookPlayerChoiceContext(model, netId.Value, combatState, GameActionType.Combat);
                await RuntimeGraphDispatcher.ExecuteAfterTurnEndHookAsync(model, hookContext, side);
                model.InvokeExecutionFinished();
            }

            foreach (var model in combatState.IterateHookListeners())
            {
                var hookContext = new HookPlayerChoiceContext(model, netId.Value, combatState, GameActionType.Combat);
                await RuntimeGraphDispatcher.ExecuteAfterTurnEndLateHookAsync(model, hookContext, side);
                model.InvokeExecutionFinished();
            }
        }, "after-turn-end graph hook dispatch");
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Hook), nameof(Hook.ModifyHandDraw))]
    private static bool Hook_ModifyHandDraw_Prefix(CombatState combatState, Player player, decimal originalCardCount, ref IEnumerable<AbstractModel> modifiers, ref decimal __result)
    {
        var current = originalCardCount;
        var changed = new List<AbstractModel>();

        foreach (var model in combatState.IterateHookListeners())
        {
            var previous = current;
            if (model is RelicModel relic &&
                RuntimeGraphDispatcher.TryEvaluateRelicDecimalModifier(relic, "relic.modify_hand_draw", "hand_draw_base", current, out var graphValue))
            {
                current = graphValue;
            }
            else
            {
                current = model.ModifyHandDraw(player, current);
            }

            if ((int)previous != (int)current)
            {
                changed.Add(model);
            }
        }

        foreach (var model in combatState.IterateHookListeners())
        {
            var previous = current;
            if (model is RelicModel relic &&
                RuntimeGraphDispatcher.TryEvaluateRelicDecimalModifier(relic, "relic.modify_hand_draw_late", "hand_draw_base", current, out var graphValue))
            {
                current = graphValue;
            }
            else
            {
                current = model.ModifyHandDrawLate(player, current);
            }

            if ((int)previous != (int)current)
            {
                changed.Add(model);
            }
        }

        modifiers = changed;
        __result = current;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Hook), nameof(Hook.ModifyMaxEnergy), new[] { typeof(CombatState), typeof(Player), typeof(decimal) })]
    private static bool Hook_ModifyMaxEnergy_Prefix(CombatState combatState, Player player, decimal amount, ref decimal __result)
    {
        var current = amount;
        foreach (var model in combatState.IterateHookListeners())
        {
            if (model is RelicModel relic &&
                RuntimeGraphDispatcher.TryEvaluateRelicMaxEnergyModifier(relic, current, out var graphValue))
            {
                current = graphValue;
            }
            else
            {
                current = model.ModifyMaxEnergy(player, current);
            }
        }

        __result = current;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Hook), nameof(Hook.ModifyXValue), new[] { typeof(CombatState), typeof(CardModel), typeof(int) })]
    private static bool Hook_ModifyXValue_Prefix(CombatState combatState, CardModel card, int originalValue, ref int __result)
    {
        var current = originalValue;
        foreach (var model in combatState.IterateHookListeners())
        {
            if (model is RelicModel relic &&
                RuntimeGraphDispatcher.TryEvaluateRelicIntModifier(relic, "relic.modify_x_value", "x_value_base", current, out var graphValue))
            {
                current = graphValue;
            }
            else
            {
                current = model.ModifyXValue(card, current);
            }
        }

        __result = current;
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

    private static async Task<T> ExecuteSafelyAsync<T>(Func<Task<T>> work, string label)
    {
        try
        {
            return await work();
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed during {label}: {ex.Message}");
            throw;
        }
    }
}

[HarmonyPatch(typeof(EnchantmentModel), nameof(EnchantmentModel.EnchantDamageAdditive), [typeof(decimal), typeof(ValueProp)])]
internal static class EnchantmentDamageAdditiveGraphPatches
{
    [HarmonyPrefix]
    private static bool Prefix(EnchantmentModel __instance, decimal originalDamage, ValueProp props, ref decimal __result)
    {
        if (RuntimeGraphDispatcher.TryEvaluateEnchantmentModifierDecimal(__instance, "enchantment.modify_damage_additive", originalDamage, props, out var value))
        {
            __result = value;
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(EnchantmentModel), nameof(EnchantmentModel.EnchantDamageMultiplicative), [typeof(decimal), typeof(ValueProp)])]
internal static class EnchantmentDamageMultiplicativeGraphPatches
{
    [HarmonyPrefix]
    private static bool Prefix(EnchantmentModel __instance, decimal originalDamage, ValueProp props, ref decimal __result)
    {
        if (RuntimeGraphDispatcher.TryEvaluateEnchantmentModifierDecimal(__instance, "enchantment.modify_damage_multiplicative", originalDamage, props, out var value))
        {
            __result = value;
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(EnchantmentModel), nameof(EnchantmentModel.EnchantBlockAdditive), [typeof(decimal), typeof(ValueProp)])]
internal static class EnchantmentBlockAdditiveGraphPatches
{
    [HarmonyPrefix]
    private static bool Prefix(EnchantmentModel __instance, decimal originalBlock, ValueProp props, ref decimal __result)
    {
        if (RuntimeGraphDispatcher.TryEvaluateEnchantmentModifierDecimal(__instance, "enchantment.modify_block_additive", originalBlock, props, out var value))
        {
            __result = value;
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(EnchantmentModel), nameof(EnchantmentModel.EnchantBlockMultiplicative), [typeof(decimal), typeof(ValueProp)])]
internal static class EnchantmentBlockMultiplicativeGraphPatches
{
    [HarmonyPrefix]
    private static bool Prefix(EnchantmentModel __instance, decimal originalBlock, ValueProp props, ref decimal __result)
    {
        if (RuntimeGraphDispatcher.TryEvaluateEnchantmentModifierDecimal(__instance, "enchantment.modify_block_multiplicative", originalBlock, props, out var value))
        {
            __result = value;
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(EnchantmentModel), nameof(EnchantmentModel.EnchantPlayCount), [typeof(int)])]
internal static class EnchantmentPlayCountGraphPatches
{
    [HarmonyPrefix]
    private static bool Prefix(EnchantmentModel __instance, int originalPlayCount, ref int __result)
    {
        if (RuntimeGraphDispatcher.TryEvaluateEnchantmentPlayCountModifier(__instance, originalPlayCount, out var value))
        {
            __result = value;
            return false;
        }

        return true;
    }
}
