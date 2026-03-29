using MegaCrit.Sts2.Core.MonsterMoves;

namespace STS2_Editor.Scripts.Editor.Core.Models;

public sealed class MonsterAiDefinition
{
    public string MonsterId { get; set; } = string.Empty;

    public List<MonsterTurnDefinition> Turns { get; set; } = new();

    public List<MonsterTurnDefinition> OpeningTurns { get; set; } = new();

    public List<MonsterPhaseDefinition> LoopPhases { get; set; } = new();

    public List<MonsterStateVariableDefinition> StateVariables { get; set; } = new();

    public List<MonsterLifecycleHookDefinition> LifecycleHooks { get; set; } = new();

    public List<MonsterEventTriggerDefinition> EventTriggers { get; set; } = new();

    public Dictionary<string, string> Metadata { get; set; } = new(StringComparer.Ordinal);

    public MonsterAiDefinition Clone()
    {
        return new MonsterAiDefinition
        {
            MonsterId = MonsterId,
            Turns = Turns.Select(turn => turn.Clone()).ToList(),
            OpeningTurns = OpeningTurns.Select(turn => turn.Clone()).ToList(),
            LoopPhases = LoopPhases.Select(phase => phase.Clone()).ToList(),
            StateVariables = StateVariables.Select(variable => variable.Clone()).ToList(),
            LifecycleHooks = LifecycleHooks.Select(hook => hook.Clone()).ToList(),
            EventTriggers = EventTriggers.Select(trigger => trigger.Clone()).ToList(),
            Metadata = new Dictionary<string, string>(Metadata, StringComparer.Ordinal)
        };
    }
}

public sealed class MonsterTurnDefinition
{
    public string TurnId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public List<MonsterMoveDefinition> Moves { get; set; } = new();

    public List<MonsterIntentDeclaration> Intents { get; set; } = new();

    public bool MustPerformOnceBeforeTransitioning { get; set; }

    public MonsterTurnDefinition Clone()
    {
        return new MonsterTurnDefinition
        {
            TurnId = TurnId,
            DisplayName = DisplayName,
            Moves = Moves.Select(move => move.Clone()).ToList(),
            Intents = Intents.Select(intent => intent.Clone()).ToList(),
            MustPerformOnceBeforeTransitioning = MustPerformOnceBeforeTransitioning
        };
    }
}

public sealed class MonsterMoveDefinition
{
    public string MoveId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string GraphId { get; set; } = string.Empty;

    public MonsterMoveDefinition Clone()
    {
        return new MonsterMoveDefinition
        {
            MoveId = MoveId,
            DisplayName = DisplayName,
            GraphId = GraphId
        };
    }
}

public sealed class MonsterPhaseDefinition
{
    public string PhaseId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public MonsterPhaseKind PhaseKind { get; set; } = MonsterPhaseKind.Sequential;

    public List<MonsterPhaseBranch> Branches { get; set; } = new();

    public MonsterPhaseDefinition Clone()
    {
        return new MonsterPhaseDefinition
        {
            PhaseId = PhaseId,
            DisplayName = DisplayName,
            PhaseKind = PhaseKind,
            Branches = Branches.Select(branch => branch.Clone()).ToList()
        };
    }
}

public sealed class MonsterPhaseBranch
{
    public string BranchId { get; set; } = string.Empty;

    public string? TargetTurnId { get; set; }

    public string? TargetPhaseId { get; set; }

    public float Weight { get; set; } = 1f;

    public string Condition { get; set; } = string.Empty;

    public MoveRepeatType RepeatType { get; set; } = MoveRepeatType.CanRepeatForever;

    public int MaxRepeats { get; set; }

    public int Cooldown { get; set; }

    public MonsterPhaseBranch Clone()
    {
        return new MonsterPhaseBranch
        {
            BranchId = BranchId,
            TargetTurnId = TargetTurnId,
            TargetPhaseId = TargetPhaseId,
            Weight = Weight,
            Condition = Condition,
            RepeatType = RepeatType,
            MaxRepeats = MaxRepeats,
            Cooldown = Cooldown
        };
    }
}

public sealed class MonsterStateVariableDefinition
{
    public string Name { get; set; } = string.Empty;

    public MonsterStateVariableType Type { get; set; } = MonsterStateVariableType.Integer;

    public string InitialValue { get; set; } = string.Empty;

    public MonsterStateVariableDefinition Clone()
    {
        return new MonsterStateVariableDefinition
        {
            Name = Name,
            Type = Type,
            InitialValue = InitialValue
        };
    }
}

public sealed class MonsterLifecycleHookDefinition
{
    public MonsterLifecycleHookType HookType { get; set; } = MonsterLifecycleHookType.AfterAddedToRoom;

    public string GraphId { get; set; } = string.Empty;

    public MonsterLifecycleHookDefinition Clone()
    {
        return new MonsterLifecycleHookDefinition
        {
            HookType = HookType,
            GraphId = GraphId
        };
    }
}

public sealed class MonsterEventTriggerDefinition
{
    public MonsterEventTriggerKind EventKind { get; set; } = MonsterEventTriggerKind.AllyDied;

    public string FilterMonsterId { get; set; } = string.Empty;

    public string TargetPhaseId { get; set; } = string.Empty;

    public string GraphId { get; set; } = string.Empty;

    public MonsterEventTriggerDefinition Clone()
    {
        return new MonsterEventTriggerDefinition
        {
            EventKind = EventKind,
            FilterMonsterId = FilterMonsterId,
            TargetPhaseId = TargetPhaseId,
            GraphId = GraphId
        };
    }
}

public sealed class MonsterIntentDeclaration
{
    public MonsterIntentType IntentType { get; set; } = MonsterIntentType.Unknown;

    public Dictionary<string, string> Parameters { get; set; } = new(StringComparer.Ordinal);

    public MonsterIntentDeclaration Clone()
    {
        return new MonsterIntentDeclaration
        {
            IntentType = IntentType,
            Parameters = new Dictionary<string, string>(Parameters, StringComparer.Ordinal)
        };
    }
}

public enum MonsterPhaseKind
{
    Sequential = 0,
    RandomBranch = 1,
    ConditionalBranch = 2
}

public enum MonsterStateVariableType
{
    Integer = 0,
    Boolean = 1,
    Float = 2,
    String = 3
}

public enum MonsterLifecycleHookType
{
    AfterAddedToRoom = 0,
    BeforeRemovedFromRoom = 1,
    OnDieToDoom = 2,
    BeforeDeath = 3,
    AfterCurrentHpChanged = 4
}

public enum MonsterEventTriggerKind
{
    AllyDied = 0,
    HpChanged = 1
}

public enum MonsterIntentType
{
    Unknown = 0,
    SingleAttack = 1,
    MultiAttack = 2,
    Buff = 3,
    Debuff = 4,
    Defend = 5,
    Summon = 6,
    Status = 7,
    Heal = 8,
    CardDebuff = 9
}

public static class MonsterAiDefinitionCloner
{
    public static MonsterAiDefinition? Clone(MonsterAiDefinition? source)
    {
        return source?.Clone();
    }
}
