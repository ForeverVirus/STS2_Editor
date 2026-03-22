using STS2_Editor.Scripts.Editor.Core.Models;
using STS2_Editor.Scripts.Editor.Core.Utilities;

namespace STS2_Editor.Scripts.Editor.Packaging;

public sealed class AssetImportService
{
    public string ManagedAssetsRootPath => ModStudioPaths.ImportsPath;

    public AssetRef ImportProjectAsset(string projectId, string sourcePath, string logicalRole)
    {
        if (string.IsNullOrWhiteSpace(projectId))
        {
            throw new ArgumentException("Project id is required.", nameof(projectId));
        }

        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            throw new ArgumentException("Source path is required.", nameof(sourcePath));
        }

        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("External asset does not exist.", sourcePath);
        }

        var fileName = PackagingPathUtility.NormalizeFileName(Path.GetFileName(sourcePath));
        var assetId = Guid.NewGuid().ToString("N");
        var managedDirectory = Path.Combine(ModStudioPaths.GetProjectAssetsDirectory(projectId), assetId);
        Directory.CreateDirectory(managedDirectory);
        var managedPath = Path.Combine(managedDirectory, fileName);
        File.Copy(sourcePath, managedPath, overwrite: true);

        return new AssetRef
        {
            Id = assetId,
            SourceType = "external",
            LogicalRole = logicalRole,
            SourcePath = Path.GetFullPath(sourcePath),
            ManagedPath = managedPath,
            PackagePath = string.Empty,
            FileName = fileName
        };
    }

    public AssetRef ImportExternalAsset(string sourcePath, string logicalRole, string? packageId = null, string? packagePath = null)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            throw new ArgumentException("Source path is required.", nameof(sourcePath));
        }

        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("External asset does not exist.", sourcePath);
        }

        var fileName = Path.GetFileName(sourcePath);
        var assetId = Guid.NewGuid().ToString("N");
        var managedDirectory = Path.Combine(ManagedAssetsRootPath, packageId ?? "shared", assetId);
        Directory.CreateDirectory(managedDirectory);
        var managedPath = Path.Combine(managedDirectory, fileName);
        File.Copy(sourcePath, managedPath, overwrite: true);

        return new AssetRef
        {
            Id = assetId,
            SourceType = "external",
            LogicalRole = logicalRole,
            SourcePath = Path.GetFullPath(sourcePath),
            ManagedPath = managedPath,
            PackagePath = packagePath ?? string.Empty,
            FileName = fileName
        };
    }

    public AssetRef CreateRuntimeReference(string resPath, string logicalRole)
    {
        return new AssetRef
        {
            SourceType = "res",
            LogicalRole = logicalRole,
            SourcePath = resPath,
            ManagedPath = resPath,
            PackagePath = resPath,
            FileName = Path.GetFileName(resPath)
        };
    }
}
