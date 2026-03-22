using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using STS2_Editor.Scripts.Editor.Core.Models;
using STS2_Editor.Scripts.Editor.Core.Utilities;

namespace STS2_Editor.Scripts.Editor.Core.Services;

public sealed class ProjectAssetBindingService
{
    private static readonly IReadOnlyDictionary<ModStudioEntityKind, AssetBindingDescriptor> Descriptors =
        new Dictionary<ModStudioEntityKind, AssetBindingDescriptor>
        {
            [ModStudioEntityKind.Card] = new()
            {
                Kind = ModStudioEntityKind.Card,
                MetadataKey = "portrait_path",
                LogicalRole = "portrait",
                DisplayNameKey = "asset.role.card_portrait",
                SupportsRuntimeCatalog = true,
                SupportsExternalImport = true
            },
            [ModStudioEntityKind.Relic] = new()
            {
                Kind = ModStudioEntityKind.Relic,
                MetadataKey = "icon_path",
                LogicalRole = "icon",
                DisplayNameKey = "asset.role.relic_icon",
                SupportsRuntimeCatalog = true,
                SupportsExternalImport = true
            },
            [ModStudioEntityKind.Potion] = new()
            {
                Kind = ModStudioEntityKind.Potion,
                MetadataKey = "image_path",
                LogicalRole = "image",
                DisplayNameKey = "asset.role.potion_image",
                SupportsRuntimeCatalog = true,
                SupportsExternalImport = true
            },
            [ModStudioEntityKind.Event] = new()
            {
                Kind = ModStudioEntityKind.Event,
                MetadataKey = "portrait_path",
                LogicalRole = "portrait",
                DisplayNameKey = "asset.role.event_portrait",
                SupportsRuntimeCatalog = true,
                SupportsExternalImport = true
            },
            [ModStudioEntityKind.Enchantment] = new()
            {
                Kind = ModStudioEntityKind.Enchantment,
                MetadataKey = "icon_path",
                LogicalRole = "icon",
                DisplayNameKey = "asset.role.enchantment_icon",
                SupportsRuntimeCatalog = true,
                SupportsExternalImport = true
            }
        };

    public bool TryGetDescriptor(ModStudioEntityKind kind, out AssetBindingDescriptor descriptor)
    {
        if (Descriptors.TryGetValue(kind, out descriptor!))
        {
            return true;
        }

        descriptor = new AssetBindingDescriptor
        {
            Kind = kind,
            SupportsExternalImport = false,
            SupportsRuntimeCatalog = false
        };
        return false;
    }

    public string? GetRuntimeAssetPath(ModStudioEntityKind kind, string entityId)
    {
        if (string.IsNullOrWhiteSpace(entityId))
        {
            return null;
        }

        return kind switch
        {
            ModStudioEntityKind.Card => ModelDb.AllCards.FirstOrDefault(card => card.Id.Entry == entityId)?.PortraitPath,
            ModStudioEntityKind.Relic => ModelDb.AllRelics.FirstOrDefault(relic => relic.Id.Entry == entityId)?.IconPath,
            ModStudioEntityKind.Potion => ModelDb.AllPotions.FirstOrDefault(potion => potion.Id.Entry == entityId)?.ImagePath,
            ModStudioEntityKind.Event => ModelDb.AllEvents.Any(evt => evt.Id.Entry == entityId)
                ? ImageHelper.GetImagePath($"events/{entityId.ToLowerInvariant()}.png")
                : null,
            ModStudioEntityKind.Enchantment => ModelDb.DebugEnchantments.FirstOrDefault(enchantment => enchantment.Id.Entry == entityId)?.IconPath,
            _ => null
        };
    }

    public IReadOnlyList<string> GetRuntimeAssetCandidates(ModStudioEntityKind kind)
    {
        IEnumerable<string?> values = kind switch
        {
            ModStudioEntityKind.Card => ModelDb.AllCards.Select(card => card.PortraitPath),
            ModStudioEntityKind.Relic => ModelDb.AllRelics.SelectMany(relic => new string?[] { relic.IconPath, relic.PackedIconPath }),
            ModStudioEntityKind.Potion => ModelDb.AllPotions.Select(potion => potion.ImagePath),
            ModStudioEntityKind.Event => ModelDb.AllEvents.Select(evt => ImageHelper.GetImagePath($"events/{evt.Id.Entry.ToLowerInvariant()}.png")),
            ModStudioEntityKind.Enchantment => ModelDb.DebugEnchantments.Select(enchantment => enchantment.IconPath),
            _ => Array.Empty<string?>()
        };

        return values
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .ToList();
    }

    public AssetBindingResult BindRuntimeAsset(EditorProject project, ModStudioEntityKind kind, string entityId, string runtimePath)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (!TryGetDescriptor(kind, out var descriptor))
        {
            throw new InvalidOperationException($"Entity kind '{kind}' does not support Phase 1 asset binding.");
        }

        if (string.IsNullOrWhiteSpace(runtimePath))
        {
            throw new InvalidOperationException("A runtime asset path is required.");
        }

        var envelope = EnsureEnvelope(project, kind, entityId);
        RemoveRoleAssets(project, envelope, descriptor.LogicalRole, deleteManagedFiles: true);
        envelope.Metadata[descriptor.MetadataKey] = runtimePath.Trim();
        return new AssetBindingResult
        {
            Descriptor = descriptor,
            MetadataValue = envelope.Metadata[descriptor.MetadataKey],
            ResolvedPath = envelope.Metadata[descriptor.MetadataKey]
        };
    }

    public AssetBindingResult ImportExternalAsset(EditorProject project, ModStudioEntityKind kind, string entityId, string sourcePath)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (!TryGetDescriptor(kind, out var descriptor))
        {
            throw new InvalidOperationException($"Entity kind '{kind}' does not support external asset import.");
        }

        if (!descriptor.SupportsExternalImport)
        {
            throw new InvalidOperationException($"Entity kind '{kind}' does not support external asset import in Phase 1.");
        }

        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            throw new ArgumentException("Source path is required.", nameof(sourcePath));
        }

        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("External asset does not exist.", sourcePath);
        }

        var envelope = EnsureEnvelope(project, kind, entityId);
        RemoveRoleAssets(project, envelope, descriptor.LogicalRole, deleteManagedFiles: true);

        var assetId = Guid.NewGuid().ToString("N");
        var projectAssetsDirectory = ModStudioPaths.GetProjectAssetsDirectory(project.Manifest.ProjectId);
        Directory.CreateDirectory(projectAssetsDirectory);
        var entityDirectory = Path.Combine(projectAssetsDirectory, kind.ToString().ToLowerInvariant(), SanitizeFileName(entityId), assetId);
        Directory.CreateDirectory(entityDirectory);

        var fileName = Path.GetFileName(sourcePath);
        var managedPath = Path.Combine(entityDirectory, fileName);
        File.Copy(sourcePath, managedPath, overwrite: true);

        var asset = new AssetRef
        {
            Id = assetId,
            SourceType = "external",
            LogicalRole = descriptor.LogicalRole,
            SourcePath = Path.GetFullPath(sourcePath),
            ManagedPath = managedPath,
            PackagePath = string.Empty,
            FileName = fileName
        };

        AddOrReplaceAsset(project.ProjectAssets, asset);
        AddOrReplaceAsset(envelope.Assets, asset);
        envelope.Metadata[descriptor.MetadataKey] = managedPath;

        return new AssetBindingResult
        {
            Descriptor = descriptor,
            MetadataValue = managedPath,
            ResolvedPath = managedPath,
            Asset = asset
        };
    }

    public AssetBindingResult ClearAssetBinding(EditorProject project, ModStudioEntityKind kind, string entityId)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (!TryGetDescriptor(kind, out var descriptor))
        {
            throw new InvalidOperationException($"Entity kind '{kind}' does not support Phase 1 asset binding.");
        }

        var envelope = EnsureEnvelope(project, kind, entityId);
        RemoveRoleAssets(project, envelope, descriptor.LogicalRole, deleteManagedFiles: true);
        envelope.Metadata.Remove(descriptor.MetadataKey);

        return new AssetBindingResult
        {
            Descriptor = descriptor,
            MetadataValue = string.Empty,
            ResolvedPath = string.Empty
        };
    }

    public string ResolveDisplayPath(EditorProject? project, EntityOverrideEnvelope? envelope, ModStudioEntityKind kind, IReadOnlyDictionary<string, string> metadata)
    {
        if (!TryGetDescriptor(kind, out var descriptor))
        {
            return string.Empty;
        }

        if (!metadata.TryGetValue(descriptor.MetadataKey, out var rawValue) || string.IsNullOrWhiteSpace(rawValue))
        {
            return string.Empty;
        }

        return rawValue;
    }

    private static EntityOverrideEnvelope EnsureEnvelope(EditorProject project, ModStudioEntityKind kind, string entityId)
    {
        var envelope = project.Overrides.FirstOrDefault(item => item.EntityKind == kind && item.EntityId == entityId);
        if (envelope != null)
        {
            return envelope;
        }

        envelope = new EntityOverrideEnvelope
        {
            EntityKind = kind,
            EntityId = entityId
        };
        project.Overrides.Add(envelope);
        return envelope;
    }

    private static void RemoveRoleAssets(EditorProject project, EntityOverrideEnvelope envelope, string logicalRole, bool deleteManagedFiles)
    {
        var removedAssetIds = envelope.Assets
            .Where(asset => string.Equals(asset.LogicalRole, logicalRole, StringComparison.Ordinal))
            .Select(asset => asset.Id)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (removedAssetIds.Count == 0)
        {
            return;
        }

        envelope.Assets.RemoveAll(asset => removedAssetIds.Contains(asset.Id, StringComparer.Ordinal));
        PruneOrphanProjectAssets(project, removedAssetIds, deleteManagedFiles);
    }

    private static void PruneOrphanProjectAssets(EditorProject project, IReadOnlyCollection<string> removedAssetIds, bool deleteManagedFiles)
    {
        var remainingIds = project.Overrides
            .SelectMany(overrideEnvelope => overrideEnvelope.Assets)
            .Select(asset => asset.Id)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var assetId in removedAssetIds)
        {
            if (remainingIds.Contains(assetId))
            {
                continue;
            }

            var asset = project.ProjectAssets.FirstOrDefault(item => string.Equals(item.Id, assetId, StringComparison.Ordinal));
            if (asset == null)
            {
                continue;
            }

            project.ProjectAssets.Remove(asset);
            if (deleteManagedFiles)
            {
                TryDeleteManagedAsset(asset.ManagedPath);
            }
        }
    }

    private static void AddOrReplaceAsset(ICollection<AssetRef> assets, AssetRef asset)
    {
        var existing = assets.FirstOrDefault(item => string.Equals(item.Id, asset.Id, StringComparison.Ordinal));
        if (existing != null)
        {
            assets.Remove(existing);
        }

        assets.Add(asset);
    }

    private static void TryDeleteManagedAsset(string? managedPath)
    {
        if (string.IsNullOrWhiteSpace(managedPath))
        {
            return;
        }

        try
        {
            if (File.Exists(managedPath))
            {
                File.Delete(managedPath);
            }

            var directory = Path.GetDirectoryName(managedPath);
            while (!string.IsNullOrWhiteSpace(directory) &&
                   directory.StartsWith(ModStudioPaths.RootPath, StringComparison.OrdinalIgnoreCase) &&
                   Directory.Exists(directory) &&
                   !Directory.EnumerateFileSystemEntries(directory).Any())
            {
                Directory.Delete(directory);
                directory = Path.GetDirectoryName(directory);
            }
        }
        catch
        {
            // Best effort cleanup only.
        }
    }

    private static string SanitizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unknown";
        }

        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        return new string(chars);
    }
}
