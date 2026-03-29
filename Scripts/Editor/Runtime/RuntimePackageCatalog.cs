using System.Security.Cryptography;
using System.Text;
using STS2_Editor.Scripts.Editor.Core.Models;
using STS2_Editor.Scripts.Editor.Core.Utilities;
using STS2_Editor.Scripts.Editor.Packaging;

namespace STS2_Editor.Scripts.Editor.Runtime;

public sealed class RuntimePackageCatalog
{
    private readonly PackageArchiveService _archiveService;
    private readonly PublishedPackageLocator _locator;

    public RuntimePackageCatalog(PackageArchiveService archiveService)
    {
        _archiveService = archiveService ?? throw new ArgumentNullException(nameof(archiveService));
        _locator = new PublishedPackageLocator();
    }

    public IReadOnlyList<RuntimeInstalledPackage> DiscoverInstalledPackages()
    {
        var packages = new List<RuntimeInstalledPackage>();
        foreach (var sourcePath in _locator.DiscoverPublishedPackageFiles())
        {
            if (TryLoadPackage(sourcePath, out var package))
            {
                packages.Add(package);
            }
        }

        return packages
            .GroupBy(package => package.PackageKey, StringComparer.Ordinal)
            .Select(group => group
                .OrderByDescending(package => File.Exists(package.PackageFilePath)
                    ? File.GetLastWriteTimeUtc(package.PackageFilePath)
                    : DateTime.MinValue)
                .ThenBy(package => package.PackageFilePath, StringComparer.OrdinalIgnoreCase)
                .First())
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

        return TryLoadArchivePackage(packagePath, out package);
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

        var normalizedProject = _archiveService.NormalizeImportedProject(
            manifest,
            project,
            ModStudioPaths.RuntimePackageCachePath);

        package.Manifest = manifest;
        package.Project = normalizedProject;
        package.PackageFilePath = Path.GetFullPath(packagePath);
        package.PackageKey = manifest.PackageKey;
        package.PackageId = manifest.PackageId;
        package.Version = manifest.Version;
        package.DisplayName = manifest.DisplayName;
        package.Checksum = NormalizeChecksum(manifest.Checksum, package.Manifest, package.Project);
        package.IsDirectoryArchive = false;
        ExtractManagedAssets(packagePath, manifest, normalizedProject);
        return true;
    }

    private void ExtractManagedAssets(string packagePath, EditorPackageManifest manifest, EditorProject project)
    {
        var cacheDirectory = ModStudioPaths.GetRuntimePackageDirectory(manifest.PackageKey);
        try
        {
            if (Directory.Exists(cacheDirectory))
            {
                Directory.Delete(cacheDirectory, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup; stale cache files will be overwritten when possible.
        }

        _archiveService.ExtractManagedAssets(packagePath, manifest, project, ModStudioPaths.RuntimePackageCachePath);
    }

    private static string NormalizeChecksum(string checksum, EditorPackageManifest manifest, EditorProject project)
    {
        if (!string.IsNullOrWhiteSpace(checksum))
        {
            return checksum.Trim();
        }

        using var sha = SHA256.Create();
        var portableProject = CreatePortableChecksumProject(project);
        var payload = ModStudioJson.Serialize(new
        {
            manifest.PackageId,
            manifest.DisplayName,
            manifest.Version,
            manifest.Author,
            manifest.Description,
            manifest.EditorVersion,
            manifest.TargetGameVersion,
            portableProject.SourceOfTruthIsRuntimeModelDb,
            portableProject.Overrides,
            portableProject.Graphs,
            portableProject.ProjectAssets
        });
        var bytes = Encoding.UTF8.GetBytes(payload);
        return Convert.ToHexString(sha.ComputeHash(bytes)).ToLowerInvariant();
    }

    private static EditorProject CreatePortableChecksumProject(EditorProject source)
    {
        var project = new EditorProject
        {
            Manifest = source.Manifest,
            Overrides = source.Overrides.Select(CloneEnvelope).ToList(),
            Graphs = source.Graphs.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            ProjectAssets = source.ProjectAssets.Select(CloneAsset).ToList(),
            SourceOfTruthIsRuntimeModelDb = source.SourceOfTruthIsRuntimeModelDb
        };

        foreach (var asset in project.ProjectAssets)
        {
            if (!string.Equals(asset.SourceType, "res", StringComparison.OrdinalIgnoreCase))
            {
                asset.SourcePath = string.Empty;
                asset.ManagedPath = string.Empty;
            }
        }

        foreach (var envelope in project.Overrides)
        {
            foreach (var asset in envelope.Assets)
            {
                if (!string.Equals(asset.SourceType, "res", StringComparison.OrdinalIgnoreCase))
                {
                    asset.SourcePath = string.Empty;
                    asset.ManagedPath = string.Empty;
                }
            }
        }

        return project;
    }

    private static EntityOverrideEnvelope CloneEnvelope(EntityOverrideEnvelope source)
    {
        return new EntityOverrideEnvelope
        {
            EntityKind = source.EntityKind,
            EntityId = source.EntityId,
            BehaviorSource = source.BehaviorSource,
            GraphId = source.GraphId,
            MonsterAi = MonsterAiDefinitionCloner.Clone(source.MonsterAi),
            Notes = source.Notes,
            Metadata = new Dictionary<string, string>(source.Metadata, StringComparer.Ordinal),
            Assets = source.Assets.Select(CloneAsset).ToList()
        };
    }

    private static AssetRef CloneAsset(AssetRef source)
    {
        return new AssetRef
        {
            Id = source.Id,
            SourceType = source.SourceType,
            LogicalRole = source.LogicalRole,
            SourcePath = source.SourcePath,
            ManagedPath = source.ManagedPath,
            PackagePath = source.PackagePath,
            FileName = source.FileName
        };
    }
}
