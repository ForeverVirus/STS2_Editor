using STS2_Editor.Scripts.Editor.Core.Models;
using STS2_Editor.Scripts.Editor.Packaging;
using STS2_Editor.Scripts.Editor.Graph;

namespace STS2_Editor.Scripts.Editor.Runtime;

public sealed class RuntimeOverrideResolver
{
    private static readonly EventGraphCompiler EventGraphCompiler = new();

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
                var graph = TryGetGraph(package, envelope.GraphId);
                result.SetOverride(key, CloneEnvelope(envelope, graph));
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

    private static BehaviorGraphDefinition? TryGetGraph(RuntimeInstalledPackage package, string? graphId)
    {
        if (string.IsNullOrWhiteSpace(graphId))
        {
            return null;
        }

        return package.Project.Graphs.TryGetValue(graphId, out var graph) ? graph : null;
    }

    private static EntityOverrideEnvelope CloneEnvelope(EntityOverrideEnvelope source, BehaviorGraphDefinition? graph = null)
    {
        var clone = new EntityOverrideEnvelope
        {
            EntityKind = source.EntityKind,
            EntityId = source.EntityId,
            BehaviorSource = source.BehaviorSource,
            GraphId = source.GraphId,
            Notes = source.Notes,
            Metadata = new Dictionary<string, string>(source.Metadata, StringComparer.Ordinal),
            Assets = source.Assets.Select(CloneAsset).ToList()
        };

        if (clone.EntityKind == ModStudioEntityKind.Event &&
            clone.BehaviorSource == BehaviorSource.Graph &&
            graph is not null)
        {
            var compiled = EventGraphCompiler.Compile(graph);
            foreach (var pair in compiled.Metadata)
            {
                clone.Metadata[pair.Key] = pair.Value;
            }

            if (!compiled.IsValid)
            {
                var errors = string.Join(" | ", compiled.Errors);
                clone.Notes = string.IsNullOrWhiteSpace(clone.Notes)
                    ? $"Event graph compilation errors: {errors}"
                    : clone.Notes + " | Event graph compilation errors: " + errors;
            }
        }

        return clone;
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
