using Godot;
using STS2_Editor.Scripts.Editor.Core.Utilities;

namespace STS2_Editor.Scripts.Editor.Runtime;

internal sealed class PublishedPackageLocator
{
    private const string PackageExtension = ".sts2pack";

    public string PublishedPackagesRootPath => ModStudioPaths.PublishedPackagesRootPath;

    public bool TryGetPublishedPackagesRootPath(out string rootPath)
    {
        rootPath = NormalizePath(PublishedPackagesRootPath);
        return Directory.Exists(rootPath);
    }

    public IReadOnlyList<string> DiscoverPublishedPackageFiles()
    {
        var rootPath = NormalizePath(PublishedPackagesRootPath);
        if (!Directory.Exists(rootPath))
        {
            return Array.Empty<string>();
        }

        return Directory.EnumerateFiles(rootPath, $"*{PackageExtension}", SearchOption.AllDirectories)
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path.Trim());
    }
}
