using STS2_Editor.Scripts.Editor.Core.Utilities;
using Godot;

namespace STS2_Editor.Scripts.Editor.Core.Utilities;

public static class ModStudioAssetReference
{
    public const string Scheme = "modstudio";
    public const string PackageAssetHost = "asset";

    public static string CreatePackageAssetReference(string packageKey, string assetId, string fileName)
    {
        if (string.IsNullOrWhiteSpace(packageKey))
        {
            throw new ArgumentException("Package key is required.", nameof(packageKey));
        }

        if (string.IsNullOrWhiteSpace(assetId))
        {
            throw new ArgumentException("Asset id is required.", nameof(assetId));
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("File name is required.", nameof(fileName));
        }

        return $"{Scheme}://{PackageAssetHost}/{Uri.EscapeDataString(packageKey)}/{Uri.EscapeDataString(assetId)}/{Uri.EscapeDataString(fileName)}";
    }

    public static bool TryParsePackageAssetReference(string? reference, out string packageKey, out string assetId, out string fileName)
    {
        packageKey = string.Empty;
        assetId = string.Empty;
        fileName = string.Empty;

        if (string.IsNullOrWhiteSpace(reference))
        {
            return false;
        }

        if (!reference.StartsWith($"{Scheme}://", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var uri = reference.Substring(Scheme.Length + 3);
        if (!uri.StartsWith($"{PackageAssetHost}/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var segments = uri.Substring(PackageAssetHost.Length + 1)
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length < 3)
        {
            return false;
        }

        packageKey = Uri.UnescapeDataString(segments[0]);
        assetId = Uri.UnescapeDataString(segments[1]);
        fileName = Uri.UnescapeDataString(string.Join('/', segments.Skip(2)));
        return !string.IsNullOrWhiteSpace(packageKey) &&
               !string.IsNullOrWhiteSpace(assetId) &&
               !string.IsNullOrWhiteSpace(fileName);
    }

    public static bool IsPackageAssetReference(string? reference)
    {
        return TryParsePackageAssetReference(reference, out _, out _, out _);
    }

    public static string? ResolveInstalledPackageAssetPath(string? reference, string? installedPackagesRootPath = null)
    {
        if (!TryParsePackageAssetReference(reference, out var packageKey, out var assetId, out var fileName))
        {
            return null;
        }

        var root = string.IsNullOrWhiteSpace(installedPackagesRootPath)
            ? ModStudioPaths.RuntimePackageCachePath
            : installedPackagesRootPath;
        return Path.Combine(root, packageKey, "assets", assetId, fileName);
    }

    public static string? NormalizeReferencePath(string? path, string? installedPackagesRootPath = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (path.StartsWith("res://", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("user://", StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        if (Path.IsPathRooted(path))
        {
            return TryLocalizeUserPath(path);
        }

        var resolved = ResolveInstalledPackageAssetPath(path, installedPackagesRootPath);
        return resolved is null ? null : TryLocalizeUserPath(resolved);
    }

    private static string TryLocalizeUserPath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var modStudioRoot = Path.GetFullPath(ModStudioPaths.RootPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (fullPath.StartsWith(modStudioRoot, StringComparison.OrdinalIgnoreCase))
        {
            var relativePath = Path.GetRelativePath(modStudioRoot, fullPath)
                .Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/');
            return string.IsNullOrWhiteSpace(relativePath)
                ? "user://sts2_editor"
                : $"user://sts2_editor/{relativePath}";
        }

        var localized = ProjectSettings.LocalizePath(fullPath);
        if (localized.StartsWith("user://", StringComparison.OrdinalIgnoreCase) ||
            localized.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
        {
            return localized;
        }

        return fullPath;
    }
}
