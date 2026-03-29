using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;
using STS2_Editor.Scripts.Editor.Core.Models;
using STS2_Editor.Scripts.Editor.Core.Utilities;
using STS2_Editor.Scripts.Editor.UI;

namespace STS2_Editor.Scripts.Editor.Core.Services;

public sealed partial class ModelMetadataService
{
    private static readonly Dictionary<string, IReadOnlyDictionary<string, string>> RuntimeMetadataCache = new(StringComparer.OrdinalIgnoreCase);
    private static bool? _cachedLanguageIsChinese;

    private static readonly IReadOnlyDictionary<ModStudioEntityKind, ModStudioAssetBinding> AssetBindings =
        new Dictionary<ModStudioEntityKind, ModStudioAssetBinding>
        {
            [ModStudioEntityKind.Card] = new() { Kind = ModStudioEntityKind.Card, LogicalRole = "portrait", MetadataKey = "portrait_path", DisplayNameKey = "asset.slot.card_portrait" },
            [ModStudioEntityKind.Relic] = new() { Kind = ModStudioEntityKind.Relic, LogicalRole = "icon", MetadataKey = "icon_path", DisplayNameKey = "asset.slot.relic_icon" },
            [ModStudioEntityKind.Potion] = new() { Kind = ModStudioEntityKind.Potion, LogicalRole = "image", MetadataKey = "image_path", DisplayNameKey = "asset.slot.potion_image" },
            [ModStudioEntityKind.Event] = new() { Kind = ModStudioEntityKind.Event, LogicalRole = "portrait", MetadataKey = "portrait_path", DisplayNameKey = "asset.slot.event_portrait", SupportsRuntimeSelection = true },
            [ModStudioEntityKind.Enchantment] = new() { Kind = ModStudioEntityKind.Enchantment, LogicalRole = "icon", MetadataKey = "icon_path", DisplayNameKey = "asset.slot.enchantment_icon" }
        };

    public IReadOnlyList<EntityBrowserItem> GetItems(ModStudioEntityKind kind, EditorProject? project = null)
    {
        var items = GetRuntimeItems(kind).ToDictionary(item => item.EntityId, StringComparer.Ordinal);
        if (project != null)
        {
            foreach (var envelope in project.Overrides.Where(overrideEnvelope => overrideEnvelope.EntityKind == kind))
            {
                if (items.TryGetValue(envelope.EntityId, out var existing))
                {
                    var mergedMetadata = MergeMetadata(GetEditableMetadata(kind, envelope.EntityId), envelope);
                    items[envelope.EntityId] = BuildMergedRuntimeItem(existing, mergedMetadata);
                    continue;
                }

                if (SupportsProjectOnlyEntries(kind))
                {
                    items[envelope.EntityId] = BuildProjectOnlyItem(project, envelope);
                }
            }
        }

        return items.Values.OrderBy(item => item.EntityId, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public IReadOnlyDictionary<string, string> GetEditableMetadata(ModStudioEntityKind kind, string entityId)
    {
        if (string.IsNullOrWhiteSpace(entityId))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        EnsureRuntimeMetadataCacheLanguage();
        var cacheKey = $"{kind}:{entityId}";
        if (RuntimeMetadataCache.TryGetValue(cacheKey, out var cached))
        {
            return new Dictionary<string, string>(cached, StringComparer.Ordinal);
        }

        var result = kind switch
        {
            ModStudioEntityKind.Character => BuildCharacterMetadata(ModelDb.AllCharacters.FirstOrDefault(character => character.Id.Entry == entityId)),
            ModStudioEntityKind.Card => BuildCardMetadata(ModelDb.AllCards.FirstOrDefault(card => card.Id.Entry == entityId)),
            ModStudioEntityKind.Relic => BuildRelicMetadata(ModelDb.AllRelics.FirstOrDefault(relic => relic.Id.Entry == entityId)),
            ModStudioEntityKind.Potion => BuildPotionMetadata(ModelDb.AllPotions.FirstOrDefault(potion => potion.Id.Entry == entityId)),
            ModStudioEntityKind.Event => BuildEventMetadata(ModelDb.AllEvents.FirstOrDefault(evt => evt.Id.Entry == entityId)),
            ModStudioEntityKind.Enchantment => BuildEnchantmentMetadata(ModelDb.DebugEnchantments.FirstOrDefault(enchantment => enchantment.Id.Entry == entityId)),
            ModStudioEntityKind.Monster => BuildMonsterMetadata(ModelDb.Monsters.FirstOrDefault(monster => string.Equals(monster.Id.Entry, entityId, StringComparison.Ordinal))),
            _ => new Dictionary<string, string>(StringComparer.Ordinal)
        };

        RuntimeMetadataCache[cacheKey] = new Dictionary<string, string>(result, StringComparer.Ordinal);
        return result;
    }

    public bool HasRuntimeEntity(ModStudioEntityKind kind, string entityId)
    {
        if (string.IsNullOrWhiteSpace(entityId))
        {
            return false;
        }

        return kind switch
        {
            ModStudioEntityKind.Character => ModelDb.AllCharacters.Any(character => character.Id.Entry == entityId),
            ModStudioEntityKind.Card => ModelDb.AllCards.Any(card => card.Id.Entry == entityId),
            ModStudioEntityKind.Relic => ModelDb.AllRelics.Any(relic => relic.Id.Entry == entityId),
            ModStudioEntityKind.Potion => ModelDb.AllPotions.Any(potion => potion.Id.Entry == entityId),
            ModStudioEntityKind.Event => ModelDb.AllEvents.Any(evt => evt.Id.Entry == entityId),
            ModStudioEntityKind.Enchantment => ModelDb.DebugEnchantments.Any(enchantment => enchantment.Id.Entry == entityId),
            ModStudioEntityKind.Monster => ModelDb.Monsters.Any(monster => string.Equals(monster.Id.Entry, entityId, StringComparison.Ordinal)),
            _ => false
        };
    }

    public bool TryGetAssetBinding(ModStudioEntityKind kind, out ModStudioAssetBinding binding) => AssetBindings.TryGetValue(kind, out binding!);

    public string? GetRuntimeAssetPath(ModStudioEntityKind kind, string entityId)
    {
        if (string.IsNullOrWhiteSpace(entityId) || !TryGetAssetBinding(kind, out var binding))
        {
            return null;
        }

        return GetEditableMetadata(kind, entityId).TryGetValue(binding.MetadataKey, out var path) && !string.IsNullOrWhiteSpace(path) ? path : null;
    }

    public IReadOnlyList<string> GetRuntimeAssetCandidates(ModStudioEntityKind kind)
    {
        IEnumerable<string> paths = kind switch
        {
            ModStudioEntityKind.Card => ModelDb.AllCards.Select(card => SafeAssetPath(() => card.PortraitPath)),
            ModStudioEntityKind.Relic => ModelDb.AllRelics.Select(relic => SafeAssetPath(() => relic.IconPath)),
            ModStudioEntityKind.Potion => ModelDb.AllPotions.Select(potion => SafeAssetPath(() => potion.ImagePath)),
            ModStudioEntityKind.Event => ModelDb.AllEvents.Select(SafeEventPortraitPath),
            ModStudioEntityKind.Enchantment => ModelDb.DebugEnchantments.Select(enchantment => SafeAssetPath(() => enchantment.IconPath)),
            _ => Array.Empty<string>()
        };

        return paths.Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();
    }

    private IReadOnlyList<EntityBrowserItem> GetRuntimeItems(ModStudioEntityKind kind)
    {
        return kind switch
        {
            ModStudioEntityKind.Character => ModelDb.AllCharacters.OrderBy(character => character.Id.Entry).Select(BuildCharacterItem).ToList(),
            ModStudioEntityKind.Card => ModelDb.AllCards.OrderBy(card => card.Id.Entry).Select(BuildCardItem).ToList(),
            ModStudioEntityKind.Relic => ModelDb.AllRelics.OrderBy(relic => relic.Id.Entry).Select(BuildRelicItem).ToList(),
            ModStudioEntityKind.Potion => ModelDb.AllPotions.OrderBy(potion => potion.Id.Entry).Select(BuildPotionItem).ToList(),
            ModStudioEntityKind.Event => ModelDb.AllEvents.OrderBy(evt => evt.Id.Entry).Select(BuildEventItem).ToList(),
            ModStudioEntityKind.Enchantment => ModelDb.DebugEnchantments.OrderBy(enchantment => enchantment.Id.Entry).Select(BuildEnchantmentItem).ToList(),
            ModStudioEntityKind.Monster => ModelDb.Monsters.OrderBy(monster => monster.Id.Entry).Select(BuildMonsterItem).ToList(),
            _ => Array.Empty<EntityBrowserItem>()
        };
    }

    private static EntityBrowserItem BuildCharacterItem(CharacterModel character) => new()
    {
        Kind = ModStudioEntityKind.Character,
        EntityId = character.Id.Entry,
        Title = SafeLocText(character.Title),
        Summary = $"HP {character.StartingHp} | Gold {character.StartingGold} | Energy {character.MaxEnergy}",
        DetailText = string.Join(Environment.NewLine,
            SourceOfTruth("CharacterModel"),
            Detail("detail.id", character.Id),
            Detail("detail.title", SafeLocText(character.Title)),
            Detail("detail.starting_hp", character.StartingHp),
            Detail("detail.starting_gold", character.StartingGold),
            Detail("detail.max_energy", character.MaxEnergy),
            Detail("detail.base_orb_slot_count", character.BaseOrbSlotCount),
            Detail("detail.starting_deck_size", character.StartingDeck.Count()),
            Detail("detail.starting_relics", JoinIds(character.StartingRelics.Select(relic => relic.Id.Entry))),
            Detail("detail.starting_potions", JoinIds(character.StartingPotions.Select(potion => potion.Id.Entry))))
    };

    private static IReadOnlyDictionary<string, string> BuildCharacterMetadata(CharacterModel? character) => character == null
        ? new Dictionary<string, string>(StringComparer.Ordinal)
        : new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["title"] = SafeLocText(character.Title),
            ["starting_hp"] = character.StartingHp.ToString(),
            ["starting_gold"] = character.StartingGold.ToString(),
            ["max_energy"] = character.MaxEnergy.ToString(),
            ["base_orb_slot_count"] = character.BaseOrbSlotCount.ToString(),
            ["starting_deck_ids"] = string.Join(", ", character.StartingDeck.Select(card => card.Id.Entry)),
            ["starting_relic_ids"] = string.Join(", ", character.StartingRelics.Select(relic => relic.Id.Entry)),
            ["starting_potion_ids"] = string.Join(", ", character.StartingPotions.Select(potion => potion.Id.Entry))
        };

    private static EntityBrowserItem BuildCardItem(CardModel card) => new()
    {
        Kind = ModStudioEntityKind.Card,
        EntityId = card.Id.Entry,
        Title = card.Title,
        Summary = $"{ModStudioFieldDisplayNames.FormatPropertyValue("type", card.Type.ToString())} | {ModStudioFieldDisplayNames.FormatPropertyValue("rarity", card.Rarity.ToString())} | {Dual("卡池", "Pool")} {ModStudioFieldDisplayNames.FormatPropertyValue("pool_id", card.Pool.Id.Entry)}",
        DetailText = string.Join(Environment.NewLine,
            SourceOfTruth("CardModel"),
            Detail("detail.id", card.Id),
            Detail("detail.title", card.Title),
            Detail("detail.type", ModStudioFieldDisplayNames.FormatPropertyValue("type", card.Type.ToString())),
            Detail("detail.rarity", ModStudioFieldDisplayNames.FormatPropertyValue("rarity", card.Rarity.ToString())),
            Detail("detail.pool", ModStudioFieldDisplayNames.FormatPropertyValue("pool_id", card.Pool.Id.Entry)),
            Detail("detail.target_type", ModStudioFieldDisplayNames.FormatPropertyValue("target_type", card.TargetType.ToString())),
            Detail("detail.energy_cost", SafeCardEnergyCostText(card)),
            Detail("detail.portrait_path", SafeAssetPath(() => card.PortraitPath)),
            Detail("detail.description_text", SafeLocText(card.Description)))
    };

    private static IReadOnlyDictionary<string, string> BuildCardMetadata(CardModel? card) => card == null
        ? new Dictionary<string, string>(StringComparer.Ordinal)
        : new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["title"] = card.Title,
            ["type"] = card.Type.ToString(),
            ["rarity"] = card.Rarity.ToString(),
            ["pool_id"] = card.Pool.Id.Entry,
            ["portrait_path"] = SafeAssetPath(() => card.PortraitPath),
            ["description"] = SafeLocText(card.Description),
            ["target_type"] = card.TargetType.ToString(),
            ["energy_cost"] = SafeCardEnergyCostValue(card),
            ["energy_cost_x"] = SafeCardCostsX(card).ToString(),
            ["canonical_star_cost"] = card.CanonicalStarCost.ToString(),
            ["star_cost_x"] = card.HasStarCostX.ToString(),
            ["can_be_generated_in_combat"] = card.CanBeGeneratedInCombat.ToString()
        };

    private static EntityBrowserItem BuildRelicItem(RelicModel relic) => new()
    {
        Kind = ModStudioEntityKind.Relic,
        EntityId = relic.Id.Entry,
        Title = SafeLocText(relic.Title),
        Summary = $"{ModStudioFieldDisplayNames.FormatPropertyValue("rarity", relic.Rarity.ToString())} | {Dual("卡池", "Pool")} {ModStudioFieldDisplayNames.FormatPropertyValue("pool_id", relic.Pool.Id.Entry)}",
        DetailText = string.Join(Environment.NewLine,
            SourceOfTruth("RelicModel"),
            Detail("detail.id", relic.Id),
            Detail("detail.title", SafeLocText(relic.Title)),
            Detail("detail.rarity", ModStudioFieldDisplayNames.FormatPropertyValue("rarity", relic.Rarity.ToString())),
            Detail("detail.pool", ModStudioFieldDisplayNames.FormatPropertyValue("pool_id", relic.Pool.Id.Entry)),
            Detail("detail.icon_path", SafeAssetPath(() => relic.IconPath)),
            Detail("detail.description_text", SafeLocText(relic.Description)))
    };

    private static IReadOnlyDictionary<string, string> BuildRelicMetadata(RelicModel? relic) => relic == null
        ? new Dictionary<string, string>(StringComparer.Ordinal)
        : new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["title"] = SafeLocText(relic.Title),
            ["rarity"] = relic.Rarity.ToString(),
            ["pool_id"] = relic.Pool.Id.Entry,
            ["icon_path"] = SafeAssetPath(() => relic.IconPath),
            ["description"] = SafeLocText(relic.Description)
        };

    private static EntityBrowserItem BuildPotionItem(PotionModel potion) => new()
    {
        Kind = ModStudioEntityKind.Potion,
        EntityId = potion.Id.Entry,
        Title = SafeLocText(potion.Title),
        Summary = $"{ModStudioFieldDisplayNames.FormatPropertyValue("rarity", potion.Rarity.ToString())} | {ModStudioFieldDisplayNames.FormatPropertyValue("usage", potion.Usage.ToString())} | {ModStudioFieldDisplayNames.FormatPropertyValue("target_type", potion.TargetType.ToString())}",
        DetailText = string.Join(Environment.NewLine,
            SourceOfTruth("PotionModel"),
            Detail("detail.id", potion.Id),
            Detail("detail.title", SafeLocText(potion.Title)),
            Detail("detail.rarity", ModStudioFieldDisplayNames.FormatPropertyValue("rarity", potion.Rarity.ToString())),
            Detail("detail.usage", ModStudioFieldDisplayNames.FormatPropertyValue("usage", potion.Usage.ToString())),
            Detail("detail.target_type", ModStudioFieldDisplayNames.FormatPropertyValue("target_type", potion.TargetType.ToString())),
            Detail("detail.pool", ModStudioFieldDisplayNames.FormatPropertyValue("pool_id", potion.Pool.Id.Entry)),
            Detail("detail.image_path", SafeAssetPath(() => potion.ImagePath)),
            Detail("detail.description_text", SafeLocText(potion.Description)))
    };

    private static IReadOnlyDictionary<string, string> BuildPotionMetadata(PotionModel? potion) => potion == null
        ? new Dictionary<string, string>(StringComparer.Ordinal)
        : new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["title"] = SafeLocText(potion.Title),
            ["rarity"] = potion.Rarity.ToString(),
            ["usage"] = potion.Usage.ToString(),
            ["target_type"] = potion.TargetType.ToString(),
            ["pool_id"] = potion.Pool.Id.Entry,
            ["image_path"] = SafeAssetPath(() => potion.ImagePath),
            ["description"] = SafeLocText(potion.Description),
            ["can_be_generated_in_combat"] = potion.CanBeGeneratedInCombat.ToString()
        };

    private static EntityBrowserItem BuildEventItem(EventModel evt) => new()
    {
        Kind = ModStudioEntityKind.Event,
        EntityId = evt.Id.Entry,
        Title = SafeLocText(evt.Title),
        Summary = $"{ModStudioFieldDisplayNames.FormatPropertyValue("layout_type", evt.LayoutType.ToString())} | {Dual("共享", "Shared")} {ModStudioFieldDisplayNames.FormatPropertyValue("is_shared", evt.IsShared.ToString())}",
        DetailText = string.Join(Environment.NewLine,
            SourceOfTruth("EventModel"),
            Detail("detail.id", evt.Id),
            Detail("detail.title", SafeLocText(evt.Title)),
            Detail("detail.layout", ModStudioFieldDisplayNames.FormatPropertyValue("layout_type", evt.LayoutType.ToString())),
            Detail("detail.is_shared", BoolText(evt.IsShared)),
            Detail("detail.portrait_path", SafeEventPortraitPath(evt)),
            Detail("detail.initial_description", SafeLocText(evt.InitialDescription)))
    };

    private static IReadOnlyDictionary<string, string> BuildEventMetadata(EventModel? evt) => evt == null
        ? new Dictionary<string, string>(StringComparer.Ordinal)
        : new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["title"] = SafeLocText(evt.Title),
            ["layout_type"] = evt.LayoutType.ToString(),
            ["is_shared"] = evt.IsShared.ToString(),
            ["portrait_path"] = SafeEventPortraitPath(evt),
            ["initial_description"] = SafeLocText(evt.InitialDescription)
        };

    private static EntityBrowserItem BuildEnchantmentItem(EnchantmentModel enchantment) => new()
    {
        Kind = ModStudioEntityKind.Enchantment,
        EntityId = enchantment.Id.Entry,
        Title = SafeLocText(enchantment.Title),
        Summary = $"{Dual("显示数值", "Show Amount")} {ModStudioFieldDisplayNames.FormatPropertyValue("show_amount", enchantment.ShowAmount.ToString())} | {Dual("图标", "Icon")} {enchantment.IconPath}",
        DetailText = string.Join(Environment.NewLine,
            SourceOfTruth("EnchantmentModel"),
            Detail("detail.id", enchantment.Id),
            Detail("detail.title", SafeLocText(enchantment.Title)),
            Detail("detail.icon_path", SafeAssetPath(() => enchantment.IconPath)),
            Detail("detail.description_text", SafeLocText(enchantment.Description)),
            Detail("detail.extra_card_text_enabled", BoolText(enchantment.HasExtraCardText)))
    };

    private static IReadOnlyDictionary<string, string> BuildEnchantmentMetadata(EnchantmentModel? enchantment) => enchantment == null
        ? new Dictionary<string, string>(StringComparer.Ordinal)
        : new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["title"] = SafeLocText(enchantment.Title),
            ["icon_path"] = SafeAssetPath(() => enchantment.IconPath),
            ["description"] = SafeLocText(enchantment.Description),
            ["show_amount"] = enchantment.ShowAmount.ToString(),
            ["has_extra_card_text"] = enchantment.HasExtraCardText.ToString(),
            ["extra_card_text"] = enchantment.HasExtraCardText ? SafeLocText(enchantment.ExtraCardText) : string.Empty
        };

    private static EntityBrowserItem BuildMonsterItem(MonsterModel monster) => new()
    {
        Kind = ModStudioEntityKind.Monster,
        EntityId = monster.Id.Entry,
        Title = SafeLocText(monster.Title),
        Summary = $"HP {MonsterHpSummary(monster)}",
        DetailText = string.Join(Environment.NewLine,
            SourceOfTruth("MonsterModel"),
            Detail("detail.id", monster.Id),
            Detail("detail.title", SafeLocText(monster.Title)),
            $"{ModStudioFieldDisplayNames.Get("min_initial_hp")}: {monster.MinInitialHp}",
            $"{ModStudioFieldDisplayNames.Get("max_initial_hp")}: {monster.MaxInitialHp}")
    };

    private static IReadOnlyDictionary<string, string> BuildMonsterMetadata(MonsterModel? monster) => monster == null
        ? new Dictionary<string, string>(StringComparer.Ordinal)
        : new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["title"] = SafeLocText(monster.Title),
            ["min_initial_hp"] = monster.MinInitialHp.ToString(),
            ["max_initial_hp"] = monster.MaxInitialHp.ToString()
        };

    private static string MonsterHpSummary(MonsterModel monster)
    {
        return monster.MinInitialHp == monster.MaxInitialHp
            ? monster.MinInitialHp.ToString()
            : $"{monster.MinInitialHp}-{monster.MaxInitialHp}";
    }

    public string GenerateProjectEntityId(EditorProject project, ModStudioEntityKind kind)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (!SupportsProjectOnlyEntries(kind))
        {
            throw new InvalidOperationException($"Entity kind '{kind}' does not support project-local creation in Phase 1.");
        }

        var projectSlug = Slugify(project.Manifest.ProjectId);
        if (projectSlug.Length > 8) projectSlug = projectSlug[..8];
        if (string.IsNullOrWhiteSpace(projectSlug)) projectSlug = "project";
        var prefix = $"ed_{projectSlug}__{kind.ToString().ToLowerInvariant()}_";
        var existingIds = new HashSet<string>(project.Overrides.Where(overrideEnvelope => overrideEnvelope.EntityKind == kind).Select(overrideEnvelope => overrideEnvelope.EntityId), StringComparer.OrdinalIgnoreCase);
        for (var index = 1; index < 10000; index++)
        {
            var candidate = $"{prefix}{index:000}";
            if (!existingIds.Contains(candidate) && !HasRuntimeEntity(kind, candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException($"Could not generate a unique Mod Studio id for kind '{kind}'.");
    }

    public IReadOnlyDictionary<string, string> CreateDefaultMetadata(ModStudioEntityKind kind, string entityId)
    {
        return kind switch
        {
            ModStudioEntityKind.Card => new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["title"] = ModStudioLocalization.F("default.card_title", entityId),
                ["type"] = CardType.Attack.ToString(),
                ["rarity"] = CardRarity.Common.ToString(),
                ["pool_id"] = ResolveDefaultCardPoolId(),
                ["portrait_path"] = ResolveDefaultAssetPath(ModStudioEntityKind.Card),
                ["description"] = ModStudioLocalization.T("default.card_description"),
                ["target_type"] = TargetType.AnyEnemy.ToString(),
                ["energy_cost"] = "1",
                ["energy_cost_x"] = false.ToString(),
                ["canonical_star_cost"] = "-1",
                ["star_cost_x"] = false.ToString(),
                ["can_be_generated_in_combat"] = true.ToString()
            },
            ModStudioEntityKind.Relic => new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["title"] = ModStudioLocalization.F("default.relic_title", entityId),
                ["rarity"] = RelicRarity.Common.ToString(),
                ["pool_id"] = ResolveDefaultRelicPoolId(),
                ["icon_path"] = ResolveDefaultAssetPath(ModStudioEntityKind.Relic),
                ["description"] = ModStudioLocalization.T("default.relic_description")
            },
            ModStudioEntityKind.Potion => new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["title"] = ModStudioLocalization.F("default.potion_title", entityId),
                ["rarity"] = PotionRarity.Common.ToString(),
                ["usage"] = PotionUsage.CombatOnly.ToString(),
                ["target_type"] = TargetType.Self.ToString(),
                ["pool_id"] = ResolveDefaultPotionPoolId(),
                ["image_path"] = ResolveDefaultAssetPath(ModStudioEntityKind.Potion),
                ["description"] = ModStudioLocalization.T("default.potion_description"),
                ["can_be_generated_in_combat"] = true.ToString()
            },
            ModStudioEntityKind.Event => CreateDefaultEventMetadata(entityId),
            ModStudioEntityKind.Enchantment => new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["title"] = ModStudioLocalization.F("default.enchantment_title", entityId),
                ["icon_path"] = ResolveDefaultAssetPath(ModStudioEntityKind.Enchantment),
                ["description"] = ModStudioLocalization.T("default.enchantment_description"),
                ["show_amount"] = false.ToString(),
                ["has_extra_card_text"] = false.ToString(),
                ["extra_card_text"] = string.Empty
            },
            _ => new Dictionary<string, string>(StringComparer.Ordinal)
        };
    }

    private static EntityBrowserItem BuildProjectOnlyItem(EditorProject project, EntityOverrideEnvelope envelope)
    {
        var metadata = envelope.Metadata;
        return envelope.EntityKind switch
        {
            ModStudioEntityKind.Card => new EntityBrowserItem
            {
                Kind = envelope.EntityKind,
                EntityId = envelope.EntityId,
                IsProjectOnly = true,
                Title = MetadataOrFallback(metadata, "title", envelope.EntityId),
                Summary = $"{ModStudioFieldDisplayNames.FormatPropertyValue("type", MetadataOrFallback(metadata, "type", CardType.Attack.ToString()))} | {ModStudioFieldDisplayNames.FormatPropertyValue("rarity", MetadataOrFallback(metadata, "rarity", CardRarity.Common.ToString()))} | {Dual("卡池", "Pool")} {ModStudioFieldDisplayNames.FormatPropertyValue("pool_id", MetadataOrFallback(metadata, "pool_id", "-"))}",
                DetailText = BuildProjectOnlyDetail(project.Manifest.Name, envelope,
                    Detail("detail.type", ModStudioFieldDisplayNames.FormatPropertyValue("type", MetadataOrFallback(metadata, "type", CardType.Attack.ToString()))),
                    Detail("detail.rarity", ModStudioFieldDisplayNames.FormatPropertyValue("rarity", MetadataOrFallback(metadata, "rarity", CardRarity.Common.ToString()))),
                    Detail("detail.pool", ModStudioFieldDisplayNames.FormatPropertyValue("pool_id", MetadataOrFallback(metadata, "pool_id", "-"))),
                    Detail("detail.target_type", ModStudioFieldDisplayNames.FormatPropertyValue("target_type", MetadataOrFallback(metadata, "target_type", TargetType.AnyEnemy.ToString()))),
                    Detail("detail.energy_cost", MetadataOrFallback(metadata, "energy_cost", "1")),
                    Detail("detail.description_text", MetadataOrFallback(metadata, "description", string.Empty)))
            },
            ModStudioEntityKind.Relic => new EntityBrowserItem
            {
                Kind = envelope.EntityKind,
                EntityId = envelope.EntityId,
                IsProjectOnly = true,
                Title = MetadataOrFallback(metadata, "title", envelope.EntityId),
                Summary = $"{ModStudioFieldDisplayNames.FormatPropertyValue("rarity", MetadataOrFallback(metadata, "rarity", RelicRarity.Common.ToString()))} | {Dual("卡池", "Pool")} {ModStudioFieldDisplayNames.FormatPropertyValue("pool_id", MetadataOrFallback(metadata, "pool_id", "-"))}",
                DetailText = BuildProjectOnlyDetail(project.Manifest.Name, envelope,
                    Detail("detail.rarity", ModStudioFieldDisplayNames.FormatPropertyValue("rarity", MetadataOrFallback(metadata, "rarity", RelicRarity.Common.ToString()))),
                    Detail("detail.pool", ModStudioFieldDisplayNames.FormatPropertyValue("pool_id", MetadataOrFallback(metadata, "pool_id", "-"))),
                    Detail("detail.icon_path", MetadataOrFallback(metadata, "icon_path", "-")),
                    Detail("detail.description_text", MetadataOrFallback(metadata, "description", string.Empty)))
            },
            ModStudioEntityKind.Potion => new EntityBrowserItem
            {
                Kind = envelope.EntityKind,
                EntityId = envelope.EntityId,
                IsProjectOnly = true,
                Title = MetadataOrFallback(metadata, "title", envelope.EntityId),
                Summary = $"{ModStudioFieldDisplayNames.FormatPropertyValue("rarity", MetadataOrFallback(metadata, "rarity", PotionRarity.Common.ToString()))} | {ModStudioFieldDisplayNames.FormatPropertyValue("usage", MetadataOrFallback(metadata, "usage", PotionUsage.CombatOnly.ToString()))} | {ModStudioFieldDisplayNames.FormatPropertyValue("target_type", MetadataOrFallback(metadata, "target_type", TargetType.Self.ToString()))}",
                DetailText = BuildProjectOnlyDetail(project.Manifest.Name, envelope,
                    Detail("detail.rarity", ModStudioFieldDisplayNames.FormatPropertyValue("rarity", MetadataOrFallback(metadata, "rarity", PotionRarity.Common.ToString()))),
                    Detail("detail.usage", ModStudioFieldDisplayNames.FormatPropertyValue("usage", MetadataOrFallback(metadata, "usage", PotionUsage.CombatOnly.ToString()))),
                    Detail("detail.target_type", ModStudioFieldDisplayNames.FormatPropertyValue("target_type", MetadataOrFallback(metadata, "target_type", TargetType.Self.ToString()))),
                    Detail("detail.pool", ModStudioFieldDisplayNames.FormatPropertyValue("pool_id", MetadataOrFallback(metadata, "pool_id", "-"))),
                    Detail("detail.image_path", MetadataOrFallback(metadata, "image_path", "-")),
                    Detail("detail.description_text", MetadataOrFallback(metadata, "description", string.Empty)))
            },
            ModStudioEntityKind.Event => new EntityBrowserItem
            {
                Kind = envelope.EntityKind,
                EntityId = envelope.EntityId,
                IsProjectOnly = true,
                Title = MetadataOrFallback(metadata, "title", envelope.EntityId),
                Summary = $"{ModStudioFieldDisplayNames.FormatPropertyValue("layout_type", MetadataOrFallback(metadata, "layout_type", EventLayoutType.Default.ToString()))} | {Dual("共享", "Shared")} {ModStudioFieldDisplayNames.FormatPropertyValue("is_shared", MetadataOrFallback(metadata, "is_shared", true.ToString()))}",
                DetailText = BuildProjectOnlyDetail(project.Manifest.Name, envelope,
                    Detail("detail.layout", ModStudioFieldDisplayNames.FormatPropertyValue("layout_type", MetadataOrFallback(metadata, "layout_type", EventLayoutType.Default.ToString()))),
                    Detail("detail.is_shared", ModStudioFieldDisplayNames.FormatPropertyValue("is_shared", MetadataOrFallback(metadata, "is_shared", true.ToString()))),
                    Detail("detail.portrait_path", MetadataOrFallback(metadata, "portrait_path", "-")),
                    Detail("detail.initial_description", MetadataOrFallback(metadata, "initial_description", string.Empty)))
            },
            ModStudioEntityKind.Enchantment => new EntityBrowserItem
            {
                Kind = envelope.EntityKind,
                EntityId = envelope.EntityId,
                IsProjectOnly = true,
                Title = MetadataOrFallback(metadata, "title", envelope.EntityId),
                Summary = $"{Dual("显示数值", "Show Amount")} {ModStudioFieldDisplayNames.FormatPropertyValue("show_amount", MetadataOrFallback(metadata, "show_amount", false.ToString()))} | {Dual("图标", "Icon")} {MetadataOrFallback(metadata, "icon_path", "-")}",
                DetailText = BuildProjectOnlyDetail(project.Manifest.Name, envelope,
                    Detail("detail.icon_path", MetadataOrFallback(metadata, "icon_path", "-")),
                    Detail("detail.description_text", MetadataOrFallback(metadata, "description", string.Empty)))
            },
            _ => new EntityBrowserItem { Kind = envelope.EntityKind, EntityId = envelope.EntityId, IsProjectOnly = true, Title = envelope.EntityId, Summary = ModStudioLocalization.T("placeholder.project_only_entry"), DetailText = ProjectOnlySource(project.Manifest.Name) }
        };
    }

    private static EntityBrowserItem BuildMergedRuntimeItem(EntityBrowserItem existing, IReadOnlyDictionary<string, string> metadata)
    {
        var title = MetadataOrFallback(metadata, "title", existing.Title);
        var summary = existing.Kind switch
        {
            ModStudioEntityKind.Character => $"HP {MetadataOrFallback(metadata, "starting_hp", "0")} | Gold {MetadataOrFallback(metadata, "starting_gold", "0")} | Energy {MetadataOrFallback(metadata, "max_energy", "0")}",
            ModStudioEntityKind.Card => $"{ModStudioFieldDisplayNames.FormatPropertyValue("type", MetadataOrFallback(metadata, "type", "Attack"))} | {ModStudioFieldDisplayNames.FormatPropertyValue("rarity", MetadataOrFallback(metadata, "rarity", "Common"))} | {Dual("卡池", "Pool")} {ModStudioFieldDisplayNames.FormatPropertyValue("pool_id", MetadataOrFallback(metadata, "pool_id", "-"))}",
            ModStudioEntityKind.Relic => $"{ModStudioFieldDisplayNames.FormatPropertyValue("rarity", MetadataOrFallback(metadata, "rarity", "Common"))} | {Dual("卡池", "Pool")} {ModStudioFieldDisplayNames.FormatPropertyValue("pool_id", MetadataOrFallback(metadata, "pool_id", "-"))}",
            ModStudioEntityKind.Potion => $"{ModStudioFieldDisplayNames.FormatPropertyValue("rarity", MetadataOrFallback(metadata, "rarity", "Common"))} | {ModStudioFieldDisplayNames.FormatPropertyValue("usage", MetadataOrFallback(metadata, "usage", "CombatOnly"))} | {ModStudioFieldDisplayNames.FormatPropertyValue("target_type", MetadataOrFallback(metadata, "target_type", "Self"))}",
            ModStudioEntityKind.Event => $"{ModStudioFieldDisplayNames.FormatPropertyValue("layout_type", MetadataOrFallback(metadata, "layout_type", "Default"))} | {Dual("共享", "Shared")} {ModStudioFieldDisplayNames.FormatPropertyValue("is_shared", MetadataOrFallback(metadata, "is_shared", "True"))}",
            ModStudioEntityKind.Enchantment => $"{Dual("显示数值", "Show Amount")} {ModStudioFieldDisplayNames.FormatPropertyValue("show_amount", MetadataOrFallback(metadata, "show_amount", "False"))} | {Dual("图标", "Icon")} {MetadataOrFallback(metadata, "icon_path", "-")}",
            _ => existing.Summary
        };

        return new EntityBrowserItem
        {
            Kind = existing.Kind,
            EntityId = existing.EntityId,
            IsProjectOnly = existing.IsProjectOnly,
            Title = title,
            Summary = summary,
            DetailText = existing.DetailText
        };
    }

    private static Dictionary<string, string> MergeMetadata(IReadOnlyDictionary<string, string> originalMetadata, EntityOverrideEnvelope? envelope)
    {
        var merged = new Dictionary<string, string>(originalMetadata, StringComparer.OrdinalIgnoreCase);
        if (envelope == null)
        {
            return merged;
        }

        foreach (var pair in envelope.Metadata)
        {
            merged[pair.Key] = pair.Value;
        }

        return merged;
    }

    private IReadOnlyDictionary<string, string> CreateDefaultEventMetadata(string entityId)
    {
        var initialDescription = ModStudioLocalization.T("default.event_initial_description");
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["title"] = ModStudioLocalization.F("default.event_title", entityId),
            ["layout_type"] = EventLayoutType.Default.ToString(),
            ["is_shared"] = true.ToString(),
            ["portrait_path"] = ResolveDefaultAssetPath(ModStudioEntityKind.Event),
            ["initial_description"] = initialDescription,
            ["event_start_page_id"] = "INITIAL",
            ["event_page.INITIAL.description"] = initialDescription,
            ["event_page.INITIAL.option_order"] = "CONTINUE",
            ["event_option.INITIAL.CONTINUE.title"] = ModStudioLocalization.T("event_template.default_option_continue_title"),
            ["event_option.INITIAL.CONTINUE.description"] = ModStudioLocalization.T("default.event_continue_description"),
            ["event_option.INITIAL.CONTINUE.next_page_id"] = "DONE",
            ["event_page.DONE.description"] = ModStudioLocalization.T("default.event_done_description")
        };
    }

    private string ResolveDefaultAssetPath(ModStudioEntityKind kind) => GetRuntimeAssetCandidates(kind).FirstOrDefault() ?? string.Empty;

    private static string ResolveDefaultCardPoolId() => ModelDb.AllCharacterCardPools.FirstOrDefault()?.Id.Entry ?? ModelDb.AllCardPools.FirstOrDefault()?.Id.Entry ?? string.Empty;

    private static string ResolveDefaultRelicPoolId() => ModelDb.AllRelicPools.FirstOrDefault()?.Id.Entry ?? string.Empty;

    private static string ResolveDefaultPotionPoolId() => ModelDb.AllPotionPools.FirstOrDefault()?.Id.Entry ?? string.Empty;

    private static bool SupportsProjectOnlyEntries(ModStudioEntityKind kind) => kind is ModStudioEntityKind.Card or ModStudioEntityKind.Relic or ModStudioEntityKind.Potion or ModStudioEntityKind.Event or ModStudioEntityKind.Enchantment;

    private static string Slugify(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : new string(value.Trim().Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static string MetadataOrFallback(IReadOnlyDictionary<string, string> metadata, string key, string fallback) => metadata.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;

    private static string BuildProjectOnlyDetail(string projectName, EntityOverrideEnvelope envelope, params string[] extraLines)
    {
        var lines = new List<string>
        {
            ProjectOnlySource(projectName),
            Detail("detail.id", envelope.EntityId),
            Detail("detail.title", MetadataOrFallback(envelope.Metadata, "title", envelope.EntityId)),
            Detail("detail.behavior_source", envelope.BehaviorSource),
            Detail("detail.graph_id", envelope.GraphId ?? "-")
        };
        lines.AddRange(extraLines);
        return string.Join(Environment.NewLine, lines);
    }

    private static string ProjectOnlySource(string projectName) => ModStudioLocalization.F("detail.project_staged_source", projectName);

    private static string SafeLocText(LocString? locString)
    {
        if (locString == null) return string.Empty;
        try { return locString.GetRawText(); }
        catch (Exception ex) { return ModStudioLocalization.F("misc.unavailable", ex.GetType().Name); }
    }

    private static string SafeCardEnergyCostText(CardModel card)
    {
        try
        {
            return card.EnergyCost.CostsX ? "X" : card.EnergyCost.Canonical.ToString();
        }
        catch (Exception ex)
        {
            return ModStudioLocalization.F("misc.unavailable", ex.GetType().Name);
        }
    }

    private static string SafeCardEnergyCostValue(CardModel card)
    {
        try
        {
            return card.EnergyCost.CostsX ? "0" : card.EnergyCost.Canonical.ToString();
        }
        catch
        {
            return "0";
        }
    }

    private static bool SafeCardCostsX(CardModel card)
    {
        try
        {
            return card.EnergyCost.CostsX;
        }
        catch
        {
            return false;
        }
    }

    private static string SafeAssetPath(Func<string> getter)
    {
        try { return getter.Invoke(); }
        catch (Exception ex) { return ModStudioLocalization.F("misc.unavailable", ex.GetType().Name); }
    }

    private static string SafeEventPortraitPath(EventModel evt) => SafeAssetPath(() => ImageHelper.GetImagePath($"events/{evt.Id.Entry.ToLowerInvariant()}.png"));

    private static string SourceOfTruth(string modelName) => ModStudioLocalization.F("detail.source_of_truth", modelName);

    private static string Detail(string key, object? value) => ModStudioLocalization.F(key, value ?? string.Empty);

    private static string BoolText(bool value) => ModStudioLocalization.T(value ? "bool.true" : "bool.false");

    private static string Dual(string zh, string en) => ModStudioLocalization.IsChinese ? zh : en;

    private static void EnsureRuntimeMetadataCacheLanguage()
    {
        if (_cachedLanguageIsChinese == ModStudioLocalization.IsChinese)
        {
            return;
        }

        _cachedLanguageIsChinese = ModStudioLocalization.IsChinese;
        RuntimeMetadataCache.Clear();
    }

    private static string JoinIds(IEnumerable<string> ids)
    {
        var values = ids.Where(id => !string.IsNullOrWhiteSpace(id)).ToList();
        return values.Count == 0 ? "-" : string.Join(", ", values);
    }

}
