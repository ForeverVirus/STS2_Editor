using STS2_Editor.Scripts.Editor.Core.Models;
using STS2_Editor.Scripts.Editor.Core.Utilities;
using STS2_Editor.Scripts.Editor.Packaging;
using MegaCrit.Sts2.Core.Logging;

namespace STS2_Editor.Scripts.Editor.Runtime;

public sealed class RuntimePackageBackend
{
    private readonly PackageArchiveService _archiveService;
    private readonly EditorPackageStore? _packageStore;
    private readonly RuntimePackageCatalog _catalog;
    private readonly PackageSessionNegotiator _negotiator = new();
    private readonly RuntimeOverrideResolver _resolver = new();

    private readonly List<RuntimeInstalledPackage> _installedPackages = new();
    private readonly List<PackageSessionState> _sessionStates = new();
    private readonly List<RemotePeerPackageSnapshot> _peerSnapshots = new();

    private RuntimeOverrideResolutionResult _lastResolution = new();
    private PackageSessionNegotiationResult _lastNegotiation = new();

    public RuntimePackageBackend(PackageArchiveService archiveService, EditorPackageStore? packageStore = null)
    {
        _archiveService = archiveService ?? throw new ArgumentNullException(nameof(archiveService));
        _packageStore = packageStore;
        _catalog = new RuntimePackageCatalog(_archiveService);
    }

    public IReadOnlyList<RuntimeInstalledPackage> InstalledPackages => _installedPackages;

    public IReadOnlyList<PackageSessionState> SessionStates => _sessionStates;

    public IReadOnlyList<RemotePeerPackageSnapshot> PeerSnapshots => _peerSnapshots;

    public PackageSessionNegotiationResult LastNegotiation => _lastNegotiation;

    public RuntimeOverrideResolutionResult LastResolution => _lastResolution;

    public void Initialize()
    {
        RefreshInstalledPackages();
        LoadSessionStates();
        NormalizeSessionStates();
        ResolveCurrentSession();
        Log.Info($"[ModStudio.Package] Installed={_installedPackages.Count} SessionStates={_sessionStates.Count} Active={string.Join(", ", _sessionStates.Where(state => state.Enabled && state.SessionEnabled).OrderBy(state => state.LoadOrder).Select(state => state.PackageKey))}");
    }

    public void RefreshInstalledPackages()
    {
        _installedPackages.Clear();
        _installedPackages.AddRange(_catalog.DiscoverInstalledPackages());
        CleanupStalePackageCaches(_installedPackages.Select(package => package.PackageKey));
    }

    public void LoadSessionStates()
    {
        _sessionStates.Clear();

        var persisted = _packageStore?.LoadSessionStates();
        if (persisted is not null && persisted.Count > 0)
        {
            _sessionStates.AddRange(CloneAndNormalize(persisted));
            return;
        }

        foreach (var package in _installedPackages)
        {
            _sessionStates.Add(CreateDefaultState(package));
        }
    }

    public void SaveSessionStates()
    {
        NormalizeSessionStates();
        _packageStore?.SaveSessionStates(_sessionStates);
    }

    public void SetPeerSnapshots(IEnumerable<RemotePeerPackageSnapshot> peerSnapshots)
    {
        _peerSnapshots.Clear();
        _peerSnapshots.AddRange(peerSnapshots.Where(snapshot => snapshot is not null));
        ResolveCurrentSession();
    }

    public PackageSessionNegotiationResult NegotiateSession(IEnumerable<RemotePeerPackageSnapshot> peerSnapshots)
    {
        _peerSnapshots.Clear();
        _peerSnapshots.AddRange(peerSnapshots.Where(snapshot => snapshot is not null));
        _lastNegotiation = _negotiator.Negotiate(_sessionStates, _peerSnapshots);
        ReplaceSessionStates(_lastNegotiation.SessionStates);
        ResolveCurrentSession();
        return _lastNegotiation;
    }

    public RuntimeOverrideResolutionResult ResolveCurrentSession()
    {
        _lastResolution = _resolver.Resolve(_installedPackages, _sessionStates);
        return _lastResolution;
    }

    public bool TryGetOverride(ModStudioEntityKind kind, string entityId, out EntityOverrideEnvelope? envelope)
    {
        return _lastResolution.Overrides.TryGetValue(new RuntimeEntityKey(kind, entityId), out envelope);
    }

    public bool TryGetInstalledPackage(string packageKey, out RuntimeInstalledPackage? package)
    {
        package = _installedPackages.FirstOrDefault(item => string.Equals(item.PackageKey, packageKey, StringComparison.Ordinal));
        return package is not null;
    }

    public void EnablePackage(string packageKey, bool enabled)
    {
        if (!TryGetSessionState(packageKey, out var state))
        {
            return;
        }

        state.Enabled = enabled;
        if (_peerSnapshots.Count > 0)
        {
            _lastNegotiation = _negotiator.Negotiate(_sessionStates, _peerSnapshots);
            ReplaceSessionStates(_lastNegotiation.SessionStates);
            ResolveCurrentSession();
            return;
        }

        if (!enabled)
        {
            state.SessionEnabled = false;
            state.DisabledReason = "disabled locally";
        }
        else
        {
            state.SessionEnabled = true;
            state.DisabledReason = string.Empty;
        }

        NormalizeSessionStates();
        SaveSessionStates();
        ResolveCurrentSession();
    }

    public void SetLoadOrder(string packageKey, int loadOrder)
    {
        if (!TryGetSessionState(packageKey, out var state))
        {
            return;
        }

        state.LoadOrder = Math.Max(0, loadOrder);
        NormalizeSessionStates();
        SaveSessionStates();
        ResolveCurrentSession();
    }

    public void RebuildFromInstalledPackages()
    {
        RefreshInstalledPackages();
        LoadSessionStates();
        NormalizeSessionStates();
        CleanupStalePackageCaches(_installedPackages.Select(package => package.PackageKey));
        ResolveCurrentSession();
        SaveSessionStates();
    }

    private void ReplaceSessionStates(IReadOnlyList<PackageSessionState> states)
    {
        _sessionStates.Clear();
        _sessionStates.AddRange(CloneAndNormalize(states));
        NormalizeSessionStates();
        SaveSessionStates();
    }

    private void NormalizeSessionStates()
    {
        var knownKeys = new HashSet<string>(_installedPackages.Select(package => package.PackageKey), StringComparer.Ordinal);
        var lookup = _sessionStates
            .Where(state => !string.IsNullOrWhiteSpace(state.PackageKey))
            .ToDictionary(state => state.PackageKey, StringComparer.Ordinal);

        foreach (var package in _installedPackages)
        {
            if (!lookup.TryGetValue(package.PackageKey, out var state))
            {
                state = CreateDefaultState(package);
                _sessionStates.Add(state);
                lookup[package.PackageKey] = state;
            }
            else
            {
                state.PackageId = package.PackageId;
                state.DisplayName = package.DisplayName;
                state.Version = package.Version;
                state.Checksum = package.Checksum;
                state.PackageFilePath = package.PackageFilePath;
                if (state.LoadOrder < 0)
                {
                    state.LoadOrder = 0;
                }
            }
        }

        _sessionStates.RemoveAll(state => !knownKeys.Contains(state.PackageKey));

        var ordered = _sessionStates
            .OrderBy(state => state.LoadOrder)
            .ThenBy(state => state.PackageKey, StringComparer.Ordinal)
            .ToList();

        _sessionStates.Clear();
        for (var index = 0; index < ordered.Count; index++)
        {
            ordered[index].LoadOrder = index;
            _sessionStates.Add(ordered[index]);
        }
    }

    private bool TryGetSessionState(string packageKey, out PackageSessionState state)
    {
        state = _sessionStates.FirstOrDefault(item => string.Equals(item.PackageKey, packageKey, StringComparison.Ordinal))!;
        return state is not null;
    }

    private static PackageSessionState CreateDefaultState(RuntimeInstalledPackage package)
    {
        return new PackageSessionState
        {
            PackageKey = package.PackageKey,
            PackageId = package.PackageId,
            DisplayName = package.DisplayName,
            Version = package.Version,
            Checksum = package.Checksum,
            PackageFilePath = package.PackageFilePath,
            LoadOrder = 0,
            Enabled = true,
            SessionEnabled = true,
            DisabledReason = string.Empty
        };
    }

    private static IReadOnlyList<PackageSessionState> CloneAndNormalize(IEnumerable<PackageSessionState> states)
    {
        return states
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
    }

    private static void CleanupStalePackageCaches(IEnumerable<string> packageKeys)
    {
        var rootPath = ModStudioPaths.RuntimePackageCachePath;
        if (!Directory.Exists(rootPath))
        {
            return;
        }

        var knownPackageKeys = new HashSet<string>(packageKeys.Where(key => !string.IsNullOrWhiteSpace(key)), StringComparer.Ordinal);
        if (knownPackageKeys.Count == 0)
        {
            foreach (var directory in Directory.EnumerateDirectories(rootPath))
            {
                try
                {
                    Directory.Delete(directory, recursive: true);
                }
                catch
                {
                    // Best effort cleanup only.
                }
            }
            return;
        }

        foreach (var directory in Directory.EnumerateDirectories(rootPath))
        {
            var packageKey = Path.GetFileName(directory);
            if (string.IsNullOrWhiteSpace(packageKey) || knownPackageKeys.Contains(packageKey))
            {
                continue;
            }

            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch
            {
                // Best effort cleanup only.
            }
        }
    }
}
