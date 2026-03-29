using STS2_Editor.Scripts.Editor.Graph;

namespace STS2_Editor.Scripts.Editor.Core.Models;

public sealed class EntityEditorViewCache
{
    public Dictionary<string, string> OriginalMetadata { get; set; } = new(StringComparer.Ordinal);

    public Dictionary<string, string> MergedMetadata { get; set; } = new(StringComparer.Ordinal);

    public BehaviorGraphDefinition? AutoGraph { get; set; }

    public MonsterAiDefinition? AutoMonsterAi { get; set; }

    public Dictionary<string, BehaviorGraphDefinition> AutoMonsterGraphs { get; set; } = new(StringComparer.Ordinal);

    public List<string> RuntimeAssetCandidates { get; set; } = new();

    public List<AssetRef> ImportedAssets { get; set; } = new();

    public bool AssetsLoaded { get; set; }
}
