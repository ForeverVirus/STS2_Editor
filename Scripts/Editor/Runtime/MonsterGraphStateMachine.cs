using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using STS2_Editor.Scripts.Editor.Core.Models;

namespace STS2_Editor.Scripts.Editor.Runtime;

internal sealed class MonsterGraphStateMachine
{
    private readonly MonsterAiDefinition _definition;

    public MonsterGraphStateMachine(MonsterAiDefinition definition)
    {
        _definition = definition;
    }

    public MonsterTurnDefinition? SelectNextTurn(MonsterModel monster, MonsterRuntimeState runtimeState)
    {
        if (runtimeState.OpeningTurnIndex < _definition.OpeningTurns.Count)
        {
            var openingTurn = _definition.OpeningTurns[runtimeState.OpeningTurnIndex++];
            runtimeState.PendingTurnId = openingTurn.TurnId;
            return openingTurn;
        }

        if (_definition.LoopPhases.Count == 0)
        {
            var fallback = runtimeState.FindFirstTurn();
            runtimeState.PendingTurnId = fallback?.TurnId ?? string.Empty;
            return fallback;
        }

        if (string.IsNullOrWhiteSpace(runtimeState.CurrentPhaseId))
        {
            runtimeState.CurrentPhaseId = _definition.LoopPhases[0].PhaseId;
        }

        var turn = ResolvePhaseTurn(monster, runtimeState, runtimeState.CurrentPhaseId, depth: 0);
        runtimeState.PendingTurnId = turn?.TurnId ?? string.Empty;
        return turn;
    }

    public IReadOnlyList<AbstractIntent> BuildIntents(MonsterTurnDefinition? turn)
    {
        if (turn == null)
        {
            return Array.Empty<AbstractIntent>();
        }

        if (turn.Intents.Count > 0)
        {
            return turn.Intents.Select(BuildIntent).ToList();
        }

        return Array.Empty<AbstractIntent>();
    }

    private MonsterTurnDefinition? ResolvePhaseTurn(
        MonsterModel monster,
        MonsterRuntimeState runtimeState,
        string? phaseId,
        int depth)
    {
        if (depth > 16)
        {
            return null;
        }

        var phase = runtimeState.FindPhase(phaseId);
        if (phase == null)
        {
            return runtimeState.FindFirstTurn();
        }

        runtimeState.CurrentPhaseId = phase.PhaseId;
        var branch = SelectBranch(monster, runtimeState, phase);
        if (branch == null)
        {
            return runtimeState.FindFirstTurn();
        }

        var selectionKey = GetSelectionKey(branch);
        runtimeState.RecordSelection(selectionKey);

        if (!string.IsNullOrWhiteSpace(branch.TargetTurnId))
        {
            return runtimeState.FindTurn(branch.TargetTurnId);
        }

        if (!string.IsNullOrWhiteSpace(branch.TargetPhaseId))
        {
            runtimeState.CurrentPhaseId = branch.TargetPhaseId;
            return ResolvePhaseTurn(monster, runtimeState, branch.TargetPhaseId, depth + 1);
        }

        return runtimeState.FindFirstTurn();
    }

    private MonsterPhaseBranch? SelectBranch(MonsterModel monster, MonsterRuntimeState runtimeState, MonsterPhaseDefinition phase)
    {
        if (phase.Branches.Count == 0)
        {
            return null;
        }

        return phase.PhaseKind switch
        {
            MonsterPhaseKind.Sequential => SelectSequentialBranch(runtimeState, phase),
            MonsterPhaseKind.RandomBranch => SelectRandomBranch(monster, runtimeState, phase),
            MonsterPhaseKind.ConditionalBranch => SelectConditionalBranch(monster, runtimeState, phase),
            _ => phase.Branches[0]
        };
    }

    private MonsterPhaseBranch? SelectSequentialBranch(MonsterRuntimeState runtimeState, MonsterPhaseDefinition phase)
    {
        var startIndex = runtimeState.SequentialBranchCursor.TryGetValue(phase.PhaseId, out var index) ? index : 0;
        for (var offset = 0; offset < phase.Branches.Count; offset++)
        {
            var branchIndex = (startIndex + offset) % phase.Branches.Count;
            var branch = phase.Branches[branchIndex];
            runtimeState.SequentialBranchCursor[phase.PhaseId] = (branchIndex + 1) % phase.Branches.Count;
            if (CanSelectBranch(runtimeState, branch))
            {
                return branch;
            }
        }

        return phase.Branches[0];
    }

    private MonsterPhaseBranch? SelectRandomBranch(MonsterModel monster, MonsterRuntimeState runtimeState, MonsterPhaseDefinition phase)
    {
        var availableBranches = phase.Branches.Where(branch => CanSelectBranch(runtimeState, branch)).ToList();
        if (availableBranches.Count == 0)
        {
            availableBranches = phase.Branches.ToList();
        }

        var totalWeight = availableBranches.Sum(branch => Math.Max(branch.Weight, 0f));
        if (totalWeight <= 0f)
        {
            return availableBranches[0];
        }

        var roll = monster.RunRng.MonsterAi.NextFloat(totalWeight);
        foreach (var branch in availableBranches)
        {
            roll -= Math.Max(branch.Weight, 0f);
            if (roll <= 0f)
            {
                return branch;
            }
        }

        return availableBranches[0];
    }

    private MonsterPhaseBranch? SelectConditionalBranch(MonsterModel monster, MonsterRuntimeState runtimeState, MonsterPhaseDefinition phase)
    {
        foreach (var branch in phase.Branches)
        {
            if (!CanSelectBranch(runtimeState, branch))
            {
                continue;
            }

            if (EvaluateCondition(monster, runtimeState, branch.Condition))
            {
                return branch;
            }
        }

        return phase.Branches.FirstOrDefault(branch => CanSelectBranch(runtimeState, branch)) ?? phase.Branches[0];
    }

    private static bool CanSelectBranch(MonsterRuntimeState runtimeState, MonsterPhaseBranch branch)
    {
        var selectionKey = GetSelectionKey(branch);
        if (branch.Cooldown > 0 &&
            runtimeState.SelectionHistory.TakeLast(branch.Cooldown).Any(entry => string.Equals(entry, selectionKey, StringComparison.Ordinal)))
        {
            return false;
        }

        return branch.RepeatType switch
        {
            MoveRepeatType.CanRepeatForever => true,
            MoveRepeatType.CannotRepeat => !string.Equals(runtimeState.SelectionHistory.LastOrDefault(), selectionKey, StringComparison.Ordinal),
            MoveRepeatType.UseOnlyOnce => !runtimeState.SelectionHistory.Any(entry => string.Equals(entry, selectionKey, StringComparison.Ordinal)),
            MoveRepeatType.CanRepeatXTimes => CanRepeatXTimes(runtimeState, selectionKey, branch.MaxRepeats),
            _ => true
        };
    }

    private static bool CanRepeatXTimes(MonsterRuntimeState runtimeState, string selectionKey, int maxRepeats)
    {
        if (maxRepeats <= 0)
        {
            return true;
        }

        return runtimeState.SelectionHistory
            .TakeLast(maxRepeats)
            .Count(entry => string.Equals(entry, selectionKey, StringComparison.Ordinal)) < maxRepeats;
    }

    private static string GetSelectionKey(MonsterPhaseBranch branch)
    {
        if (!string.IsNullOrWhiteSpace(branch.TargetTurnId))
        {
            return $"turn:{branch.TargetTurnId}";
        }

        if (!string.IsNullOrWhiteSpace(branch.TargetPhaseId))
        {
            return $"phase:{branch.TargetPhaseId}";
        }

        return $"branch:{branch.BranchId}";
    }

    private static bool EvaluateCondition(MonsterModel monster, MonsterRuntimeState runtimeState, string? condition)
    {
        if (string.IsNullOrWhiteSpace(condition))
        {
            return true;
        }

        try
        {
            return IsTruthy(new ConditionExpressionEvaluator(monster, runtimeState, condition).Evaluate());
        }
        catch
        {
            var trimmed = condition.Trim();
            return runtimeState.Variables.TryGetValue(trimmed, out var rawValue) && IsTruthy(rawValue);
        }
    }

    private static bool CompareValues(object? leftValue, object? rightValue, string op)
    {
        if (TryToDecimal(leftValue, out var leftDecimal) && TryToDecimal(rightValue, out var rightDecimal))
        {
            return op switch
            {
                "==" => leftDecimal == rightDecimal,
                "!=" => leftDecimal != rightDecimal,
                "<" => leftDecimal < rightDecimal,
                "<=" => leftDecimal <= rightDecimal,
                ">" => leftDecimal > rightDecimal,
                ">=" => leftDecimal >= rightDecimal,
                _ => false
            };
        }

        var leftText = leftValue?.ToString() ?? string.Empty;
        var rightText = rightValue?.ToString() ?? string.Empty;
        return op switch
        {
            "==" => string.Equals(leftText, rightText, StringComparison.OrdinalIgnoreCase),
            "!=" => !string.Equals(leftText, rightText, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static bool TryToDecimal(object? rawValue, out decimal value)
    {
        value = 0m;
        switch (rawValue)
        {
            case decimal decimalValue:
                value = decimalValue;
                return true;
            case int intValue:
                value = intValue;
                return true;
            case long longValue:
                value = longValue;
                return true;
            case float floatValue:
                value = (decimal)floatValue;
                return true;
            case double doubleValue:
                value = (decimal)doubleValue;
                return true;
            case string stringValue when decimal.TryParse(stringValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed):
                value = parsed;
                return true;
            default:
                return false;
        }
    }

    private static bool IsTruthy(object? rawValue)
    {
        return rawValue switch
        {
            null => false,
            bool boolValue => boolValue,
            int intValue => intValue != 0,
            long longValue => longValue != 0,
            decimal decimalValue => decimalValue != 0m,
            float floatValue => Math.Abs(floatValue) > float.Epsilon,
            double doubleValue => Math.Abs(doubleValue) > double.Epsilon,
            string stringValue when bool.TryParse(stringValue, out var boolValue) => boolValue,
            string stringValue when decimal.TryParse(stringValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var decimalValue) => decimalValue != 0m,
            string stringValue => !string.IsNullOrWhiteSpace(stringValue),
            _ => true
        };
    }

    private sealed class ConditionExpressionEvaluator
    {
        private readonly MonsterModel _monster;
        private readonly MonsterRuntimeState _runtimeState;
        private readonly string _text;
        private int _index;

        public ConditionExpressionEvaluator(MonsterModel monster, MonsterRuntimeState runtimeState, string text)
        {
            _monster = monster;
            _runtimeState = runtimeState;
            _text = text;
        }

        public object? Evaluate()
        {
            var result = ParseOr();
            SkipWhitespace();
            return result;
        }

        private object? ParseOr()
        {
            var left = ParseAnd();
            while (true)
            {
                SkipWhitespace();
                if (!TryConsume("||"))
                {
                    return left;
                }

                var right = ParseAnd();
                left = IsTruthy(left) || IsTruthy(right);
            }
        }

        private object? ParseAnd()
        {
            var left = ParseEquality();
            while (true)
            {
                SkipWhitespace();
                if (!TryConsume("&&"))
                {
                    return left;
                }

                var right = ParseEquality();
                left = IsTruthy(left) && IsTruthy(right);
            }
        }

        private object? ParseEquality()
        {
            var left = ParseComparison();
            while (true)
            {
                SkipWhitespace();
                if (TryConsume("=="))
                {
                    left = CompareValues(left, ParseComparison(), "==");
                    continue;
                }

                if (TryConsume("!="))
                {
                    left = CompareValues(left, ParseComparison(), "!=");
                    continue;
                }

                return left;
            }
        }

        private object? ParseComparison()
        {
            var left = ParseAdditive();
            while (true)
            {
                SkipWhitespace();
                if (TryConsume("<="))
                {
                    left = CompareValues(left, ParseAdditive(), "<=");
                    continue;
                }

                if (TryConsume(">="))
                {
                    left = CompareValues(left, ParseAdditive(), ">=");
                    continue;
                }

                if (TryConsume("<"))
                {
                    left = CompareValues(left, ParseAdditive(), "<");
                    continue;
                }

                if (TryConsume(">"))
                {
                    left = CompareValues(left, ParseAdditive(), ">");
                    continue;
                }

                return left;
            }
        }

        private object? ParseAdditive()
        {
            var left = ParseMultiplicative();
            while (true)
            {
                SkipWhitespace();
                if (TryConsume("+"))
                {
                    left = ConvertToDecimal(left) + ConvertToDecimal(ParseMultiplicative());
                    continue;
                }

                if (TryConsume("-"))
                {
                    left = ConvertToDecimal(left) - ConvertToDecimal(ParseMultiplicative());
                    continue;
                }

                return left;
            }
        }

        private object? ParseMultiplicative()
        {
            var left = ParseUnary();
            while (true)
            {
                SkipWhitespace();
                if (TryConsume("*"))
                {
                    left = ConvertToDecimal(left) * ConvertToDecimal(ParseUnary());
                    continue;
                }

                if (TryConsume("/"))
                {
                    left = ConvertToDecimal(left) / ConvertToDecimal(ParseUnary());
                    continue;
                }

                return left;
            }
        }

        private object? ParseUnary()
        {
            SkipWhitespace();
            if (TryConsume("!"))
            {
                return !IsTruthy(ParseUnary());
            }

            if (TryConsume("-"))
            {
                return -ConvertToDecimal(ParseUnary());
            }

            return ParsePrimary();
        }

        private object? ParsePrimary()
        {
            SkipWhitespace();
            if (TryConsume("("))
            {
                var value = ParseOr();
                TryConsume(")");
                return value;
            }

            if (Peek() == '"')
            {
                return ParseString();
            }

            if (char.IsDigit(Peek()))
            {
                return ParseNumber();
            }

            var token = ParseToken();
            SkipWhitespace();
            if (TryConsume("("))
            {
                var args = new List<object?>();
                SkipWhitespace();
                if (!TryConsume(")"))
                {
                    while (true)
                    {
                        args.Add(ParseOr());
                        SkipWhitespace();
                        if (TryConsume(")"))
                        {
                            break;
                        }

                        TryConsume(",");
                    }
                }

                return ResolveFunction(token, args);
            }

            return ResolveToken(token);
        }

        private object? ResolveFunction(string token, IReadOnlyList<object?> args)
        {
            var normalized = token.Trim();
            if (string.Equals(normalized, "$monster.has_power", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "monster.has_power", StringComparison.OrdinalIgnoreCase))
            {
                var powerId = args.FirstOrDefault()?.ToString() ?? string.Empty;
                return _monster.Creature.Powers.Any(power =>
                    string.Equals(power.Id.Entry, powerId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(power.GetType().Name, powerId, StringComparison.OrdinalIgnoreCase));
            }

            return ResolveToken(normalized);
        }

        private object? ResolveToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            if (bool.TryParse(token, out var boolValue))
            {
                return boolValue;
            }

            if (decimal.TryParse(token, NumberStyles.Number, CultureInfo.InvariantCulture, out var decimalValue))
            {
                return decimalValue;
            }

            if (token.StartsWith("$state.", StringComparison.OrdinalIgnoreCase) ||
                token.StartsWith("state.", StringComparison.OrdinalIgnoreCase))
            {
                var key = token[(token.IndexOf('.') + 1)..];
                return _runtimeState.Variables.TryGetValue(key, out var value) ? value : null;
            }

            if (token.StartsWith("$monster.", StringComparison.OrdinalIgnoreCase) ||
                token.StartsWith("monster.", StringComparison.OrdinalIgnoreCase))
            {
                return ResolveMonsterValue(token[(token.IndexOf('.') + 1)..]);
            }

            return _runtimeState.Variables.TryGetValue(token, out var rawValue) ? rawValue : token.Trim('"');
        }

        private object? ResolveMonsterValue(string memberKey)
        {
            var normalized = NormalizeMemberKey(memberKey);
            if (normalized == "SLOTNAME")
            {
                return _monster.Creature.SlotName ?? string.Empty;
            }

            if (normalized == "CURRENTHP")
            {
                return _monster.Creature.CurrentHp;
            }

            if (normalized == "MAXHP")
            {
                return _monster.Creature.MaxHp;
            }

            if (normalized == "COUNTALLIES")
            {
                return _monster.Creature.CombatState.GetTeammatesOf(_monster.Creature).Count(creature => creature.IsAlive && creature != _monster.Creature);
            }

            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var property = _monster.GetType().GetProperties(flags).FirstOrDefault(candidate => NormalizeMemberKey(candidate.Name) == normalized);
            if (property != null)
            {
                return property.GetValue(_monster);
            }

            var field = _monster.GetType().GetFields(flags).FirstOrDefault(candidate => NormalizeMemberKey(candidate.Name.TrimStart('_')) == normalized);
            if (field != null)
            {
                return field.GetValue(_monster);
            }

            return null;
        }

        private decimal ConvertToDecimal(object? value)
        {
            return TryToDecimal(value, out var decimalValue) ? decimalValue : 0m;
        }

        private string ParseToken()
        {
            SkipWhitespace();
            var start = _index;
            while (_index < _text.Length)
            {
                var current = _text[_index];
                if (char.IsLetterOrDigit(current) || current is '_' or '$' or '.')
                {
                    _index++;
                    continue;
                }

                break;
            }

            return _text[start.._index];
        }

        private string ParseString()
        {
            TryConsume("\"");
            var start = _index;
            while (_index < _text.Length && _text[_index] != '"')
            {
                _index++;
            }

            var result = _text[start.._index];
            TryConsume("\"");
            return result;
        }

        private decimal ParseNumber()
        {
            var start = _index;
            while (_index < _text.Length && (char.IsDigit(_text[_index]) || _text[_index] == '.'))
            {
                _index++;
            }

            return decimal.Parse(_text[start.._index], CultureInfo.InvariantCulture);
        }

        private void SkipWhitespace()
        {
            while (_index < _text.Length && char.IsWhiteSpace(_text[_index]))
            {
                _index++;
            }
        }

        private bool TryConsume(string token)
        {
            SkipWhitespace();
            if (!_text.AsSpan(_index).StartsWith(token, StringComparison.Ordinal))
            {
                return false;
            }

            _index += token.Length;
            return true;
        }

        private char Peek()
        {
            SkipWhitespace();
            return _index < _text.Length ? _text[_index] : '\0';
        }

        private static string NormalizeMemberKey(string value)
        {
            return new string(value.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        }
    }

    private static AbstractIntent BuildIntent(MonsterIntentDeclaration intent)
    {
        return intent.IntentType switch
        {
            MonsterIntentType.SingleAttack => new SingleAttackIntent(ParseIntParameter(intent.Parameters, "amount", 0)),
            MonsterIntentType.MultiAttack => new MultiAttackIntent(ParseIntParameter(intent.Parameters, "amount", 0), ParseIntParameter(intent.Parameters, "repeat", ParseIntParameter(intent.Parameters, "count", 2))),
            MonsterIntentType.Buff => new BuffIntent(),
            MonsterIntentType.Debuff => new DebuffIntent(),
            MonsterIntentType.Defend => new DefendIntent(),
            MonsterIntentType.Summon => new SummonIntent(),
            MonsterIntentType.Status => new StatusIntent(ParseIntParameter(intent.Parameters, "card_count", ParseIntParameter(intent.Parameters, "count", 1))),
            MonsterIntentType.Heal => new HealIntent(),
            MonsterIntentType.CardDebuff => new CardDebuffIntent(),
            _ => new UnknownIntent()
        };
    }

    private static int ParseIntParameter(IReadOnlyDictionary<string, string> parameters, string key, int defaultValue)
    {
        return parameters.TryGetValue(key, out var value) && int.TryParse(value, out var parsed)
            ? parsed
            : defaultValue;
    }
}
