using STS2_Editor.Scripts.Editor.Core.Models;
using STS2_Editor.Scripts.Editor.Packaging;

namespace STS2_Editor.Scripts.Editor.Runtime;

public sealed class RuntimeOverrideResolver
{
    public RuntimeOverrideResolutionResult Resolve(
        IReadOnlyCollection<RuntimeInstalledPackage> installedPackages,
        IReadOnlyCollection<PackageSessionState> sessionStates)
    {
        var result = new RuntimeOverrideResolutionResult();
        var conflictMap = new Dictionary<RuntimeEntityKey, PackageOverrideConflict>();
        var packageLookup = installedPackages
            .ToDictionary(package => package.PackageKey, StringComparer.Ordinal);
        var orderedStates = sessionStates
            .Where(state => state.Enabled && state.SessionEnabled)
            .OrderBy(state => state.LoadOrder)
            .ThenBy(state => state.PackageKey, StringComparer.Ordinal)
            .ToList();

        foreach (var state in orderedStates)
        {
            if (!packageLookup.TryGetValue(state.PackageKey, out var package))
            {
                continue;
            }

            result.RecordPackage(state.PackageKey);

            foreach (var envelope in package.Project.Overrides)
            {
                var key = new RuntimeEntityKey(envelope.EntityKind, envelope.EntityId);
                TrackConflict(conflictMap, key, state);
                result.SetOverride(key, CloneEnvelope(envelope));
            }

            foreach (var graphPair in package.Project.Graphs)
            {
                result.SetGraph(graphPair.Key, graphPair.Value);
            }

            foreach (var asset in package.Project.ProjectAssets)
            {
                result.AddAsset(CloneAsset(asset));
            }
        }

        foreach (var conflict in conflictMap.Values.Where(conflict => conflict.Participants.Count > 1))
        {
            result.AddConflict(conflict);
        }

        return result;
    }

    private static void TrackConflict(
        IDictionary<RuntimeEntityKey, PackageOverrideConflict> conflictMap,
        RuntimeEntityKey key,
        PackageSessionState state)
    {
        if (!conflictMap.TryGetValue(key, out var conflict))
        {
            conflict = new PackageOverrideConflict
            {
                EntityKind = key.Kind,
                EntityId = key.EntityId
            };
            conflictMap[key] = conflict;
        }

        conflict.AddParticipant(new PackageOverrideConflictParticipant
        {
            PackageKey = state.PackageKey,
            DisplayName = state.DisplayName,
            LoadOrder = state.LoadOrder
        });
        conflict.WinningPackageKey = state.PackageKey;
    }

    private static EntityOverrideEnvelope CloneEnvelope(EntityOverrideEnvelope source)
    {
        return new EntityOverrideEnvelope
        {
            EntityKind = source.EntityKind,
            EntityId = source.EntityId,
            BehaviorSource = source.BehaviorSource,
            GraphId = source.GraphId,
            Notes = source.Notes,
            Metadata = new Dictionary<string, string>(source.Metadata, StringComparer.Ordinal),
            Assets = source.Assets.Select(CloneAsset).ToList()
        };
    }

    private static AssetRef CloneAsset(AssetRef source)
    {
        return new AssetRef
        {
            Id = source.Id,
            SourceType = source.SourceType,
            LogicalRole = source.LogicalRole,
            SourcePath = source.SourcePath,
            ManagedPath = source.ManagedPath,
            PackagePath = source.PackagePath,
            FileName = source.FileName
        };
    }
}
