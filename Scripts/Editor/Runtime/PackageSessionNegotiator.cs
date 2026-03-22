using STS2_Editor.Scripts.Editor.Core.Models;
using STS2_Editor.Scripts.Editor.Packaging;

namespace STS2_Editor.Scripts.Editor.Runtime;

public sealed class PackageSessionNegotiator
{
    public PackageSessionNegotiationResult Negotiate(
        IReadOnlyCollection<PackageSessionState> localStates,
        IReadOnlyCollection<RemotePeerPackageSnapshot> peerSnapshots)
    {
        var result = new PackageSessionNegotiationResult();
        var localByKey = localStates
            .OrderBy(state => state.LoadOrder)
            .ThenBy(state => state.PackageKey, StringComparer.Ordinal)
            .ToDictionary(state => state.PackageKey, StringComparer.Ordinal);

        var peerPackageMaps = peerSnapshots
            .Where(snapshot => snapshot is not null)
            .Select(snapshot => new
            {
                snapshot.PeerId,
                Packages = snapshot.Packages.ToDictionary(package => package.PackageKey, package => package.Checksum, StringComparer.Ordinal)
            })
            .ToList();

        foreach (var state in localByKey.Values.OrderBy(state => state.LoadOrder))
        {
            var cloned = Clone(state);
            if (!cloned.Enabled)
            {
                cloned.SessionEnabled = false;
                cloned.DisabledReason = "disabled locally";
                result.AddSessionState(cloned);
                continue;
            }

            if (peerPackageMaps.Count == 0)
            {
                cloned.SessionEnabled = true;
                cloned.DisabledReason = string.Empty;
                result.AddSessionState(cloned);
                continue;
            }

            var match = true;
            foreach (var peer in peerPackageMaps)
            {
                if (!peer.Packages.TryGetValue(cloned.PackageKey, out var checksum))
                {
                    match = false;
                    cloned.DisabledReason = $"missing on peer '{peer.PeerId}'";
                    break;
                }

                if (!string.Equals(checksum, cloned.Checksum, StringComparison.OrdinalIgnoreCase))
                {
                    match = false;
                    cloned.DisabledReason = $"checksum mismatch on peer '{peer.PeerId}'";
                    break;
                }
            }

            cloned.SessionEnabled = match;
            if (match)
            {
                cloned.DisabledReason = string.Empty;
            }
            else
            {
                result.HasPeerConflicts = true;
            }

            result.AddSessionState(cloned);
        }

        return result;
    }

    private static PackageSessionState Clone(PackageSessionState state)
    {
        return new PackageSessionState
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
        };
    }
}
