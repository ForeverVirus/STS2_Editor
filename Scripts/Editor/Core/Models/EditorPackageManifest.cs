namespace STS2_Editor.Scripts.Editor.Core.Models;

public sealed class EditorPackageManifest
{
    public string PackageId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Version { get; set; } = "1.0.0";

    public string Author { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string EditorVersion { get; set; } = "phase1";

    public string TargetGameVersion { get; set; } = "unknown";

    public string Checksum { get; set; } = string.Empty;

    public DateTimeOffset ExportedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public int OverrideCount { get; set; }

    public int GraphCount { get; set; }

    public int AssetCount { get; set; }

    public string PackageKey => $"{PackageId}@{Version}";
}
