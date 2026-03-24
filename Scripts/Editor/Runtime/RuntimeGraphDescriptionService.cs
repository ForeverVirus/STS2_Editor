using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using STS2_Editor.Scripts.Editor.Graph;

namespace STS2_Editor.Scripts.Editor.Runtime;

internal static class RuntimeGraphDescriptionService
{
    private static readonly GraphDescriptionGenerator Generator = new();

    public static bool TryGetCardDescription(CardModel card, Creature? target, bool upgradedPreview, out string description)
    {
        description = string.Empty;
        if (!RuntimeGraphOverrides.TryGetExecutableGraph(Core.Models.ModStudioEntityKind.Card, card.Id.Entry, out _, out var graph) ||
            graph == null)
        {
            return false;
        }

        var previewContext = BuildPreviewContext(card, target, upgradedPreview);
        var generation = Generator.Generate(graph, card, previewContext);
        description = !string.IsNullOrWhiteSpace(generation.PreviewDescription)
            ? generation.PreviewDescription
            : generation.TemplateDescription;
        return !string.IsNullOrWhiteSpace(description);
    }

    private static DynamicPreviewContext BuildPreviewContext(CardModel card, Creature? target, bool upgradedPreview)
    {
        return RuntimeGraphPreviewService.BuildCardPreviewContext(card, target, upgradedPreview);
    }
}
