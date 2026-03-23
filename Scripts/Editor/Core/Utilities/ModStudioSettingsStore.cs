using System.Text.Json;
using STS2_Editor.Scripts.Editor.Core.Models;

namespace STS2_Editor.Scripts.Editor.Core.Utilities;

internal static class ModStudioSettingsStore
{
    private const int MaxRecentProjects = 12;

    public static string SettingsFilePath => ModStudioPaths.SettingsFilePath;

    public static ModStudioSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
            {
                return new ModStudioSettings();
            }

            var settings = JsonSerializer.Deserialize<ModStudioSettings>(File.ReadAllText(SettingsFilePath), ModStudioJson.Options);
            return Normalize(settings ?? new ModStudioSettings());
        }
        catch
        {
            return new ModStudioSettings();
        }
    }

    public static void Save(ModStudioSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var normalized = Normalize(settings);
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsFilePath) ?? ModStudioPaths.RootPath);
        ModStudioJson.Save(SettingsFilePath, normalized);
    }

    public static void SetLastProjectPath(string? projectPath)
    {
        var settings = Load();
        settings.LastProjectPath = NormalizeProjectPath(projectPath);
        Save(settings);
    }

    public static void RecordRecentProject(string? projectPath)
    {
        var normalizedPath = NormalizeProjectPath(projectPath);
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return;
        }

        var settings = Load();
        settings.LastProjectPath = normalizedPath;

        var recent = NormalizeRecentPaths(settings.RecentProjectPaths);
        InsertOrMoveToFront(recent, normalizedPath);

        settings.RecentProjectPaths = recent;
        Save(settings);
    }

    public static ModStudioSettings Normalize(ModStudioSettings? settings)
    {
        var normalized = settings ?? new ModStudioSettings();
        normalized.UiLanguageCode = NormalizeLanguageCode(normalized.UiLanguageCode);
        normalized.LastProjectPath = NormalizeProjectPath(normalized.LastProjectPath);
        normalized.RecentProjectPaths = NormalizeRecentPaths(normalized.RecentProjectPaths);
        return normalized;
    }

    private static List<string> NormalizeRecentPaths(IEnumerable<string>? recentPaths)
    {
        var result = new List<string>();
        if (recentPaths == null)
        {
            return result;
        }

        foreach (var path in recentPaths)
        {
            var normalizedPath = NormalizeProjectPath(path);
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                continue;
            }

            if (result.Any(existing => string.Equals(existing, normalizedPath, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            result.Add(normalizedPath);
            if (result.Count >= MaxRecentProjects)
            {
                break;
            }
        }

        return result;
    }

    private static void InsertOrMoveToFront(ICollection<string> target, string? projectPath)
    {
        var normalizedPath = NormalizeProjectPath(projectPath);
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return;
        }

        if (target is List<string> listTarget)
        {
            for (var i = listTarget.Count - 1; i >= 0; i--)
            {
                if (string.Equals(listTarget[i], normalizedPath, StringComparison.OrdinalIgnoreCase))
                {
                    listTarget.RemoveAt(i);
                }
            }

            listTarget.Insert(0, normalizedPath);
            while (listTarget.Count > MaxRecentProjects)
            {
                listTarget.RemoveAt(listTarget.Count - 1);
            }
        }
    }

    private static string NormalizeLanguageCode(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return ModStudioLocalization.ChineseLanguageCode;
        }

        var normalized = languageCode.Trim();
        if (normalized.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
        {
            return ModStudioLocalization.ChineseLanguageCode;
        }

        if (normalized.StartsWith("en", StringComparison.OrdinalIgnoreCase))
        {
            return ModStudioLocalization.EnglishLanguageCode;
        }

        return ModStudioLocalization.ChineseLanguageCode;
    }

    private static string NormalizeProjectPath(string? projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return string.Empty;
        }

        var trimmed = projectPath.Trim();
        try
        {
            var fullPath = Path.GetFullPath(trimmed);
            return Path.TrimEndingDirectorySeparator(fullPath);
        }
        catch
        {
            return trimmed;
        }
    }
}
