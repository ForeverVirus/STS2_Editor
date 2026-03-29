using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Models.Monsters;
using STS2_Editor.Scripts.Editor.Core.Models;

namespace STS2_Editor.Scripts.Editor.Graph;

public sealed class NativeMonsterAiImportResult
{
    public MonsterAiDefinition Definition { get; init; } = new();

    public IReadOnlyList<BehaviorGraphDefinition> MoveGraphs { get; init; } = Array.Empty<BehaviorGraphDefinition>();

    public bool IsPartial { get; init; }

    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();

    public string Summary { get; init; } = string.Empty;
}

public sealed class NativeMonsterAiImporter
{
    private static readonly FieldInfo InitialStateField = AccessTools.Field(typeof(MonsterMoveStateMachine), "_initialState")!;
    private static readonly PropertyInfo ConditionalStatesProperty = AccessTools.Property(typeof(ConditionalBranchState), "States")!;
    private static readonly FieldInfo ConditionalIdField = AccessTools.Field(ConditionalStatesProperty.PropertyType.GenericTypeArguments[0], "id")!;
    private static readonly FieldInfo MovePerformField = AccessTools.Field(typeof(MoveState), "_onPerform")!;

    private readonly NativeBehaviorGraphAutoImporter _graphAutoImporter = new();
    private readonly NativeBehaviorGraphTranslator _graphTranslator = new();

    public bool TryImportMonster(string entityId, out NativeMonsterAiImportResult? result)
    {
        result = null;
        var canonicalMonster = ResolveMonster(entityId);
        if (canonicalMonster == null)
        {
            return false;
        }

        var mutableMonster = canonicalMonster.ToMutable();
        mutableMonster.SetUpForCombat();
        var stateMachine = mutableMonster.MoveStateMachine;
        if (stateMachine == null)
        {
            return false;
        }

        var definition = new MonsterAiDefinition
        {
            MonsterId = canonicalMonster.Id.Entry
        };

        var notes = new List<string>();
        var moveGraphs = new List<BehaviorGraphDefinition>();
        var allMoves = stateMachine.States.Values.OfType<MoveState>().ToList();
        var turnsById = allMoves.ToDictionary(move => move.Id, BuildTurnDefinition, StringComparer.Ordinal);

        var initialState = InitialStateField.GetValue(stateMachine) as MonsterState;
        if (initialState == null)
        {
            return false;
        }

        BuildStructure(canonicalMonster, initialState, stateMachine, definition, turnsById, notes);

        foreach (var turn in turnsById.Values)
        {
            foreach (var move in turn.Moves)
            {
                if (TryBuildMoveGraph(canonicalMonster, move.MoveId, move.DisplayName, out var graph, out var moveNote))
                {
                    moveGraphs.Add(graph!);
                    move.GraphId = graph!.GraphId;
                }
                else
                {
                    var scaffoldId = $"monster_{canonicalMonster.Id.Entry}_{move.MoveId}".ToLowerInvariant();
                    var scaffold = BehaviorGraphTemplateFactory.CreateDefaultScaffold(scaffoldId, ModStudioEntityKind.Monster, move.DisplayName, canonicalMonster.Id.Entry);
                    moveGraphs.Add(scaffold);
                    move.GraphId = scaffold.GraphId;
                    if (!string.IsNullOrWhiteSpace(moveNote))
                    {
                        notes.Add(moveNote);
                    }
                }
            }
        }

        definition.Turns = turnsById.Values.Select(turn => turn.Clone()).ToList();

        result = new NativeMonsterAiImportResult
        {
            Definition = definition,
            MoveGraphs = moveGraphs,
            IsPartial = notes.Count > 0,
            Notes = notes,
            Summary = string.Join(
                Environment.NewLine,
                new[]
                {
                    $"Monster: {canonicalMonster.Id.Entry}",
                    $"Opening turns: {definition.OpeningTurns.Count}",
                    $"Loop phases: {definition.LoopPhases.Count}",
                    $"Imported move graphs: {moveGraphs.Count}",
                    $"Notes: {(notes.Count == 0 ? "-" : string.Join(" | ", notes.Distinct(StringComparer.OrdinalIgnoreCase)))}"
                })
        };
        return true;
    }

    private static MonsterModel? ResolveMonster(string entityId)
    {
        return ModelDb.Monsters.FirstOrDefault(monster => string.Equals(monster.Id.Entry, entityId, StringComparison.OrdinalIgnoreCase))
            ?? ModelDb.Monsters.FirstOrDefault(monster => string.Equals(monster.GetType().Name, entityId, StringComparison.OrdinalIgnoreCase));
    }

    private static void BuildStructure(
        MonsterModel monster,
        MonsterState initialState,
        MonsterMoveStateMachine stateMachine,
        MonsterAiDefinition definition,
        IReadOnlyDictionary<string, MonsterTurnDefinition> turnsById,
        ICollection<string> notes)
    {
        definition.OpeningTurns.Clear();
        definition.LoopPhases.Clear();
        BuildPhaseRecursive(
            monster,
            initialState,
            initialState,
            stateMachine,
            definition,
            turnsById,
            new HashSet<string>(StringComparer.Ordinal),
            notes);

        var initialPhaseIndex = definition.LoopPhases.FindIndex(phase => string.Equals(phase.PhaseId, initialState.Id, StringComparison.Ordinal));
        if (initialPhaseIndex > 0)
        {
            var initialPhase = definition.LoopPhases[initialPhaseIndex];
            definition.LoopPhases.RemoveAt(initialPhaseIndex);
            definition.LoopPhases.Insert(0, initialPhase);
        }
    }

    private static void BuildPhaseRecursive(
        MonsterModel monster,
        MonsterState state,
        MonsterState initialState,
        MonsterMoveStateMachine stateMachine,
        MonsterAiDefinition definition,
        IReadOnlyDictionary<string, MonsterTurnDefinition> turnsById,
        ISet<string> visitedPhaseIds,
        ICollection<string> notes)
    {
        if (!visitedPhaseIds.Add(state.Id))
        {
            return;
        }

        switch (state)
        {
            case RandomBranchState randomBranch:
                definition.LoopPhases.Add(BuildRandomPhase(monster, randomBranch, initialState, stateMachine, definition, turnsById, visitedPhaseIds, notes));
                break;
            case ConditionalBranchState conditionalBranch:
                definition.LoopPhases.Add(BuildConditionalPhase(monster, conditionalBranch, initialState, stateMachine, definition, turnsById, visitedPhaseIds, notes));
                break;
            case MoveState moveState:
                definition.LoopPhases.Add(BuildMovePhase(monster, moveState, initialState, stateMachine, definition, turnsById, visitedPhaseIds, notes));
                break;
        }
    }

    private static MonsterPhaseDefinition BuildMovePhase(
        MonsterModel monster,
        MoveState moveState,
        MonsterState initialState,
        MonsterMoveStateMachine stateMachine,
        MonsterAiDefinition definition,
        IReadOnlyDictionary<string, MonsterTurnDefinition> turnsById,
        ISet<string> visitedPhaseIds,
        ICollection<string> notes)
    {
        var phase = new MonsterPhaseDefinition
        {
            PhaseId = moveState.Id,
            DisplayName = moveState.Id,
            PhaseKind = MonsterPhaseKind.Sequential
        };

        phase.Branches.Add(new MonsterPhaseBranch
        {
            BranchId = $"{moveState.Id}_turn",
            TargetTurnId = moveState.Id,
            RepeatType = MoveRepeatType.CanRepeatForever
        });

        var nextStateId = moveState.FollowUpState?.Id ?? moveState.FollowUpStateId ?? initialState.Id;
        phase.Branches.Add(BuildPhaseTransitionBranch(
            monster,
            $"{moveState.Id}_next",
            nextStateId,
            initialState,
            stateMachine,
            definition,
            turnsById,
            visitedPhaseIds,
            notes));

        return phase;
    }

    private static MonsterPhaseDefinition BuildRandomPhase(
        MonsterModel monster,
        RandomBranchState randomBranch,
        MonsterState initialState,
        MonsterMoveStateMachine stateMachine,
        MonsterAiDefinition definition,
        IReadOnlyDictionary<string, MonsterTurnDefinition> turnsById,
        ISet<string> visitedPhaseIds,
        ICollection<string> notes)
    {
        var phase = new MonsterPhaseDefinition
        {
            PhaseId = randomBranch.Id,
            DisplayName = randomBranch.Id,
            PhaseKind = MonsterPhaseKind.RandomBranch
        };

        for (var index = 0; index < randomBranch.States.Count; index++)
        {
            var sourceBranch = randomBranch.States[index];
            var branch = BuildPhaseTransitionBranch(
                monster,
                $"{randomBranch.Id}_branch_{index + 1:00}",
                sourceBranch.stateId,
                initialState,
                stateMachine,
                definition,
                turnsById,
                visitedPhaseIds,
                notes);
            branch.Weight = SafeGetWeight(sourceBranch);
            branch.RepeatType = sourceBranch.repeatType;
            branch.MaxRepeats = sourceBranch.maxTimes;
            branch.Cooldown = sourceBranch.cooldown;
            phase.Branches.Add(branch);
        }

        return phase;
    }

    private static MonsterPhaseDefinition BuildConditionalPhase(
        MonsterModel monster,
        ConditionalBranchState conditionalBranch,
        MonsterState initialState,
        MonsterMoveStateMachine stateMachine,
        MonsterAiDefinition definition,
        IReadOnlyDictionary<string, MonsterTurnDefinition> turnsById,
        ISet<string> visitedPhaseIds,
        ICollection<string> notes)
    {
        var phase = new MonsterPhaseDefinition
        {
            PhaseId = conditionalBranch.Id,
            DisplayName = conditionalBranch.Id,
            PhaseKind = MonsterPhaseKind.ConditionalBranch
        };

        var conditionalBranches = ConditionalStatesProperty.GetValue(conditionalBranch) as System.Collections.IEnumerable;
        if (conditionalBranches == null)
        {
            return phase;
        }

        var index = 0;
        foreach (var sourceBranch in conditionalBranches)
        {
            var targetId = ConditionalIdField.GetValue(sourceBranch)?.ToString() ?? string.Empty;
            var branch = BuildPhaseTransitionBranch(
                monster,
                $"{conditionalBranch.Id}_branch_{index + 1:00}",
                targetId,
                initialState,
                stateMachine,
                definition,
                turnsById,
                visitedPhaseIds,
                notes);
            branch.Condition = ResolveConditionalBranchCondition(monster, conditionalBranch.Id, index);
            phase.Branches.Add(branch);
            index++;
        }

        return phase;
    }

    private static MonsterPhaseBranch BuildPhaseTransitionBranch(
        MonsterModel monster,
        string branchId,
        string? targetStateId,
        MonsterState initialState,
        MonsterMoveStateMachine stateMachine,
        MonsterAiDefinition definition,
        IReadOnlyDictionary<string, MonsterTurnDefinition> turnsById,
        ISet<string> visitedPhaseIds,
        ICollection<string> notes)
    {
        var branch = new MonsterPhaseBranch
        {
            BranchId = branchId,
            RepeatType = MoveRepeatType.CanRepeatForever
        };

        var resolvedStateId = string.IsNullOrWhiteSpace(targetStateId) ? initialState.Id : targetStateId;
        if (!stateMachine.States.TryGetValue(resolvedStateId, out var targetState))
        {
            branch.TargetPhaseId = initialState.Id;
            notes.Add($"Branch '{branchId}' targets unsupported state '{resolvedStateId}'. Falling back to initial state '{initialState.Id}'.");
            return branch;
        }

        branch.TargetPhaseId = targetState.Id;
        BuildPhaseRecursive(monster, targetState, initialState, stateMachine, definition, turnsById, visitedPhaseIds, notes);
        return branch;
    }

    private static string ResolveConditionalBranchCondition(MonsterModel monster, string branchStateId, int branchIndex)
    {
        if (string.Equals(monster.Id.Entry, "QUEEN", StringComparison.OrdinalIgnoreCase) &&
            (string.Equals(branchStateId, "YOURE_MINE_NOW_BRANCH", StringComparison.Ordinal) ||
             string.Equals(branchStateId, "BURN_BRIGHT_FOR_ME_BRANCH", StringComparison.Ordinal)) &&
            branchIndex is 0 or 1)
        {
            return branchIndex == 0 ? "!$monster.has_amalgam_died" : "$monster.has_amalgam_died";
        }

        return NativeMonsterConditionSourceResolver.TryResolveCondition(monster, branchStateId, branchIndex, out var resolvedCondition)
            ? resolvedCondition
            : $"native_condition_{branchIndex + 1}";
    }

    private static MonsterPhaseDefinition BuildSequentialPhase(string phaseId, IReadOnlyList<MoveState> moves)
    {
        var phase = new MonsterPhaseDefinition
        {
            PhaseId = phaseId,
            DisplayName = phaseId,
            PhaseKind = MonsterPhaseKind.Sequential
        };

        for (var index = 0; index < moves.Count; index++)
        {
            phase.Branches.Add(new MonsterPhaseBranch
            {
                BranchId = $"{phaseId}_branch_{index + 1:00}",
                TargetTurnId = moves[index].Id,
                RepeatType = MoveRepeatType.CanRepeatForever
            });
        }

        return phase;
    }

    private static MonsterTurnDefinition BuildTurnDefinition(MoveState move)
    {
        return new MonsterTurnDefinition
        {
            TurnId = move.Id,
            DisplayName = move.Id,
            MustPerformOnceBeforeTransitioning = move.MustPerformOnceBeforeTransitioning,
            Moves =
            [
                new MonsterMoveDefinition
                {
                    MoveId = move.Id,
                    DisplayName = move.Id
                }
            ],
            Intents = move.Intents.Select(BuildIntent).ToList()
        };
    }

    private bool TryBuildMoveGraph(
        MonsterModel monster,
        string moveId,
        string displayName,
        out BehaviorGraphDefinition? graph,
        out string note)
    {
        graph = null;
        note = string.Empty;
        var mutableMonster = monster.ToMutable();
        mutableMonster.SetUpForCombat();
        var moveState = mutableMonster.MoveStateMachine?.States.Values.OfType<MoveState>().FirstOrDefault(state => string.Equals(state.Id, moveId, StringComparison.Ordinal));
        if (moveState == null)
        {
            note = $"Move '{moveId}' could not be resolved for auto-import.";
            return false;
        }

        if (MovePerformField.GetValue(moveState) is not Delegate performDelegate || performDelegate.Method is not MethodInfo performMethod)
        {
            note = $"Move '{moveId}' does not expose an importable delegate.";
            return false;
        }

        if (!_graphAutoImporter.TryCreateGraphFromMethod(
                ModStudioEntityKind.Monster,
                $"{monster.Id.Entry}_{moveId}",
                string.IsNullOrWhiteSpace(displayName) ? moveId : displayName,
                monster,
                performMethod,
                "monster.move",
                "current_target",
                "self",
                out var importResult) ||
            importResult.Graph == null)
        {
            if (TryBuildSyntheticMoveGraph(monster, moveId, displayName, out graph))
            {
                note = string.Empty;
                return true;
            }

            note = importResult.Summary;
            return false;
        }

        importResult.Graph.GraphId = $"monster_{monster.Id.Entry}_{moveId}".ToLowerInvariant();
        importResult.Graph.Name = string.IsNullOrWhiteSpace(displayName) ? moveId : displayName;
        graph = importResult.Graph;
        note = importResult.IsPartial ? importResult.Summary : string.Empty;
        return true;
    }

    private bool TryBuildSyntheticMoveGraph(
        MonsterModel monster,
        string moveId,
        string displayName,
        out BehaviorGraphDefinition? graph)
    {
        graph = null;
        var source = moveId switch
        {
            "INITIAL_SLEEP_MOVE" when string.Equals(monster.Id.Entry, "BYGONE_EFFIGY", StringComparison.OrdinalIgnoreCase) => new NativeBehaviorGraphSource
            {
                EntityKind = ModStudioEntityKind.Monster,
                GraphId = $"monster_{monster.Id.Entry}_{moveId}".ToLowerInvariant(),
                Name = string.IsNullOrWhiteSpace(displayName) ? moveId : displayName,
                Description = monster.Id.Entry,
                TriggerId = "monster.move",
                Steps =
                [
                    new NativeBehaviorStep
                    {
                        Kind = "monster.talk",
                        Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["text"] = "...",
                            ["duration"] = "0.5"
                        }
                    }
                ]
            },
            "DAMPEN_MOVE" when string.Equals(monster.Id.Entry, "MAGI_KNIGHT", StringComparison.OrdinalIgnoreCase) => new NativeBehaviorGraphSource
            {
                EntityKind = ModStudioEntityKind.Monster,
                GraphId = $"monster_{monster.Id.Entry}_{moveId}".ToLowerInvariant(),
                Name = string.IsNullOrWhiteSpace(displayName) ? moveId : displayName,
                Description = monster.Id.Entry,
                TriggerId = "monster.move",
                Steps =
                [
                    new NativeBehaviorStep
                    {
                        Kind = "combat.apply_power",
                        Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["power_id"] = "DAMPEN_POWER",
                            ["amount"] = "1",
                            ["target"] = "all_enemies"
                        }
                    }
                ]
            },
            _ => null
        };

        if (source == null)
        {
            return false;
        }

        var translation = _graphTranslator.Translate(source);
        graph = translation.Graph;
        return true;
    }

    private static MonsterIntentDeclaration BuildIntent(AbstractIntent intent)
    {
        var declaration = new MonsterIntentDeclaration
        {
            IntentType = intent switch
            {
                SingleAttackIntent => MonsterIntentType.SingleAttack,
                MultiAttackIntent => MonsterIntentType.MultiAttack,
                BuffIntent => MonsterIntentType.Buff,
                DebuffIntent => MonsterIntentType.Debuff,
                DefendIntent => MonsterIntentType.Defend,
                SummonIntent => MonsterIntentType.Summon,
                StatusIntent => MonsterIntentType.Status,
                HealIntent => MonsterIntentType.Heal,
                CardDebuffIntent => MonsterIntentType.CardDebuff,
                _ => MonsterIntentType.Unknown
            }
        };

        if (intent is AttackIntent attackIntent && attackIntent.DamageCalc != null)
        {
            declaration.Parameters["amount"] = SafeEvaluateAttackAmount(attackIntent).ToString();
        }

        if (intent is MultiAttackIntent multiAttackIntent)
        {
            declaration.Parameters["count"] = SafeEvaluateRepeatCount(multiAttackIntent).ToString();
        }

        if (intent is StatusIntent statusIntent)
        {
            declaration.Parameters["count"] = statusIntent.CardCount.ToString();
        }

        return declaration;
    }

    private static float SafeGetWeight(RandomBranchState.StateWeight branch)
    {
        try
        {
            return branch.GetWeight();
        }
        catch
        {
            return 1f;
        }
    }

    private static int SafeEvaluateAttackAmount(AttackIntent attackIntent)
    {
        try
        {
            return attackIntent.DamageCalc == null ? 0 : (int)attackIntent.DamageCalc();
        }
        catch
        {
            return 0;
        }
    }

    private static int SafeEvaluateRepeatCount(MultiAttackIntent attackIntent)
    {
        try
        {
            return attackIntent.Repeats;
        }
        catch
        {
            return 1;
        }
    }
}
