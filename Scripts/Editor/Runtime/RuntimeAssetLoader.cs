using Godot;
using STS2_Editor.Scripts.Editor.Core.Utilities;

namespace STS2_Editor.Scripts.Editor.Runtime;

internal static class RuntimeAssetLoader
{
    private static readonly Dictionary<string, Texture2D> TextureCache = new(StringComparer.OrdinalIgnoreCase);

    public static Texture2D? LoadTexture(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            path = ResolveReferencePath(path);
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            if (TextureCache.TryGetValue(path, out var cached) && GodotObject.IsInstanceValid(cached))
            {
                return cached;
            }

            Texture2D? texture = null;
            if (path.StartsWith("res://", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("user://", StringComparison.OrdinalIgnoreCase))
            {
                var loadPath = path;
                if (path.StartsWith("user://", StringComparison.OrdinalIgnoreCase))
                {
                    loadPath = ProjectSettings.GlobalizePath(path);
                }

                texture = ResourceLoader.Load<Texture2D>(loadPath, null, ResourceLoader.CacheMode.Reuse);
            }
            else if (Path.IsPathRooted(path) && File.Exists(path))
            {
                var image = Image.LoadFromFile(path);
                texture = ImageTexture.CreateFromImage(image);
            }

            if (texture != null)
            {
                TextureCache[path] = texture;
            }

            return texture;
        }
        catch
        {
            return null;
        }
    }

    public static string? GetOverriddenPathOrNull(string metadataPath)
    {
        var resolved = ResolveReferencePath(metadataPath);
        return string.IsNullOrWhiteSpace(resolved) ? null : resolved;
    }

    private static string ResolveReferencePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var normalized = ModStudioAssetReference.NormalizeReferencePath(path);
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            return normalized;
        }

        return string.Empty;
    }
}
