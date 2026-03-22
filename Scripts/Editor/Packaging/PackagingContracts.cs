using STS2_Editor.Scripts.Editor.Core.Models;

namespace STS2_Editor.Scripts.Editor.Packaging;

public sealed class PackageExportOptions
{
    public string? PackageId { get; set; }

    public string? Version { get; set; }

    public string? DisplayName { get; set; }

    public string? Author { get; set; }

    public string? Description { get; set; }

    public string? EditorVersion { get; set; }

    public string? TargetGameVersion { get; set; }

    public bool IncludeManagedAssets { get; set; } = true;
}

public sealed class PackageImportResult
{
    public string PackageFilePath { get; set; } = string.Empty;

    public EditorPackageManifest Manifest { get; set; } = new();

    public EditorProject Project { get; set; } = new();

    public bool ChecksumVerified { get; set; }
}

public sealed class PackageInstallResult
{
    public PackageSessionState PackageState { get; set; } = new();

    public bool ReplacedExistingInstall { get; set; }
}

public sealed class AssetImportResult
{
    public AssetRef Asset { get; set; } = new();

    public string ImportedPath { get; set; } = string.Empty;
}
