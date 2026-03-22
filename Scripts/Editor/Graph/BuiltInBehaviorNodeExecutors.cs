using System.Globalization;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

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
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("value.compare", ExecuteCompareAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("debug.log", ExecuteLogAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("combat.damage", ExecuteDamageAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("combat.gain_block", ExecuteGainBlockAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("combat.heal", ExecuteHealAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("combat.draw_cards", ExecuteDrawCardsAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("combat.apply_power", ExecuteApplyPowerAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("player.gain_energy", ExecuteGainEnergyAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("player.gain_stars", ExecuteGainStarsAsync));
        registry.RegisterExecutor(new DelegateBehaviorNodeExecutor("player.gain_gold", ExecuteGainGoldAsync));
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

        var amount = context.ResolveDecimal(GetProperty(node, "amount"));
        var props = ParseValueProps(GetProperty(node, "props"));
        await CreatureCmd.Damage(context.ChoiceContext, targets, amount, props, context.Owner?.Creature, context.Card);
    }

    private static async Task ExecuteGainBlockAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context)
    {
        var targets = context.ResolveTargets(GetProperty(node, "target", "self"));
        if (targets.Count == 0)
        {
            return;
        }

        var amount = context.ResolveDecimal(GetProperty(node, "amount"));
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

        var amount = context.ResolveDecimal(GetProperty(node, "amount"));
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

        var amount = context.ResolveDecimal(GetProperty(node, "amount", "1"));
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

        var canonicalPower = ModelDb.AllPowers.FirstOrDefault(power => string.Equals(power.Id.Entry, powerId, StringComparison.Ordinal));
        if (canonicalPower == null)
        {
            Log.Warn($"Mod Studio graph '{context.Graph?.GraphId}' could not resolve power '{powerId}'.");
            return;
        }

        var amount = context.ResolveDecimal(GetProperty(node, "amount", "1"));
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

        var amount = context.ResolveDecimal(GetProperty(node, "amount", "1"));
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

        var amount = context.ResolveDecimal(GetProperty(node, "amount", "1"));
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

        var amount = context.ResolveDecimal(GetProperty(node, "amount", "1"));
        if (amount <= 0m)
        {
            return;
        }

        await PlayerCmd.GainGold(amount, context.Owner);
    }

    private static string GetProperty(BehaviorGraphNodeDefinition node, string key, string defaultValue = "")
    {
        return node.Properties.TryGetValue(key, out var value) ? value : defaultValue;
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
