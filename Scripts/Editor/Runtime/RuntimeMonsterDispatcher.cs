using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using STS2_Editor.Scripts.Editor.Core.Models;
using STS2_Editor.Scripts.Editor.Graph;

namespace STS2_Editor.Scripts.Editor.Runtime;

internal static class RuntimeMonsterDispatcher
{
    private static readonly ConditionalWeakTable<MonsterModel, MonsterRuntimeState> RuntimeStates = new();
    private static readonly MethodInfo NextMoveSetter = AccessTools.PropertySetter(typeof(MonsterModel), nameof(MonsterModel.NextMove))!;
    private static readonly FieldInfo MoveStateMachineField = AccessTools.Field(typeof(MonsterModel), "_moveStateMachine")!;
    private static readonly FieldInfo SpawnedThisTurnField = AccessTools.Field(typeof(MonsterModel), "_spawnedThisTurn")!;
    private static readonly FieldInfo IsPerformingMoveField = AccessTools.Field(typeof(MonsterModel), "_isPerformingMove")!;

    public static bool ShouldUseOverride(MonsterModel? monster)
    {
        return monster != null &&
               RuntimeGraphDispatcher.TryGetMonsterOverride(monster.Id.Entry, out _, out var monsterAi) &&
               monsterAi != null;
    }

    public static void ExecuteSetUp(MonsterModel monster)
    {
        if (!RuntimeGraphDispatcher.TryGetMonsterOverride(monster.Id.Entry, out _, out var monsterAi) ||
            monsterAi == null)
        {
            return;
        }

        RuntimeStates.Remove(monster);
        var runtimeState = new MonsterRuntimeState(monster, monsterAi);
        RuntimeStates.Add(monster, runtimeState);

        var placeholder = new MoveState("AUTHORED_MONSTER_PENDING", NoOpMove, new UnknownIntent());
        MoveStateMachineField.SetValue(monster, new MonsterMoveStateMachine(new MonsterState[] { placeholder }, placeholder));
        NextMoveSetter.Invoke(monster, new object[] { placeholder });
        SpawnedThisTurnField.SetValue(monster, true);
    }

    public static void ExecuteRollMove(MonsterModel monster, IEnumerable<Creature> targets)
    {
        if (!TryGetState(monster, out var runtimeState))
        {
            return;
        }

        if (string.Equals(monster.NextMove.Id, MonsterModel.stunnedMoveId, StringComparison.Ordinal))
        {
            if (!monster.NextMove.CanTransitionAway)
            {
                return;
            }

            var resumeTurnId = !string.IsNullOrWhiteSpace(monster.NextMove.FollowUpStateId)
                ? monster.NextMove.FollowUpStateId
                : runtimeState.ResumeTurnId;
            if (!string.IsNullOrWhiteSpace(resumeTurnId))
            {
                ForceTransition(monster, runtimeState, resumeTurnId);
                runtimeState.ResumeTurnId = string.Empty;
                return;
            }
        }

        var stateMachine = new MonsterGraphStateMachine(runtimeState.Definition);
        if (runtimeState.HasPendingTurn)
        {
            var pendingTurn = runtimeState.GetPendingTurn();
            if (pendingTurn != null)
            {
                var pendingIntents = stateMachine.BuildIntents(pendingTurn);
                var pendingMove = new MoveState(pendingTurn.TurnId, NoOpMove, pendingIntents.ToArray())
                {
                    MustPerformOnceBeforeTransitioning = pendingTurn.MustPerformOnceBeforeTransitioning
                };

                NextMoveSetter.Invoke(monster, new object[] { pendingMove });
                return;
            }

            runtimeState.PendingTurnId = string.Empty;
        }

        var turn = stateMachine.SelectNextTurn(monster, runtimeState);
        var intents = stateMachine.BuildIntents(turn);
        var nextMove = new MoveState(turn?.TurnId ?? "AUTHORED_MONSTER_PENDING", NoOpMove, intents.ToArray())
        {
            MustPerformOnceBeforeTransitioning = turn?.MustPerformOnceBeforeTransitioning ?? false
        };

        NextMoveSetter.Invoke(monster, new object[] { nextMove });
    }

    public static async Task ExecutePerformMoveAsync(MonsterModel monster)
    {
        if (!TryGetState(monster, out var runtimeState))
        {
            return;
        }

        if (string.Equals(monster.NextMove.Id, MonsterModel.stunnedMoveId, StringComparison.Ordinal))
        {
            var stunTargets = monster.CombatState.PlayerCreatures.Where(creature => creature.IsAlive).ToList();
            await monster.NextMove.PerformMove(stunTargets);
            if (!string.IsNullOrWhiteSpace(runtimeState.ResumeTurnId))
            {
                ForceTransition(monster, runtimeState, runtimeState.ResumeTurnId);
                runtimeState.ResumeTurnId = string.Empty;
            }
            return;
        }

        var turn = runtimeState.GetPendingTurn();
        if (turn == null)
        {
            ExecuteRollMove(monster, monster.CombatState.PlayerCreatures);
            turn = runtimeState.GetPendingTurn();
            if (turn == null)
            {
                return;
            }
        }

        var combatState = monster.CombatState;
        await Cmd.CustomScaledWait(0.1f, 0.2f);
        IsPerformingMoveField.SetValue(monster, true);

        var targets = combatState.PlayerCreatures.Where(creature => creature.IsAlive).ToList();
        var target = targets.FirstOrDefault();
        Log.Info($"Monster {monster.Id.Entry} performing authored turn {turn.TurnId}");
        RuntimeMonsterProofHarness.LogMove(monster.Id.Entry, turn.TurnId);

        foreach (var move in turn.Moves)
        {
            if (!RuntimeGraphDispatcher.TryGetResolvedGraph(move.GraphId, out var graph) || graph == null)
            {
                continue;
            }

            var context = CreateExecutionContext(monster, runtimeState, target);
            context.Graph = graph;
            context.TriggerId = "monster.move";
            context["monster_turn_id"] = turn.TurnId;
            context["monster_move_id"] = move.MoveId;
            context["monster_move_name"] = move.DisplayName;
            foreach (var pair in runtimeState.Variables)
            {
                context[pair.Key] = pair.Value;
            }

            var entryNodeId = RuntimeGraphDispatcher.ResolveGraphEntryNode(graph, "monster.move");
            await RuntimeGraphDispatcher.ExecuteResolvedGraphAsync(graph, context, entryNodeId);
            SyncVariablesFromContext(runtimeState, context);
        }

        runtimeState.RecordTurnPerformed(turn);
        CombatManager.Instance.History.MonsterPerformedMove(combatState, monster, monster.NextMove, targets);

        IsPerformingMoveField.SetValue(monster, false);
        if (monster.Creature.IsDead && Hook.ShouldCreatureBeRemovedFromCombatAfterDeath(combatState, monster.Creature))
        {
            combatState.RemoveCreature(monster.Creature);
        }

        await Cmd.CustomScaledWait(0.1f, 0.4f);
    }

    public static async Task ExecuteAfterCreatureAddedAsync(Creature creature)
    {
        if (!creature.IsEnemy || creature.Monster == null || !TryGetState(creature.Monster, out var runtimeState))
        {
            return;
        }
        MonsterLifecycleManager.Attach(creature.Monster, runtimeState);
        await MonsterLifecycleManager.ExecuteAfterAddedAsync(creature.Monster, runtimeState);
    }

    public static async Task ExecuteBeforeRemovedAsync(Creature creature)
    {
        if (!creature.IsEnemy || creature.Monster == null || !TryGetState(creature.Monster, out var runtimeState))
        {
            return;
        }

        await MonsterLifecycleManager.ExecuteBeforeRemovedAsync(creature.Monster, runtimeState);
        MonsterLifecycleManager.Detach(creature.Monster);
    }

    public static async Task ExecuteBeforeDeathAsync(Creature creature)
    {
        if (!creature.IsEnemy || creature.Monster == null || !TryGetState(creature.Monster, out var runtimeState))
        {
            return;
        }

        await MonsterLifecycleManager.ExecuteBeforeDeathAsync(creature);
    }

    public static async Task ExecuteAfterCurrentHpChangedAsync(Creature creature, decimal delta)
    {
        await MonsterLifecycleManager.ExecuteAfterCurrentHpChangedAsync(creature, delta);
    }

    public static void ExecuteOnDieToDoom(MonsterModel monster)
    {
        if (!TryGetState(monster, out var runtimeState))
        {
            return;
        }

        MonsterLifecycleManager.ExecuteOnDieToDoomAsync(monster, runtimeState).GetAwaiter().GetResult();
    }

    internal static void ForceTransition(MonsterModel monster, MonsterRuntimeState runtimeState, string targetTurnId)
    {
        var turn = runtimeState.FindTurn(targetTurnId);
        if (turn == null)
        {
            return;
        }

        runtimeState.PendingTurnId = turn.TurnId;
        RuntimeMonsterProofHarness.LogForceTransition(monster.Id.Entry, turn.TurnId);
        var intents = new MonsterGraphStateMachine(runtimeState.Definition).BuildIntents(turn);
        var nextMove = new MoveState(turn.TurnId, NoOpMove, intents.ToArray())
        {
            MustPerformOnceBeforeTransitioning = turn.MustPerformOnceBeforeTransitioning
        };
        NextMoveSetter.Invoke(monster, new object[] { nextMove });
    }

    internal static void ForcePhaseTransition(MonsterModel monster, MonsterRuntimeState runtimeState, string targetPhaseId)
    {
        runtimeState.CurrentPhaseId = targetPhaseId;
        runtimeState.PendingTurnId = string.Empty;
        RuntimeMonsterProofHarness.LogForcePhase(monster.Id.Entry, targetPhaseId);
        var nextTurn = new MonsterGraphStateMachine(runtimeState.Definition).SelectNextTurn(monster, runtimeState);
        if (nextTurn != null)
        {
            ForceTransition(monster, runtimeState, nextTurn.TurnId);
        }
    }

    internal static string ResolveResumeTurnId(MonsterModel monster)
    {
        return TryGetState(monster, out var runtimeState)
            ? runtimeState.PendingTurnId ?? runtimeState.ResumeTurnId ?? runtimeState.TurnHistory.LastOrDefault() ?? string.Empty
            : string.Empty;
    }

    internal static bool TryGetStateForPatch(MonsterModel monster, out MonsterRuntimeState runtimeState)
    {
        return TryGetState(monster, out runtimeState);
    }

    internal static bool TryGetRuntimeState(MonsterModel monster, out MonsterRuntimeState runtimeState)
    {
        return TryGetState(monster, out runtimeState);
    }

    private static bool TryGetState(MonsterModel monster, out MonsterRuntimeState runtimeState)
    {
        runtimeState = null!;
        return RuntimeStates.TryGetValue(monster, out runtimeState);
    }

    private static BehaviorGraphExecutionContext CreateExecutionContext(
        MonsterModel monster,
        MonsterRuntimeState runtimeState,
        Creature? target)
    {
        return new BehaviorGraphExecutionContext
        {
            ChoiceContext = new BlockingPlayerChoiceContext(),
            SourceModel = monster,
            Monster = monster,
            MonsterCreature = monster.Creature,
            MonsterState = runtimeState,
            CombatState = monster.CombatState,
            RunState = monster.CombatState.RunState,
            Target = target
        };
    }

    internal static void SyncVariablesFromContext(MonsterRuntimeState runtimeState, BehaviorGraphExecutionContext context)
    {
        foreach (var variable in runtimeState.Definition.StateVariables)
        {
            if (string.IsNullOrWhiteSpace(variable.Name))
            {
                continue;
            }

            if (context.State.TryGetValue(variable.Name, out var rawValue))
            {
                runtimeState.Variables[variable.Name] = NormalizeVariableValue(variable.Type, rawValue);
            }
        }
    }

    private static object? NormalizeVariableValue(MonsterStateVariableType type, object? rawValue)
    {
        return type switch
        {
            MonsterStateVariableType.Boolean => rawValue switch
            {
                bool boolValue => boolValue,
                string stringValue when bool.TryParse(stringValue, out var parsed) => parsed,
                _ => false
            },
            MonsterStateVariableType.Integer => rawValue switch
            {
                int intValue => intValue,
                decimal decimalValue => (int)decimalValue,
                string stringValue when int.TryParse(stringValue, out var parsed) => parsed,
                _ => 0
            },
            MonsterStateVariableType.Float => rawValue switch
            {
                decimal decimalValue => decimalValue,
                int intValue => intValue,
                string stringValue when decimal.TryParse(stringValue, out var parsed) => parsed,
                _ => 0m
            },
            MonsterStateVariableType.String => rawValue?.ToString() ?? string.Empty,
            _ => rawValue
        };
    }

    private static Task NoOpMove(IReadOnlyList<Creature> _)
    {
        return Task.CompletedTask;
    }
}
