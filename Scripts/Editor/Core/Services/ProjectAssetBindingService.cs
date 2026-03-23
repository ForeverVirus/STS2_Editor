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
            ModStudioEntityKind.Card => SafeGetCardPortraitPath(entityId),
            ModStudioEntityKind.Relic => SafeGetRelicIconPath(entityId),
            ModStudioEntityKind.Potion => SafeGetPotionImagePath(entityId),
            ModStudioEntityKind.Event => SafeGetEventImagePath(entityId),
            ModStudioEntityKind.Enchantment => SafeGetEnchantmentIconPath(entityId),
            _ => null
        };
    }

    public IReadOnlyList<string> GetRuntimeAssetCandidates(ModStudioEntityKind kind)
    {
        return EnumerateRuntimeAssetCandidates(kind)
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

    public AssetBindingResult BindProjectAsset(EditorProject project, ModStudioEntityKind kind, string entityId, string assetId)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (!TryGetDescriptor(kind, out var descriptor))
        {
            throw new InvalidOperationException($"Entity kind '{kind}' does not support Phase 1 asset binding.");
        }

        if (string.IsNullOrWhiteSpace(assetId))
        {
            throw new ArgumentException("Asset id is required.", nameof(assetId));
        }

        var asset = project.ProjectAssets.FirstOrDefault(item =>
            string.Equals(item.Id, assetId, StringComparison.Ordinal) &&
            string.Equals(item.LogicalRole, descriptor.LogicalRole, StringComparison.Ordinal));
        if (asset == null)
        {
            throw new InvalidOperationException($"Project asset '{assetId}' was not found for role '{descriptor.LogicalRole}'.");
        }

        var envelope = EnsureEnvelope(project, kind, entityId);
        var alreadyBound = envelope.Assets.Any(item =>
            string.Equals(item.Id, asset.Id, StringComparison.Ordinal) &&
            string.Equals(item.LogicalRole, descriptor.LogicalRole, StringComparison.Ordinal));
        if (alreadyBound &&
            envelope.Metadata.TryGetValue(descriptor.MetadataKey, out var existingValue) &&
            string.Equals(existingValue, asset.ManagedPath, StringComparison.OrdinalIgnoreCase))
        {
            return new AssetBindingResult
            {
                Descriptor = descriptor,
                MetadataValue = asset.ManagedPath,
                ResolvedPath = asset.ManagedPath,
                Asset = asset
            };
        }

        RemoveRoleAssets(project, envelope, descriptor.LogicalRole, deleteManagedFiles: true);
        AddOrReplaceAsset(project.ProjectAssets, asset);
        AddOrReplaceAsset(envelope.Assets, asset);
        envelope.Metadata[descriptor.MetadataKey] = asset.ManagedPath;

        return new AssetBindingResult
        {
            Descriptor = descriptor,
            MetadataValue = asset.ManagedPath,
            ResolvedPath = asset.ManagedPath,
            Asset = asset
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

    private static IEnumerable<string?> EnumerateRuntimeAssetCandidates(ModStudioEntityKind kind)
    {
        return kind switch
        {
            ModStudioEntityKind.Card => EnumerateSafeCardPortraits(),
            ModStudioEntityKind.Relic => EnumerateSafeRelicIcons(),
            ModStudioEntityKind.Potion => EnumerateSafePotionImages(),
            ModStudioEntityKind.Event => EnumerateSafeEventImages(),
            ModStudioEntityKind.Enchantment => EnumerateSafeEnchantmentIcons(),
            _ => Array.Empty<string?>()
        };
    }

    private static IEnumerable<string?> EnumerateSafeCardPortraits()
    {
        foreach (var card in ModelDb.AllCards)
        {
            string? path = null;
            try
            {
                path = card.PortraitPath;
            }
            catch
            {
                // Some runtime cards derive portrait state dynamically and can throw.
            }

            if (!string.IsNullOrWhiteSpace(path))
            {
                yield return path;
            }
        }
    }

    private static IEnumerable<string?> EnumerateSafeRelicIcons()
    {
        foreach (var relic in ModelDb.AllRelics)
        {
            string? iconPath = null;
            string? packedIconPath = null;

            try
            {
                iconPath = relic.IconPath;
            }
            catch
            {
                // Ignore invalid icon path resolution for individual relics.
            }

            try
            {
                packedIconPath = relic.PackedIconPath;
            }
            catch
            {
                // Ignore invalid packed icon path resolution for individual relics.
            }

            if (!string.IsNullOrWhiteSpace(iconPath))
            {
                yield return iconPath;
            }

            if (!string.IsNullOrWhiteSpace(packedIconPath))
            {
                yield return packedIconPath;
            }
        }
    }

    private static IEnumerable<string?> EnumerateSafePotionImages()
    {
        foreach (var potion in ModelDb.AllPotions)
        {
            string? path = null;
            try
            {
                path = potion.ImagePath;
            }
            catch
            {
                // Ignore invalid image path resolution for individual potions.
            }

            if (!string.IsNullOrWhiteSpace(path))
            {
                yield return path;
            }
        }
    }

    private static IEnumerable<string?> EnumerateSafeEventImages()
    {
        foreach (var evt in ModelDb.AllEvents)
        {
            string? path = null;
            try
            {
                path = ImageHelper.GetImagePath($"events/{evt.Id.Entry.ToLowerInvariant()}.png");
            }
            catch
            {
                // Ignore invalid image path resolution for individual events.
            }

            if (!string.IsNullOrWhiteSpace(path))
            {
                yield return path;
            }
        }
    }

    private static IEnumerable<string?> EnumerateSafeEnchantmentIcons()
    {
        foreach (var enchantment in ModelDb.DebugEnchantments)
        {
            string? path = null;
            try
            {
                path = enchantment.IconPath;
            }
            catch
            {
                // Ignore invalid icon path resolution for individual enchantments.
            }

            if (!string.IsNullOrWhiteSpace(path))
            {
                yield return path;
            }
        }
    }

    private static string? SafeGetCardPortraitPath(string entityId)
    {
        var card = ModelDb.AllCards.FirstOrDefault(candidate => candidate.Id.Entry == entityId);
        if (card == null)
        {
            return null;
        }

        try
        {
            return card.PortraitPath;
        }
        catch
        {
            return null;
        }
    }

    private static string? SafeGetRelicIconPath(string entityId)
    {
        var relic = ModelDb.AllRelics.FirstOrDefault(candidate => candidate.Id.Entry == entityId);
        if (relic == null)
        {
            return null;
        }

        try
        {
            return relic.IconPath;
        }
        catch
        {
            return null;
        }
    }

    private static string? SafeGetPotionImagePath(string entityId)
    {
        var potion = ModelDb.AllPotions.FirstOrDefault(candidate => candidate.Id.Entry == entityId);
        if (potion == null)
        {
            return null;
        }

        try
        {
            return potion.ImagePath;
        }
        catch
        {
            return null;
        }
    }

    private static string? SafeGetEventImagePath(string entityId)
    {
        if (!ModelDb.AllEvents.Any(evt => evt.Id.Entry == entityId))
        {
            return null;
        }

        try
        {
            return ImageHelper.GetImagePath($"events/{entityId.ToLowerInvariant()}.png");
        }
        catch
        {
            return null;
        }
    }

    private static string? SafeGetEnchantmentIconPath(string entityId)
    {
        var enchantment = ModelDb.DebugEnchantments.FirstOrDefault(candidate => candidate.Id.Entry == entityId);
        if (enchantment == null)
        {
            return null;
        }

        try
        {
            return enchantment.IconPath;
        }
        catch
        {
            return null;
        }
    }
}
