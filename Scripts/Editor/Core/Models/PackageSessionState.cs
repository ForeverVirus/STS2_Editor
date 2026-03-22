namespace STS2_Editor.Scripts.Editor.Core.Models;

public sealed class PackageSessionState
{
    public string PackageKey { get; set; } = string.Empty;

    public string PackageId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string Checksum { get; set; } = string.Empty;

    public string PackageFilePath { get; set; } = string.Empty;

    public int LoadOrder { get; set; }

    public bool Enabled { get; set; } = true;

    public bool SessionEnabled { get; set; } = true;

    public string DisabledReason { get; set; } = string.Empty;
}
