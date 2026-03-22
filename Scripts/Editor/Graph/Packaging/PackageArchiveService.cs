using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using STS2_Editor.Scripts.Editor.Core.Models;
using STS2_Editor.Scripts.Editor.Core.Utilities;

namespace STS2_Editor.Scripts.Editor.Packaging;

public sealed class PackageArchiveService
{
    private const string ArchiveManifestEntry = "manifest.json";
    private const string ArchiveProjectEntry = "project.json";
    private const string ArchiveAssetsRoot = "assets";

    public EditorPackageManifest CreateManifest(EditorProject project, PackageExportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        options ??= new PackageExportOptions();

        var packageId = string.IsNullOrWhiteSpace(options.PackageId)
            ? project.Manifest.ProjectId
            : options.PackageId;
        var displayName = string.IsNullOrWhiteSpace(options.DisplayName)
            ? project.Manifest.Name
            : options.DisplayName;

        var manifest = new EditorPackageManifest
        {
            PackageId = packageId,
            DisplayName = displayName,
            Version = string.IsNullOrWhiteSpace(options.Version) ? "1.0.0" : options.Version,
            Author = string.IsNullOrWhiteSpace(options.Author) ? project.Manifest.Author : options.Author,
            Description = string.IsNullOrWhiteSpace(options.Description) ? project.Manifest.Description : options.Description,
            EditorVersion = string.IsNullOrWhiteSpace(options.EditorVersion) ? project.Manifest.EditorVersion : options.EditorVersion,
            TargetGameVersion = string.IsNullOrWhiteSpace(options.TargetGameVersion) ? project.Manifest.TargetGameVersion : options.TargetGameVersion,
            ExportedAtUtc = DateTimeOffset.UtcNow,
            OverrideCount = project.Overrides.Count,
            GraphCount = project.Graphs.Count,
            AssetCount = CountAssets(project)
        };

        manifest.Checksum = ComputeChecksum(manifest, project);
        return manifest;
    }

    public byte[] Export(EditorPackageManifest manifest, EditorProject project)
    {
        return Export(manifest, project, includeManagedAssets: true);
    }

    public byte[] Export(EditorPackageManifest manifest, EditorProject project, bool includeManagedAssets)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(project);

        var exportProject = CloneProjectForArchive(project, manifest.PackageKey);
        var exportMetadataProject = CloneProjectForArchiveMetadata(exportProject);
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true, entryNameEncoding: Encoding.UTF8))
        {
            WriteJsonEntry(archive, ArchiveManifestEntry, manifest);
            WriteJsonEntry(archive, ArchiveProjectEntry, exportMetadataProject);
            if (includeManagedAssets)
            {
                WriteAssetEntries(archive, exportProject);
            }
        }

        return output.ToArray();
    }

    public string Export(EditorProject project, PackageExportOptions? options = null, string? packageFilePath = null)
    {
        var manifest = CreateManifest(project, options);
        var fileName = PackagingPathUtility.NormalizeFileName($"{manifest.DisplayName}-{manifest.Version}");
        var targetPath = packageFilePath;
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            targetPath = Path.Combine(ModStudioPaths.ExportsPath, fileName);
        }

        targetPath = PackagingPathUtility.EnsureArchiveExtension(targetPath);
        Export(targetPath, manifest, project, options?.IncludeManagedAssets ?? true);
        return targetPath;
    }

    public void Export(string packageFilePath, EditorPackageManifest manifest, EditorProject project, bool includeManagedAssets = true)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(packageFilePath) ?? ".");
        File.WriteAllBytes(packageFilePath, Export(manifest, project, includeManagedAssets));
    }

    public bool TryImport(string packageFilePath, out EditorPackageManifest? manifest, out EditorProject? project)
    {
        manifest = null;
        project = null;

        if (!File.Exists(packageFilePath))
        {
            return false;
        }

        using var archive = ZipFile.OpenRead(packageFilePath);
        manifest = ReadJsonEntry<EditorPackageManifest>(archive, ArchiveManifestEntry);
        project = ReadJsonEntry<EditorProject>(archive, ArchiveProjectEntry);
        return manifest is not null && project is not null;
    }

    public string ComputeChecksum(EditorPackageManifest manifest, EditorProject project)
    {
        using var sha = SHA256.Create();
        var portableProject = CreatePortableChecksumProject(project, manifest.PackageKey);
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

        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    private static void WriteJsonEntry<T>(ZipArchive archive, string entryName, T value)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: false);
        writer.Write(ModStudioJson.Serialize(value));
    }

    private static T? ReadJsonEntry<T>(ZipArchive archive, string entryName)
    {
        var entry = archive.GetEntry(entryName);
        if (entry is null)
        {
            return default;
        }

        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var json = reader.ReadToEnd();
        return JsonSerializer.Deserialize<T>(json, ModStudioJson.Options);
    }

    public EditorProject NormalizeImportedProject(EditorPackageManifest manifest, EditorProject project, string? installedPackagesRootPath = null)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(project);

        var normalized = CloneProjectForArchive(project, manifest.PackageKey);
        foreach (var asset in normalized.ProjectAssets.Where(ShouldTreatAsManagedAsset))
        {
            asset.ManagedPath = EnsureInstalledAssetPath(manifest.PackageKey, asset, installedPackagesRootPath);
            asset.SourcePath = asset.PackagePath;
        }

        foreach (var envelope in normalized.Overrides)
        {
            foreach (var asset in envelope.Assets.Where(ShouldTreatAsManagedAsset))
            {
                asset.ManagedPath = EnsureInstalledAssetPath(manifest.PackageKey, asset, installedPackagesRootPath);
                asset.SourcePath = asset.PackagePath;
            }
        }

        return normalized;
    }

    public void ExtractManagedAssets(string packageFilePath, EditorPackageManifest manifest, EditorProject project, string? installedPackagesRootPath = null)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(project);

        if (!File.Exists(packageFilePath))
        {
            return;
        }

        var destinationRoot = string.IsNullOrWhiteSpace(installedPackagesRootPath)
            ? ModStudioPaths.GetInstalledPackageAssetsDirectory(manifest.PackageKey)
            : Path.Combine(installedPackagesRootPath, manifest.PackageKey, "assets");
        Directory.CreateDirectory(destinationRoot);

        using var archive = ZipFile.OpenRead(packageFilePath);
        foreach (var asset in EnumeratePackedAssets(project))
        {
            if (!ShouldTreatAsManagedAsset(asset) || string.IsNullOrWhiteSpace(asset.PackagePath))
            {
                continue;
            }

            var archiveEntry = archive.GetEntry(PackagingPathUtility.NormalizeArchivePath(asset.PackagePath));
            if (archiveEntry is null)
            {
                continue;
            }

            var assetFilePath = EnsureInstalledAssetPath(manifest.PackageKey, asset, installedPackagesRootPath);
            Directory.CreateDirectory(Path.GetDirectoryName(assetFilePath) ?? destinationRoot);
            using var input = archiveEntry.Open();
            using var output = File.Create(assetFilePath);
            input.CopyTo(output);
        }
    }

    private static EditorProject CloneProjectForArchive(EditorProject source, string packageKey)
    {
        var project = new EditorProject
        {
            Manifest = new EditorProjectManifest
            {
                ProjectId = source.Manifest.ProjectId,
                Name = source.Manifest.Name,
                Author = source.Manifest.Author,
                Description = source.Manifest.Description,
                EditorVersion = source.Manifest.EditorVersion,
                TargetGameVersion = source.Manifest.TargetGameVersion,
                CreatedAtUtc = source.Manifest.CreatedAtUtc,
                UpdatedAtUtc = source.Manifest.UpdatedAtUtc
            },
            Overrides = source.Overrides.Select(CloneEnvelope).ToList(),
            Graphs = source.Graphs.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.Ordinal),
            ProjectAssets = source.ProjectAssets.Select(CloneAsset).ToList(),
            SourceOfTruthIsRuntimeModelDb = source.SourceOfTruthIsRuntimeModelDb
        };

        foreach (var asset in project.ProjectAssets.Where(ShouldPackManagedAsset))
        {
            asset.PackagePath = BuildPackageAssetReference(packageKey, asset);
        }

        foreach (var envelope in project.Overrides)
        {
            foreach (var asset in envelope.Assets.Where(ShouldPackManagedAsset))
            {
                asset.PackagePath = BuildPackageAssetReference(packageKey, asset);
            }
        }

        RewriteMetadataAssetPaths(project);

        return project;
    }

    private static EditorProject CreatePortableChecksumProject(EditorProject source, string packageKey)
    {
        var project = CloneProjectForArchive(source, packageKey);
        foreach (var asset in project.ProjectAssets)
        {
            if (ShouldPackManagedAsset(asset))
            {
                asset.ManagedPath = string.Empty;
                asset.SourcePath = string.Empty;
            }
        }

        foreach (var envelope in project.Overrides)
        {
            foreach (var asset in envelope.Assets)
            {
                if (ShouldPackManagedAsset(asset))
                {
                    asset.ManagedPath = string.Empty;
                    asset.SourcePath = string.Empty;
                }
            }
        }

        return project;
    }

    private static EditorProject CloneProjectForArchiveMetadata(EditorProject source)
    {
        var project = new EditorProject
        {
            Manifest = new EditorProjectManifest
            {
                ProjectId = source.Manifest.ProjectId,
                Name = source.Manifest.Name,
                Author = source.Manifest.Author,
                Description = source.Manifest.Description,
                EditorVersion = source.Manifest.EditorVersion,
                TargetGameVersion = source.Manifest.TargetGameVersion,
                CreatedAtUtc = source.Manifest.CreatedAtUtc,
                UpdatedAtUtc = source.Manifest.UpdatedAtUtc
            },
            Overrides = source.Overrides.Select(CloneEnvelopeForMetadata).ToList(),
            Graphs = source.Graphs.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.Ordinal),
            ProjectAssets = source.ProjectAssets.Select(CloneAssetForMetadata).ToList(),
            SourceOfTruthIsRuntimeModelDb = source.SourceOfTruthIsRuntimeModelDb
        };

        return project;
    }

    private static void WriteAssetEntries(ZipArchive archive, EditorProject project)
    {
        foreach (var asset in EnumeratePackedAssets(project))
        {
            if (!File.Exists(asset.ManagedPath))
            {
                continue;
            }

            var entry = archive.CreateEntry(PackagingPathUtility.NormalizeArchivePath(asset.PackagePath), CompressionLevel.Optimal);
            using var input = File.OpenRead(asset.ManagedPath);
            using var output = entry.Open();
            input.CopyTo(output);
        }
    }

    private static IEnumerable<AssetRef> EnumeratePackedAssets(EditorProject project)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var asset in project.ProjectAssets.Where(ShouldPackManagedAsset))
        {
            if (seen.Add(asset.Id))
            {
                yield return asset;
            }
        }

        foreach (var asset in project.Overrides.SelectMany(overrideEnvelope => overrideEnvelope.Assets).Where(ShouldPackManagedAsset))
        {
            if (seen.Add(asset.Id))
            {
                yield return asset;
            }
        }
    }

    private static bool ShouldPackManagedAsset(AssetRef asset)
    {
        return !string.Equals(asset.SourceType, "res", StringComparison.OrdinalIgnoreCase) &&
               !string.IsNullOrWhiteSpace(asset.ManagedPath);
    }

    private static bool ShouldTreatAsManagedAsset(AssetRef asset)
    {
        return !string.Equals(asset.SourceType, "res", StringComparison.OrdinalIgnoreCase);
    }

    private static int CountAssets(EditorProject project)
    {
        return project.ProjectAssets
            .Concat(project.Overrides.SelectMany(overrideEnvelope => overrideEnvelope.Assets))
            .Select(asset => asset.Id)
            .Distinct(StringComparer.Ordinal)
            .Count();
    }

    private static string BuildPackageAssetReference(string packageKey, AssetRef asset)
    {
        var fileName = string.IsNullOrWhiteSpace(asset.FileName)
            ? Path.GetFileName(asset.ManagedPath)
            : asset.FileName;
        fileName = PackagingPathUtility.NormalizeFileName(fileName);
        return ModStudioAssetReference.CreatePackageAssetReference(packageKey, asset.Id, fileName);
    }

    private static string EnsureInstalledAssetPath(string packageKey, AssetRef asset, string? installedPackagesRootPath = null)
    {
        var fileName = string.IsNullOrWhiteSpace(asset.FileName)
            ? Path.GetFileName(asset.ManagedPath)
            : asset.FileName;
        fileName = PackagingPathUtility.NormalizeFileName(fileName);
        return string.IsNullOrWhiteSpace(installedPackagesRootPath)
            ? ModStudioPaths.GetInstalledPackageAssetFilePath(packageKey, asset.Id, fileName)
            : Path.Combine(installedPackagesRootPath, packageKey, "assets", asset.Id, fileName);
    }

    private static void RewriteMetadataAssetPaths(EditorProject project)
    {
        var replacements = BuildAssetReplacementLookup(project);
        if (replacements.Count == 0)
        {
            return;
        }

        foreach (var envelope in project.Overrides)
        {
            foreach (var key in envelope.Metadata.Keys.ToList())
            {
                var normalized = NormalizeLookupKey(envelope.Metadata[key]);
                if (normalized == null || !replacements.TryGetValue(normalized, out var replacement))
                {
                    continue;
                }

                envelope.Metadata[key] = replacement;
            }
        }
    }

    private static Dictionary<string, string> BuildAssetReplacementLookup(EditorProject project)
    {
        var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var asset in project.ProjectAssets.Concat(project.Overrides.SelectMany(overrideEnvelope => overrideEnvelope.Assets)))
        {
            if (!ShouldPackManagedAsset(asset) || string.IsNullOrWhiteSpace(asset.PackagePath))
            {
                continue;
            }

            AddReplacement(lookup, asset.ManagedPath, asset.PackagePath);
            AddReplacement(lookup, asset.SourcePath, asset.PackagePath);
        }

        return lookup;
    }

    private static void AddReplacement(IDictionary<string, string> lookup, string? originalPath, string replacement)
    {
        var normalized = NormalizeLookupKey(originalPath);
        if (normalized == null)
        {
            return;
        }

        lookup[normalized] = replacement;
    }

    private static string? NormalizeLookupKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!Path.IsPathRooted(value))
        {
            return value.Trim();
        }

        try
        {
            return Path.GetFullPath(value).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch
        {
            return value.Trim();
        }
    }

    private static EntityOverrideEnvelope CloneEnvelope(EntityOverrideEnvelope source)
    {
        return new EntityOverrideEnvelope
        {
            EntityKind = source.EntityKind,
            EntityId = source.EntityId,
            BehaviorSource = source.BehaviorSource,
            GraphId = source.GraphId,
            Notes = source.Notes,
            Metadata = new Dictionary<string, string>(source.Metadata, StringComparer.Ordinal),
            Assets = source.Assets.Select(CloneAsset).ToList()
        };
    }

    private static EntityOverrideEnvelope CloneEnvelopeForMetadata(EntityOverrideEnvelope source)
    {
        return new EntityOverrideEnvelope
        {
            EntityKind = source.EntityKind,
            EntityId = source.EntityId,
            BehaviorSource = source.BehaviorSource,
            GraphId = source.GraphId,
            Notes = source.Notes,
            Metadata = new Dictionary<string, string>(source.Metadata, StringComparer.Ordinal),
            Assets = source.Assets.Select(CloneAssetForMetadata).ToList()
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

    private static AssetRef CloneAssetForMetadata(AssetRef source)
    {
        var asset = CloneAsset(source);
        if (ShouldPackManagedAsset(asset))
        {
            asset.SourcePath = asset.PackagePath;
            asset.ManagedPath = string.Empty;
        }

        return asset;
    }
}
