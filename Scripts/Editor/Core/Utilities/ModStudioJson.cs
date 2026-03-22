using System.Text.Json;
using System.Text.Json.Serialization;

namespace STS2_Editor.Scripts.Editor.Core.Utilities;

public static class ModStudioJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static T LoadOrDefault<T>(string path, Func<T> fallbackFactory)
    {
        if (!File.Exists(path))
        {
            return fallbackFactory();
        }

        string json = File.ReadAllText(path);
        T? value = JsonSerializer.Deserialize<T>(json, Options);
        return value ?? fallbackFactory();
    }

    public static void Save<T>(string path, T value)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = JsonSerializer.Serialize(value, Options);
        File.WriteAllText(path, json);
    }

    public static string Serialize<T>(T value)
    {
        return JsonSerializer.Serialize(value, Options);
    }
}
