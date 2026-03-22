namespace STS2_Editor.Scripts.Editor.Core.Models;

public sealed class AssetRef
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string SourceType { get; set; } = "res";

    public string LogicalRole { get; set; } = string.Empty;

    public string SourcePath { get; set; } = string.Empty;

    public string ManagedPath { get; set; } = string.Empty;

    public string PackagePath { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;
}
