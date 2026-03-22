using MegaCrit.Sts2.Core.Models;
using STS2_Editor.Scripts.Editor.Core.Models;

namespace STS2_Editor.Scripts.Editor.Core.Services;

public sealed class ModelMetadataService
{
    public IReadOnlyList<EntityBrowserItem> GetItems(ModStudioEntityKind kind)
    {
        return kind switch
        {
            ModStudioEntityKind.Character => ModelDb.AllCharacters
                .OrderBy(character => character.Id.Entry)
                .Select(BuildCharacterItem)
                .ToList(),
            ModStudioEntityKind.Card => ModelDb.AllCards
                .OrderBy(card => card.Id.Entry)
                .Select(BuildCardItem)
                .ToList(),
            ModStudioEntityKind.Relic => ModelDb.AllRelics
                .OrderBy(relic => relic.Id.Entry)
                .Select(BuildRelicItem)
                .ToList(),
            ModStudioEntityKind.Potion => ModelDb.AllPotions
                .OrderBy(potion => potion.Id.Entry)
                .Select(BuildPotionItem)
                .ToList(),
            ModStudioEntityKind.Event => ModelDb.AllEvents
                .OrderBy(evt => evt.Id.Entry)
                .Select(BuildEventItem)
                .ToList(),
            ModStudioEntityKind.Enchantment => ModelDb.DebugEnchantments
                .OrderBy(enchantment => enchantment.Id.Entry)
                .Select(BuildEnchantmentItem)
                .ToList(),
            _ => Array.Empty<EntityBrowserItem>()
        };
    }

    public IReadOnlyDictionary<string, string> GetEditableMetadata(ModStudioEntityKind kind, string entityId)
    {
        if (string.IsNullOrWhiteSpace(entityId))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        return kind switch
        {
            ModStudioEntityKind.Character => BuildCharacterMetadata(ModelDb.AllCharacters.FirstOrDefault(character => character.Id.Entry == entityId)),
            ModStudioEntityKind.Card => BuildCardMetadata(ModelDb.AllCards.FirstOrDefault(card => card.Id.Entry == entityId)),
            ModStudioEntityKind.Relic => BuildRelicMetadata(ModelDb.AllRelics.FirstOrDefault(relic => relic.Id.Entry == entityId)),
            ModStudioEntityKind.Potion => BuildPotionMetadata(ModelDb.AllPotions.FirstOrDefault(potion => potion.Id.Entry == entityId)),
            ModStudioEntityKind.Event => BuildEventMetadata(ModelDb.AllEvents.FirstOrDefault(evt => evt.Id.Entry == entityId)),
            ModStudioEntityKind.Enchantment => BuildEnchantmentMetadata(ModelDb.DebugEnchantments.FirstOrDefault(enchantment => enchantment.Id.Entry == entityId)),
            _ => new Dictionary<string, string>(StringComparer.Ordinal)
        };
    }

    private static EntityBrowserItem BuildCharacterItem(CharacterModel character)
    {
        return new EntityBrowserItem
        {
            Kind = ModStudioEntityKind.Character,
            EntityId = character.Id.Entry,
            Title = SafeText(() => character.Title.GetFormattedText()),
            Summary = $"HP {character.StartingHp} | Gold {character.StartingGold} | Energy {character.MaxEnergy}",
            DetailText = string.Join(
                Environment.NewLine,
                $"[Source Of Truth] runtime CharacterModel",
                $"Id: {character.Id}",
                $"Title: {SafeText(() => character.Title.GetFormattedText())}",
                $"Starting HP: {character.StartingHp}",
                $"Starting Gold: {character.StartingGold}",
                $"Max Energy: {character.MaxEnergy}",
                $"Starting Deck Size: {character.StartingDeck.Count()}",
                $"Starting Relics: {string.Join(", ", character.StartingRelics.Select(relic => relic.Id.Entry))}",
                $"Starting Potions: {string.Join(", ", character.StartingPotions.Select(potion => potion.Id.Entry))}")
        };
    }

    private static IReadOnlyDictionary<string, string> BuildCharacterMetadata(CharacterModel? character)
    {
        if (character is null)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["title"] = SafeText(() => character.Title.GetFormattedText()),
            ["starting_hp"] = character.StartingHp.ToString(),
            ["starting_gold"] = character.StartingGold.ToString(),
            ["max_energy"] = character.MaxEnergy.ToString(),
            ["starting_deck_ids"] = string.Join(", ", character.StartingDeck.Select(card => card.Id.Entry)),
            ["starting_relic_ids"] = string.Join(", ", character.StartingRelics.Select(relic => relic.Id.Entry)),
            ["starting_potion_ids"] = string.Join(", ", character.StartingPotions.Select(potion => potion.Id.Entry))
        };
    }

    private static EntityBrowserItem BuildCardItem(CardModel card)
    {
        return new EntityBrowserItem
        {
            Kind = ModStudioEntityKind.Card,
            EntityId = card.Id.Entry,
            Title = card.Title,
            Summary = $"{card.Type} | {card.Rarity} | Pool {card.Pool.Id.Entry}",
            DetailText = string.Join(
                Environment.NewLine,
                $"[Source Of Truth] runtime CardModel",
                $"Id: {card.Id}",
                $"Title: {card.Title}",
                $"Type: {card.Type}",
                $"Rarity: {card.Rarity}",
                $"Pool: {card.Pool.Id.Entry}",
                $"Portrait Path: {card.PortraitPath}",
                $"Description: {SafeText(() => card.Description.GetFormattedText())}")
        };
    }

    private static IReadOnlyDictionary<string, string> BuildCardMetadata(CardModel? card)
    {
        if (card is null)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["title"] = card.Title,
            ["type"] = card.Type.ToString(),
            ["rarity"] = card.Rarity.ToString(),
            ["pool_id"] = card.Pool.Id.Entry,
            ["portrait_path"] = card.PortraitPath,
            ["description"] = SafeText(() => card.Description.GetFormattedText())
        };
    }

    private static EntityBrowserItem BuildRelicItem(RelicModel relic)
    {
        return new EntityBrowserItem
        {
            Kind = ModStudioEntityKind.Relic,
            EntityId = relic.Id.Entry,
            Title = SafeText(() => relic.Title.GetFormattedText()),
            Summary = $"{relic.Rarity} | Pool {relic.Pool.Id.Entry}",
            DetailText = string.Join(
                Environment.NewLine,
                $"[Source Of Truth] runtime RelicModel",
                $"Id: {relic.Id}",
                $"Title: {SafeText(() => relic.Title.GetFormattedText())}",
                $"Rarity: {relic.Rarity}",
                $"Pool: {relic.Pool.Id.Entry}",
                $"Icon Path: {relic.IconPath}",
                $"Description: {SafeText(() => relic.Description.GetFormattedText())}")
        };
    }

    private static IReadOnlyDictionary<string, string> BuildRelicMetadata(RelicModel? relic)
    {
        if (relic is null)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["title"] = SafeText(() => relic.Title.GetFormattedText()),
            ["rarity"] = relic.Rarity.ToString(),
            ["pool_id"] = relic.Pool.Id.Entry,
            ["icon_path"] = relic.IconPath,
            ["description"] = SafeText(() => relic.Description.GetFormattedText())
        };
    }

    private static EntityBrowserItem BuildPotionItem(PotionModel potion)
    {
        return new EntityBrowserItem
        {
            Kind = ModStudioEntityKind.Potion,
            EntityId = potion.Id.Entry,
            Title = SafeText(() => potion.Title.GetFormattedText()),
            Summary = $"{potion.Rarity} | {potion.Usage} | {potion.TargetType}",
            DetailText = string.Join(
                Environment.NewLine,
                $"[Source Of Truth] runtime PotionModel",
                $"Id: {potion.Id}",
                $"Title: {SafeText(() => potion.Title.GetFormattedText())}",
                $"Rarity: {potion.Rarity}",
                $"Usage: {potion.Usage}",
                $"Target Type: {potion.TargetType}",
                $"Image Path: {potion.ImagePath}",
                $"Description: {SafeText(() => potion.Description.GetFormattedText())}")
        };
    }

    private static IReadOnlyDictionary<string, string> BuildPotionMetadata(PotionModel? potion)
    {
        if (potion is null)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["title"] = SafeText(() => potion.Title.GetFormattedText()),
            ["rarity"] = potion.Rarity.ToString(),
            ["usage"] = potion.Usage.ToString(),
            ["target_type"] = potion.TargetType.ToString(),
            ["image_path"] = potion.ImagePath,
            ["description"] = SafeText(() => potion.Description.GetFormattedText())
        };
    }

    private static EntityBrowserItem BuildEventItem(EventModel evt)
    {
        return new EntityBrowserItem
        {
            Kind = ModStudioEntityKind.Event,
            EntityId = evt.Id.Entry,
            Title = SafeText(() => evt.Title.GetFormattedText()),
            Summary = $"{evt.LayoutType} | Shared {evt.IsShared}",
            DetailText = string.Join(
                Environment.NewLine,
                $"[Source Of Truth] runtime EventModel",
                $"Id: {evt.Id}",
                $"Title: {SafeText(() => evt.Title.GetFormattedText())}",
                $"Layout: {evt.LayoutType}",
                $"Is Shared: {evt.IsShared}",
                $"Initial Description: {SafeText(() => evt.InitialDescription.GetFormattedText())}")
        };
    }

    private static IReadOnlyDictionary<string, string> BuildEventMetadata(EventModel? evt)
    {
        if (evt is null)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["title"] = SafeText(() => evt.Title.GetFormattedText()),
            ["layout_type"] = evt.LayoutType.ToString(),
            ["is_shared"] = evt.IsShared.ToString(),
            ["initial_description"] = SafeText(() => evt.InitialDescription.GetFormattedText())
        };
    }

    private static EntityBrowserItem BuildEnchantmentItem(EnchantmentModel enchantment)
    {
        return new EntityBrowserItem
        {
            Kind = ModStudioEntityKind.Enchantment,
            EntityId = enchantment.Id.Entry,
            Title = SafeText(() => enchantment.Title.GetFormattedText()),
            Summary = $"Show Amount {enchantment.ShowAmount} | Icon {enchantment.IconPath}",
            DetailText = string.Join(
                Environment.NewLine,
                $"[Source Of Truth] runtime EnchantmentModel",
                $"Id: {enchantment.Id}",
                $"Title: {SafeText(() => enchantment.Title.GetFormattedText())}",
                $"Icon Path: {enchantment.IconPath}",
                $"Description: {SafeText(() => enchantment.Description.GetFormattedText())}",
                $"Extra Card Text Enabled: {enchantment.HasExtraCardText}")
        };
    }

    private static IReadOnlyDictionary<string, string> BuildEnchantmentMetadata(EnchantmentModel? enchantment)
    {
        if (enchantment is null)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["title"] = SafeText(() => enchantment.Title.GetFormattedText()),
            ["icon_path"] = enchantment.IconPath,
            ["description"] = SafeText(() => enchantment.Description.GetFormattedText()),
            ["show_amount"] = enchantment.ShowAmount.ToString(),
            ["has_extra_card_text"] = enchantment.HasExtraCardText.ToString()
        };
    }

    private static string SafeText(Func<string> getter)
    {
        try
        {
            return getter();
        }
        catch (Exception ex)
        {
            return $"<unavailable: {ex.GetType().Name}>";
        }
    }
}
