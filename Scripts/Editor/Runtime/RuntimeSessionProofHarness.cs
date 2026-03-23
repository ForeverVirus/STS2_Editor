using System.Linq;
using MegaCrit.Sts2.Core.Logging;
using STS2_Editor.Scripts.Editor.Core.Models;
using STS2_Editor.Scripts.Editor.Core.Utilities;

namespace STS2_Editor.Scripts.Editor.Runtime;

internal static class RuntimeSessionProofHarness
{
    private const string PeerSnapshotArgPrefix = "--modstudio-proof-peers=";
    private static readonly string? ProofRequestPath = ParseProofRequestPath();
    private static int _applied;

    public static void ApplyNegotiationIfRequested(EditorRuntimeRegistry registry)
    {
        if (registry == null || string.IsNullOrWhiteSpace(ProofRequestPath) || Interlocked.Exchange(ref _applied, 1) != 0)
        {
            return;
        }

        var requestPath = ProofRequestPath!;
        if (!File.Exists(requestPath))
        {
            Log.Warn($"[ModStudio.SessionProof] Proof request file not found: {requestPath}");
            return;
        }

        SessionProofRequest request;
        try
        {
            request = ModStudioJson.LoadOrDefault(requestPath, () => new SessionProofRequest());
        }
        catch (Exception ex)
        {
            Log.Warn($"[ModStudio.SessionProof] Failed to load proof request '{requestPath}': {ex.Message}");
            return;
        }

        if (request.PeerSnapshots.Count == 0)
        {
            Log.Warn($"[ModStudio.SessionProof] Proof request '{requestPath}' contained no peer snapshots.");
            return;
        }

        var negotiation = registry.Negotiate(request.PeerSnapshots);
        Log.Info($"[ModStudio.SessionProof] Applied proof request '{requestPath}' peers={request.PeerSnapshots.Count} activePackages={negotiation.ActivePackageKeys.Count} peerConflicts={negotiation.HasPeerConflicts}");
        LogSessionStates(request.FocusPackageKeys, registry.SessionStates);
        LogAppliedPackages(request.FocusPackageKeys, registry.LastResolution.AppliedPackageKeys);
        LogConflictWinners(request.FocusEntities, registry.LastResolution.Conflicts);
    }

    private static void LogSessionStates(IReadOnlyCollection<string> focusPackageKeys, IReadOnlyCollection<PackageSessionState> sessionStates)
    {
        var filtered = focusPackageKeys.Count == 0
            ? sessionStates.OrderBy(state => state.LoadOrder)
            : sessionStates
                .Where(state => focusPackageKeys.Contains(state.PackageKey, StringComparer.Ordinal))
                .OrderBy(state => state.LoadOrder);

        foreach (var state in filtered)
        {
            Log.Info($"[ModStudio.SessionProof] Package {state.PackageKey} enabled={state.Enabled} sessionEnabled={state.SessionEnabled} loadOrder={state.LoadOrder} reason='{state.DisabledReason}'");
        }
    }

    private static void LogAppliedPackages(IReadOnlyCollection<string> focusPackageKeys, IReadOnlyCollection<string> appliedPackageKeys)
    {
        var filtered = focusPackageKeys.Count == 0
            ? appliedPackageKeys
            : appliedPackageKeys.Where(packageKey => focusPackageKeys.Contains(packageKey, StringComparer.Ordinal)).ToList();

        Log.Info($"[ModStudio.SessionProof] Applied package order: {string.Join(" -> ", filtered)}");
    }

    private static void LogConflictWinners(
        IReadOnlyCollection<SessionProofEntityFocus> focusEntities,
        IReadOnlyCollection<PackageOverrideConflict> conflicts)
    {
        IEnumerable<PackageOverrideConflict> filteredConflicts = conflicts;
        if (focusEntities.Count > 0)
        {
            var focusKeys = focusEntities
                .Select(CreateConflictFocusKey)
                .ToHashSet(StringComparer.Ordinal);
            filteredConflicts = conflicts.Where(conflict => focusKeys.Contains(CreateConflictFocusKey(conflict.EntityKind, conflict.EntityId)));
        }

        foreach (var conflict in filteredConflicts)
        {
            var participants = string.Join(", ", conflict.Participants
                .OrderBy(participant => participant.LoadOrder)
                .Select(participant => $"{participant.PackageKey}@{participant.LoadOrder}"));
            Log.Info($"[ModStudio.SessionProof] Conflict {conflict.EntityKind}:{conflict.EntityId} winner={conflict.WinningPackageKey} participants=[{participants}]");
        }
    }

    private static string CreateConflictFocusKey(SessionProofEntityFocus focus)
    {
        if (!Enum.TryParse<ModStudioEntityKind>(focus.EntityKind, ignoreCase: true, out var entityKind))
        {
            return string.Empty;
        }

        return CreateConflictFocusKey(entityKind, focus.EntityId);
    }

    private static string CreateConflictFocusKey(ModStudioEntityKind kind, string entityId)
    {
        return $"{kind}:{entityId}";
    }

    private static string? ParseProofRequestPath()
    {
        foreach (var arg in Environment.GetCommandLineArgs())
        {
            if (!arg.StartsWith(PeerSnapshotArgPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var rawPath = arg[PeerSnapshotArgPrefix.Length..].Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                return null;
            }

            return Path.GetFullPath(rawPath);
        }

        return null;
    }

    private sealed class SessionProofRequest
    {
        public List<RemotePeerPackageSnapshot> PeerSnapshots { get; set; } = new();

        public List<string> FocusPackageKeys { get; set; } = new();

        public List<SessionProofEntityFocus> FocusEntities { get; set; } = new();
    }

    private sealed class SessionProofEntityFocus
    {
        public string EntityKind { get; set; } = string.Empty;

        public string EntityId { get; set; } = string.Empty;
    }
}
