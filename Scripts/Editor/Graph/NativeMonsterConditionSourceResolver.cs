using System.Reflection;
using System.Text.RegularExpressions;
using MegaCrit.Sts2.Core.Models;

namespace STS2_Editor.Scripts.Editor.Graph;

internal static class NativeMonsterConditionSourceResolver
{
    private static readonly Dictionary<string, IReadOnlyList<string>> Cache = new(StringComparer.Ordinal);

    public static bool TryResolveCondition(MonsterModel monster, string branchStateId, int branchIndex, out string condition)
    {
        condition = string.Empty;
        if (monster == null || string.IsNullOrWhiteSpace(branchStateId))
        {
            return false;
        }

        if (string.Equals(monster.Id.Entry, "QUEEN", StringComparison.OrdinalIgnoreCase))
        {
            if ((string.Equals(branchStateId, "YOURE_MINE_NOW_BRANCH", StringComparison.Ordinal) ||
                 string.Equals(branchStateId, "BURN_BRIGHT_FOR_ME_BRANCH", StringComparison.Ordinal)) &&
                branchIndex is 0 or 1)
            {
                condition = branchIndex == 0 ? "!$monster.has_amalgam_died" : "$monster.has_amalgam_died";
                return true;
            }
        }

        var cacheKey = $"{monster.GetType().FullName}:{branchStateId}";
        if (!Cache.TryGetValue(cacheKey, out var expressions))
        {
            expressions = ExtractBranchConditions(monster.GetType(), branchStateId);
            Cache[cacheKey] = expressions;
        }

        if (branchIndex < 0 || branchIndex >= expressions.Count)
        {
            return false;
        }

        condition = TranslateConditionExpression(monster.GetType(), expressions[branchIndex]);
        return !string.IsNullOrWhiteSpace(condition);
    }

    private static IReadOnlyList<string> ExtractBranchConditions(Type monsterType, string branchStateId)
    {
        var sourcePath = ResolveMonsterSourcePath(monsterType);
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            return Array.Empty<string>();
        }

        var lines = File.ReadAllLines(sourcePath);
        string? variableName = null;
        var declarationPattern = new Regex($@"ConditionalBranchState\s+(?<var>\w+)\s*=.*new\s+ConditionalBranchState\(""{Regex.Escape(branchStateId)}""\)", RegexOptions.Compiled);
        for (var index = 0; index < lines.Length; index++)
        {
            var match = declarationPattern.Match(lines[index]);
            if (!match.Success)
            {
                continue;
            }

            variableName = match.Groups["var"].Value;
            var results = new List<string>();
            for (var scan = index + 1; scan < lines.Length; scan++)
            {
                var trimmed = lines[scan].Trim();
                if (trimmed.StartsWith("return new MonsterMoveStateMachine", StringComparison.Ordinal))
                {
                    break;
                }

                if (!trimmed.Contains($"{variableName}.AddState(", StringComparison.Ordinal))
                {
                    continue;
                }

                var statement = trimmed;
                while (!statement.Contains(");", StringComparison.Ordinal) && scan + 1 < lines.Length)
                {
                    scan++;
                    statement += " " + lines[scan].Trim();
                }

                var conditionMatch = Regex.Match(statement, @"\(\)\s*=>\s*(.+)\)\s*;", RegexOptions.Compiled);
                if (conditionMatch.Success)
                {
                    results.Add(conditionMatch.Groups[1].Value.Trim());
                }
            }

            return results;
        }

        return Array.Empty<string>();
    }

    private static string TranslateConditionExpression(Type monsterType, string rawExpression)
    {
        var expression = rawExpression.Trim();
        if (string.IsNullOrWhiteSpace(expression))
        {
            return string.Empty;
        }

        expression = Regex.Replace(
            expression,
            @"\(\(\w+\)base\.Creature\.Monster\)\.(\w+)",
            match => $"$monster.{ToSnakeCase(match.Groups[1].Value)}",
            RegexOptions.Compiled);

        expression = Regex.Replace(
            expression,
            @"base\.Creature\.HasPower<(\w+)>\(\)",
            match => $"$monster.has_power(\"{ResolveModelIdEntry(match.Groups[1].Value, typeof(PowerModel))}\")",
            RegexOptions.Compiled);

        expression = expression.Replace("base.Creature.SlotName", "$monster.slot_name", StringComparison.Ordinal);
        expression = expression.Replace("base.Creature.CurrentHp", "$monster.current_hp", StringComparison.Ordinal);
        expression = expression.Replace("base.Creature.MaxHp", "$monster.max_hp", StringComparison.Ordinal);
        expression = expression.Replace("base.Creature", "$monster.creature", StringComparison.Ordinal);
        expression = expression.Replace("base.Creature.Monster", "$monster", StringComparison.Ordinal);
        expression = expression.Replace("GetAllyCount()", "$monster.count_allies", StringComparison.Ordinal);

        var memberNames = monsterType
            .GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(member => member.MemberType is MemberTypes.Field or MemberTypes.Property)
            .Select(member => member.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(name => name.Length)
            .ToList();

        foreach (var memberName in memberNames)
        {
            expression = Regex.Replace(
                expression,
                $@"(?<![\w\.$]){Regex.Escape(memberName)}(?![\w\(])",
                $"$monster.{ToSnakeCase(memberName.TrimStart('_'))}",
                RegexOptions.Compiled);
        }

        return expression;
    }

    private static string ResolveModelIdEntry(string typeName, Type baseType)
    {
        var type = ModelDb.AllAbstractModelSubtypes.FirstOrDefault(candidate =>
            candidate.IsSubclassOf(baseType) &&
            string.Equals(candidate.Name, typeName, StringComparison.OrdinalIgnoreCase));
        return type == null ? typeName : ModelDb.GetId(type).Entry;
    }

    private static string? ResolveMonsterSourcePath(Type monsterType)
    {
        var fileName = $"{monsterType.Name}.cs";
        foreach (var start in EnumerateSearchRoots())
        {
            for (var current = new DirectoryInfo(start); current != null; current = current.Parent)
            {
                var candidate = Path.Combine(current.FullName, "STS2_Proj", "src", "Core", "Models", "Monsters", fileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateSearchRoots()
    {
        yield return Environment.CurrentDirectory;
        yield return AppContext.BaseDirectory;
        yield return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppContext.BaseDirectory;
    }

    private static string ToSnakeCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var snake = Regex.Replace(value, "([a-z0-9])([A-Z])", "$1_$2");
        return snake.Replace("__", "_", StringComparison.Ordinal).ToLowerInvariant();
    }
}
