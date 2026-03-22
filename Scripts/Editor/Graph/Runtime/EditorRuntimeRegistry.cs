using STS2_Editor.Scripts.Editor.Core.Models;
using STS2_Editor.Scripts.Editor.Packaging;

namespace STS2_Editor.Scripts.Editor.Runtime;

public sealed class EditorRuntimeRegistry
{
    private readonly PackageArchiveService _archiveService;
    private readonly EditorPackageStore _packageStore;
    private readonly RuntimePackageBackend _backend;

    public EditorRuntimeRegistry(PackageArchiveService archiveService, EditorPackageStore packageStore)
    {
        _archiveService = archiveService ?? throw new ArgumentNullException(nameof(archiveService));
        _packageStore = packageStore ?? throw new ArgumentNullException(nameof(packageStore));
        _backend = new RuntimePackageBackend(_archiveService, _packageStore);
    }

    public IReadOnlyList<PackageSessionState> SessionStates => _backend.SessionStates;

    public IReadOnlyList<RuntimeInstalledPackage> InstalledPackages => _backend.InstalledPackages;

    public PackageSessionNegotiationResult LastNegotiation => _backend.LastNegotiation;

    public RuntimeOverrideResolutionResult LastResolution => _backend.LastResolution;

    public void Initialize()
    {
        _backend.Initialize();
    }

    public void SetSessionStates(IEnumerable<PackageSessionState> states)
    {
        var normalized = states
            .OrderBy(state => state.LoadOrder)
            .ThenBy(state => state.PackageKey, StringComparer.Ordinal)
            .Select((state, index) =>
            {
                state.LoadOrder = index;
                return state;
            })
            .ToList();

        _packageStore.SaveSessionStates(normalized);
        _backend.RebuildFromInstalledPackages();
    }

    public void Refresh()
    {
        _backend.RebuildFromInstalledPackages();
    }

    public PackageInstallResult ImportPackage(string packageFilePath, bool enabledByDefault = true)
    {
        var result = _packageStore.InstallPackage(packageFilePath, enabledByDefault);
        _backend.RebuildFromInstalledPackages();
        return result;
    }

    public void EnablePackage(string packageKey, bool enabled)
    {
        _backend.EnablePackage(packageKey, enabled);
    }

    public bool MovePackage(string packageKey, int direction)
    {
        if (direction == 0)
        {
            return false;
        }

        var states = SessionStates
            .OrderBy(state => state.LoadOrder)
            .ThenBy(state => state.PackageKey, StringComparer.Ordinal)
            .Select(state => new PackageSessionState
            {
                PackageKey = state.PackageKey,
                PackageId = state.PackageId,
                DisplayName = state.DisplayName,
                Version = state.Version,
                Checksum = state.Checksum,
                PackageFilePath = state.PackageFilePath,
                LoadOrder = state.LoadOrder,
                Enabled = state.Enabled,
                SessionEnabled = state.SessionEnabled,
                DisabledReason = state.DisabledReason
            })
            .ToList();

        var index = states.FindIndex(state => string.Equals(state.PackageKey, packageKey, StringComparison.Ordinal));
        if (index < 0)
        {
            return false;
        }

        var newIndex = Math.Clamp(index + direction, 0, states.Count - 1);
        if (newIndex == index)
        {
            return false;
        }

        (states[index], states[newIndex]) = (states[newIndex], states[index]);
        SetSessionStates(states);
        return true;
    }

    public PackageSessionNegotiationResult Negotiate(IEnumerable<RemotePeerPackageSnapshot> peerSnapshots)
    {
        return _backend.NegotiateSession(peerSnapshots);
    }

    public bool TryGetOverride(ModStudioEntityKind kind, string entityId, out EntityOverrideEnvelope? envelope)
    {
        return _backend.TryGetOverride(kind, entityId, out envelope);
    }
}
