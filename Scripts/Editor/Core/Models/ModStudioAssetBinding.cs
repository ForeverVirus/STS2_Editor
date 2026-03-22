namespace STS2_Editor.Scripts.Editor.Core.Models;

public sealed class ModStudioAssetBinding
{
    public ModStudioEntityKind Kind { get; init; }

    public string LogicalRole { get; init; } = string.Empty;

    public string MetadataKey { get; init; } = string.Empty;

    public string DisplayNameKey { get; init; } = string.Empty;

    public bool SupportsRuntimeSelection { get; init; } = true;

    public bool SupportsExternalImport { get; init; } = true;
}
