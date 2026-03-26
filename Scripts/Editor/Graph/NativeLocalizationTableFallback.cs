using System.Text.Json;
using MegaCrit.Sts2.Core.Localization;

namespace STS2_Editor.Scripts.Editor.Graph;

internal static class NativeLocalizationTableFallback
{
    private static readonly Dictionary<string, IReadOnlyDictionary<string, string>> CachedTables = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object CacheLock = new();

    public static string TryGetText(LocString? locString)
    {
        if (locString == null)
        {
            return string.Empty;
        }

        try
        {
            var formatted = locString.GetFormattedText();
            if (!string.IsNullOrWhiteSpace(formatted))
            {
                return formatted.Trim();
            }
        }
        catch
        {
        }

        try
        {
            var raw = locString.GetRawText();
            if (!string.IsNullOrWhiteSpace(raw))
            {
                return raw.Trim();
            }
        }
        catch
        {
        }

        return TryGetTableValue(locString.LocTable, locString.LocEntryKey);
    }

    public static IReadOnlyDictionary<string, string> GetTableEntries(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            return EmptyTable;
        }

        lock (CacheLock)
        {
            if (CachedTables.TryGetValue(tableName, out var cached))
            {
                return cached;
            }

            var loaded = LoadTableEntries(tableName);
            CachedTables[tableName] = loaded;
            return loaded;
        }
    }

    private static string TryGetTableValue(string tableName, string entryKey)
    {
        if (string.IsNullOrWhiteSpace(tableName) || string.IsNullOrWhiteSpace(entryKey))
        {
            return string.Empty;
        }

        var table = GetTableEntries(tableName);
        return table.TryGetValue(entryKey, out var value)
            ? value
            : string.Empty;
    }

    private static IReadOnlyDictionary<string, string> LoadTableEntries(string tableName)
    {
        foreach (var root in EnumerateRootCandidates())
        {
            foreach (var language in new[] { "eng", "zhs" })
            {
                var filePath = Path.Combine(root, "STS2_Proj", "localization", language, $"{tableName}.json");
                if (!File.Exists(filePath))
                {
                    continue;
                }

                try
                {
                    var json = File.ReadAllText(filePath);
                    var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (parsed != null)
                    {
                        return parsed;
                    }
                }
                catch
                {
                }
            }
        }

        return EmptyTable;
    }

    private static IEnumerable<string> EnumerateRootCandidates()
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            if (string.IsNullOrWhiteSpace(start))
            {
                continue;
            }

            var directory = new DirectoryInfo(Path.GetFullPath(start));
            while (directory != null)
            {
                if (visited.Add(directory.FullName) &&
                    File.Exists(Path.Combine(directory.FullName, "STS2_Editor.csproj")) &&
                    Directory.Exists(Path.Combine(directory.FullName, "STS2_Proj", "localization")))
                {
                    yield return directory.FullName;
                }

                directory = directory.Parent;
            }
        }
    }

    private static IReadOnlyDictionary<string, string> EmptyTable { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
