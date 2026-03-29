using STS2_Editor.Scripts.Editor.Core.Models;
using MegaCrit.Sts2.Core.Models;

namespace STS2_Editor.Scripts.Editor.Runtime;

public sealed class MonsterRuntimeState
{
    private readonly Dictionary<string, MonsterTurnDefinition> _turnsById;
    private readonly Dictionary<string, MonsterPhaseDefinition> _phasesById;

    public MonsterRuntimeState(MonsterModel monster, MonsterAiDefinition definition)
    {
        Monster = monster;
        Definition = definition;
        _turnsById = new Dictionary<string, MonsterTurnDefinition>(StringComparer.Ordinal);
        foreach (var turn in definition.Turns.Concat(definition.OpeningTurns))
        {
            if (string.IsNullOrWhiteSpace(turn.TurnId) || _turnsById.ContainsKey(turn.TurnId))
            {
                continue;
            }

            _turnsById[turn.TurnId] = turn;
        }
        _phasesById = definition.LoopPhases
            .Where(phase => !string.IsNullOrWhiteSpace(phase.PhaseId))
            .ToDictionary(phase => phase.PhaseId, phase => phase, StringComparer.Ordinal);

        foreach (var variable in definition.StateVariables)
        {
            if (string.IsNullOrWhiteSpace(variable.Name))
            {
                continue;
            }

            Variables[variable.Name] = ParseInitialValue(variable);
        }
    }

    public MonsterModel Monster { get; }

    public MonsterAiDefinition Definition { get; }

    public Dictionary<string, object?> Variables { get; } = new(StringComparer.Ordinal);

    public List<string> TurnHistory { get; } = new();

    public List<string> SelectionHistory { get; } = new();

    public Dictionary<string, int> SequentialBranchCursor { get; } = new(StringComparer.Ordinal);

    public int OpeningTurnIndex { get; set; }

    public string CurrentPhaseId { get; set; } = string.Empty;

    public string PendingTurnId { get; set; } = string.Empty;

    public string ResumeTurnId { get; set; } = string.Empty;

    public bool HasPendingTurn => !string.IsNullOrWhiteSpace(PendingTurnId);

    public MonsterTurnDefinition? GetPendingTurn()
    {
        return FindTurn(PendingTurnId);
    }

    public MonsterTurnDefinition? FindTurn(string? turnId)
    {
        if (string.IsNullOrWhiteSpace(turnId))
        {
            return null;
        }

        return _turnsById.TryGetValue(turnId, out var turn) ? turn : null;
    }

    public MonsterPhaseDefinition? FindPhase(string? phaseId)
    {
        if (string.IsNullOrWhiteSpace(phaseId))
        {
            return null;
        }

        return _phasesById.TryGetValue(phaseId, out var phase) ? phase : null;
    }

    public MonsterTurnDefinition? FindFirstTurn()
    {
        return Definition.OpeningTurns.FirstOrDefault()
            ?? Definition.Turns.FirstOrDefault()
            ?? _turnsById.Values.FirstOrDefault();
    }

    public void RecordSelection(string selectionKey)
    {
        if (!string.IsNullOrWhiteSpace(selectionKey))
        {
            SelectionHistory.Add(selectionKey);
        }
    }

    public void RecordTurnPerformed(MonsterTurnDefinition turn)
    {
        if (!string.IsNullOrWhiteSpace(turn.TurnId))
        {
            TurnHistory.Add(turn.TurnId);
        }

        PendingTurnId = string.Empty;
    }

    private static object? ParseInitialValue(MonsterStateVariableDefinition variable)
    {
        return variable.Type switch
        {
            MonsterStateVariableType.Boolean when bool.TryParse(variable.InitialValue, out var boolValue) => boolValue,
            MonsterStateVariableType.Float when decimal.TryParse(variable.InitialValue, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var decimalValue) => decimalValue,
            MonsterStateVariableType.Integer when int.TryParse(variable.InitialValue, out var intValue) => intValue,
            MonsterStateVariableType.String => variable.InitialValue,
            _ => variable.InitialValue
        };
    }
}
