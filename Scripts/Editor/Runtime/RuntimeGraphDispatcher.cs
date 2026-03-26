using System.Reflection;
using System.Globalization;
using HarmonyLib;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.ValueProps;
using STS2_Editor.Scripts.Editor.Core.Models;
using STS2_Editor.Scripts.Editor.Graph;

namespace STS2_Editor.Scripts.Editor.Runtime;

internal static class RuntimeGraphDispatcher
{
    private static readonly MethodInfo CardGetResultPileTypeMethod = AccessTools.Method(typeof(CardModel), "GetResultPileType")!;
    private static readonly MethodInfo CardPlayPowerCardFlyVfxMethod = AccessTools.Method(typeof(CardModel), "PlayPowerCardFlyVfx")!;
    private static readonly MethodInfo CardCurrentTargetSetter = AccessTools.PropertySetter(typeof(CardModel), nameof(CardModel.CurrentTarget))!;
    private static readonly FieldInfo CardPlayedField = AccessTools.Field(typeof(CardModel), "Played")!;
    private static readonly FieldInfo CardStarCostChangedField = AccessTools.Field(typeof(CardModel), "StarCostChanged")!;
    private static readonly FieldInfo CardTemporaryStarCostsField = AccessTools.Field(typeof(CardModel), "_temporaryStarCosts")!;
    private static readonly Dictionary<Type, MethodInfo?> CardOnPlayMethods = new();
    private static readonly Dictionary<Type, MethodInfo?> PotionOnUseMethods = new();
    private static readonly BehaviorGraphExecutor Executor = new(ModStudioBootstrap.GraphRegistry);

    public static bool ShouldUseGraphForCard(CardModel card)
    {
        return TryGetGraphOverride(ModStudioEntityKind.Card, card.Id.Entry, out _, out _);
    }

    public static bool ShouldUseGraphForPotion(PotionModel potion)
    {
        return TryGetGraphOverride(ModStudioEntityKind.Potion, potion.Id.Entry, out _, out _);
    }

    public static bool ShouldUseGraphForEnchantmentOnEnchant(EnchantmentModel enchantment)
    {
        return TryGetGraphOverride(ModStudioEntityKind.Enchantment, enchantment.Id.Entry, out _, out var graph) &&
               !string.IsNullOrWhiteSpace(ResolveEntryNode(graph!, "enchantment.on_enchant", allowDefaultFallback: false));
    }

    public static bool TryEvaluateEnchantmentModifierDecimal(
        EnchantmentModel enchantment,
        string triggerId,
        decimal baseValue,
        ValueProp props,
        out decimal result)
    {
        result = 0m;
        if (!TryGetGraphOverride(ModStudioEntityKind.Enchantment, enchantment.Id.Entry, out _, out var graph))
        {
            return false;
        }

        var entryNodeId = ResolveEntryNode(graph!, triggerId, allowDefaultFallback: false);
        if (string.IsNullOrWhiteSpace(entryNodeId))
        {
            return false;
        }

        var card = enchantment.Card;
        var owner = card.Owner;
        var context = new BehaviorGraphExecutionContext
        {
            Graph = graph,
            TriggerId = triggerId,
            SourceModel = enchantment,
            Card = card,
            Enchantment = enchantment,
            Owner = owner,
            CombatState = card.CombatState ?? owner.Creature.CombatState,
            RunState = owner.RunState,
            Target = card.CurrentTarget
        };
        context["modifier_base"] = baseValue;
        context["modifier_props"] = props.ToString();
        ExecuteGraphAndApplyStateAsync(graph!, context, entryNodeId).GetAwaiter().GetResult();
        if (!context.TryGetState<object>("modifier_result", out var rawValue))
        {
            return false;
        }

        result = rawValue switch
        {
            decimal decimalValue => decimalValue,
            int intValue => intValue,
            long longValue => longValue,
            float floatValue => (decimal)floatValue,
            double doubleValue => (decimal)doubleValue,
            string stringValue when decimal.TryParse(stringValue, out var parsed) => parsed,
            _ => 0m
        };
        return true;
    }

    public static bool TryEvaluateEnchantmentPlayCountModifier(
        EnchantmentModel enchantment,
        int originalPlayCount,
        out int result)
    {
        result = originalPlayCount;
        if (!TryGetGraphOverride(ModStudioEntityKind.Enchantment, enchantment.Id.Entry, out _, out var graph))
        {
            return false;
        }

        var entryNodeId = ResolveEntryNode(graph!, "enchantment.modify_play_count", allowDefaultFallback: false);
        if (string.IsNullOrWhiteSpace(entryNodeId))
        {
            return false;
        }

        var card = enchantment.Card;
        var owner = card.Owner;
        var context = new BehaviorGraphExecutionContext
        {
            Graph = graph,
            TriggerId = "enchantment.modify_play_count",
            SourceModel = enchantment,
            Card = card,
            Enchantment = enchantment,
            Owner = owner,
            CombatState = card.CombatState ?? owner.Creature.CombatState,
            RunState = owner.RunState,
            Target = card.CurrentTarget
        };
        context["play_count_base"] = originalPlayCount;
        ExecuteGraphAndApplyStateAsync(graph!, context, entryNodeId).GetAwaiter().GetResult();
        if (!context.TryGetState<object>("modifier_result", out var rawValue))
        {
            return false;
        }

        result = rawValue switch
        {
            int intValue => intValue,
            decimal decimalValue => (int)decimalValue,
            long longValue => (int)longValue,
            float floatValue => (int)floatValue,
            double doubleValue => (int)doubleValue,
            string stringValue when int.TryParse(stringValue, out var parsed) => parsed,
            _ => originalPlayCount
        };
        return true;
    }

    public static bool TryEvaluateRelicDecimalModifier(
        RelicModel relic,
        string triggerId,
        string baseStateKey,
        decimal baseValue,
        Action<BehaviorGraphExecutionContext>? configureContext,
        out decimal result)
    {
        result = baseValue;
        if (!TryGetGraphOverride(ModStudioEntityKind.Relic, relic.Id.Entry, out _, out var graph))
        {
            return false;
        }

        var entryNodeId = ResolveEntryNode(graph!, triggerId, allowDefaultFallback: false);
        if (string.IsNullOrWhiteSpace(entryNodeId))
        {
            return false;
        }

        var context = new BehaviorGraphExecutionContext
        {
            Graph = graph,
            TriggerId = triggerId,
            SourceModel = relic,
            Relic = relic,
            Owner = relic.Owner,
            CombatState = relic.Owner?.Creature?.CombatState,
            RunState = relic.Owner?.RunState
        };
        context["modifier_base"] = baseValue;
        context[baseStateKey] = baseValue;
        PopulateRelicStateSnapshot(context, relic);
        configureContext?.Invoke(context);
        ExecuteGraphAndApplyStateAsync(graph!, context, entryNodeId).GetAwaiter().GetResult();
        if (!context.TryGetState<object>("modifier_result", out var rawValue))
        {
            return false;
        }

        result = rawValue switch
        {
            decimal decimalValue => decimalValue,
            int intValue => intValue,
            long longValue => longValue,
            float floatValue => (decimal)floatValue,
            double doubleValue => (decimal)doubleValue,
            string stringValue when decimal.TryParse(stringValue, out var parsed) => parsed,
            _ => baseValue
        };
        return true;
    }

    public static bool TryEvaluateRelicDecimalModifier(
        RelicModel relic,
        string triggerId,
        string baseStateKey,
        decimal baseValue,
        out decimal result)
    {
        return TryEvaluateRelicDecimalModifier(relic, triggerId, baseStateKey, baseValue, configureContext: null, out result);
    }

    public static bool TryEvaluateRelicIntModifier(
        RelicModel relic,
        string triggerId,
        string baseStateKey,
        int baseValue,
        out int result)
    {
        result = baseValue;
        if (!TryGetGraphOverride(ModStudioEntityKind.Relic, relic.Id.Entry, out _, out var graph))
        {
            return false;
        }

        var entryNodeId = ResolveEntryNode(graph!, triggerId, allowDefaultFallback: false);
        if (string.IsNullOrWhiteSpace(entryNodeId))
        {
            return false;
        }

        var context = new BehaviorGraphExecutionContext
        {
            Graph = graph,
            TriggerId = triggerId,
            SourceModel = relic,
            Relic = relic,
            Owner = relic.Owner,
            CombatState = relic.Owner?.Creature?.CombatState,
            RunState = relic.Owner?.RunState
        };
        context["modifier_base"] = baseValue;
        context[baseStateKey] = baseValue;
        ExecuteGraphAndApplyStateAsync(graph!, context, entryNodeId).GetAwaiter().GetResult();
        if (!context.TryGetState<object>("modifier_result", out var rawValue))
        {
            return false;
        }

        result = rawValue switch
        {
            int intValue => intValue,
            decimal decimalValue => (int)decimalValue,
            long longValue => (int)longValue,
            float floatValue => (int)floatValue,
            double doubleValue => (int)doubleValue,
            string stringValue when int.TryParse(stringValue, out var parsed) => parsed,
            _ => baseValue
        };
        return true;
    }

    public static bool TryEvaluateRelicMaxEnergyModifier(
        RelicModel relic,
        decimal baseValue,
        out decimal result)
    {
        return TryEvaluateRelicDecimalModifier(relic, "relic.modify_max_energy", "max_energy_base", baseValue, out result);
    }

    public static bool TryApplyRelicRewardModifier(
        RelicModel relic,
        string triggerId,
        Player player,
        List<Reward> rewards,
        AbstractRoom? room,
        bool? isMimicked,
        out bool modified)
    {
        modified = false;
        if (player != relic.Owner ||
            !TryGetGraphOverride(ModStudioEntityKind.Relic, relic.Id.Entry, out _, out var graph))
        {
            return false;
        }

        var entryNodeId = ResolveEntryNode(graph!, triggerId, allowDefaultFallback: false);
        if (string.IsNullOrWhiteSpace(entryNodeId))
        {
            return false;
        }

        var context = BuildHookContext(relic, choiceContext: null, cardPlay: null, potion: null, target: null, card: null);
        context.Graph = graph;
        context.TriggerId = triggerId;
        context["reward_list"] = rewards;
        context["HookPlayer"] = player;
        context["RoomType"] = room?.RoomType.ToString() ?? string.Empty;
        context["RoomIsCombat"] = room?.RoomType.IsCombatRoom() ?? false;
        context["IsMimicked"] = isMimicked ?? false;
        context["CurrentActIndex"] = player.RunState.CurrentActIndex;
        context["IsFinalAct"] = player.RunState.CurrentActIndex >= player.RunState.Acts.Count - 1;
        PopulateRelicStateSnapshot(context, relic);

        var beforeCount = rewards.Count;
        ExecuteGraphAndApplyStateAsync(graph!, context, entryNodeId).GetAwaiter().GetResult();
        modified = rewards.Count != beforeCount || TryResolveContextBool(context, "reward_modified");
        return true;
    }

    public static bool TryApplyRelicCardRewardOptionsModifier(
        RelicModel relic,
        string triggerId,
        Player player,
        List<CardCreationResult> cardRewardOptions,
        CardCreationOptions creationOptions,
        out bool modified)
    {
        modified = false;
        if (player != relic.Owner ||
            !TryGetGraphOverride(ModStudioEntityKind.Relic, relic.Id.Entry, out _, out var graph))
        {
            return false;
        }

        var entryNodeId = ResolveEntryNode(graph!, triggerId, allowDefaultFallback: false);
        if (string.IsNullOrWhiteSpace(entryNodeId))
        {
            return false;
        }

        var context = BuildHookContext(relic, choiceContext: null, cardPlay: null, potion: null, target: null, card: null);
        context.Graph = graph;
        context.TriggerId = triggerId;
        context["HookPlayer"] = player;
        context["card_reward_options"] = cardRewardOptions;
        context["card_reward_creation_options"] = creationOptions;
        context["CurrentRoomType"] = player.RunState.CurrentRoom?.RoomType.ToString() ?? string.Empty;
        context["CurrentRoomIsCombat"] = player.RunState.CurrentRoom?.RoomType.IsCombatRoom() ?? false;
        PopulateRelicStateSnapshot(context, relic);

        ExecuteGraphAndApplyStateAsync(graph!, context, entryNodeId).GetAwaiter().GetResult();
        modified = TryResolveContextBool(context, "card_reward_options_modified");
        return true;
    }

    public static bool TryEvaluateRelicBoolHook(
        RelicModel relic,
        string triggerId,
        Player player,
        Action<BehaviorGraphExecutionContext>? configureContext,
        out bool result)
    {
        result = true;
        if (player != relic.Owner ||
            !TryGetGraphOverride(ModStudioEntityKind.Relic, relic.Id.Entry, out _, out var graph))
        {
            return false;
        }

        var entryNodeId = ResolveEntryNode(graph!, triggerId, allowDefaultFallback: false);
        if (string.IsNullOrWhiteSpace(entryNodeId))
        {
            return false;
        }

        var context = BuildHookContext(relic, choiceContext: null, cardPlay: null, potion: null, target: null, card: null);
        context.Graph = graph;
        context.TriggerId = triggerId;
        context["HookPlayer"] = player;
        PopulateRelicStateSnapshot(context, relic);
        configureContext?.Invoke(context);

        ExecuteGraphAndApplyStateAsync(graph!, context, entryNodeId).GetAwaiter().GetResult();
        if (!context.TryGetState<object>("hook_result", out var rawValue))
        {
            return false;
        }

        result = rawValue switch
        {
            bool boolValue => boolValue,
            string stringValue when bool.TryParse(stringValue, out var parsed) => parsed,
            int intValue => intValue != 0,
            decimal decimalValue => decimalValue != 0m,
            _ => true
        };
        return true;
    }

    public static bool TryApplyRelicMapModifier(
        RelicModel relic,
        string triggerId,
        IRunState runState,
        ActMap map,
        int actIndex,
        out ActMap result)
    {
        result = map;
        if (!TryGetGraphOverride(ModStudioEntityKind.Relic, relic.Id.Entry, out _, out var graph))
        {
            return false;
        }

        var entryNodeId = ResolveEntryNode(graph!, triggerId, allowDefaultFallback: false);
        if (string.IsNullOrWhiteSpace(entryNodeId))
        {
            return false;
        }

        var context = BuildHookContext(relic, choiceContext: null, cardPlay: null, potion: null, target: null, card: null);
        context.Graph = graph;
        context.TriggerId = triggerId;
        context["generated_map_input"] = map;
        context["generated_map_result"] = map;
        context["ActIndex"] = actIndex;
        context["CurrentActIndex"] = actIndex;
        PopulateRelicStateSnapshot(context, relic);

        ExecuteGraphAndApplyStateAsync(graph!, context, entryNodeId).GetAwaiter().GetResult();
        if (!context.TryGetState<object>("generated_map_result", out var rawValue) ||
            rawValue is not ActMap graphMap)
        {
            return false;
        }

        result = graphMap;
        return true;
    }

    public static bool TryApplyRelicUnknownRoomTypeModifier(
        RelicModel relic,
        IRunState runState,
        IReadOnlySet<RoomType> roomTypes,
        out IReadOnlySet<RoomType> result)
    {
        result = roomTypes;
        if (!TryGetGraphOverride(ModStudioEntityKind.Relic, relic.Id.Entry, out _, out var graph))
        {
            return false;
        }

        var entryNodeId = ResolveEntryNode(graph!, "relic.modify_unknown_map_point_room_types", allowDefaultFallback: false);
        if (string.IsNullOrWhiteSpace(entryNodeId))
        {
            return false;
        }

        var context = BuildHookContext(relic, choiceContext: null, cardPlay: null, potion: null, target: null, card: null);
        context.Graph = graph;
        context.TriggerId = "relic.modify_unknown_map_point_room_types";
        context["unknown_room_types"] = new HashSet<RoomType>(roomTypes);
        PopulateRelicStateSnapshot(context, relic);

        ExecuteGraphAndApplyStateAsync(graph!, context, entryNodeId).GetAwaiter().GetResult();
        if (!context.TryGetState<object>("unknown_room_types_result", out var rawValue) ||
            rawValue is not HashSet<RoomType> updated)
        {
            return false;
        }

        result = updated;
        return true;
    }

    public static async Task ExecuteCardPlayWrapperAsync(
        CardModel card,
        PlayerChoiceContext choiceContext,
        Creature? target,
        bool isAutoPlay,
        ResourceInfo resources,
        bool skipCardPileVisuals)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(choiceContext);

        var combatState = card.CombatState ?? card.Owner.Creature.CombatState ?? throw new InvalidOperationException($"Card '{card.Id.Entry}' is not attached to a combat state.");
        choiceContext.PushModel(card);
        try
        {
            await CombatManager.Instance.WaitForUnpause();
            SetCardCurrentTarget(card, target);

            if (!isAutoPlay)
            {
                await CardPileCmd.AddDuringManualCardPlay(card);
            }
            else
            {
                await CardPileCmd.Add(card, PileType.Play, CardPilePosition.Bottom, null, skipCardPileVisuals);
                if (!skipCardPileVisuals)
                {
                    await Cmd.CustomScaledWait(0.25f, 0.35f);
                }
            }

            var (resultPileType, resultPilePosition) = Hook.ModifyCardPlayResultPileTypeAndPosition(
                combatState,
                card,
                isAutoPlay,
                resources,
                GetCardResultPileType(card),
                CardPilePosition.Bottom,
                out IEnumerable<AbstractModel> modifiers);
            foreach (var modifier in modifiers)
            {
                await modifier.AfterModifyingCardPlayResultPileOrPosition(card, resultPileType, resultPilePosition);
            }

            var playCount = card.GetEnchantedReplayCount() + 1;
            playCount = Hook.ModifyCardPlayCount(combatState, card, playCount, target, out var modifyingModels);
            await Hook.AfterModifyingCardPlayCount(combatState, card, modifyingModels);

            var playStartTime = Time.GetTicksMsec();
            for (var playIndex = 0; playIndex < playCount; playIndex++)
            {
                if (card.Type == CardType.Power)
                {
                    await PlayPowerCardFlyVfxAsync(card);
                }
                else if (playIndex > 0)
                {
                    var cardNode = NCard.FindOnTable(card);
                    if (cardNode != null)
                    {
                        await cardNode.AnimMultiCardPlay();
                    }
                }

                var cardPlay = new CardPlay
                {
                    Card = card,
                    Target = target,
                    ResultPile = resultPileType,
                    Resources = resources,
                    IsAutoPlay = isAutoPlay,
                    PlayIndex = playIndex,
                    PlayCount = playCount
                };

                await Hook.BeforeCardPlayed(combatState, cardPlay);
                CombatManager.Instance.History.CardPlayStarted(combatState, cardPlay);
                await ExecuteCardOnPlayAsync(card, choiceContext, cardPlay);
                card.InvokeExecutionFinished();

                if (card.Enchantment != null)
                {
                    await ExecuteEnchantmentOnPlayAsync(card.Enchantment, choiceContext, cardPlay);
                    card.Enchantment.InvokeExecutionFinished();
                }

                if (card.Affliction != null)
                {
                    var affliction = card.Affliction;
                    await affliction.OnPlay(choiceContext, target);
                    affliction.InvokeExecutionFinished();
                }

                CombatManager.Instance.History.CardPlayFinished(combatState, cardPlay);
                if (CombatManager.Instance.IsInProgress)
                {
                    await Hook.AfterCardPlayed(combatState, choiceContext, cardPlay);
                }
            }

            if (!skipCardPileVisuals)
            {
                var elapsedSeconds = (float)(Time.GetTicksMsec() - playStartTime) / 1000f;
                await Cmd.CustomScaledWait(0.15f - elapsedSeconds, 0.3f - elapsedSeconds);
            }

            if (card.Pile?.Type == PileType.Play)
            {
                switch (resultPileType)
                {
                    case PileType.None:
                        await CardPileCmd.RemoveFromCombat(new[] { card }, true);
                        break;
                    case PileType.Exhaust:
                        await CardCmd.Exhaust(choiceContext, card, false, skipCardPileVisuals);
                        break;
                    default:
                        await CardPileCmd.Add(card, resultPileType, resultPilePosition, null, skipCardPileVisuals);
                        break;
                }
            }

            await CombatManager.Instance.CheckForEmptyHand(choiceContext, card.Owner);
            if (card.EnergyCost.AfterCardPlayedCleanup())
            {
                card.InvokeEnergyCostChanged();
            }

            CleanupTemporaryStarCosts(card);
            InvokeCardEvent(CardPlayedField, card);
        }
        finally
        {
            SetCardCurrentTarget(card, null);
            if (choiceContext.LastInvolvedModel == card)
            {
                choiceContext.PopModel(card);
            }
        }
    }

    public static async Task ExecutePotionOnUseWrapperAsync(PotionModel potion, PlayerChoiceContext choiceContext, Creature? target)
    {
        ArgumentNullException.ThrowIfNull(potion);
        ArgumentNullException.ThrowIfNull(choiceContext);

        potion.RemoveBeforeUse();
        var combatState = potion.Owner.Creature.CombatState;
        choiceContext.PushModel(potion);
        try
        {
            await CombatManager.Instance.WaitForUnpause();
            await Hook.BeforePotionUsed(potion.Owner.RunState, combatState, potion, target);
            if (TestMode.IsOff && combatState != null)
            {
                var combatRoom = NCombatRoom.Instance;
                if (combatRoom == null)
                {
                    Log.Warn("Mod Studio graph potion execution could not find NCombatRoom. Falling back to logic without throw VFX.");
                }

                var creatureNode = combatRoom?.GetCreatureNode(potion.Owner.Creature);
                var targetPosition = Vector2.Zero;
                if (potion.TargetType.IsSingleTarget())
                {
                    var targetNode = combatRoom?.GetCreatureNode(target);
                    if (targetNode != null)
                    {
                        targetPosition = targetNode.GetBottomOfHitbox();
                    }
                }
                else
                {
                    var affectedCreatures = (potion.TargetType != TargetType.AllEnemies
                        ? combatState.GetCreaturesOnSide(CombatSide.Player)
                        : combatState.GetCreaturesOnSide(CombatSide.Enemy))
                        .Where(creature => creature.IsHittable)
                        .ToList();
                    foreach (var creature in affectedCreatures)
                    {
                        var targetNode = combatRoom?.GetCreatureNode(creature);
                        if (targetNode != null)
                        {
                            targetPosition += targetNode.VfxSpawnPosition;
                        }
                    }

                    if (affectedCreatures.Count > 0)
                    {
                        targetPosition /= affectedCreatures.Count;
                    }
                }

                if (creatureNode != null && combatRoom != null)
                {
                    var throwVfx = NItemThrowVfx.Create(creatureNode.VfxSpawnPosition, targetPosition, potion.Image);
                    combatRoom.CombatVfxContainer.AddChildSafely(throwVfx);
                    await Cmd.Wait(0.5f);
                }
            }

            await ExecutePotionOnUseAsync(potion, choiceContext, target);
            potion.InvokeExecutionFinished();

            if (combatState != null && CombatManager.Instance.IsInProgress)
            {
                CombatManager.Instance.History.PotionUsed(combatState, potion, target);
            }

            await Hook.AfterPotionUsed(potion.Owner.RunState, combatState, potion, target);
            potion.Owner.RunState.CurrentMapPointHistoryEntry?.GetEntry(potion.Owner.NetId).PotionUsed.Add(potion.Id);
            await CombatManager.Instance.CheckForEmptyHand(choiceContext, potion.Owner);
        }
        finally
        {
            if (choiceContext.LastInvolvedModel == potion)
            {
                choiceContext.PopModel(potion);
            }
        }
    }

    public static Task ExecuteBeforeCardPlayedHookAsync(AbstractModel model, CardPlay cardPlay)
    {
        return ExecuteHookAsync(
            model,
            "before_card_played",
            BuildHookContext(model, choiceContext: null, cardPlay, potion: null, target: cardPlay.Target),
            () => model.BeforeCardPlayed(cardPlay));
    }

    public static Task ExecuteBeforeAttackHookAsync(AbstractModel model, AttackCommand attackCommand)
    {
        var context = BuildHookContext(model, choiceContext: null, cardPlay: null, potion: null, target: null, card: null);
        context["attack_command"] = attackCommand;
        return ExecuteHookAsync(
            model,
            "before_attack",
            context,
            () => model.BeforeAttack(attackCommand));
    }

    public static Task ExecuteAfterAttackHookAsync(AbstractModel model, AttackCommand attackCommand)
    {
        var context = BuildHookContext(model, choiceContext: null, cardPlay: null, potion: null, target: null, card: null);
        context["attack_command"] = attackCommand;
        return ExecuteHookAsync(
            model,
            "after_attack",
            context,
            () => model.AfterAttack(attackCommand));
    }

    public static Task ExecuteAfterActEnteredHookAsync(AbstractModel model)
    {
        return ExecuteHookAsync(
            model,
            "after_act_entered",
            BuildHookContext(model, choiceContext: null, cardPlay: null, potion: null, target: null, card: null),
            () => model.AfterActEntered());
    }

    public static Task ExecuteAfterCardPlayedHookAsync(AbstractModel model, PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        return ExecuteHookAsync(
            model,
            "after_card_played",
            BuildHookContext(model, choiceContext, cardPlay, potion: null, target: cardPlay.Target),
            () => model.AfterCardPlayed(choiceContext, cardPlay));
    }

    public static Task ExecuteAfterCardPlayedLateHookAsync(AbstractModel model, PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        return ExecuteHookAsync(
            model,
            "after_card_played_late",
            BuildHookContext(model, choiceContext, cardPlay, potion: null, target: cardPlay.Target),
            () => model.AfterCardPlayedLate(choiceContext, cardPlay));
    }

    public static Task ExecuteBeforePotionUsedHookAsync(AbstractModel model, PotionModel potion, Creature? target)
    {
        return ExecuteHookAsync(
            model,
            "before_potion_used",
            BuildHookContext(model, choiceContext: null, cardPlay: null, potion, target),
            () => model.BeforePotionUsed(potion, target));
    }

    public static Task ExecuteAfterPotionUsedHookAsync(AbstractModel model, PotionModel potion, Creature? target)
    {
        return ExecuteHookAsync(
            model,
            "after_potion_used",
            BuildHookContext(model, choiceContext: null, cardPlay: null, potion, target),
            () => model.AfterPotionUsed(potion, target));
    }

    private static async Task ExecuteCardOnPlayAsync(CardModel card, PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!TryGetGraphOverride(ModStudioEntityKind.Card, card.Id.Entry, out _, out var graph))
        {
            await InvokeProtectedTaskAsync(GetOrCreateCardOnPlayMethod(card.GetType()), card, choiceContext, cardPlay);
            return;
        }

        var entryNodeId = ResolveEntryNode(graph!, "card.on_play");
        var context = new BehaviorGraphExecutionContext
        {
            Graph = graph,
            TriggerId = "card.on_play",
            ChoiceContext = choiceContext,
            SourceModel = card,
            Card = card,
            CardPlay = cardPlay,
            Owner = card.Owner,
            CombatState = card.CombatState ?? card.Owner.Creature.CombatState,
            RunState = card.Owner.RunState,
            Target = cardPlay.Target
        };
        await ExecuteGraphAndApplyStateAsync(graph!, context, entryNodeId);
    }

    private static async Task ExecutePotionOnUseAsync(PotionModel potion, PlayerChoiceContext choiceContext, Creature? target)
    {
        if (!TryGetGraphOverride(ModStudioEntityKind.Potion, potion.Id.Entry, out _, out var graph))
        {
            await InvokeProtectedTaskAsync(GetOrCreatePotionOnUseMethod(potion.GetType()), potion, choiceContext, target);
            return;
        }

        var entryNodeId = ResolveEntryNode(graph!, "potion.on_use");
        var context = new BehaviorGraphExecutionContext
        {
            Graph = graph,
            TriggerId = "potion.on_use",
            ChoiceContext = choiceContext,
            SourceModel = potion,
            Potion = potion,
            Owner = potion.Owner,
            CombatState = potion.Owner.Creature.CombatState,
            RunState = potion.Owner.RunState,
            Target = target
        };
        await ExecuteGraphAndApplyStateAsync(graph!, context, entryNodeId);
    }

    private static async Task ExecuteEnchantmentOnPlayAsync(EnchantmentModel enchantment, PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!TryGetGraphOverride(ModStudioEntityKind.Enchantment, enchantment.Id.Entry, out _, out var graph))
        {
            await enchantment.OnPlay(choiceContext, cardPlay);
            return;
        }

        var owner = enchantment.Card.Owner;
        var context = new BehaviorGraphExecutionContext
        {
            Graph = graph,
            TriggerId = "enchantment.on_play",
            ChoiceContext = choiceContext,
            SourceModel = enchantment,
            Card = enchantment.Card,
            CardPlay = cardPlay,
            Enchantment = enchantment,
            Owner = owner,
            CombatState = enchantment.Card.CombatState ?? owner.Creature.CombatState,
            RunState = owner.RunState,
            Target = cardPlay.Target
        };
        await ExecuteGraphAndApplyStateAsync(graph!, context, ResolveEntryNode(graph!, "enchantment.on_play"));
    }

    public static async Task ExecuteEnchantmentOnEnchantAsync(EnchantmentModel enchantment)
    {
        if (!TryGetGraphOverride(ModStudioEntityKind.Enchantment, enchantment.Id.Entry, out _, out var graph))
        {
            return;
        }

        var entryNodeId = ResolveEntryNode(graph!, "enchantment.on_enchant", allowDefaultFallback: false);
        if (string.IsNullOrWhiteSpace(entryNodeId))
        {
            return;
        }

        var card = enchantment.Card;
        var owner = card.Owner;
        var context = new BehaviorGraphExecutionContext
        {
            Graph = graph,
            TriggerId = "enchantment.on_enchant",
            SourceModel = enchantment,
            Card = card,
            Enchantment = enchantment,
            Owner = owner,
            CombatState = card.CombatState ?? owner.Creature.CombatState,
            RunState = owner.RunState,
            Target = card.CurrentTarget
        };
        await ExecuteGraphAndApplyStateAsync(graph!, context, entryNodeId);
    }

    public static Task ExecuteAfterCardDrawnHookAsync(AbstractModel model, PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        return ExecuteHookAsync(
            model,
            "after_card_drawn",
            BuildHookContext(model, choiceContext, cardPlay: null, potion: null, target: card.CurrentTarget, card),
            () => model.AfterCardDrawn(choiceContext, card, fromHandDraw));
    }

    public static Task ExecuteAfterCardEnteredCombatHookAsync(AbstractModel model, CardModel card)
    {
        var context = BuildHookContext(model, choiceContext: null, cardPlay: null, potion: null, target: card.CurrentTarget, card);
        context["EnteredCardOwnerIsOwner"] = model is RelicModel relic && card.Owner == relic.Owner;
        context["EnteredCardIsColorless"] = card.VisualCardPool?.IsColorless ?? false;
        return ExecuteHookAsync(
            model,
            "after_card_entered_combat",
            context,
            () => model.AfterCardEnteredCombat(card));
    }

    public static Task ExecuteAfterCardChangedPilesHookAsync(AbstractModel model, CardModel card, PileType oldPileType, AbstractModel? source)
    {
        var context = BuildHookContext(model, choiceContext: null, cardPlay: null, potion: null, target: card.CurrentTarget, card);
        context["old_pile_type"] = oldPileType.ToString();
        if (source != null)
        {
            context["hook_source_model"] = source;
            context["hook_source_model_id"] = source.Id.Entry;
        }

        return ExecuteHookAsync(
            model,
            "after_card_changed_piles",
            context,
            () => model.AfterCardChangedPiles(card, oldPileType, source));
    }

    public static Task ExecuteAfterCardChangedPilesLateHookAsync(AbstractModel model, CardModel card, PileType oldPileType, AbstractModel? source)
    {
        var context = BuildHookContext(model, choiceContext: null, cardPlay: null, potion: null, target: card.CurrentTarget, card);
        context["old_pile_type"] = oldPileType.ToString();
        if (source != null)
        {
            context["hook_source_model"] = source;
            context["hook_source_model_id"] = source.Id.Entry;
        }

        return ExecuteHookAsync(
            model,
            "after_card_changed_piles_late",
            context,
            () => model.AfterCardChangedPilesLate(card, oldPileType, source));
    }

    public static Task ExecuteAfterCardExhaustedHookAsync(AbstractModel model, PlayerChoiceContext choiceContext, CardModel card, bool causedByEthereal)
    {
        var context = BuildHookContext(model, choiceContext, cardPlay: null, potion: null, target: card.CurrentTarget, card);
        context["caused_by_ethereal"] = causedByEthereal.ToString();
        context["ExhaustedCausedByEthereal"] = causedByEthereal;
        context["CardOwnerIsOwner"] = model is RelicModel relic && card.Owner == relic.Owner;
        return ExecuteHookAsync(
            model,
            "after_card_exhausted",
            context,
            () => model.AfterCardExhausted(choiceContext, card, causedByEthereal));
    }

    public static Task ExecuteAfterDamageReceivedHookAsync(
        AbstractModel model,
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        var context = BuildHookContext(model, choiceContext, cardPlay: null, potion: null, target, cardSource);
        context["damage_result"] = result;
        context["damage_props"] = props.ToString();
        if (dealer != null)
        {
            context["damage_dealer"] = dealer;
            context["damage_dealer_id"] = dealer.ModelId.Entry;
        }

        return ExecuteHookAsync(
            model,
            "after_damage_received",
            context,
            () => model.AfterDamageReceived(choiceContext, target, result, props, dealer, cardSource));
    }

    public static Task ExecuteAfterDamageReceivedLateHookAsync(
        AbstractModel model,
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        var context = BuildHookContext(model, choiceContext, cardPlay: null, potion: null, target, cardSource);
        context["damage_result"] = result;
        context["damage_props"] = props.ToString();
        if (dealer != null)
        {
            context["damage_dealer"] = dealer;
            context["damage_dealer_id"] = dealer.ModelId.Entry;
        }

        return ExecuteHookAsync(
            model,
            "after_damage_received_late",
            context,
            () => model.AfterDamageReceivedLate(choiceContext, target, result, props, dealer, cardSource));
    }

    public static Task ExecuteAfterDamageGivenHookAsync(
        AbstractModel model,
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        var context = BuildHookContext(model, choiceContext, cardPlay: null, potion: null, target, cardSource);
        if (model is RelicModel relic)
        {
            context["DealerIsOwnerOrPetOwner"] = dealer == relic.Owner?.Creature || dealer?.PetOwner == relic.Owner;
        }
        context["TargetIsPlayer"] = target.IsPlayer;
        context["BlockWasBroken"] = result.WasBlockBroken;
        return ExecuteHookAsync(
            model,
            "after_damage_given",
            context,
            () => model.AfterDamageGiven(choiceContext, dealer, result, props, target, cardSource));
    }

    public static Task ExecuteAfterGoldGainedHookAsync(AbstractModel model, Player player)
    {
        var context = BuildHookContext(model, choiceContext: null, cardPlay: null, potion: null, target: null, card: null);
        context["player_net_id"] = player.NetId.ToString();
        return ExecuteHookAsync(
            model,
            "after_gold_gained",
            context,
            () => model.AfterGoldGained(player));
    }

    public static Task ExecuteAfterCurrentHpChangedHookAsync(AbstractModel model, Creature creature, decimal delta)
    {
        var context = BuildHookContext(model, choiceContext: null, cardPlay: null, potion: null, target: creature, card: null);
        context["Delta"] = delta;
        context["CurrentHp"] = creature.CurrentHp;
        context["MaxHp"] = creature.MaxHp;
        context["IsInCombat"] = CombatManager.Instance.IsInProgress;
        if (model is RelicModel relic)
        {
            context["TargetIsOwner"] = creature == relic.Owner?.Creature;
            if (relic.DynamicVars.TryGetValue("HpThreshold", out var threshold))
            {
                context["CurrentHpAboveThreshold"] = creature.CurrentHp > creature.MaxHp * (threshold.BaseValue / 100m);
            }
        }

        return ExecuteHookAsync(
            model,
            "after_current_hp_changed",
            context,
            () => model.AfterCurrentHpChanged(creature, delta));
    }

    public static Task ExecuteAfterPotionDiscardedHookAsync(AbstractModel model, PotionModel potion)
    {
        var context = BuildHookContext(model, choiceContext: null, cardPlay: null, potion, target: null, card: null);
        context["IsInCombat"] = CombatManager.Instance.IsInProgress;
        if (model is RelicModel relic)
        {
            context["OwnerPotionCount"] = relic.Owner?.Potions.Count() ?? 0;
        }

        return ExecuteHookAsync(
            model,
            "after_potion_discarded",
            context,
            () => model.AfterPotionDiscarded(potion));
    }

    public static Task ExecuteAfterPotionProcuredHookAsync(AbstractModel model, PotionModel potion)
    {
        var context = BuildHookContext(model, choiceContext: null, cardPlay: null, potion, target: null, card: null);
        context["IsInCombat"] = CombatManager.Instance.IsInProgress;
        if (model is RelicModel relic)
        {
            context["OwnerPotionCount"] = relic.Owner?.Potions.Count() ?? 0;
        }

        return ExecuteHookAsync(
            model,
            "after_potion_procured",
            context,
            () => model.AfterPotionProcured(potion));
    }

    public static Task ExecuteAfterOrbChanneledHookAsync(AbstractModel model, PlayerChoiceContext choiceContext, Player player, OrbModel orb)
    {
        var context = BuildHookContext(model, choiceContext, cardPlay: null, potion: null, target: player.Creature, card: null);
        context["HookPlayerIsOwner"] = model is RelicModel relic && player == relic.Owner;
        context["hook_orb_model"] = orb;
        return ExecuteHookAsync(
            model,
            "after_orb_channeled",
            context,
            () => model.AfterOrbChanneled(choiceContext, player, orb));
    }

    public static Task ExecuteAfterBlockClearedHookAsync(AbstractModel model, Creature creature)
    {
        if (model is RelicModel relic && creature != relic.Owner?.Creature)
        {
            return model.AfterBlockCleared(creature);
        }

        var context = BuildHookContext(model, choiceContext: null, cardPlay: null, potion: null, target: creature, card: null);
        context["CombatRound"] = creature.CombatState?.RoundNumber ?? 0;
        return ExecuteHookAsync(
            model,
            "after_block_cleared",
            context,
            () => model.AfterBlockCleared(creature));
    }

    public static Task ExecuteAfterRestSiteHealHookAsync(AbstractModel model, Player player, bool isMimicked)
    {
        if (model is RelicModel relic && player != relic.Owner)
        {
            return model.AfterRestSiteHeal(player, isMimicked);
        }

        var context = BuildHookContext(model, choiceContext: null, cardPlay: null, potion: null, target: player.Creature, card: null);
        context["IsMimicked"] = isMimicked;
        return ExecuteHookAsync(
            model,
            "after_rest_site_heal",
            context,
            () => model.AfterRestSiteHeal(player, isMimicked));
    }

    public static Task ExecuteAfterDiedToDoomHookAsync(AbstractModel model, HookPlayerChoiceContext choiceContext, IReadOnlyList<Creature> creatures)
    {
        var context = BuildHookContext(model, choiceContext, cardPlay: null, potion: null, target: null, card: null);
        if (model is RelicModel relic)
        {
            var doomedDeaths = creatures.Count(creature => creature != relic.Owner?.Creature);
            context["DoomDeathCount"] = doomedDeaths;
            if (relic.DynamicVars.TryGetValue("Heal", out var healVar))
            {
                context["DoomHealAmount"] = healVar.BaseValue * doomedDeaths;
            }
            else
            {
                context["DoomHealAmount"] = doomedDeaths;
            }
        }

        return ExecuteHookAsync(
            model,
            "after_died_to_doom",
            context,
            () => choiceContext.AssignTaskAndWaitForPauseOrCompletion(model.AfterDiedToDoom(choiceContext, creatures)));
    }

    public static Task ExecuteAfterDeathHookAsync(AbstractModel model, HookPlayerChoiceContext choiceContext, Creature creature, bool wasRemovalPrevented, float deathAnimLength)
    {
        var context = BuildHookContext(model, choiceContext, cardPlay: null, potion: null, target: creature, card: null);
        if (model is RelicModel relic)
        {
            context["DeathTargetIsEnemy"] = creature.Side != relic.Owner?.Creature?.Side;
        }
        context["WasRemovalPrevented"] = wasRemovalPrevented;
        context["DeathAnimLength"] = deathAnimLength;
        return ExecuteHookAsync(
            model,
            "after_death",
            context,
            () => choiceContext.AssignTaskAndWaitForPauseOrCompletion(model.AfterDeath(choiceContext, creature, wasRemovalPrevented, deathAnimLength)));
    }

    public static Task ExecuteAfterPreventingBlockClearHookAsync(AbstractModel preventer, Creature creature)
    {
        var context = BuildHookContext(preventer, choiceContext: null, cardPlay: null, potion: null, target: creature, card: null);
        if (preventer is RelicModel relic)
        {
            context["PreventerIsSelf"] = true;
            context["CurrentBlock"] = creature.Block;
            context["BlockAboveCap"] = Math.Max(0, creature.Block - 10);
            context["CreatureIsOwner"] = creature == relic.Owner?.Creature;
        }

        return ExecuteHookAsync(
            preventer,
            "after_preventing_block_clear",
            context,
            () => preventer.AfterPreventingBlockClear(preventer, creature));
    }

    public static Task ExecuteAfterPreventingDeathHookAsync(AbstractModel preventer, Creature creature)
    {
        var context = BuildHookContext(preventer, choiceContext: null, cardPlay: null, potion: null, target: creature, card: null);
        if (preventer is RelicModel relic)
        {
            context["TargetIsOwner"] = creature == relic.Owner?.Creature;
            if (relic.DynamicVars.TryGetValue("Heal", out var healVar))
            {
                context["ReviveHealAmount"] = Math.Max(1m, creature.MaxHp * (healVar.BaseValue / 100m));
            }
        }

        return ExecuteHookAsync(
            preventer,
            "after_preventing_death",
            context,
            () => preventer.AfterPreventingDeath(creature));
    }

    public static Task ExecuteAfterStarsSpentHookAsync(AbstractModel model, int amount, Player spender)
    {
        var context = BuildHookContext(model, choiceContext: null, cardPlay: null, potion: null, target: spender.Creature, card: null);
        context["HookPlayerIsOwner"] = model is RelicModel relic && spender == relic.Owner;
        context["StarsSpentAmount"] = amount;
        return ExecuteHookAsync(
            model,
            "after_stars_spent",
            context,
            () => model.AfterStarsSpent(amount, spender));
    }

    public static Task ExecuteBeforeTurnEndVeryEarlyHookAsync(AbstractModel model, HookPlayerChoiceContext choiceContext, CombatSide side)
    {
        var context = BuildBeforeTurnEndContext(model, choiceContext, side);
        return ExecuteHookAsync(
            model,
            "before_turn_end_very_early",
            context,
            () => choiceContext.AssignTaskAndWaitForPauseOrCompletion(model.BeforeTurnEndVeryEarly(choiceContext, side)));
    }

    public static Task ExecuteBeforeTurnEndEarlyHookAsync(AbstractModel model, HookPlayerChoiceContext choiceContext, CombatSide side)
    {
        var context = BuildBeforeTurnEndContext(model, choiceContext, side);
        return ExecuteHookAsync(
            model,
            "before_turn_end_early",
            context,
            () => choiceContext.AssignTaskAndWaitForPauseOrCompletion(model.BeforeTurnEndEarly(choiceContext, side)));
    }

    public static Task ExecuteBeforeTurnEndHookAsync(AbstractModel model, HookPlayerChoiceContext choiceContext, CombatSide side)
    {
        var context = BuildBeforeTurnEndContext(model, choiceContext, side);
        return ExecuteHookAsync(
            model,
            "before_turn_end",
            context,
            () => choiceContext.AssignTaskAndWaitForPauseOrCompletion(model.BeforeTurnEnd(choiceContext, side)));
    }

    public static Task ExecuteAfterCardDiscardedHookAsync(AbstractModel model, PlayerChoiceContext choiceContext, CardModel card)
    {
        var target = model is RelicModel { Owner: not null } relic
            ? relic.Owner.RunState.Rng.CombatTargets.NextItem(relic.Owner.Creature.CombatState.HittableEnemies)
            : null;
        var context = BuildHookContext(model, choiceContext, cardPlay: null, potion: null, target, card);
        context["DiscardingOwnerIsOwner"] = model is RelicModel cardRelic && card.Owner == cardRelic.Owner;
        context["IsOwnerCurrentSide"] = model is RelicModel sideRelic && sideRelic.Owner?.Creature?.Side == sideRelic.Owner?.Creature?.CombatState?.CurrentSide;
        return ExecuteHookAsync(
            model,
            "after_card_discarded",
            context,
            () => model.AfterCardDiscarded(choiceContext, card));
    }

    public static Task ExecuteAfterShuffleHookAsync(AbstractModel model, PlayerChoiceContext choiceContext, Player shuffler)
    {
        var context = BuildHookContext(model, choiceContext, cardPlay: null, potion: null, target: shuffler.Creature, card: null);
        context["HookPlayerIsOwner"] = model is RelicModel relic && shuffler == relic.Owner;
        return ExecuteHookAsync(
            model,
            "after_shuffle",
            context,
            () => model.AfterShuffle(choiceContext, shuffler));
    }

    public static Task ExecuteAfterHandEmptiedHookAsync(AbstractModel model, PlayerChoiceContext choiceContext, Player player)
    {
        var context = BuildHookContext(model, choiceContext, cardPlay: null, potion: null, target: player.Creature, card: null);
        context["HookPlayerIsOwner"] = model is RelicModel relic && player == relic.Owner;
        context["IsPlayPhase"] = CombatManager.Instance.IsPlayPhase;
        return ExecuteHookAsync(
            model,
            "after_hand_emptied",
            context,
            () => model.AfterHandEmptied(choiceContext, player));
    }

    public static Task ExecuteAfterPlayerTurnStartHookAsync(AbstractModel model, PlayerChoiceContext choiceContext, Player player)
    {
        return ExecuteHookAsync(
            model,
            "after_player_turn_start",
            BuildHookContext(model, choiceContext, cardPlay: null, potion: null, target: null, card: null),
            () => model.AfterPlayerTurnStart(choiceContext, player));
    }

    public static Task ExecuteAfterPlayerTurnStartEarlyHookAsync(AbstractModel model, PlayerChoiceContext choiceContext, Player player)
    {
        return ExecuteHookAsync(
            model,
            "after_player_turn_start_early",
            BuildHookContext(model, choiceContext, cardPlay: null, potion: null, target: null, card: null),
            () => model.AfterPlayerTurnStartEarly(choiceContext, player));
    }

    public static Task ExecuteAfterPlayerTurnStartLateHookAsync(AbstractModel model, PlayerChoiceContext choiceContext, Player player)
    {
        return ExecuteHookAsync(
            model,
            "after_player_turn_start_late",
            BuildHookContext(model, choiceContext, cardPlay: null, potion: null, target: null, card: null),
            () => model.AfterPlayerTurnStartLate(choiceContext, player));
    }

    public static Task ExecuteBeforeCombatStartHookAsync(AbstractModel model)
    {
        return ExecuteHookAsync(
            model,
            "before_combat_start",
            BuildHookContext(model, choiceContext: null, cardPlay: null, potion: null, target: null, card: null),
            () => model.BeforeCombatStart());
    }

    public static Task ExecuteBeforeCombatStartLateHookAsync(AbstractModel model)
    {
        return ExecuteHookAsync(
            model,
            "before_combat_start_late",
            BuildHookContext(model, choiceContext: null, cardPlay: null, potion: null, target: null, card: null),
            () => model.BeforeCombatStartLate());
    }

    public static async Task ExecuteRelicAfterObtainedAsync(RelicModel relic)
    {
        if (!TryGetGraphOverride(ModStudioEntityKind.Relic, relic.Id.Entry, out _, out var graph))
        {
            await relic.AfterObtained();
            return;
        }

        var entryNodeId = ResolveEntryNode(graph!, "relic.after_obtained", allowDefaultFallback: false);
        if (string.IsNullOrWhiteSpace(entryNodeId))
        {
            await relic.AfterObtained();
            return;
        }

        var context = new BehaviorGraphExecutionContext
        {
            Graph = graph,
            TriggerId = "relic.after_obtained",
            SourceModel = relic,
            Relic = relic,
            Owner = relic.Owner,
            RunState = relic.Owner?.RunState,
            CombatState = relic.Owner?.Creature?.CombatState
        };
        context["IsInCombat"] = CombatManager.Instance.IsInProgress;
        context["OwnerPotionCount"] = relic.Owner?.Potions.Count() ?? 0;
        PopulateRelicStateSnapshot(context, relic);
        await ExecuteGraphAndApplyStateAsync(graph!, context, entryNodeId);
    }

    public static Task ExecuteAfterCombatEndHookAsync(AbstractModel model, CombatRoom room)
    {
        var context = BuildHookContext(model, choiceContext: null, cardPlay: null, potion: null, target: null, card: null);
        context["combat_room"] = room;
        context["combat_room_type"] = room.RoomType.ToString();
        return ExecuteHookAsync(
            model,
            "after_combat_end",
            context,
            () => model.AfterCombatEnd(room));
    }

    public static Task ExecuteAfterCombatVictoryEarlyHookAsync(AbstractModel model, CombatRoom room)
    {
        var context = BuildHookContext(model, choiceContext: null, cardPlay: null, potion: null, target: null, card: null);
        context["combat_room"] = room;
        context["combat_room_type"] = room.RoomType.ToString();
        return ExecuteHookAsync(
            model,
            "after_combat_victory_early",
            context,
            () => model.AfterCombatVictoryEarly(room));
    }

    public static Task ExecuteAfterCombatVictoryHookAsync(AbstractModel model, CombatRoom room)
    {
        var context = BuildHookContext(model, choiceContext: null, cardPlay: null, potion: null, target: null, card: null);
        context["combat_room"] = room;
        context["combat_room_type"] = room.RoomType.ToString();
        return ExecuteHookAsync(
            model,
            "after_combat_victory",
            context,
            () => model.AfterCombatVictory(room));
    }

    public static Task ExecuteAfterRoomEnteredHookAsync(AbstractModel model, AbstractRoom room)
    {
        var context = BuildHookContext(model, choiceContext: null, cardPlay: null, potion: null, target: null, card: null);
        context["room"] = room;
        context["room_type"] = room.RoomType.ToString();
        return ExecuteHookAsync(
            model,
            "after_room_entered",
            context,
            () => model.AfterRoomEntered(room));
    }

    public static Task ExecuteBeforeHandDrawHookAsync(AbstractModel model, Player player, PlayerChoiceContext choiceContext, CombatState combatState)
    {
        var context = BuildHookContext(model, choiceContext, cardPlay: null, potion: null, target: null, card: null);
        context["player_net_id"] = player.NetId.ToString();
        context["hook_combat_state"] = combatState;
        context["HookPlayerIsOwner"] = model is RelicModel relic && player == relic.Owner;
        context["CombatRound"] = combatState.RoundNumber;
        return ExecuteHookAsync(
            model,
            "before_hand_draw",
            context,
            () => model.BeforeHandDraw(player, choiceContext, combatState));
    }

    public static Task ExecuteBeforeHandDrawLateHookAsync(AbstractModel model, Player player, PlayerChoiceContext choiceContext, CombatState combatState)
    {
        var context = BuildHookContext(model, choiceContext, cardPlay: null, potion: null, target: null, card: null);
        context["player_net_id"] = player.NetId.ToString();
        context["hook_combat_state"] = combatState;
        context["HookPlayerIsOwner"] = model is RelicModel relic && player == relic.Owner;
        context["CombatRound"] = combatState.RoundNumber;
        return ExecuteHookAsync(
            model,
            "before_hand_draw_late",
            context,
            () => model.BeforeHandDrawLate(player, choiceContext, combatState));
    }

    public static Task ExecuteAfterEnergyResetHookAsync(AbstractModel model, Player player)
    {
        var context = BuildHookContext(model, choiceContext: null, cardPlay: null, potion: null, target: null, card: null);
        context["player_net_id"] = player.NetId.ToString();
        return ExecuteHookAsync(
            model,
            "after_energy_reset",
            context,
            () => model.AfterEnergyReset(player));
    }

    public static Task ExecuteAfterEnergyResetLateHookAsync(AbstractModel model, Player player)
    {
        var context = BuildHookContext(model, choiceContext: null, cardPlay: null, potion: null, target: null, card: null);
        context["player_net_id"] = player.NetId.ToString();
        return ExecuteHookAsync(
            model,
            "after_energy_reset_late",
            context,
            () => model.AfterEnergyResetLate(player));
    }

    public static Task ExecuteAfterSideTurnStartHookAsync(AbstractModel model, CombatSide side, CombatState combatState)
    {
        var context = BuildHookContext(model, choiceContext: null, cardPlay: null, potion: null, target: null, card: null);
        context["combat_side"] = side.ToString();
        context["hook_combat_state"] = combatState;
        return ExecuteHookAsync(
            model,
            "after_side_turn_start",
            context,
            () => model.AfterSideTurnStart(side, combatState));
    }

    public static Task ExecuteBeforeSideTurnStartHookAsync(AbstractModel model, PlayerChoiceContext choiceContext, CombatSide side, CombatState combatState)
    {
        var context = BuildHookContext(model, choiceContext, cardPlay: null, potion: null, target: null, card: null);
        context["combat_side"] = side.ToString();
        context["hook_combat_state"] = combatState;
        return ExecuteHookAsync(
            model,
            "before_side_turn_start",
            context,
            () => model.BeforeSideTurnStart(choiceContext, side, combatState));
    }

    public static Task ExecuteBeforePlayPhaseStartHookAsync(AbstractModel model, PlayerChoiceContext choiceContext, Player player)
    {
        var context = BuildHookContext(model, choiceContext, cardPlay: null, potion: null, target: null, card: null);
        context["player_net_id"] = player.NetId.ToString();
        return ExecuteHookAsync(
            model,
            "before_play_phase_start",
            context,
            () => model.BeforePlayPhaseStart(choiceContext, player));
    }

    public static Task ExecuteAfterTurnEndHookAsync(AbstractModel model, PlayerChoiceContext choiceContext, CombatSide side)
    {
        var context = BuildHookContext(model, choiceContext, cardPlay: null, potion: null, target: null, card: null);
        context["combat_side"] = side.ToString();
        return ExecuteHookAsync(
            model,
            "after_turn_end",
            context,
            () => model.AfterTurnEnd(choiceContext, side));
    }

    public static Task ExecuteAfterTurnEndLateHookAsync(AbstractModel model, PlayerChoiceContext choiceContext, CombatSide side)
    {
        var context = BuildHookContext(model, choiceContext, cardPlay: null, potion: null, target: null, card: null);
        context["combat_side"] = side.ToString();
        return ExecuteHookAsync(
            model,
            "after_turn_end_late",
            context,
            () => model.AfterTurnEndLate(choiceContext, side));
    }

    public static Task ExecuteBeforeFlushHookAsync(AbstractModel model, PlayerChoiceContext choiceContext, Player player)
    {
        return ExecuteHookAsync(
            model,
            "before_flush",
            BuildHookContext(model, choiceContext, cardPlay: null, potion: null, target: null, card: null),
            () => model.BeforeFlush(choiceContext, player));
    }

    public static Task ExecuteBeforeFlushLateHookAsync(AbstractModel model, PlayerChoiceContext choiceContext, Player player)
    {
        return ExecuteHookAsync(
            model,
            "before_flush_late",
            BuildHookContext(model, choiceContext, cardPlay: null, potion: null, target: null, card: null),
            () => model.BeforeFlushLate(choiceContext, player));
    }

    private static async Task ExecuteHookAsync(
        AbstractModel model,
        string triggerId,
        BehaviorGraphExecutionContext context,
        Func<Task> fallback)
    {
        if (!TryGetHookModelKind(model, out var kind))
        {
            await fallback();
            return;
        }

        if (!TryGetGraphOverride(kind, model.Id.Entry, out _, out var graph))
        {
            await fallback();
            return;
        }

        context.Graph = graph;
        context.TriggerId = $"{kind.ToString().ToLowerInvariant()}.{triggerId}";
        var entryNodeId = ResolveEntryNode(graph!, context.TriggerId, allowDefaultFallback: false);
        if (string.IsNullOrWhiteSpace(entryNodeId))
        {
            await fallback();
            return;
        }

        await ExecuteGraphAndApplyStateAsync(graph!, context, entryNodeId);
    }

    private static BehaviorGraphExecutionContext BuildHookContext(
        AbstractModel model,
        PlayerChoiceContext? choiceContext,
        CardPlay? cardPlay,
        PotionModel? potion,
        Creature? target,
        CardModel? card = null)
    {
        return model switch
        {
            RelicModel relic => new BehaviorGraphExecutionContext
            {
                ChoiceContext = choiceContext,
                SourceModel = relic,
                CardPlay = cardPlay,
                Card = card ?? cardPlay?.Card,
                Potion = potion,
                Relic = relic,
                Owner = relic.Owner,
                CombatState = relic.Owner?.Creature?.CombatState,
                RunState = relic.Owner?.RunState,
                Target = target
            },
            EnchantmentModel enchantment => new BehaviorGraphExecutionContext
            {
                ChoiceContext = choiceContext,
                SourceModel = enchantment,
                CardPlay = cardPlay,
                Card = card ?? enchantment.Card,
                Potion = potion,
                Enchantment = enchantment,
                Owner = enchantment.Card.Owner,
                CombatState = enchantment.Card.CombatState ?? enchantment.Card.Owner.Creature.CombatState,
                RunState = enchantment.Card.Owner.RunState,
                Target = target
            },
            _ => new BehaviorGraphExecutionContext
            {
                ChoiceContext = choiceContext,
                SourceModel = model,
                CardPlay = cardPlay,
                Card = card ?? cardPlay?.Card,
                Potion = potion,
                Target = target
            }
        };
    }

    private static BehaviorGraphExecutionContext BuildBeforeTurnEndContext(AbstractModel model, HookPlayerChoiceContext choiceContext, CombatSide side)
    {
        var context = BuildHookContext(model, choiceContext, cardPlay: null, potion: null, target: null, card: null);
        context["TurnSide"] = side.ToString();
        if (model is RelicModel relic && relic.Owner != null)
        {
            context["IsOwnerTurnSide"] = side == relic.Owner.Creature.Side;
            context["OwnerSide"] = relic.Owner.Creature.Side.ToString();
            context["HandCount"] = PileType.Hand.GetPile(relic.Owner).Cards.Count;
            context["CurrentBlock"] = relic.Owner.Creature.Block;
            context["CombatRound"] = relic.Owner.Creature.CombatState?.RoundNumber ?? 0;
            context["PlayedAttackThisTurn"] = CombatManager.Instance.History.CardPlaysFinished.Any(entry =>
                entry.HappenedThisTurn(relic.Owner.Creature.CombatState) &&
                entry.CardPlay.Card.Type == CardType.Attack &&
                entry.CardPlay.Card.Owner == relic.Owner);
        }

        return context;
    }

    private static void PopulateRelicStateSnapshot(BehaviorGraphExecutionContext context, RelicModel relic)
    {
        context["Status"] = relic.Status.ToString();
        context["IsUsedUp"] = relic.IsUsedUp;
        context["DisplayAmount"] = relic.DisplayAmount;

        foreach (var pair in relic.DynamicVars)
        {
            context[pair.Key] = pair.Value.BaseValue;
        }

        var properties = relic.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
        foreach (var property in properties)
        {
            if (!property.CanRead || property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            object? value;
            try
            {
                value = property.GetValue(relic);
            }
            catch
            {
                continue;
            }

            switch (value)
            {
                case null:
                    continue;
                case bool or int or long or decimal or double or float or string:
                    context[property.Name] = value;
                    break;
                case Enum enumValue:
                    context[property.Name] = enumValue.ToString();
                    break;
            }
        }

        var nonPublicProperties = relic.GetType().GetProperties(BindingFlags.Instance | BindingFlags.NonPublic);
        foreach (var property in nonPublicProperties)
        {
            if (!property.CanRead || property.GetIndexParameters().Length > 0 || context.State.ContainsKey(property.Name))
            {
                continue;
            }

            object? value;
            try
            {
                value = property.GetValue(relic);
            }
            catch
            {
                continue;
            }

            switch (value)
            {
                case null:
                    continue;
                case bool or int or long or decimal or double or float or string:
                    context[property.Name] = value;
                    break;
                case Enum enumValue:
                    context[property.Name] = enumValue.ToString();
                    break;
            }
        }

        var fields = relic.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        foreach (var field in fields)
        {
            if (context.State.ContainsKey(field.Name))
            {
                continue;
            }

            object? value;
            try
            {
                value = field.GetValue(relic);
            }
            catch
            {
                continue;
            }

            switch (value)
            {
                case null:
                    continue;
                case bool or int or long or decimal or double or float or string:
                    context[field.Name] = value;
                    break;
                case Enum enumValue:
                    context[field.Name] = enumValue.ToString();
                    break;
            }
        }
    }

    private static async Task ExecuteGraphAndApplyStateAsync(
        BehaviorGraphDefinition graph,
        BehaviorGraphExecutionContext context,
        string entryNodeId)
    {
        await Executor.ExecuteAsync(graph, context, entryNodeId);
        if (context.SourceModel != null)
        {
            ApplyModelStateWriteBack(context.SourceModel, context);
        }
    }

    private static void ApplyModelStateWriteBack(AbstractModel model, BehaviorGraphExecutionContext context)
    {
        foreach (var pair in context.State)
        {
            if (!TryWriteProperty(model, pair.Key, pair.Value))
            {
                TryWriteField(model, pair.Key, pair.Value);
            }
        }
    }

    private static bool TryWriteProperty(AbstractModel model, string key, object? rawValue)
    {
        var property = model.GetType().GetProperty(key, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property == null || !property.CanWrite || property.GetIndexParameters().Length > 0)
        {
            return false;
        }

        if (!TryConvertMemberValue(rawValue, property.PropertyType, out var converted))
        {
            return false;
        }

        try
        {
            var current = property.CanRead ? property.GetValue(model) : null;
            if (Equals(current, converted))
            {
                return true;
            }

            property.SetValue(model, converted);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryWriteField(AbstractModel model, string key, object? rawValue)
    {
        var field = model.GetType().GetField(key, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field == null || field.IsInitOnly)
        {
            return false;
        }

        if (!TryConvertMemberValue(rawValue, field.FieldType, out var converted))
        {
            return false;
        }

        try
        {
            if (Equals(field.GetValue(model), converted))
            {
                return true;
            }

            field.SetValue(model, converted);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryConvertMemberValue(object? rawValue, Type targetType, out object? converted)
    {
        converted = null;
        if (rawValue == null)
        {
            return !targetType.IsValueType || Nullable.GetUnderlyingType(targetType) != null;
        }

        if (targetType.IsInstanceOfType(rawValue))
        {
            converted = rawValue;
            return true;
        }

        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        try
        {
            if (underlyingType.IsEnum)
            {
                if (rawValue is string stringValue && Enum.TryParse(underlyingType, stringValue, ignoreCase: true, out var enumValue))
                {
                    converted = enumValue;
                    return true;
                }

                if (rawValue is IConvertible)
                {
                    converted = Enum.ToObject(underlyingType, rawValue);
                    return true;
                }

                return false;
            }

            if (rawValue is string decimalString && underlyingType == typeof(decimal) &&
                decimal.TryParse(decimalString, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedDecimal))
            {
                converted = parsedDecimal;
                return true;
            }

            if (rawValue is IConvertible)
            {
                converted = Convert.ChangeType(rawValue, underlyingType, CultureInfo.InvariantCulture);
                return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static bool TryResolveContextBool(BehaviorGraphExecutionContext context, string key)
    {
        return context.TryGetState<object>(key, out var rawValue) && rawValue switch
        {
            bool boolValue => boolValue,
            string stringValue when bool.TryParse(stringValue, out var parsed) => parsed,
            int intValue => intValue != 0,
            decimal decimalValue => decimalValue != 0m,
            _ => false
        };
    }

    private static bool TryGetHookModelKind(AbstractModel model, out ModStudioEntityKind kind)
    {
        kind = model switch
        {
            RelicModel => ModStudioEntityKind.Relic,
            EnchantmentModel => ModStudioEntityKind.Enchantment,
            _ => default
        };
        return model is RelicModel or EnchantmentModel;
    }

    private static bool TryGetGraphOverride(
        ModStudioEntityKind kind,
        string entityId,
        out EntityOverrideEnvelope? envelope,
        out BehaviorGraphDefinition? graph)
    {
        envelope = null;
        graph = null;
        if (!ModStudioBootstrap.RuntimeRegistry.TryGetOverride(kind, entityId, out envelope) ||
            envelope == null ||
            envelope.BehaviorSource != BehaviorSource.Graph ||
            string.IsNullOrWhiteSpace(envelope.GraphId))
        {
            return false;
        }

        return ModStudioBootstrap.RuntimeRegistry.LastResolution.Graphs.TryGetValue(envelope.GraphId, out graph);
    }

    private static string ResolveEntryNode(BehaviorGraphDefinition graph, string triggerId, bool allowDefaultFallback = true)
    {
        if (graph.Metadata.TryGetValue($"trigger.{triggerId}", out var mappedEntryNodeId) && !string.IsNullOrWhiteSpace(mappedEntryNodeId))
        {
            return mappedEntryNodeId;
        }

        if (graph.Metadata.TryGetValue(triggerId, out mappedEntryNodeId) && !string.IsNullOrWhiteSpace(mappedEntryNodeId))
        {
            return mappedEntryNodeId;
        }

        if (graph.Metadata.TryGetValue("trigger.default", out mappedEntryNodeId) && !string.IsNullOrWhiteSpace(mappedEntryNodeId))
        {
            return mappedEntryNodeId;
        }

        return allowDefaultFallback ? graph.EntryNodeId : string.Empty;
    }

    private static MethodInfo? GetOrCreateCardOnPlayMethod(Type cardType)
    {
        lock (CardOnPlayMethods)
        {
            if (!CardOnPlayMethods.TryGetValue(cardType, out var method))
            {
                method = AccessTools.Method(cardType, "OnPlay", new[] { typeof(PlayerChoiceContext), typeof(CardPlay) });
                CardOnPlayMethods[cardType] = method;
            }

            return method;
        }
    }

    private static MethodInfo? GetOrCreatePotionOnUseMethod(Type potionType)
    {
        lock (PotionOnUseMethods)
        {
            if (!PotionOnUseMethods.TryGetValue(potionType, out var method))
            {
                method = AccessTools.Method(potionType, "OnUse", new[] { typeof(PlayerChoiceContext), typeof(Creature) });
                PotionOnUseMethods[potionType] = method;
            }

            return method;
        }
    }

    private static async Task InvokeProtectedTaskAsync(MethodInfo? method, object instance, params object?[] args)
    {
        if (method == null)
        {
            return;
        }

        var result = method.Invoke(instance, args);
        if (result is Task task)
        {
            await task;
        }
    }

    private static void SetCardCurrentTarget(CardModel card, Creature? target)
    {
        CardCurrentTargetSetter.Invoke(card, new object?[] { target });
    }

    private static PileType GetCardResultPileType(CardModel card)
    {
        return CardGetResultPileTypeMethod.Invoke(card, Array.Empty<object>()) is PileType pileType
            ? pileType
            : PileType.None;
    }

    private static async Task PlayPowerCardFlyVfxAsync(CardModel card)
    {
        if (CardPlayPowerCardFlyVfxMethod.Invoke(card, Array.Empty<object>()) is Task task)
        {
            await task;
        }
    }

    private static void CleanupTemporaryStarCosts(CardModel card)
    {
        if (CardTemporaryStarCostsField.GetValue(card) is not List<TemporaryCardCost> temporaryCosts)
        {
            return;
        }

        if (temporaryCosts.RemoveAll(cost => cost.ClearsWhenCardIsPlayed) > 0)
        {
            InvokeCardEvent(CardStarCostChangedField, card);
        }
    }

    private static void InvokeCardEvent(FieldInfo field, CardModel card)
    {
        if (field.GetValue(card) is Action action)
        {
            action.Invoke();
        }
    }
}
