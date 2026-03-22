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

        return result;
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
