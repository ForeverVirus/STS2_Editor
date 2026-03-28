using System.Globalization;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Enchantments;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace STS2_Editor.Scripts.Editor.Graph;

internal static class BuiltInBehaviorNodeExecutors
{
    public static void RegisterInto(BehaviorGraphRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("flow.entry", (_, _) => Task.CompletedTask));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("flow.exit", (_, _) => Task.CompletedTask));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("flow.sequence", (_, _) => Task.CompletedTask));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("flow.branch", (_, _) => Task.CompletedTask));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("flow.random_choice", (_, _) => Task.CompletedTask));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("value.set", ExecuteSetAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("value.add", ExecuteAddAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("value.multiply", ExecuteMultiplyAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("value.compare", ExecuteCompareAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("debug.log", ExecuteLogAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("card.select_cards", ExecuteSelectCardsAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("combat.damage", ExecuteDamageAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("combat.gain_block", ExecuteGainBlockAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("combat.heal", ExecuteHealAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("combat.draw_cards", ExecuteDrawCardsAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("combat.apply_power", ExecuteApplyPowerAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("player.gain_energy", ExecuteGainEnergyAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("player.gain_stars", ExecuteGainStarsAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("player.gain_gold", ExecuteGainGoldAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("player.lose_energy", ExecuteLoseEnergyAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("player.lose_gold", ExecuteLoseGoldAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("player.gain_max_potion_count", ExecuteGainMaxPotionCountAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("combat.discard_cards", ExecuteDiscardCardsAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("combat.exhaust_cards", ExecuteExhaustCardsAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("combat.create_card", ExecuteCreateCardAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("cardpile.move_cards", ExecuteMoveCardsAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("combat.remove_card", ExecuteRemoveCardAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("combat.transform_card", ExecuteTransformCardAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("card.discard_and_draw", ExecuteDiscardAndDrawAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("card.apply_keyword", ExecuteApplyCardKeywordAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("card.remove_keyword", ExecuteRemoveCardKeywordAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("card.upgrade", ExecuteUpgradeCardAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("card.downgrade", ExecuteDowngradeCardAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("card.enchant", ExecuteEnchantCardAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("card.autoplay", ExecuteAutoPlayCardAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("card.apply_single_turn_sly", ExecuteApplySingleTurnSlyAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("cardpile.auto_play_from_draw_pile", ExecuteAutoPlayFromDrawPileAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("orb.channel", ExecuteChannelOrbAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("orb.passive", ExecuteOrbPassiveAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("orb.add_slots", ExecuteAddOrbSlotsAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("orb.remove_slots", ExecuteRemoveOrbSlotsAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("orb.evoke_next", ExecuteEvokeNextOrbAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("potion.procure", ExecuteProcurePotionAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("potion.discard", ExecuteDiscardPotionAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("relic.obtain", ExecuteObtainRelicAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("relic.remove", ExecuteRemoveRelicAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("relic.replace", ExecuteReplaceRelicAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("relic.melt", ExecuteMeltRelicAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("player.add_pet", ExecuteAddPetAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("player.forge", ExecuteForgeAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("player.complete_quest", ExecuteCompleteQuestAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("player.rest_heal", ExecuteRestHealAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("player.end_turn", ExecuteEndTurnAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("combat.repeat", ExecuteRepeatAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("combat.lose_block", ExecuteLoseBlockAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("player.lose_hp", ExecuteLoseHpAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("player.gain_max_hp", ExecuteGainMaxHpAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("player.lose_max_hp", ExecuteLoseMaxHpAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("creature.set_current_hp", ExecuteSetCurrentHpAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("creature.kill", ExecuteCreatureKillAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("creature.stun", ExecuteCreatureStunAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("power.remove", ExecuteRemovePowerAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("power.modify_amount", ExecuteModifyPowerAmountAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("cardpile.shuffle", ExecuteShuffleCardPileAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("modifier.damage_additive", ExecuteModifierDamageAdditiveAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("modifier.damage_multiplicative", ExecuteModifierDamageMultiplicativeAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("modifier.block_additive", ExecuteModifierBlockAdditiveAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("modifier.block_multiplicative", ExecuteModifierBlockMultiplicativeAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("modifier.play_count", ExecuteModifierPlayCountAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("modifier.hand_draw", ExecuteModifierHandDrawAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("modifier.x_value", ExecuteModifierXValueAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("modifier.max_energy", ExecuteModifierMaxEnergyAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("enchantment.set_status", ExecuteEnchantmentSetStatusAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("card.set_cost_delta", ExecuteCardSetCostDeltaAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("card.set_cost_absolute", ExecuteCardSetCostAbsoluteAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("card.set_cost_this_combat", ExecuteCardSetCostThisCombatAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("card.add_cost_until_played", ExecuteCardAddCostUntilPlayedAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("event.page", ExecuteEventPageAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("event.option", ExecuteEventOptionAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("event.goto_page", ExecuteEventGotoPageAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("event.proceed", ExecuteEventProceedAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("event.start_combat", ExecuteEventStartCombatAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("event.reward", ExecuteEventRewardAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("reward.offer_custom", ExecuteOfferCustomRewardAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("reward.mark_card_rewards_rerollable", ExecuteMarkCardRewardsRerollableAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("reward.card_options_upgrade", ExecuteCardRewardOptionsUpgradeAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("reward.card_options_enchant", ExecuteCardRewardOptionsEnchantAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("map.replace_generated", ExecuteReplaceGeneratedMapAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("map.remove_unknown_room_type", ExecuteRemoveUnknownRoomTypeAsync));
    }

    private static Task ExecuteSetAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        var key = GetProperty(node, "key");
        if (string.IsNullOrWhiteSpace(key))
        {
            return Task.CompletedTask;
        }

        context[key] = context.ResolveObject(GetProperty(node, "value"));
        return Task.CompletedTask;
    }

    private static Task ExecuteAddAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        var key = GetProperty(node, "key");
        if (string.IsNullOrWhiteSpace(key))
        {
            return Task.CompletedTask;
        }

        var delta = context.ResolveDecimal(GetProperty(node, "delta"));
        var current = context.TryGetState<object>(key, out var existing)
            ? ConvertToDecimal(existing)
            : 0m;
        context[key] = current + delta;
        return Task.CompletedTask;
    }

    private static Task ExecuteMultiplyAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        var key = GetProperty(node, "key");
        if (string.IsNullOrWhiteSpace(key))
        {
            return Task.CompletedTask;
        }

        var factor = context.ResolveDecimal(GetProperty(node, "factor"), 1m);
        var current = context.TryGetState<object>(key, out var existing)
            ? ConvertToDecimal(existing)
            : 0m;
        context[key] = current * factor;
        return Task.CompletedTask;
    }

    private static Task ExecuteCompareAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        var resultKey = GetProperty(node, "result_key", "last_compare");
        var comparisonOperator = GetProperty(node, "operator", "eq").Trim().ToLowerInvariant();
        var left = context.ResolveObject(GetProperty(node, "left"));
        var right = context.ResolveObject(GetProperty(node, "right"));
        context[resultKey] = EvaluateComparison(left, right, comparisonOperator);
        return Task.CompletedTask;
    }

    private static Task ExecuteLogAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        var message = context.ResolveString(GetProperty(node, "message"), $"Graph {context.Graph?.GraphId} executed debug.log.");
        context.ExecutionLog.Add(message);
        Log.Info($"[ModStudio.Graph] {message}");
        return Task.CompletedTask;
    }

    private static async Task ExecuteSelectCardsAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        if (context.Owner == null)
        {
            return;
        }

        var stateKey = GetProperty(node, "state_key", "selected_cards");
        var selectionMode = GetProperty(node, "selection_mode", "simple_grid");
        var sourcePile = GetProperty(node, "source_pile", PileType.Deck.ToString());
        var amount = Math.Max(1, (int)DynamicValueEvaluator.EvaluateRuntimeDecimal(node, "count", context, 1m));
        var allCards = ResolveCardsFromPileOrAll(context.Owner, sourcePile);
        if (allCards.Count == 0)
        {
            context[stateKey] = Array.Empty<CardModel>();
            return;
        }

        var selectedCards = new List<CardModel>();
        if (context.ChoiceContext == null)
        {
            selectedCards.AddRange(allCards.Take(amount));
            context[stateKey] = selectedCards;
            return;
        }

        var prefs = BuildSelectorPrefs(node, amount);
        var sourceModel = ResolveSourceModel(context) ?? context.Card;
        switch (selectionMode.Trim().ToLowerInvariant())
        {
            case "hand":
                if (sourceModel != null)
                {
                    selectedCards.AddRange(await CardSelectCmd.FromHand(context.ChoiceContext, context.Owner, prefs, null, sourceModel));
                }
                break;
            case "hand_for_discard":
                if (sourceModel != null)
                {
                    selectedCards.AddRange(await CardSelectCmd.FromHandForDiscard(context.ChoiceContext, context.Owner, prefs, null, sourceModel));
                }
                break;
            case "hand_for_upgrade":
            {
                if (sourceModel != null)
                {
                    var upgraded = await CardSelectCmd.FromHandForUpgrade(context.ChoiceContext, context.Owner, sourceModel);
                    if (upgraded != null)
                    {
                        selectedCards.Add(upgraded);
                    }
                }

                break;
            }
            case "deck_for_upgrade":
                selectedCards.AddRange(await CardSelectCmd.FromDeckForUpgrade(context.Owner, prefs));
                break;
            case "deck_for_enchantment":
            {
                var enchantmentId = GetProperty(node, "enchantment_id");
                var canonicalEnchantment = ModelDb.DebugEnchantments.FirstOrDefault(candidate =>
                    string.Equals(candidate.Id.Entry, enchantmentId, StringComparison.OrdinalIgnoreCase));
                if (canonicalEnchantment != null)
                {
                    selectedCards.AddRange(await CardSelectCmd.FromDeckForEnchantment(context.Owner, canonicalEnchantment, amount, prefs));
                }
                else
                {
                    selectedCards.AddRange(await CardSelectCmd.FromDeckGeneric(context.Owner, prefs));
                }

                break;
            }
            case "deck_for_transformation":
                selectedCards.AddRange(await CardSelectCmd.FromDeckForTransformation(context.Owner, prefs));
                break;
            case "deck_for_removal":
                selectedCards.AddRange(await CardSelectCmd.FromDeckForRemoval(context.Owner, prefs));
                break;
            case "choose_a_card_screen":
            {
                var selected = await CardSelectCmd.FromChooseACardScreen(
                    context.ChoiceContext,
                    allCards.Take(Math.Min(3, allCards.Count)).ToList(),
                    context.Owner,
                    canSkip: GetBoolProperty(node, "allow_cancel", false));
                if (selected != null)
                {
                    selectedCards.Add(selected);
                }

                break;
            }
            case "choose_bundle":
                selectedCards.AddRange((await CardSelectCmd.FromChooseABundleScreen(
                    context.Owner,
                    allCards.Chunk(Math.Max(1, amount)).Select(chunk => (IReadOnlyList<CardModel>)chunk.ToList()).ToList())).Take(amount));
                break;
            case "simple_grid_rewards":
                selectedCards.AddRange(await CardSelectCmd.FromSimpleGrid(context.ChoiceContext, allCards, context.Owner, prefs));
                break;
            default:
                selectedCards.AddRange(await CardSelectCmd.FromSimpleGrid(context.ChoiceContext, allCards, context.Owner, prefs));
                break;
        }

        context[stateKey] = selectedCards;
    }

    private static async Task ExecuteDamageAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        if (context.ChoiceContext == null)
        {
            return;
        }

        var targets = context.ResolveTargets(GetProperty(node, "target", "current_target"));
        if (targets.Count == 0)
        {
            return;
        }

        var amount = DynamicValueEvaluator.EvaluateRuntimeDecimal(node, "amount", context);
        var props = ParseValueProps(GetProperty(node, "props"));
        var results = (await CreatureCmd.Damage(context.ChoiceContext, targets, amount, props, context.Owner?.Creature, context.Card)).ToList();
        if (results.Count == 0)
        {
            return;
        }

        context["last_damage_results"] = results;
        context["last_damage_receivers"] = results.Select(result => result.Receiver).Where(receiver => receiver != null).ToList();
        context["last_damage_total"] = results.Sum(result => result.TotalDamage);
        context["last_damage_overkill"] = results.Sum(result => result.OverkillDamage);
        context["last_damage_total_plus_overkill"] = results.Sum(result => result.TotalDamage + result.OverkillDamage);
        context["last_damage_result"] = results[0];
    }

    private static async Task ExecuteGainBlockAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        var targets = context.ResolveTargets(GetProperty(node, "target", "self"));
        if (targets.Count == 0)
        {
            return;
        }

        var amount = DynamicValueEvaluator.EvaluateRuntimeDecimal(node, "amount", context);
        var props = ParseValueProps(GetProperty(node, "props"));
        foreach (var target in targets)
        {
            await CreatureCmd.GainBlock(target, amount, props, context.CardPlay);
        }
    }

    private static async Task ExecuteHealAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        var targets = context.ResolveTargets(GetProperty(node, "target", "self"));
        if (targets.Count == 0)
        {
            return;
        }

        var amount = DynamicValueEvaluator.EvaluateRuntimeDecimal(node, "amount", context);
        foreach (var target in targets)
        {
            await CreatureCmd.Heal(target, amount);
        }
    }

    private static async Task ExecuteDrawCardsAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        if (context.ChoiceContext == null || context.Owner == null)
        {
            return;
        }

        var amount = DynamicValueEvaluator.EvaluateRuntimeDecimal(node, "amount", context, 1m);
        await CardPileCmd.Draw(context.ChoiceContext, amount, context.Owner);
    }

    private static async Task ExecuteApplyPowerAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        var powerId = GetProperty(node, "power_id");
        if (string.IsNullOrWhiteSpace(powerId))
        {
            return;
        }

        var targets = context.ResolveTargets(GetProperty(node, "target", "current_target"));
        if (targets.Count == 0)
        {
            return;
        }

        var canonicalPower = ResolvePowerTemplate(powerId);
        if (canonicalPower == null)
        {
            Log.Warn($"Mod Studio graph '{context.Graph?.GraphId}' could not resolve power '{powerId}'.");
            return;
        }

        var amount = DynamicValueEvaluator.EvaluateRuntimeDecimal(node, "amount", context, 1m);
        foreach (var target in targets)
        {
            await PowerCmd.Apply(canonicalPower.ToMutable(), target, amount, context.Owner?.Creature, context.Card);
        }
    }

    private static async Task ExecuteGainEnergyAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        if (context.Owner == null)
        {
            return;
        }

        var amount = DynamicValueEvaluator.EvaluateRuntimeDecimal(node, "amount", context, 1m);
        if (amount <= 0m)
        {
            return;
        }

        await PlayerCmd.GainEnergy(amount, context.Owner);
    }

    private static async Task ExecuteGainStarsAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        if (context.Owner == null)
        {
            return;
        }

        var amount = DynamicValueEvaluator.EvaluateRuntimeDecimal(node, "amount", context, 1m);
        if (amount <= 0m)
        {
            return;
        }

        await PlayerCmd.GainStars(amount, context.Owner);
    }

    private static async Task ExecuteGainGoldAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        if (context.Owner == null)
        {
            return;
        }

        var amount = DynamicValueEvaluator.EvaluateRuntimeDecimal(node, "amount", context, 1m);
        if (amount <= 0m)
        {
            return;
        }

        await PlayerCmd.GainGold(amount, context.Owner);
    }

    private static async Task ExecuteLoseEnergyAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        if (context.Owner == null)
        {
            return;
        }

        var amount = DynamicValueEvaluator.EvaluateRuntimeDecimal(node, "amount", context, 1m);
        if (amount <= 0m)
        {
            return;
        }

        await PlayerCmd.LoseEnergy(amount, context.Owner);
    }

    private static async Task ExecuteLoseGoldAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        if (context.Owner == null)
        {
            return;
        }

        var amount = DynamicValueEvaluator.EvaluateRuntimeDecimal(node, "amount", context, 1m);
        if (amount <= 0m)
        {
            return;
        }

        var lossTypeText = GetProperty(node, "gold_loss_type", GoldLossType.Lost.ToString());
        if (!Enum.TryParse<GoldLossType>(lossTypeText, ignoreCase: true, out var lossType))
        {
            lossType = GoldLossType.Lost;
        }

        await PlayerCmd.LoseGold(amount, context.Owner, lossType);
    }

    private static async Task ExecuteGainMaxPotionCountAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        if (context.Owner == null)
        {
            return;
        }

        var amount = DynamicValueEvaluator.EvaluateRuntimeDecimal(node, "amount", context, 1m);
        if (amount <= 0m)
        {
            return;
        }

        await PlayerCmd.GainMaxPotionCount((int)amount, context.Owner);
    }

    private static async Task ExecuteDiscardCardsAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        if (context.Owner == null)
        {
            return;
        }

        var cards = ResolveCards(node, context);
        if (cards.Count == 0)
        {
            return;
        }

        if (context.ChoiceContext != null)
        {
            await CardCmd.Discard(context.ChoiceContext, cards);
            return;
        }

        await CardPileCmd.Add(cards, PileType.Discard);
    }

    private static async Task ExecuteExhaustCardsAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        if (context.Owner == null)
        {
            return;
        }

        var cards = ResolveCards(node, context);
        if (cards.Count == 0)
        {
            return;
        }

        if (context.ChoiceContext != null)
        {
            foreach (var card in cards)
            {
                await CardCmd.Exhaust(context.ChoiceContext, card, causedByEthereal: false, skipVisuals: false);
            }
            return;
        }

        await CardPileCmd.Add(cards, PileType.Exhaust);
    }

    private static async Task ExecuteCreateCardAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        if (context.Owner?.RunState == null)
        {
            return;
        }

        var cardId = GetProperty(node, "card_id");
        if (string.IsNullOrWhiteSpace(cardId))
        {
            return;
        }

        var canonicalCard = ModelDb.AllCards.FirstOrDefault(card => string.Equals(card.Id.Entry, cardId, StringComparison.OrdinalIgnoreCase));
        if (canonicalCard == null)
        {
            Log.Warn($"Mod Studio graph '{context.Graph?.GraphId}' could not resolve card '{cardId}' for combat.create_card.");
            return;
        }

        var targetPile = ParsePileType(GetProperty(node, "target_pile", "hand"), PileType.Hand);
        var count = Math.Max(1, (int)DynamicValueEvaluator.EvaluateRuntimeDecimal(node, "count", context, 1m));
        for (var i = 0; i < count; i++)
        {
            var created = context.Owner.RunState.CreateCard(canonicalCard, context.Owner);
            await CardPileCmd.Add(created, targetPile);
        }
    }

    private static async Task ExecuteMoveCardsAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        if (context.Owner == null)
        {
            return;
        }

        var sourcePileSelector = GetProperty(node, "source_pile", PileType.Discard.ToString());
        var targetPile = ParsePileType(GetProperty(node, "target_pile", PileType.Hand.ToString()), PileType.Hand);
        var cards = ResolveCardsFromPileOrAll(context.Owner, sourcePileSelector);
        if (cards.Count == 0)
        {
            context["moved_cards"] = Array.Empty<CardModel>();
            return;
        }

        var exactEnergyCost = (int)DynamicValueEvaluator.EvaluateRuntimeDecimal(node, "exact_energy_cost", context, -1m);
        var includeXCost = GetBoolProperty(node, "include_x_cost", false);
        var cardTypeScope = GetProperty(node, "card_type_scope", "any");
        var limit = Math.Max(0, (int)DynamicValueEvaluator.EvaluateRuntimeDecimal(node, "count", context, 0m));

        IEnumerable<CardModel> filtered = cards
            .Where(card => MatchesEnergyCost(card, exactEnergyCost, includeXCost))
            .Where(card => MatchesCardTypeScope(card, cardTypeScope));

        if (limit > 0)
        {
            filtered = filtered.Take(limit);
        }

        var movedCards = filtered.ToList();
        context["moved_cards"] = movedCards;
        if (movedCards.Count == 0)
        {
            return;
        }

        foreach (var card in movedCards)
        {
            await CardPileCmd.Add(card, targetPile);
        }
    }

    private static async Task ExecuteRemoveCardAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        if (context.Owner == null)
        {
            return;
        }

        var cards = ResolveCards(node, context);
        if (cards.Count == 0)
        {
            return;
        }

        foreach (var card in cards)
        {
            switch (card.Pile?.Type)
            {
                case PileType.Deck:
                    await CardPileCmd.RemoveFromDeck(card, true);
                    break;
                case PileType.None:
                    break;
                default:
                    await CardPileCmd.RemoveFromCombat(card, false);
                    break;
            }
        }
    }

    private static async Task ExecuteTransformCardAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        if (context.Owner?.RunState == null)
        {
            return;
        }

        var cards = ResolveCards(node, context);
        if (cards.Count == 0)
        {
            return;
        }

        var randomReplacement = GetBoolProperty(node, "random_replacement", false);
        var replacementId = GetProperty(node, "replacement_card_id");
        foreach (var original in cards)
        {
            if (randomReplacement || string.IsNullOrWhiteSpace(replacementId))
            {
                var rng = context.Owner.PlayerRng.Rewards;
                await CardCmd.TransformToRandom(original, rng);
                continue;
            }

            var canonicalCard = ModelDb.AllCards.FirstOrDefault(card => string.Equals(card.Id.Entry, replacementId, StringComparison.OrdinalIgnoreCase));
            if (canonicalCard == null)
            {
                Log.Warn($"Mod Studio graph '{context.Graph?.GraphId}' could not resolve replacement card '{replacementId}'.");
                continue;
            }

            var replacement = context.Owner.RunState.CreateCard(canonicalCard, context.Owner);
            await CardCmd.Transform(original, replacement);
        }
    }

    private static async Task ExecuteDiscardAndDrawAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        if (context.Owner == null)
        {
            return;
        }

        var cards = ResolveCards(node, context);
        if (cards.Count == 0)
        {
            return;
        }

        var drawCount = Math.Max(0, (int)DynamicValueEvaluator.EvaluateRuntimeDecimal(node, "draw_count", context, 0m));
        if (context.ChoiceContext != null)
        {
            await CardCmd.DiscardAndDraw(context.ChoiceContext, cards, drawCount);
            return;
        }

        await CardPileCmd.Add(cards, PileType.Discard);
        if (drawCount > 0)
        {
            await CardPileCmd.Draw(new ThrowingPlayerChoiceContext(), drawCount, context.Owner);
        }
    }

    private static Task ExecuteApplyCardKeywordAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        var cards = ResolveCards(node, context);
        if (cards.Count == 0)
        {
            return Task.CompletedTask;
        }

        var keywordText = GetProperty(node, "keyword");
        if (!Enum.TryParse<CardKeyword>(keywordText, ignoreCase: true, out var keyword))
        {
            return Task.CompletedTask;
        }

        foreach (var card in cards)
        {
            CardCmd.ApplyKeyword(card, keyword);
        }

        return Task.CompletedTask;
    }

    private static Task ExecuteUpgradeCardAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        var cards = ResolveCards(node, context);
        if (cards.Count == 0)
        {
            return Task.CompletedTask;
        }

        var styleText = GetProperty(node, "card_preview_style", CardPreviewStyle.HorizontalLayout.ToString());
        if (!Enum.TryParse<CardPreviewStyle>(styleText, ignoreCase: true, out var style))
        {
            style = CardPreviewStyle.HorizontalLayout;
        }

        CardCmd.Upgrade(cards, style);
        return Task.CompletedTask;
    }

    private static Task ExecuteDowngradeCardAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        var cards = ResolveCards(node, context);
        if (cards.Count == 0)
        {
            return Task.CompletedTask;
        }

        foreach (var card in cards)
        {
            CardCmd.Downgrade(card);
        }

        return Task.CompletedTask;
    }

    private static Task ExecuteEnchantCardAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        var cards = ResolveCards(node, context);
        if (cards.Count == 0)
        {
            return Task.CompletedTask;
        }

        var enchantmentId = GetProperty(node, "enchantment_id");
        if (string.IsNullOrWhiteSpace(enchantmentId))
        {
            return Task.CompletedTask;
        }

        var amount = DynamicValueEvaluator.EvaluateRuntimeDecimal(node, "amount", context, 1m);
        var enchantment = ModelDb.DebugEnchantments.FirstOrDefault(candidate => string.Equals(candidate.Id.Entry, enchantmentId, StringComparison.OrdinalIgnoreCase));
        if (enchantment == null)
        {
            Log.Warn($"Mod Studio graph '{context.Graph?.GraphId}' could not resolve enchantment '{enchantmentId}'.");
            return Task.CompletedTask;
        }

        foreach (var card in cards)
        {
            try
            {
                CardCmd.Enchant(enchantment.ToMutable(), card, amount);
            }
            catch (InvalidOperationException exception)
            {
                Log.Warn($"Mod Studio graph '{context.Graph?.GraphId}' could not enchant card '{card.Id.Entry}' with '{enchantmentId}': {exception.Message}");
            }
        }

        return Task.CompletedTask;
    }

    private static async Task ExecuteAutoPlayCardAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        if (context.ChoiceContext == null)
        {
            return;
        }

        var cards = ResolveCards(node, context);
        if (cards.Count == 0)
        {
            return;
        }

        var autoPlayTypeText = GetProperty(node, "auto_play_type", AutoPlayType.Default.ToString());
        if (!Enum.TryParse<AutoPlayType>(autoPlayTypeText, ignoreCase: true, out var autoPlayType))
        {
            autoPlayType = AutoPlayType.Default;
        }

        var skipXCapture = GetBoolProperty(node, "skip_x_capture", false);
        var skipCardPileVisuals = GetBoolProperty(node, "skip_card_pile_visuals", false);
        var target = context.ResolveTargets(GetProperty(node, "target", "current_target")).FirstOrDefault();
        foreach (var card in cards)
        {
            await CardCmd.AutoPlay(context.ChoiceContext, card, target, autoPlayType, skipXCapture, skipCardPileVisuals);
        }
    }

    private static Task ExecuteApplySingleTurnSlyAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        var cards = ResolveCards(node, context);
        foreach (var card in cards)
        {
            CardCmd.ApplySingleTurnSly(card);
        }

        return Task.CompletedTask;
    }

    private static Task ExecuteRemoveCardKeywordAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        var keywordText = GetProperty(node, "keyword");
        if (!Enum.TryParse<CardKeyword>(keywordText, ignoreCase: true, out var keyword))
        {
            return Task.CompletedTask;
        }

        var cards = ResolveCards(node, context);
        foreach (var card in cards)
        {
            CardCmd.RemoveKeyword(card, keyword);
        }

        return Task.CompletedTask;
    }

    private static async Task ExecuteAutoPlayFromDrawPileAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        if (context.Owner == null || context.ChoiceContext == null)
        {
            return;
        }

        var count = Math.Max(1, (int)DynamicValueEvaluator.EvaluateRuntimeDecimal(node, "count", context, 1m));
        var positionText = GetProperty(node, "position", CardPilePosition.Bottom.ToString());
        if (!Enum.TryParse<CardPilePosition>(positionText, ignoreCase: true, out var position))
        {
            position = CardPilePosition.Bottom;
        }

        var forceExhaust = GetBoolProperty(node, "force_exhaust", false);
        await CardPileCmd.AutoPlayFromDrawPile(context.ChoiceContext, context.Owner, count, position, forceExhaust);
    }

    private static async Task ExecuteChannelOrbAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        if (context.Owner == null)
        {
            return;
        }

        var orbId = GetProperty(node, "orb_id");
        if (string.IsNullOrWhiteSpace(orbId))
        {
            return;
        }

        var canonicalOrb = ModelDb.Orbs.FirstOrDefault(candidate => string.Equals(candidate.Id.Entry, orbId, StringComparison.OrdinalIgnoreCase));
        if (canonicalOrb == null)
        {
            Log.Warn($"Mod Studio graph '{context.Graph?.GraphId}' could not resolve orb '{orbId}'.");
            return;
        }

        // HookPlayerChoiceContext can deadlock when orb channeling itself emits nested combat hooks.
        // Native relic implementations use a blocking context for these hook-time orb actions.
        var choiceContext = context.ChoiceContext is HookPlayerChoiceContext
            ? new BlockingPlayerChoiceContext()
            : context.ChoiceContext ?? new ThrowingPlayerChoiceContext();

        await OrbCmd.Channel(choiceContext, canonicalOrb.ToMutable(), context.Owner);
    }

    private static async Task ExecuteOrbPassiveAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        if (context.Owner == null || context.ChoiceContext == null)
        {
            return;
        }

        var orbId = GetProperty(node, "orb_id");
        var orb = context.Owner.PlayerCombatState?.OrbQueue?.Orbs
            .FirstOrDefault(candidate =>
                string.IsNullOrWhiteSpace(orbId) ||
                string.Equals(candidate.Id.Entry, orbId, StringComparison.OrdinalIgnoreCase));
        if (orb == null)
        {
            return;
        }

        var target = context.ResolveTargets(GetProperty(node, "target", "current_target")).FirstOrDefault();
        await OrbCmd.Passive(context.ChoiceContext, orb, target);
    }

    private static Task ExecuteAddOrbSlotsAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        if (context.Owner == null)
        {
            return Task.CompletedTask;
        }

        var amount = Math.Max(1, (int)DynamicValueEvaluator.EvaluateRuntimeDecimal(node, "amount", context, 1m));
        return OrbCmd.AddSlots(context.Owner, amount);
    }

    private static Task ExecuteRemoveOrbSlotsAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        if (context.Owner == null)
        {
            return Task.CompletedTask;
        }

        var amount = Math.Max(1, (int)DynamicValueEvaluator.EvaluateRuntimeDecimal(node, "amount", context, 1m));
        OrbCmd.RemoveSlots(context.Owner, amount);
        return Task.CompletedTask;
    }

    private static Task ExecuteEvokeNextOrbAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        if (context.Owner == null)
        {
            return Task.CompletedTask;
        }

        var dequeue = GetBoolProperty(node, "dequeue", true);
        return OrbCmd.EvokeNext(context.ChoiceContext ?? new ThrowingPlayerChoiceContext(), context.Owner, dequeue);
    }

    private static async Task ExecuteProcurePotionAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        if (context.Owner == null)
        {
            return;
        }

        var potionId = GetProperty(node, "potion_id");
        if (string.IsNullOrWhiteSpace(potionId))
        {
            return;
        }

        var canonicalPotion = ModelDb.AllPotions.FirstOrDefault(candidate => string.Equals(candidate.Id.Entry, potionId, StringComparison.OrdinalIgnoreCase));
        if (canonicalPotion == null)
        {
            Log.Warn($"Mod Studio graph '{context.Graph?.GraphId}' could not resolve potion '{potionId}'.");
            return;
        }

        await PotionCmd.TryToProcure(canonicalPotion.ToMutable(), context.Owner);
    }

    private static async Task ExecuteDiscardPotionAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        var potion = ResolvePotion(node, context);
        if (potion == null)
        {
            return;
        }

        await PotionCmd.Discard(potion);
    }

    private static async Task ExecuteObtainRelicAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        if (context.Owner == null)
        {
            return;
        }

        var relicId = GetProperty(node, "relic_id");
        if (string.IsNullOrWhiteSpace(relicId))
        {
            return;
        }

        var canonicalRelic = ModelDb.AllRelics.FirstOrDefault(candidate => string.Equals(candidate.Id.Entry, relicId, StringComparison.OrdinalIgnoreCase));
        if (canonicalRelic == null)
        {
            Log.Warn($"Mod Studio graph '{context.Graph?.GraphId}' could not resolve relic '{relicId}'.");
            return;
        }

        await RelicCmd.Obtain(canonicalRelic.ToMutable(), context.Owner);
    }

    private static async Task ExecuteRemoveRelicAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        var relic = ResolveRelic(node, context);
        if (relic == null)
        {
            return;
        }

        await RelicCmd.Remove(relic);
    }

    private static async Task ExecuteReplaceRelicAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        var relic = ResolveRelic(node, context);
        if (relic == null || context.Owner == null)
        {
            return;
        }

        var replacementRelicId = GetProperty(node, "replacement_relic_id");
        if (string.IsNullOrWhiteSpace(replacementRelicId))
        {
            return;
        }

        var replacementRelic = ModelDb.AllRelics.FirstOrDefault(candidate => string.Equals(candidate.Id.Entry, replacementRelicId, StringComparison.OrdinalIgnoreCase));
        if (replacementRelic == null)
        {
            Log.Warn($"Mod Studio graph '{context.Graph?.GraphId}' could not resolve replacement relic '{replacementRelicId}'.");
            return;
        }

        await RelicCmd.Replace(relic, replacementRelic.ToMutable());
    }

    private static async Task ExecuteMeltRelicAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        var relic = ResolveRelic(node, context);
        if (relic == null)
        {
            return;
        }

        await RelicCmd.Melt(relic);
    }

    private static async Task ExecuteAddPetAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        if (context.Owner?.Creature?.CombatState == null)
        {
            return;
        }

        var monsterId = GetProperty(node, "monster_id");
        if (string.IsNullOrWhiteSpace(monsterId))
        {
            return;
        }

        var canonicalMonster = ModelDb.Monsters.FirstOrDefault(candidate => string.Equals(candidate.Id.Entry, monsterId, StringComparison.OrdinalIgnoreCase))
            ?? ModelDb.Monsters.FirstOrDefault(candidate => string.Equals(candidate.GetType().Name, monsterId, StringComparison.OrdinalIgnoreCase))
            ?? ModelDb.Monsters.FirstOrDefault(candidate =>
                string.Equals(NormalizeLookupKey(candidate.Id.Entry), NormalizeLookupKey(monsterId), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(NormalizeLookupKey(candidate.GetType().Name), NormalizeLookupKey(monsterId), StringComparison.OrdinalIgnoreCase));
        if (canonicalMonster == null)
        {
            Log.Warn($"Mod Studio graph '{context.Graph?.GraphId}' could not resolve monster '{monsterId}' for player.add_pet.");
            return;
        }

        var creature = context.Owner.Creature.CombatState.CreateCreature(canonicalMonster.ToMutable(), context.Owner.Creature.Side, null);
        await PlayerCmd.AddPet(creature, context.Owner);
    }

    private static async Task ExecuteForgeAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        if (context.Owner == null)
        {
            return;
        }

        var amount = DynamicValueEvaluator.EvaluateRuntimeDecimal(node, "amount", context, 1m);
        var source = ResolveSourceModel(context) ?? context.Card;
        await ForgeCmd.Forge(amount, context.Owner, source);
    }

    private static Task ExecuteCompleteQuestAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        if (context.Card == null)
        {
            return Task.CompletedTask;
        }

        PlayerCmd.CompleteQuest(context.Card);
        return Task.CompletedTask;
    }

    private static Task ExecuteRestHealAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        if (context.Owner == null)
        {
            return Task.CompletedTask;
        }

        var playSfx = GetBoolProperty(node, "play_sfx", true);
        return PlayerCmd.MimicRestSiteHeal(context.Owner, playSfx);
    }

    private static Task ExecuteEndTurnAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        if (context.Owner == null)
        {
            return Task.CompletedTask;
        }

        PlayerCmd.EndTurn(context.Owner, canBackOut: false);
        return Task.CompletedTask;
    }

    private static Task ExecuteRepeatAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        context["repeat.count"] = DynamicValueEvaluator.EvaluateRuntimeDecimal(node, "count", context, 1m);
        return Task.CompletedTask;
    }

    private static async Task ExecuteLoseBlockAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        var targets = context.ResolveTargets(GetProperty(node, "target", "current_target"));
        if (targets.Count == 0)
        {
            return;
        }

        var amount = DynamicValueEvaluator.EvaluateRuntimeDecimal(node, "amount", context, 0m);
        if (amount <= 0m)
        {
            return;
        }

        foreach (var target in targets)
        {
            await CreatureCmd.LoseBlock(target, amount);
        }
    }

    private static async Task ExecuteLoseHpAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        if (context.Owner == null || context.ChoiceContext == null)
        {
            return;
        }

        var targets = context.ResolveTargets(GetProperty(node, "target", "self"));
        if (targets.Count == 0)
        {
            return;
        }

        var amount = DynamicValueEvaluator.EvaluateRuntimeDecimal(node, "amount", context, 0m);
        if (amount <= 0m)
        {
            return;
        }

        var props = ParseValueProps(GetProperty(node, "props", "none")) | ValueProp.Unblockable | ValueProp.Unpowered;
        await CreatureCmd.Damage(context.ChoiceContext, targets, amount, props, context.Owner.Creature);
    }

    private static async Task ExecuteGainMaxHpAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        if (context.Owner == null)
        {
            return;
        }

        var targets = context.ResolveTargets(GetProperty(node, "target", "self"));
        if (targets.Count == 0)
        {
            return;
        }

        var amount = DynamicValueEvaluator.EvaluateRuntimeDecimal(node, "amount", context, 0m);
        if (amount <= 0m)
        {
            return;
        }

        foreach (var target in targets)
        {
            await CreatureCmd.GainMaxHp(target, amount);
        }
    }

    private static async Task ExecuteLoseMaxHpAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        if (context.ChoiceContext == null)
        {
            return;
        }

        var targets = context.ResolveTargets(GetProperty(node, "target", "self"));
        if (targets.Count == 0)
        {
            return;
        }

        var amount = DynamicValueEvaluator.EvaluateRuntimeDecimal(node, "amount", context, 0m);
        if (amount <= 0m)
        {
            return;
        }

        var isFromCard = GetBoolProperty(node, "is_from_card", false);
        foreach (var target in targets)
        {
            await CreatureCmd.LoseMaxHp(context.ChoiceContext, target, amount, isFromCard);
        }
    }

    private static async Task ExecuteSetCurrentHpAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        var targets = context.ResolveTargets(GetProperty(node, "target", "self"));
        if (targets.Count == 0)
        {
            return;
        }

        var amount = DynamicValueEvaluator.EvaluateRuntimeDecimal(node, "amount", context, 0m);
        foreach (var target in targets)
        {
            await CreatureCmd.SetCurrentHp(target, amount);
        }
    }

    private static async Task ExecuteCreatureKillAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        var targets = context.ResolveTargets(GetProperty(node, "target", "current_target"));
        if (targets.Count == 0)
        {
            return;
        }

        var force = GetBoolProperty(node, "force", false);
        foreach (var target in targets)
        {
            await CreatureCmd.Kill(target, force);
        }
    }

    private static async Task ExecuteCreatureStunAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        var targets = context.ResolveTargets(GetProperty(node, "target", "current_target"));
        if (targets.Count == 0)
        {
            return;
        }

        var nextMoveId = GetProperty(node, "next_move_id");
        foreach (var target in targets)
        {
            await CreatureCmd.Stun(target, nextMoveId);
        }
    }

    private static async Task ExecuteRemovePowerAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        var powerId = GetProperty(node, "power_id");
        if (string.IsNullOrWhiteSpace(powerId))
        {
            return;
        }

        var targets = context.ResolveTargets(GetProperty(node, "target", "current_target"));
        if (targets.Count == 0)
        {
            return;
        }

        foreach (var target in targets)
        {
            var power = target.Powers.FirstOrDefault(candidate => string.Equals(candidate.Id.Entry, powerId, StringComparison.OrdinalIgnoreCase));
            if (power == null)
            {
                continue;
            }

            await PowerCmd.Remove(power);
        }
    }

    private static async Task ExecuteModifyPowerAmountAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        var powerId = GetProperty(node, "power_id");
        if (string.IsNullOrWhiteSpace(powerId))
        {
            return;
        }

        var targets = context.ResolveTargets(GetProperty(node, "target", "current_target"));
        if (targets.Count == 0)
        {
            return;
        }

        var amount = DynamicValueEvaluator.EvaluateRuntimeDecimal(node, "amount", context, 1m);
        var silent = GetBoolProperty(node, "silent", false);
        var cardSource = context.Card ?? context.CardPlay?.Card;
        foreach (var target in targets)
        {
            var power = target.Powers.FirstOrDefault(candidate => string.Equals(candidate.Id.Entry, powerId, StringComparison.OrdinalIgnoreCase));
            if (power == null)
            {
                continue;
            }

            await PowerCmd.ModifyAmount(power, amount, context.Owner?.Creature, cardSource, silent);
        }
    }

    private static async Task ExecuteShuffleCardPileAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        if (context.ChoiceContext == null || context.Owner == null)
        {
            return;
        }

        await CardPileCmd.Shuffle(context.ChoiceContext, context.Owner);
    }

    private static Task ExecuteModifierDamageAdditiveAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        context["modifier_result"] = DynamicValueEvaluator.EvaluateRuntimeDecimal(node, "amount", context, 0m);
        return Task.CompletedTask;
    }

    private static Task ExecuteModifierDamageMultiplicativeAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        context["modifier_result"] = DynamicValueEvaluator.EvaluateRuntimeDecimal(node, "amount", context, 1m);
        return Task.CompletedTask;
    }

    private static Task ExecuteModifierBlockAdditiveAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        context["modifier_result"] = DynamicValueEvaluator.EvaluateRuntimeDecimal(node, "amount", context, 0m);
        return Task.CompletedTask;
    }

    private static Task ExecuteModifierBlockMultiplicativeAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        context["modifier_result"] = DynamicValueEvaluator.EvaluateRuntimeDecimal(node, "amount", context, 1m);
        return Task.CompletedTask;
    }

    private static Task ExecuteModifierPlayCountAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        var basePlayCount = context.TryGetState<object>("play_count_base", out var rawBase)
            ? ConvertToDecimal(rawBase)
            : 1m;
        var delta = DynamicValueEvaluator.EvaluateRuntimeDecimal(node, "amount", context, 0m);
        var mode = GetProperty(node, "mode", "delta").Trim().ToLowerInvariant();
        context["modifier_result"] = mode switch
        {
            "absolute" => delta,
            _ => basePlayCount + delta
        };
        return Task.CompletedTask;
    }

    private static Task ExecuteModifierHandDrawAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        var baseHandDraw = context.TryGetState<object>("hand_draw_base", out var rawBase)
            ? ConvertToDecimal(rawBase)
            : 0m;
        var delta = DynamicValueEvaluator.EvaluateRuntimeDecimal(node, "amount", context, 0m);
        var mode = GetProperty(node, "mode", "delta").Trim().ToLowerInvariant();
        context["modifier_result"] = mode switch
        {
            "absolute" => delta,
            _ => baseHandDraw + delta
        };
        return Task.CompletedTask;
    }

    private static Task ExecuteModifierXValueAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        var baseXValue = context.TryGetState<object>("x_value_base", out var rawBase)
            ? ConvertToDecimal(rawBase)
            : 0m;
        var delta = DynamicValueEvaluator.EvaluateRuntimeDecimal(node, "amount", context, 0m);
        var mode = GetProperty(node, "mode", "delta").Trim().ToLowerInvariant();
        context["modifier_result"] = mode switch
        {
            "absolute" => delta,
            _ => baseXValue + delta
        };
        return Task.CompletedTask;
    }

    private static Task ExecuteModifierMaxEnergyAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        var baseMaxEnergy = context.TryGetState<object>("max_energy_base", out var rawBase)
            ? ConvertToDecimal(rawBase)
            : 0m;
        var delta = DynamicValueEvaluator.EvaluateRuntimeDecimal(node, "amount", context, 0m);
        var mode = GetProperty(node, "mode", "delta").Trim().ToLowerInvariant();
        context["modifier_result"] = mode switch
        {
            "absolute" => delta,
            _ => baseMaxEnergy + delta
        };
        return Task.CompletedTask;
    }

    private static Task ExecuteEnchantmentSetStatusAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        if (context.Enchantment == null)
        {
            return Task.CompletedTask;
        }

        var statusText = GetProperty(node, "status", "Disabled");
        if (!Enum.TryParse<EnchantmentStatus>(statusText, ignoreCase: true, out var status))
        {
            status = EnchantmentStatus.Disabled;
        }

        context.Enchantment.Status = status;
        return Task.CompletedTask;
    }

    private static Task ExecuteCardSetCostDeltaAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        foreach (var card in ResolveCards(node, context))
        {
            var amount = (int)DynamicValueEvaluator.EvaluateRuntimeDecimal(node, "amount", context, 0m);
            card.EnergyCost.UpgradeBy(amount);
        }

        return Task.CompletedTask;
    }

    private static Task ExecuteCardSetCostAbsoluteAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        foreach (var card in ResolveCards(node, context))
        {
            var amount = (int)DynamicValueEvaluator.EvaluateRuntimeDecimal(node, "amount", context, 0m);
            card.EnergyCost.SetCustomBaseCost(amount);
        }

        return Task.CompletedTask;
    }

    private static Task ExecuteCardSetCostThisCombatAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        foreach (var card in ResolveCards(node, context))
        {
            var amount = (int)DynamicValueEvaluator.EvaluateRuntimeDecimal(node, "amount", context, 0m);
            card.EnergyCost.SetThisCombat(amount);
        }

        return Task.CompletedTask;
    }

    private static Task ExecuteCardAddCostUntilPlayedAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        foreach (var card in ResolveCards(node, context))
        {
            var amount = (int)DynamicValueEvaluator.EvaluateRuntimeDecimal(node, "amount", context, -1m);
            card.EnergyCost.AddUntilPlayed(amount);
        }

        return Task.CompletedTask;
    }

    private static Task ExecuteEventPageAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        context["event.page_id"] = GetProperty(node, "page_id");
        context["event.title"] = GetProperty(node, "title");
        context["event.description"] = GetProperty(node, "description");
        context["event.is_start"] = GetBoolProperty(node, "is_start", false);
        context["event.option_order"] = GetProperty(node, "option_order");
        return Task.CompletedTask;
    }

    private static Task ExecuteEventOptionAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        context["event.option_id"] = GetProperty(node, "option_id");
        context["event.page_id"] = GetProperty(node, "page_id");
        context["event.title"] = GetProperty(node, "title");
        context["event.description"] = GetProperty(node, "description");
        context["event.next_page_id"] = GetProperty(node, "next_page_id");
        context["event.encounter_id"] = GetProperty(node, "encounter_id");
        context["event.resume_page_id"] = GetProperty(node, "resume_page_id");
        context["event.is_proceed"] = GetBoolProperty(node, "is_proceed", false);
        context["event.save_choice_to_history"] = GetBoolProperty(node, "save_choice_to_history", true);
        context["event.reward_kind"] = GetProperty(node, "reward_kind");
        context["event.reward_amount"] = GetProperty(node, "reward_amount");
        context["event.reward_target"] = GetProperty(node, "reward_target");
        context["event.reward_props"] = GetProperty(node, "reward_props");
        context["event.reward_power_id"] = GetProperty(node, "reward_power_id");
        return Task.CompletedTask;
    }

    private static Task ExecuteEventGotoPageAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        context["event.next_page_id"] = GetProperty(node, "next_page_id");
        return Task.CompletedTask;
    }

    private static Task ExecuteEventProceedAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        context["event.is_proceed"] = true;
        return Task.CompletedTask;
    }

    private static Task ExecuteEventStartCombatAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        context["event.encounter_id"] = GetProperty(node, "encounter_id");
        context["event.resume_page_id"] = GetProperty(node, "resume_page_id");
        return Task.CompletedTask;
    }

    private static Task ExecuteEventRewardAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        context["event.reward_kind"] = GetProperty(node, "reward_kind");
        context["event.reward_amount"] = GetProperty(node, "reward_amount");
        context["event.reward_target"] = GetProperty(node, "reward_target");
        context["event.reward_props"] = GetProperty(node, "reward_props");
        context["event.reward_power_id"] = GetProperty(node, "reward_power_id");
        return Task.CompletedTask;
    }

    private static async Task ExecuteOfferCustomRewardAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        if (context.Owner == null)
        {
            return;
        }

        var rewardKind = GetProperty(node, "reward_kind", "custom").Trim().ToLowerInvariant();
        var rewardCount = Math.Max(1, (int)DynamicValueEvaluator.EvaluateRuntimeDecimal(node, "reward_count", context, 1m));
        var amount = Math.Max(0, (int)DynamicValueEvaluator.EvaluateRuntimeDecimal(node, "amount", context, 0m));
        var cardCount = Math.Max(1, (int)DynamicValueEvaluator.EvaluateRuntimeDecimal(node, "card_count", context, 3m));
        var rewards = new List<Reward>();
        switch (rewardKind)
        {
            case "gold":
                rewards.Add(new GoldReward(amount, context.Owner));
                break;
            case "card_reward":
            case "card":
            {
                var roomTypeText = GetProperty(node, "reward_room_type", RoomType.Monster.ToString());
                var roomType = Enum.TryParse<RoomType>(roomTypeText, ignoreCase: true, out var parsedRoomType)
                    ? parsedRoomType
                    : RoomType.Monster;
                rewards.Add(new CardReward(CardCreationOptions.ForRoom(context.Owner, roomType), cardCount, context.Owner));
                break;
            }
            case "relic":
            {
                var relicId = GetProperty(node, "relic_id");
                var canonicalRelic = ModelDb.AllRelics.FirstOrDefault(candidate => string.Equals(candidate.Id.Entry, relicId, StringComparison.OrdinalIgnoreCase));
                rewards.Add(canonicalRelic == null ? new RelicReward(context.Owner) : new RelicReward(canonicalRelic.ToMutable(), context.Owner));
                break;
            }
            case "potion":
            {
                var potionId = GetProperty(node, "potion_id");
                var canonicalPotion = ModelDb.AllPotions.FirstOrDefault(candidate => string.Equals(candidate.Id.Entry, potionId, StringComparison.OrdinalIgnoreCase));
                rewards.Add(canonicalPotion == null ? new PotionReward(context.Owner) : new PotionReward(canonicalPotion.ToMutable(), context.Owner));
                break;
            }
            case "special_card":
            {
                var cardId = GetProperty(node, "card_id");
                var canonicalCard = ModelDb.AllCards.FirstOrDefault(candidate => string.Equals(candidate.Id.Entry, cardId, StringComparison.OrdinalIgnoreCase));
                if (canonicalCard != null)
                {
                    rewards.Add(new SpecialCardReward(canonicalCard.ToMutable(), context.Owner));
                }

                break;
            }
            case "card_removal":
                rewards.Add(new CardRemovalReward(context.Owner));
                break;
            default:
                context.ExecutionLog.Add($"Reward offer node '{node.NodeId}' is in custom placeholder mode and requires manual setup.");
                return;
        }

        if (rewards.Count == 0)
        {
            return;
        }

        var expanded = new List<Reward>();
        for (var i = 0; i < rewardCount; i++)
        {
            expanded.AddRange(rewards);
        }

        if (context.TryGetState<object>("reward_list", out var rewardListState) &&
            rewardListState is ICollection<Reward> rewardList)
        {
            foreach (var reward in expanded)
            {
                rewardList.Add(reward);
            }

            context["reward_modified"] = true;
            return;
        }

        await RewardsCmd.OfferCustom(context.Owner, expanded);
    }

    private static Task ExecuteMarkCardRewardsRerollableAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        _ = node;
        if (!context.TryGetState<object>("reward_list", out var rewardListState) ||
            rewardListState is not IEnumerable<Reward> rewardList)
        {
            return Task.CompletedTask;
        }

        var changed = false;
        foreach (var cardReward in rewardList.OfType<CardReward>())
        {
            if (cardReward.CanReroll)
            {
                continue;
            }

            cardReward.CanReroll = true;
            changed = true;
        }

        if (changed)
        {
            context["reward_modified"] = true;
        }

        return Task.CompletedTask;
    }

    private static Task ExecuteReplaceGeneratedMapAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        if (context.RunState == null)
        {
            return Task.CompletedTask;
        }

        var mapKind = GetProperty(node, "map_kind").Trim().ToLowerInvariant();
        context["generated_map_result"] = mapKind switch
        {
            "golden_path" => new GoldenPathActMap(context.RunState),
            _ => context["generated_map_result"]
        };
        return Task.CompletedTask;
    }

    private static Task ExecuteRemoveUnknownRoomTypeAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        if (!context.TryGetState<object>("unknown_room_types", out var rawValue) ||
            rawValue is not HashSet<RoomType> roomTypes)
        {
            return Task.CompletedTask;
        }

        var roomTypeText = GetProperty(node, "room_type", RoomType.Monster.ToString());
        if (!Enum.TryParse<RoomType>(roomTypeText, ignoreCase: true, out var roomType))
        {
            return Task.CompletedTask;
        }

        roomTypes.Remove(roomType);
        context["unknown_room_types_result"] = roomTypes;
        return Task.CompletedTask;
    }

    private static Task ExecuteCardRewardOptionsUpgradeAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        if (context.Owner == null ||
            context.SourceModel is not RelicModel modifyingRelic ||
            !TryGetCardRewardOptions(context, out var cardRewards))
        {
            return Task.CompletedTask;
        }

        if (GetBoolProperty(node, "require_hook_upgrades_enabled", false) &&
            TryGetCardRewardCreationOptions(context, out var creationOptions) &&
            creationOptions.Flags.HasFlag(CardCreationFlags.NoHookUpgrades))
        {
            return Task.CompletedTask;
        }

        var scope = GetProperty(node, "card_type_scope", "any");
        var changed = false;
        foreach (var option in cardRewards)
        {
            var card = option.Card;
            if (!MatchesCardTypeScope(card, scope) || !card.IsUpgradable)
            {
                continue;
            }

            var clone = context.Owner.RunState.CloneCard(card);
            CardCmd.Upgrade(clone, CardPreviewStyle.None);
            option.ModifyCard(clone, modifyingRelic);
            changed = true;
        }

        if (changed)
        {
            context["card_reward_options_modified"] = true;
        }

        return Task.CompletedTask;
    }

    private static Task ExecuteCardRewardOptionsEnchantAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        if (context.Owner == null ||
            context.SourceModel is not RelicModel modifyingRelic ||
            !TryGetCardRewardOptions(context, out var cardRewards))
        {
            return Task.CompletedTask;
        }

        var enchantmentId = GetProperty(node, "enchantment_id");
        if (string.IsNullOrWhiteSpace(enchantmentId))
        {
            return Task.CompletedTask;
        }

        var canonicalEnchantment = ResolveEnchantmentTemplate(enchantmentId);
        if (canonicalEnchantment == null)
        {
            return Task.CompletedTask;
        }

        var amount = DynamicValueEvaluator.EvaluateRuntimeDecimal(node, "amount", context, 1m);
        var selection = GetProperty(node, "selection", "all").Trim().ToLowerInvariant();
        var validOptions = cardRewards.Where(option => canonicalEnchantment.CanEnchant(option.Card)).ToList();
        if (validOptions.Count == 0)
        {
            return Task.CompletedTask;
        }

        IEnumerable<CardCreationResult> selectedOptions = selection == "random_one"
            ? validOptions.Take(1)
            : validOptions;

        var changed = false;
        foreach (var option in selectedOptions)
        {
            var clone = context.Owner.RunState.CloneCard(option.Card);
            CardCmd.Enchant(canonicalEnchantment.ToMutable(), clone, amount);
            option.ModifyCard(clone, modifyingRelic);
            changed = true;
        }

        if (changed)
        {
            context["card_reward_options_modified"] = true;
        }

        return Task.CompletedTask;
    }

    private static string GetProperty(BehaviorGraphNodeDefinition node, string key, string defaultValue = "")
    {
        return node.Properties.TryGetValue(key, out var value) ? value : defaultValue;
    }

    private static bool GetBoolProperty(BehaviorGraphNodeDefinition node, string key, bool defaultValue = false)
    {
        return bool.TryParse(GetProperty(node, key, defaultValue.ToString()), out var result) ? result : defaultValue;
    }

    private static bool TryGetCardRewardOptions(BehaviorGraphExecutionContext context, out List<CardCreationResult> cardRewards)
    {
        if (context.TryGetState<object>("card_reward_options", out var rawValue) &&
            rawValue is List<CardCreationResult> typed)
        {
            cardRewards = typed;
            return true;
        }

        cardRewards = new List<CardCreationResult>();
        return false;
    }

    private static bool TryGetCardRewardCreationOptions(BehaviorGraphExecutionContext context, out CardCreationOptions creationOptions)
    {
        if (context.TryGetState<object>("card_reward_creation_options", out var rawValue) &&
            rawValue is CardCreationOptions typed)
        {
            creationOptions = typed;
            return true;
        }

        creationOptions = null!;
        return false;
    }

    private static EnchantmentModel? ResolveEnchantmentTemplate(string enchantmentId)
    {
        var canonical = ModelDb.DebugEnchantments.FirstOrDefault(candidate => string.Equals(candidate.Id.Entry, enchantmentId, StringComparison.OrdinalIgnoreCase));
        if (canonical != null)
        {
            return canonical;
        }

        return ModelDb.DebugEnchantments.FirstOrDefault(candidate => string.Equals(candidate.GetType().Name, enchantmentId, StringComparison.OrdinalIgnoreCase));
    }

    private static PowerModel? ResolvePowerTemplate(string powerId)
    {
        var canonical = ModelDb.AllPowers.FirstOrDefault(candidate => string.Equals(candidate.Id.Entry, powerId, StringComparison.OrdinalIgnoreCase));
        if (canonical != null)
        {
            return canonical;
        }

        canonical = ModelDb.AllPowers.FirstOrDefault(candidate => string.Equals(candidate.GetType().Name, powerId, StringComparison.OrdinalIgnoreCase));
        if (canonical != null)
        {
            return canonical;
        }

        var normalized = NormalizeLookupKey(powerId);
        return ModelDb.AllPowers.FirstOrDefault(candidate =>
            string.Equals(NormalizeLookupKey(candidate.Id.Entry), normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(NormalizeLookupKey(candidate.GetType().Name), normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeLookupKey(string rawValue)
    {
        return new string((rawValue ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
    }

    private static PileType ParsePileType(string rawValue, PileType defaultValue)
    {
        return Enum.TryParse<PileType>(rawValue, ignoreCase: true, out var pileType) ? pileType : defaultValue;
    }

    private static AbstractModel? ResolveSourceModel(BehaviorGraphExecutionContext context)
    {
        if (context.SourceModel != null) return context.SourceModel;
        if (context.Card != null) return context.Card;
        if (context.Potion != null) return context.Potion;
        if (context.Relic != null) return context.Relic;
        if (context.Event != null) return context.Event;
        if (context.Enchantment != null) return context.Enchantment;
        return null;
    }

    private static CardSelectorPrefs BuildSelectorPrefs(BehaviorGraphNodeDefinition node, int amount)
    {
        var promptKind = GetProperty(node, "prompt_kind", "generic").Trim().ToLowerInvariant();
        var prompt = promptKind switch
        {
            "discard" => CardSelectorPrefs.DiscardSelectionPrompt,
            "exhaust" => CardSelectorPrefs.ExhaustSelectionPrompt,
            "transform" => CardSelectorPrefs.TransformSelectionPrompt,
            "upgrade" => CardSelectorPrefs.UpgradeSelectionPrompt,
            "remove" => CardSelectorPrefs.RemoveSelectionPrompt,
            "enchant" => CardSelectorPrefs.EnchantSelectionPrompt,
            _ => CardSelectorPrefs.DiscardSelectionPrompt
        };

        var prefs = new CardSelectorPrefs(prompt, amount)
        {
            Cancelable = GetBoolProperty(node, "allow_cancel", false)
        };

        return prefs;
    }

    private static List<CardModel> ResolveCardsFromPileOrAll(MegaCrit.Sts2.Core.Entities.Players.Player owner, string selector)
    {
        selector = selector?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(selector) || selector.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return EnumerateCards(owner).ToList();
        }

        if (Enum.TryParse<PileType>(selector, ignoreCase: true, out var pileType))
        {
            var pile = CardPile.Get(pileType, owner);
            return pile?.Cards.ToList() ?? new List<CardModel>();
        }

        return EnumerateCards(owner).ToList();
    }

    private static bool MatchesEnergyCost(CardModel card, int exactEnergyCost, bool includeXCost)
    {
        if (card.EnergyCost.CostsX)
        {
            return includeXCost && (exactEnergyCost < 0 || exactEnergyCost == 0);
        }

        if (exactEnergyCost < 0)
        {
            return true;
        }

        return card.EnergyCost.GetWithModifiers(CostModifiers.All) == exactEnergyCost;
    }

    private static bool MatchesCardTypeScope(CardModel card, string scope)
    {
        scope = scope?.Trim().ToLowerInvariant() ?? "any";
        return scope switch
        {
            "any" => true,
            "attack" => card.Type == CardType.Attack,
            "skill" => card.Type == CardType.Skill,
            "power" => card.Type == CardType.Power,
            "status" => card.Type == CardType.Status,
            "curse" => card.Type == CardType.Curse,
            "attack_skill" => card.Type is CardType.Attack or CardType.Skill,
            "attack_power" => card.Type is CardType.Attack or CardType.Power,
            "skill_power" => card.Type is CardType.Skill or CardType.Power,
            "attack_skill_power" or "non_status" => card.Type is CardType.Attack or CardType.Skill or CardType.Power,
            _ => true
        };
    }

    private static PotionModel? ResolvePotion(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        var potionId = GetProperty(node, "potion_id");
        if (!string.IsNullOrWhiteSpace(potionId) && context.Owner != null)
        {
            return context.Owner.Potions.FirstOrDefault(candidate => string.Equals(candidate.Id.Entry, potionId, StringComparison.OrdinalIgnoreCase));
        }

        return context.Potion;
    }

    private static RelicModel? ResolveRelic(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        var relicId = GetProperty(node, "relic_id");
        if (!string.IsNullOrWhiteSpace(relicId) && context.Owner != null)
        {
            return context.Owner.Relics.FirstOrDefault(candidate => string.Equals(candidate.Id.Entry, relicId, StringComparison.OrdinalIgnoreCase));
        }

        return context.Relic;
    }

    private static List<CardModel> ResolveCards(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        var cardIds = GetCardIds(node);
        var cards = new List<CardModel>();

        if (context.Owner == null)
        {
            return cards;
        }

        var stateKey = GetProperty(node, "card_state_key");
        if (!string.IsNullOrWhiteSpace(stateKey) && context.TryGetState<object>(stateKey, out var stateValue))
        {
            switch (stateValue)
            {
                case IEnumerable<CardModel> enumerable:
                    cards.AddRange(enumerable.Where(candidate => candidate != null).Distinct());
                    if (cards.Count > 0)
                    {
                        return cards;
                    }
                    break;
                case CardModel singleCard:
                    cards.Add(singleCard);
                    return cards;
            }
        }

        if (cardIds.Count > 0)
        {
            foreach (var cardId in cardIds)
            {
                var card = EnumerateCards(context.Owner).FirstOrDefault(candidate => string.Equals(candidate.Id.Entry, cardId, StringComparison.OrdinalIgnoreCase));
                if (card != null && !cards.Contains(card))
                {
                    cards.Add(card);
                }
            }

            return cards;
        }

        if (context.Card != null)
        {
            cards.Add(context.Card);
            return cards;
        }

        if (context.CardPlay?.Card != null)
        {
            cards.Add(context.CardPlay.Card);
        }

        return cards;
    }

    private static IReadOnlyList<string> GetCardIds(BehaviorGraphNodeDefinition node)
    {
        var raw = GetProperty(node, "card_ids");
        if (string.IsNullOrWhiteSpace(raw))
        {
            var singleCardId = GetProperty(node, "card_id");
            return string.IsNullOrWhiteSpace(singleCardId) ? Array.Empty<string>() : new[] { singleCardId.Trim() };
        }

        return raw
            .Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static IEnumerable<CardModel> EnumerateCards(MegaCrit.Sts2.Core.Entities.Players.Player owner)
    {
        var piles = new[] { PileType.Draw, PileType.Hand, PileType.Discard, PileType.Exhaust, PileType.Play, PileType.Deck };
        var seen = new HashSet<CardModel>();
        foreach (var pileType in piles)
        {
            var pile = CardPile.Get(pileType, owner);
            if (pile == null)
            {
                continue;
            }

            foreach (var card in pile.Cards)
            {
                if (seen.Add(card))
                {
                    yield return card;
                }
            }
        }
    }

    private static bool GetBoolProperty(BehaviorGraphNodeDefinition node, string key)
    {
        return GetBoolProperty(node, key, false);
    }

    private static decimal ConvertToDecimal(object? value)
    {
        if (value is null)
        {
            return 0m;
        }

        return value switch
        {
            decimal decimalValue => decimalValue,
            int intValue => intValue,
            long longValue => longValue,
            float floatValue => (decimal)floatValue,
            double doubleValue => (decimal)doubleValue,
            string stringValue when decimal.TryParse(stringValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => 0m
        };
    }

    private static bool EvaluateComparison(object? left, object? right, string comparisonOperator)
    {
        if (TryConvertComparableDecimal(left, out var leftDecimal) && TryConvertComparableDecimal(right, out var rightDecimal))
        {
            return comparisonOperator switch
            {
                "eq" or "==" => leftDecimal == rightDecimal,
                "neq" or "!=" => leftDecimal != rightDecimal,
                "gt" or ">" => leftDecimal > rightDecimal,
                "gte" or ">=" => leftDecimal >= rightDecimal,
                "lt" or "<" => leftDecimal < rightDecimal,
                "lte" or "<=" => leftDecimal <= rightDecimal,
                _ => leftDecimal == rightDecimal
            };
        }

        var leftString = left?.ToString() ?? string.Empty;
        var rightString = right?.ToString() ?? string.Empty;
        return comparisonOperator switch
        {
            "neq" or "!=" => !string.Equals(leftString, rightString, StringComparison.Ordinal),
            _ => string.Equals(leftString, rightString, StringComparison.Ordinal)
        };
    }

    private static bool TryConvertComparableDecimal(object? value, out decimal result)
    {
        result = 0m;
        if (value is null)
        {
            return false;
        }

        return value switch
        {
            decimal decimalValue => SetResult(decimalValue, out result),
            int intValue => SetResult(intValue, out result),
            long longValue => SetResult(longValue, out result),
            float floatValue => SetResult((decimal)floatValue, out result),
            double doubleValue => SetResult((decimal)doubleValue, out result),
            string stringValue when decimal.TryParse(stringValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) => SetResult(parsed, out result),
            _ => false
        };
    }

    private static bool SetResult(decimal value, out decimal result)
    {
        result = value;
        return true;
    }

    private static ValueProp ParseValueProps(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue) || string.Equals(rawValue, "none", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        return Enum.TryParse<ValueProp>(rawValue.Replace("|", ",", StringComparison.Ordinal), ignoreCase: true, out var props)
            ? props
            : 0;
    }

    private sealed class DelegateBehaviorNodeExecutor : IBehaviorNodeExecutor
    {
        private readonly Func<BehaviorGraphNodeDefinition, BehaviorGraphExecutionContext, Task> _executeAsync;

        public DelegateBehaviorNodeExecutor(string nodeType, Func<BehaviorGraphNodeDefinition, BehaviorGraphExecutionContext, Task> executeAsync)
        {
            NodeType = nodeType;
            _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
        }

        public string NodeType { get; }

        public bool CanExecute(BehaviorGraphNodeDefinition node)
        {
            return node != null;
        }

        public Task ExecuteAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
        {
            return _executeAsync(node, context);
        }
    }
}
