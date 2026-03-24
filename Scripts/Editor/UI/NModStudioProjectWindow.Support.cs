using MegaCrit.Sts2.Core.Models;
using STS2_Editor.Scripts.Editor.Core.Models;
using STS2_Editor.Scripts.Editor.Graph;

namespace STS2_Editor.Scripts.Editor.UI;

public sealed partial class NModStudioProjectWindow
{
    private STS2_Editor.Scripts.Editor.Graph.DynamicPreviewContext BuildPreviewContext(EntityBrowserItem item, IReadOnlyDictionary<string, string> metadata)
    {
        var context = new STS2_Editor.Scripts.Editor.Graph.DynamicPreviewContext
        {
            EntityKind = item.Kind,
            EntityId = item.EntityId,
            TargetSelector = MetadataOrFallback(metadata, "target_type", "current_target"),
            CurrentEnergy = ParseDecimal(metadata, "energy_cost", 1m),
            CurrentStars = ParseDecimal(metadata, "canonical_star_cost", 0m),
            CurrentBlock = 0m,
            HandCount = 5,
            DrawPileCount = 10,
            DiscardPileCount = 0,
            ExhaustPileCount = 0,
            MissingHp = 0m
        };

        if (metadata.TryGetValue("upgraded", out var upgraded) && bool.TryParse(upgraded, out var upgradedValue))
        {
            context.Upgraded = upgradedValue;
        }

        PopulatePreviewMultipliers(context);
        return context;
    }

    private static AbstractModel? ResolveSourceModel(ModStudioEntityKind kind, string entityId)
    {
        return kind switch
        {
            ModStudioEntityKind.Card => ModelDb.AllCards.FirstOrDefault(item => string.Equals(item.Id.Entry, entityId, StringComparison.Ordinal)),
            ModStudioEntityKind.Relic => ModelDb.AllRelics.FirstOrDefault(item => string.Equals(item.Id.Entry, entityId, StringComparison.Ordinal)),
            ModStudioEntityKind.Potion => ModelDb.AllPotions.FirstOrDefault(item => string.Equals(item.Id.Entry, entityId, StringComparison.Ordinal)),
            ModStudioEntityKind.Event => ModelDb.AllEvents.FirstOrDefault(item => string.Equals(item.Id.Entry, entityId, StringComparison.Ordinal)),
            ModStudioEntityKind.Enchantment => ModelDb.DebugEnchantments.FirstOrDefault(item => string.Equals(item.Id.Entry, entityId, StringComparison.Ordinal)),
            ModStudioEntityKind.Character => ModelDb.AllCharacters.FirstOrDefault(item => string.Equals(item.Id.Entry, entityId, StringComparison.Ordinal)),
            _ => null
        };
    }

    private static string MetadataOrFallback(IReadOnlyDictionary<string, string> metadata, string key, string fallback)
    {
        return metadata.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;
    }

    private static decimal ParseDecimal(IReadOnlyDictionary<string, string> metadata, string key, decimal fallback)
    {
        return metadata.TryGetValue(key, out var value) &&
               decimal.TryParse(value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    private static void PopulatePreviewMultipliers(STS2_Editor.Scripts.Editor.Graph.DynamicPreviewContext context)
    {
        context.FormulaMultipliers["hand_count"] = context.HandCount;
        context.FormulaMultipliers["cards"] = context.HandCount;
        context.FormulaMultipliers["stars"] = context.CurrentStars;
        context.FormulaMultipliers["energy"] = context.CurrentEnergy;
        context.FormulaMultipliers["current_block"] = context.CurrentBlock;
        context.FormulaMultipliers["draw_pile"] = context.DrawPileCount;
        context.FormulaMultipliers["discard_pile"] = context.DiscardPileCount;
        context.FormulaMultipliers["exhaust_pile"] = context.ExhaustPileCount;
        context.FormulaMultipliers["missing_hp"] = context.MissingHp;
    }
}
