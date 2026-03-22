using STS2_Editor.Scripts.Editor.Core.Models;
using STS2_Editor.Scripts.Editor.Graph;

namespace STS2_Editor.Scripts.Editor.Runtime;

public sealed class RuntimeInstalledPackage
{
    public string PackageKey { get; set; } = string.Empty;

    public string PackageId { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Checksum { get; set; } = string.Empty;

    public string PackageFilePath { get; set; } = string.Empty;

    public EditorPackageManifest Manifest { get; set; } = new();

    public EditorProject Project { get; set; } = new();

    public bool IsDirectoryArchive { get; set; }
}

public sealed class RemotePeerPackageSnapshot
{
    public string PeerId { get; set; } = string.Empty;

    public IReadOnlyList<RemotePeerPackageState> Packages { get; set; } = Array.Empty<RemotePeerPackageState>();
}

public readonly record struct RuntimeEntityKey(ModStudioEntityKind Kind, string EntityId);

public sealed class PackageSessionNegotiationResult
{
    private readonly List<PackageSessionState> _sessionStates = new();
    private readonly List<string> _activePackageKeys = new();
    private readonly Dictionary<string, string> _disabledReasons = new(StringComparer.Ordinal);

    public IReadOnlyList<PackageSessionState> SessionStates => _sessionStates;

    public IReadOnlyList<string> ActivePackageKeys => _activePackageKeys;

    public IReadOnlyDictionary<string, string> DisabledReasons => _disabledReasons;

    public bool HasPeerConflicts { get; set; }

    public void AddSessionState(PackageSessionState state)
    {
        _sessionStates.Add(state);
        if (state.SessionEnabled)
        {
            _activePackageKeys.Add(state.PackageKey);
        }

        if (!string.IsNullOrWhiteSpace(state.DisabledReason))
        {
            _disabledReasons[state.PackageKey] = state.DisabledReason;
        }
    }
}

public sealed class RuntimeOverrideResolutionResult
{
    private readonly Dictionary<RuntimeEntityKey, EntityOverrideEnvelope> _overrides = new();
    private readonly Dictionary<string, BehaviorGraphDefinition> _graphs = new(StringComparer.Ordinal);
    private readonly List<AssetRef> _assets = new();
    private readonly List<string> _appliedPackageKeys = new();

    public IReadOnlyDictionary<RuntimeEntityKey, EntityOverrideEnvelope> Overrides => _overrides;

    public IReadOnlyDictionary<string, BehaviorGraphDefinition> Graphs => _graphs;

    public IReadOnlyList<AssetRef> Assets => _assets;

    public IReadOnlyList<string> AppliedPackageKeys => _appliedPackageKeys;

    public void RecordPackage(string packageKey)
    {
        if (!string.IsNullOrWhiteSpace(packageKey))
        {
            _appliedPackageKeys.Add(packageKey);
        }
    }

    public void SetOverride(RuntimeEntityKey key, EntityOverrideEnvelope envelope)
    {
        _overrides[key] = envelope;
    }

    public void SetGraph(string graphId, BehaviorGraphDefinition graph)
    {
        _graphs[graphId] = graph;
    }

    public void AddAsset(AssetRef asset)
    {
        _assets.Add(asset);
    }
}
