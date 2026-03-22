namespace STS2_Editor.Scripts.Editor.Runtime;

public static class EditorRuntimeRegistryExtensions
{
    public static PackageSessionNegotiationResult ApplyNegotiation(
        this EditorRuntimeRegistry registry,
        RuntimePackageBackend backend,
        IEnumerable<RemotePeerPackageSnapshot> peerSnapshots)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(backend);
        ArgumentNullException.ThrowIfNull(peerSnapshots);

        var result = backend.NegotiateSession(peerSnapshots);
        registry.SetSessionStates(result.SessionStates);
        return result;
    }

    public static RuntimeOverrideResolutionResult RebuildOverrides(
        this EditorRuntimeRegistry registry,
        RuntimePackageBackend backend)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(backend);

        backend.RefreshInstalledPackages();
        backend.LoadSessionStates();
        backend.ResolveCurrentSession();
        registry.SetSessionStates(backend.SessionStates);
        return backend.LastResolution;
    }
}
