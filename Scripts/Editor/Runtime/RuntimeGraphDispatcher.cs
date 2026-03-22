using System.Reflection;
using HarmonyLib;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.TestSupport;
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
        await Executor.ExecuteAsync(graph!, context, entryNodeId);
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
        await Executor.ExecuteAsync(graph!, context, entryNodeId);
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
        await Executor.ExecuteAsync(graph!, context, ResolveEntryNode(graph!, "enchantment.on_play"));
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

        await Executor.ExecuteAsync(graph!, context, entryNodeId);
    }

    private static BehaviorGraphExecutionContext BuildHookContext(
        AbstractModel model,
        PlayerChoiceContext? choiceContext,
        CardPlay? cardPlay,
        PotionModel? potion,
        Creature? target)
    {
        return model switch
        {
            RelicModel relic => new BehaviorGraphExecutionContext
            {
                ChoiceContext = choiceContext,
                SourceModel = relic,
                CardPlay = cardPlay,
                Card = cardPlay?.Card,
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
                Card = enchantment.Card,
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
                Card = cardPlay?.Card,
                Potion = potion,
                Target = target
            }
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
