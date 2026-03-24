using System.Globalization;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2_Editor.Scripts.Editor.Graph;

public sealed class BehaviorGraphExecutionContext
{
    private readonly Dictionary<string, object?> _state = new(StringComparer.Ordinal);

    public IDictionary<string, object?> State => _state;

    public BehaviorGraphDefinition? Graph { get; set; }

    public string TriggerId { get; set; } = string.Empty;

    public PlayerChoiceContext? ChoiceContext { get; init; }

    public AbstractModel? SourceModel { get; init; }

    public CardModel? Card { get; init; }

    public CardPlay? CardPlay { get; init; }

    public PotionModel? Potion { get; init; }

    public RelicModel? Relic { get; init; }

    public EventModel? Event { get; init; }

    public EnchantmentModel? Enchantment { get; init; }

    public Player? Owner { get; init; }

    public Creature? SourceCreature => Owner?.Creature;

    public CombatState? CombatState { get; init; }

    public IRunState? RunState { get; init; }

    public Creature? Target { get; set; }

    public IList<string> ExecutionLog { get; } = new List<string>();

    public object? this[string key]
    {
        get => _state.TryGetValue(key, out var value) ? value : null;
        set => _state[key] = value;
    }

    public bool TryGetState<T>(string key, out T? value)
    {
        value = default;
        if (!_state.TryGetValue(key, out var raw) || raw is null)
        {
            return false;
        }

        if (raw is T typed)
        {
            value = typed;
            return true;
        }

        try
        {
            value = (T)Convert.ChangeType(raw, typeof(T));
            return true;
        }
        catch
        {
            value = default;
            return false;
        }
    }

    public object? ResolveReference(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        if (key.StartsWith("state.", StringComparison.OrdinalIgnoreCase))
        {
            return this[key["state.".Length..]];
        }

        return key.ToLowerInvariant() switch
        {
            "trigger" => TriggerId,
            "source_model" => SourceModel,
            "card" => Card,
            "card_play" => CardPlay,
            "potion" => Potion,
            "relic" => Relic,
            "event" => Event,
            "enchantment" => Enchantment,
            "owner" or "owner_player" => Owner,
            "owner_creature" or "source_creature" or "self" => Owner?.Creature,
            "target" or "current_target" => Target,
            "combat_state" => CombatState,
            "run_state" => RunState,
            "choice_context" => ChoiceContext,
            _ => this[key]
        };
    }

    public object? ResolveObject(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        if (rawValue.StartsWith("$", StringComparison.Ordinal))
        {
            return ResolveReference(rawValue[1..]);
        }

        if (bool.TryParse(rawValue, out var boolValue))
        {
            return boolValue;
        }

        if (decimal.TryParse(rawValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var decimalValue))
        {
            return decimalValue;
        }

        return rawValue;
    }

    public string ResolveString(string? rawValue, string defaultValue = "")
    {
        var resolved = ResolveObject(rawValue);
        return resolved?.ToString() ?? defaultValue;
    }

    public int ResolveInt(string? rawValue, int defaultValue = 0)
    {
        var resolved = ResolveObject(rawValue);
        if (resolved is int intValue)
        {
            return intValue;
        }

        return TryConvertToDecimal(resolved, out var decimalValue) ? (int)decimalValue : defaultValue;
    }

    public decimal ResolveDecimal(string? rawValue, decimal defaultValue = 0m)
    {
        var resolved = ResolveObject(rawValue);
        return TryConvertToDecimal(resolved, out var decimalValue) ? decimalValue : defaultValue;
    }

    public bool ResolveBool(string? rawValue, bool defaultValue = false)
    {
        var resolved = ResolveObject(rawValue);
        return TryConvertToBool(resolved, out var boolValue) ? boolValue : defaultValue;
    }

    public IReadOnlyList<Creature> ResolveTargets(string? selector)
    {
        selector = string.IsNullOrWhiteSpace(selector) ? "current_target" : selector.Trim().ToLowerInvariant();
        var combatState = CombatState ?? Owner?.Creature?.CombatState;
        var ownerCreature = Owner?.Creature;
        var ownerSide = ownerCreature?.Side ?? Target?.Side ?? CombatSide.Player;

        return selector switch
        {
            "target" or "current_target" => Target == null ? Array.Empty<Creature>() : new[] { Target },
            "self" or "owner" or "owner_creature" or "source_creature" => ownerCreature == null ? Array.Empty<Creature>() : new[] { ownerCreature },
            "all_players" or "players" => combatState == null
                ? Array.Empty<Creature>()
                : combatState.GetCreaturesOnSide(CombatSide.Player).Where(x => x.IsAlive).ToList(),
            "all_allies" or "all_friendlies" or "allies" => combatState == null
                ? Array.Empty<Creature>()
                : combatState.GetCreaturesOnSide(ownerSide).Where(x => x.IsAlive).ToList(),
            "other_enemies" or "other_opponents" => combatState == null
                ? Array.Empty<Creature>()
                : combatState
                    .GetCreaturesOnSide(ownerSide == CombatSide.Player ? CombatSide.Enemy : CombatSide.Player)
                    .Where(x => x.IsAlive && x != Target)
                    .ToList(),
            "all_enemies" or "all_opponents" or "enemies" or "opponents" => combatState == null
                ? Array.Empty<Creature>()
                : combatState.GetCreaturesOnSide(ownerSide == CombatSide.Player ? CombatSide.Enemy : CombatSide.Player).Where(x => x.IsAlive).ToList(),
            _ => Array.Empty<Creature>()
        };
    }

    private static bool TryConvertToDecimal(object? value, out decimal result)
    {
        result = 0m;
        if (value is null)
        {
            return false;
        }

        switch (value)
        {
            case decimal decimalValue:
                result = decimalValue;
                return true;
            case int intValue:
                result = intValue;
                return true;
            case long longValue:
                result = longValue;
                return true;
            case float floatValue:
                result = (decimal)floatValue;
                return true;
            case double doubleValue:
                result = (decimal)doubleValue;
                return true;
            case string stringValue when decimal.TryParse(stringValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed):
                result = parsed;
                return true;
            default:
                return false;
        }
    }

    private static bool TryConvertToBool(object? value, out bool result)
    {
        result = false;
        if (value is null)
        {
            return false;
        }

        switch (value)
        {
            case bool boolValue:
                result = boolValue;
                return true;
            case string stringValue when bool.TryParse(stringValue, out var parsed):
                result = parsed;
                return true;
            default:
                if (TryConvertToDecimal(value, out var number))
                {
                    result = number != 0m;
                    return true;
                }

                return false;
        }
    }
}
