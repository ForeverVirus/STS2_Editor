using STS2_Editor.Scripts.Editor.Graph;

namespace STS2_Editor.Scripts.Editor.Core.Models;

public sealed class EditorProject
{
    public EditorProjectManifest Manifest { get; set; } = new();

    public List<EntityOverrideEnvelope> Overrides { get; set; } = new();

    public Dictionary<string, BehaviorGraphDefinition> Graphs { get; set; } = new();

    public List<AssetRef> ProjectAssets { get; set; } = new();

    public bool SourceOfTruthIsRuntimeModelDb { get; set; } = true;
}
