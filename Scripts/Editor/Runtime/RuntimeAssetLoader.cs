using Godot;
using MegaCrit.Sts2.Core.Logging;
using STS2_Editor.Scripts.Editor.Core.Utilities;

namespace STS2_Editor.Scripts.Editor.Runtime;

internal static class RuntimeAssetLoader
{
    private static readonly Dictionary<string, Texture2D> TextureCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> LoggedSuccessfulManagedLoads = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> LoggedFailedManagedLoads = new(StringComparer.OrdinalIgnoreCase);
    private const string ManagedUserRoot = "user://sts2_editor/";

    public static Texture2D? LoadTexture(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            var originalPath = path;
            var isManagedAssetReference = IsManagedAssetReference(originalPath);
            path = ResolveReferencePath(path);
            if (string.IsNullOrWhiteSpace(path))
            {
                TryLogManagedFailure(originalPath, path, isManagedAssetReference);
                return null;
            }

            if (TextureCache.TryGetValue(path, out var cached) && GodotObject.IsInstanceValid(cached))
            {
                TryLogManagedSuccess(originalPath, path, isManagedAssetReference, fromCache: true);
                return cached;
            }

            Texture2D? texture = null;
            if (path.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
            {
                texture = ResourceLoader.Load<Texture2D>(path, null, ResourceLoader.CacheMode.Reuse);
            }
            else if (path.StartsWith("user://", StringComparison.OrdinalIgnoreCase))
            {
                var image = Image.LoadFromFile(ProjectSettings.GlobalizePath(path));
                texture = ImageTexture.CreateFromImage(image);
            }
            else if (Path.IsPathRooted(path) && File.Exists(path))
            {
                var image = Image.LoadFromFile(path);
                texture = ImageTexture.CreateFromImage(image);
            }

            if (texture != null)
            {
                TextureCache[path] = texture;
                TryLogManagedSuccess(originalPath, path, isManagedAssetReference, fromCache: false);
            }
            else
            {
                TryLogManagedFailure(originalPath, path, isManagedAssetReference);
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

    private static void TryLogManagedSuccess(string originalPath, string resolvedPath, bool isManagedAssetReference, bool fromCache)
    {
        if (!isManagedAssetReference || !LoggedSuccessfulManagedLoads.Add(originalPath))
        {
            return;
        }

        var sizeSuffix = string.Empty;
        if (TextureCache.TryGetValue(resolvedPath, out var cachedTexture) && GodotObject.IsInstanceValid(cachedTexture))
        {
            var size = cachedTexture.GetSize();
            sizeSuffix = $" size={size.X:0}x{size.Y:0}";
        }

        Log.Info($"[ModStudio.Asset] Loaded managed texture (cache={fromCache}): {originalPath} -> {resolvedPath}{sizeSuffix}");
    }

    private static void TryLogManagedFailure(string originalPath, string resolvedPath, bool isManagedAssetReference)
    {
        if (!isManagedAssetReference || !LoggedFailedManagedLoads.Add(originalPath))
        {
            return;
        }

        var suffix = string.IsNullOrWhiteSpace(resolvedPath) ? "<unresolved>" : resolvedPath;
        Log.Warn($"[ModStudio.Asset] Failed to load managed texture: {originalPath} -> {suffix}");
    }

    private static bool IsManagedAssetReference(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (ModStudioAssetReference.IsPackageAssetReference(path))
        {
            return true;
        }

        if (path.StartsWith(ManagedUserRoot, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!Path.IsPathRooted(path))
        {
            return false;
        }

        var fullPath = Path.GetFullPath(path);
        var modStudioRoot = Path.GetFullPath(ModStudioPaths.RootPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(modStudioRoot, StringComparison.OrdinalIgnoreCase);
    }
}
