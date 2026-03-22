using System.Security.Cryptography;
using System.Text;
using STS2_Editor.Scripts.Editor.Core.Models;
using STS2_Editor.Scripts.Editor.Core.Utilities;
using STS2_Editor.Scripts.Editor.Packaging;

namespace STS2_Editor.Scripts.Editor.Runtime;

public sealed class RuntimePackageCatalog
{
    private readonly PackageArchiveService _archiveService;
    private readonly string _installedPackagesRootPath;

    public RuntimePackageCatalog(PackageArchiveService archiveService)
    {
        _archiveService = archiveService ?? throw new ArgumentNullException(nameof(archiveService));
        _installedPackagesRootPath = ModStudioPaths.InstalledPackagesPath;
    }

    public IReadOnlyList<RuntimeInstalledPackage> DiscoverInstalledPackages()
    {
        if (!Directory.Exists(_installedPackagesRootPath))
        {
            return Array.Empty<RuntimeInstalledPackage>();
        }

        var packages = new List<RuntimeInstalledPackage>();
        foreach (var sourcePath in EnumeratePackageSources())
        {
            if (TryLoadPackage(sourcePath, out var package))
            {
                packages.Add(package);
            }
        }

        return packages
            .OrderBy(package => package.Manifest.PackageKey, StringComparer.Ordinal)
            .ThenBy(package => package.PackageFilePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public bool TryLoadPackage(string packagePath, out RuntimeInstalledPackage package)
    {
        package = new RuntimeInstalledPackage();

        if (string.IsNullOrWhiteSpace(packagePath))
        {
            return false;
        }

        if (!File.Exists(packagePath) && !Directory.Exists(packagePath))
        {
            return false;
        }

        if (TryLoadArchivePackage(packagePath, out package))
        {
            return true;
        }

        return TryLoadDirectoryPackage(packagePath, out package);
    }

    private IEnumerable<string> EnumeratePackageSources()
    {
        foreach (var file in Directory.EnumerateFiles(_installedPackagesRootPath, "*.*", SearchOption.AllDirectories))
        {
            var extension = Path.GetExtension(file);
            if (extension.Equals(".sts2pack", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                yield return file;
            }
        }

        foreach (var directory in Directory.EnumerateDirectories(_installedPackagesRootPath, "*", SearchOption.AllDirectories))
        {
            if (File.Exists(Path.Combine(directory, "manifest.json")) &&
                File.Exists(Path.Combine(directory, "project.json")))
            {
                yield return directory;
            }
        }
    }

    private bool TryLoadArchivePackage(string packagePath, out RuntimeInstalledPackage package)
    {
        package = new RuntimeInstalledPackage();
        if (!_archiveService.TryImport(packagePath, out var manifest, out var project) ||
            manifest is null ||
            project is null)
        {
            return false;
        }

        package.Manifest = manifest;
        package.Project = project;
        package.PackageFilePath = Path.GetFullPath(packagePath);
        package.PackageKey = manifest.PackageKey;
        package.PackageId = manifest.PackageId;
        package.Version = manifest.Version;
        package.DisplayName = manifest.DisplayName;
        package.Checksum = NormalizeChecksum(manifest.Checksum, package.Manifest, package.Project);
        package.IsDirectoryArchive = false;
        return true;
    }

    private bool TryLoadDirectoryPackage(string packagePath, out RuntimeInstalledPackage package)
    {
        package = new RuntimeInstalledPackage();
        var manifestPath = Path.Combine(packagePath, "manifest.json");
        var projectPath = Path.Combine(packagePath, "project.json");

        if (!File.Exists(manifestPath) || !File.Exists(projectPath))
        {
            return false;
        }

        var manifest = ModStudioJson.LoadOrDefault(manifestPath, () => new EditorPackageManifest());
        var project = ModStudioJson.LoadOrDefault(projectPath, () => new EditorProject());

        package.Manifest = manifest;
        package.Project = project;
        package.PackageFilePath = Path.GetFullPath(packagePath);
        package.PackageKey = manifest.PackageKey;
        package.PackageId = manifest.PackageId;
        package.Version = manifest.Version;
        package.DisplayName = manifest.DisplayName;
        package.Checksum = NormalizeChecksum(manifest.Checksum, package.Manifest, package.Project);
        package.IsDirectoryArchive = true;
        return true;
    }

    private static string NormalizeChecksum(string checksum, EditorPackageManifest manifest, EditorProject project)
    {
        if (!string.IsNullOrWhiteSpace(checksum))
        {
            return checksum.Trim();
        }

        using var sha = SHA256.Create();
        var payload = ModStudioJson.Serialize(new
        {
            manifest.PackageId,
            manifest.DisplayName,
            manifest.Version,
            manifest.Author,
            manifest.Description,
            project.Overrides,
            project.Graphs,
            project.ProjectAssets
        });
        var bytes = Encoding.UTF8.GetBytes(payload);
        return Convert.ToHexString(sha.ComputeHash(bytes)).ToLowerInvariant();
    }
}
