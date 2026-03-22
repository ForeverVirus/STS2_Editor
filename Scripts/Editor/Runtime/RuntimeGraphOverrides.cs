using STS2_Editor.Scripts.Editor.Core.Models;
using STS2_Editor.Scripts.Editor.Graph;

namespace STS2_Editor.Scripts.Editor.Runtime;

internal static class RuntimeGraphOverrides
{
    public static bool TryGetExecutableGraph(
        ModStudioEntityKind kind,
        string entityId,
        out EntityOverrideEnvelope? envelope,
        out BehaviorGraphDefinition? graph)
    {
        envelope = null;
        graph = null;

        if (!ModStudioBootstrap.RuntimeRegistry.TryGetOverride(kind, entityId, out envelope) ||
            envelope is null ||
            envelope.BehaviorSource != BehaviorSource.Graph ||
            string.IsNullOrWhiteSpace(envelope.GraphId))
        {
            return false;
        }

        return ModStudioBootstrap.RuntimeRegistry.TryGetGraph(envelope.GraphId, out graph);
    }
}
