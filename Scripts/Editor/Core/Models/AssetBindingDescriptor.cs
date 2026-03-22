namespace STS2_Editor.Scripts.Editor.Core.Models;

public sealed class AssetBindingDescriptor
{
    public ModStudioEntityKind Kind { get; init; }

    public string MetadataKey { get; init; } = string.Empty;

    public string LogicalRole { get; init; } = string.Empty;

    public string DisplayNameKey { get; init; } = string.Empty;

    public bool SupportsRuntimeCatalog { get; init; } = true;

    public bool SupportsExternalImport { get; init; } = true;
}

public sealed class AssetBindingResult
{
    public AssetBindingDescriptor Descriptor { get; init; } = new();

    public string MetadataValue { get; init; } = string.Empty;

    public string ResolvedPath { get; init; } = string.Empty;

    public AssetRef? Asset { get; init; }
}
