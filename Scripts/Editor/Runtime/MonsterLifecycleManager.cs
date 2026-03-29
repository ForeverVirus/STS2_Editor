using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using STS2_Editor.Scripts.Editor.Core.Models;
using STS2_Editor.Scripts.Editor.Graph;

namespace STS2_Editor.Scripts.Editor.Runtime;

internal static class MonsterLifecycleManager
{
    public static void Attach(MonsterModel monster, MonsterRuntimeState runtimeState)
    {
        _ = monster;
        _ = runtimeState;
    }

    public static void Detach(MonsterModel monster)
    {
        _ = monster;
    }

    public static Task ExecuteAfterAddedAsync(MonsterModel monster, MonsterRuntimeState runtimeState)
    {
        return ExecuteHooksAsync(monster, runtimeState, MonsterLifecycleHookType.AfterAddedToRoom, "monster.after_added_to_room", monster.CombatState.PlayerCreatures.FirstOrDefault());
    }

    public static Task ExecuteBeforeRemovedAsync(MonsterModel monster, MonsterRuntimeState runtimeState)
    {
        return ExecuteHooksAsync(monster, runtimeState, MonsterLifecycleHookType.BeforeRemovedFromRoom, "monster.before_removed_from_room", monster.CombatState.PlayerCreatures.FirstOrDefault());
    }

    public static Task ExecuteOnDieToDoomAsync(MonsterModel monster, MonsterRuntimeState runtimeState)
    {
        return ExecuteHooksAsync(monster, runtimeState, MonsterLifecycleHookType.OnDieToDoom, "monster.on_die_to_doom", monster.CombatState.PlayerCreatures.FirstOrDefault());
    }

    public static Task ExecuteBeforeDeathAsync(Creature creature)
    {
        if (creature.Monster == null || !RuntimeMonsterDispatcher.TryGetRuntimeState(creature.Monster, out var runtimeState))
        {
            return Task.CompletedTask;
        }

        return ExecuteHooksAsync(creature.Monster, runtimeState, MonsterLifecycleHookType.BeforeDeath, "monster.before_death", creature);
    }

    public static async Task ExecuteAfterCurrentHpChangedAsync(Creature creature, decimal delta)
    {
        if (creature.Monster != null && RuntimeMonsterDispatcher.TryGetRuntimeState(creature.Monster, out var runtimeState))
        {
            await ExecuteHooksAsync(creature.Monster, runtimeState, MonsterLifecycleHookType.AfterCurrentHpChanged, "monster.after_current_hp_changed", creature);
        }

        if (creature.CombatState == null)
        {
            return;
        }

        foreach (var ally in creature.CombatState.Creatures.Where(candidate => candidate.Monster != null && candidate.IsAlive))
        {
            if (!RuntimeMonsterDispatcher.TryGetRuntimeState(ally.Monster!, out var allyState))
            {
                continue;
            }

            foreach (var trigger in allyState.Definition.EventTriggers.Where(trigger => trigger.EventKind == MonsterEventTriggerKind.HpChanged))
            {
                await ExecuteEventTriggerAsync(ally.Monster!, allyState, trigger, creature);
            }
        }
    }

    public static async Task ExecuteAllyDiedAsync(Creature deceased)
    {
        if (deceased.CombatState == null)
        {
            return;
        }

        foreach (var ally in deceased.CombatState.Creatures.Where(candidate => candidate.Monster != null && candidate != deceased))
        {
            if (!RuntimeMonsterDispatcher.TryGetRuntimeState(ally.Monster!, out var runtimeState))
            {
                continue;
            }

            foreach (var trigger in runtimeState.Definition.EventTriggers.Where(trigger => trigger.EventKind == MonsterEventTriggerKind.AllyDied))
            {
                if (!MatchesMonsterFilter(deceased, trigger.FilterMonsterId))
                {
                    continue;
                }

                await ExecuteEventTriggerAsync(ally.Monster!, runtimeState, trigger, deceased);
            }
        }
    }

    private static async Task ExecuteEventTriggerAsync(
        MonsterModel monster,
        MonsterRuntimeState runtimeState,
        MonsterEventTriggerDefinition trigger,
        Creature eventCreature)
    {
        if (!string.IsNullOrWhiteSpace(trigger.TargetPhaseId))
        {
            RuntimeMonsterDispatcher.ForcePhaseTransition(monster, runtimeState, trigger.TargetPhaseId);
        }

        if (string.IsNullOrWhiteSpace(trigger.GraphId) ||
            !RuntimeGraphDispatcher.TryGetResolvedGraph(trigger.GraphId, out var graph) ||
            graph == null)
        {
            return;
        }

        var context = BuildContext(monster, runtimeState, graph, "monster.event_trigger", eventCreature);
        context["event_creature"] = eventCreature;
        context["event_monster_id"] = eventCreature.Monster?.Id.Entry ?? string.Empty;
        var entryNodeId = RuntimeGraphDispatcher.ResolveGraphEntryNode(graph, "monster.event_trigger");
        await RuntimeGraphDispatcher.ExecuteResolvedGraphAsync(graph, context, entryNodeId);
        RuntimeMonsterDispatcher.SyncVariablesFromContext(runtimeState, context);
    }

    private static async Task ExecuteHooksAsync(
        MonsterModel monster,
        MonsterRuntimeState runtimeState,
        MonsterLifecycleHookType hookType,
        string triggerId,
        Creature? target)
    {
        foreach (var hook in runtimeState.Definition.LifecycleHooks.Where(candidate => candidate.HookType == hookType))
        {
            if (!RuntimeGraphDispatcher.TryGetResolvedGraph(hook.GraphId, out var graph) || graph == null)
            {
                continue;
            }

            var context = BuildContext(monster, runtimeState, graph, triggerId, target);
            var entryNodeId = RuntimeGraphDispatcher.ResolveGraphEntryNode(graph, triggerId);
            await RuntimeGraphDispatcher.ExecuteResolvedGraphAsync(graph, context, entryNodeId);
            RuntimeMonsterDispatcher.SyncVariablesFromContext(runtimeState, context);
        }
    }

    private static BehaviorGraphExecutionContext BuildContext(
        MonsterModel monster,
        MonsterRuntimeState runtimeState,
        BehaviorGraphDefinition graph,
        string triggerId,
        Creature? target)
    {
        var context = new BehaviorGraphExecutionContext
        {
            ChoiceContext = new MegaCrit.Sts2.Core.GameActions.Multiplayer.BlockingPlayerChoiceContext(),
            Graph = graph,
            TriggerId = triggerId,
            SourceModel = monster,
            Monster = monster,
            MonsterCreature = monster.Creature,
            MonsterState = runtimeState,
            CombatState = monster.CombatState,
            RunState = monster.CombatState.RunState,
            Target = target
        };

        foreach (var pair in runtimeState.Variables)
        {
            context[pair.Key] = pair.Value;
        }

        return context;
    }

    private static bool MatchesMonsterFilter(Creature creature, string filterMonsterId)
    {
        if (string.IsNullOrWhiteSpace(filterMonsterId))
        {
            return true;
        }

        return creature.Monster != null &&
               (string.Equals(creature.Monster.Id.Entry, filterMonsterId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(creature.Monster.GetType().Name, filterMonsterId, StringComparison.OrdinalIgnoreCase));
    }
}
