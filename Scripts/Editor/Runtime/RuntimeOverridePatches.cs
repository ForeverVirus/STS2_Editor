using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Unlocks;
using Godot;
using STS2_Editor.Scripts.Editor.Core.Models;

namespace STS2_Editor.Scripts.Editor.Runtime;

[HarmonyPatch]
internal static class RuntimeOverridePatches
{
    private static readonly HashSet<string> LoggedTextureOverrideApplications = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> LoggedPoolFallbackApplications = new(StringComparer.OrdinalIgnoreCase);

[HarmonyPrefix]
    [HarmonyPatch(typeof(LocString), nameof(LocString.GetRawText))]
    private static bool LocString_GetRawText_Prefix(LocString __instance, ref string __result)
    {
        if (RuntimeOverrideMetadata.TryGetLocalizedText(__instance.LocTable, __instance.LocEntryKey, out var overrideText))
        {
            __result = overrideText;
            return false;
        }

        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(LocString), nameof(LocString.Exists), new Type[] { typeof(string), typeof(string) })]
    private static bool LocString_Exists_Static_Prefix(string table, string key, ref bool __result)
    {
        if (RuntimeOverrideMetadata.TryGetLocalizedText(table, key, out _))
        {
            __result = true;
            return false;
        }

        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(LocString), nameof(LocString.Exists), new Type[0])]
    private static bool LocString_Exists_Instance_Prefix(LocString __instance, ref bool __result)
    {
        if (RuntimeOverrideMetadata.TryGetLocalizedText(__instance.LocTable, __instance.LocEntryKey, out _))
        {
            __result = true;
            return false;
        }

        return true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Player), nameof(Player.CreateForNewRun), [typeof(CharacterModel), typeof(UnlockState), typeof(ulong)])]
    private static void Player_CreateForNewRun_Postfix(CharacterModel character, ref Player __result)
    {
        try
        {
            var hasCharacterOverride = false;
            try
            {
                hasCharacterOverride = ModStudioBootstrap.RuntimeRegistry.TryGetOverride(ModStudioEntityKind.Character, character.Id.Entry, out _);
            }
            catch
            {
            }

            Log.Info($"[ModStudio.Override] Player.CreateForNewRun {character.Id.Entry} overridePresent={hasCharacterOverride}");
            ApplyCharacterOverrides(character, __result);
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to apply character overrides for '{character.Id.Entry}': {ex.Message}");
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Player), "PopulateStartingInventory")]
    private static void Player_PopulateStartingInventory_Postfix(Player __instance)
    {
        try
        {
            var character = __instance.Character;
            var hasCharacterOverride = false;
            try
            {
                hasCharacterOverride = ModStudioBootstrap.RuntimeRegistry.TryGetOverride(ModStudioEntityKind.Character, character.Id.Entry, out _);
            }
            catch
            {
            }

            Log.Info($"[ModStudio.Override] Player.PopulateStartingInventory {character.Id.Entry} overridePresent={hasCharacterOverride}");
            ApplyCharacterOverrides(character, __instance);
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to apply PopulateStartingInventory overrides for '{__instance.Character.Id.Entry}': {ex.Message}");
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CardModel), "get_PortraitPath")]
    private static void CardModel_get_PortraitPath_Postfix(CardModel __instance, ref string __result)
    {
        if (TryGetOverriddenResourcePath(ModStudioEntityKind.Card, __instance.Id.Entry, "portrait_path", out var path))
        {
            __result = path;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CardModel), "get_AllPortraitPaths")]
    private static void CardModel_get_AllPortraitPaths_Postfix(CardModel __instance, ref IEnumerable<string> __result)
    {
        if (TryGetOverriddenResourcePath(ModStudioEntityKind.Card, __instance.Id.Entry, "portrait_path", out var path))
        {
            __result = new[] { path };
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CardModel), "get_Portrait")]
    private static void CardModel_get_Portrait_Postfix(CardModel __instance, ref Texture2D __result)
    {
        if (TryLoadTextureOverride(ModStudioEntityKind.Card, __instance.Id.Entry, "portrait_path", out var texture))
        {
            __result = texture;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CardModel), "get_Type")]
    private static void CardModel_get_Type_Postfix(CardModel __instance, ref CardType __result)
    {
        if (RuntimeOverrideMetadata.TryGetEnum(ModStudioEntityKind.Card, __instance.Id.Entry, "type", out CardType type))
        {
            __result = type;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CardModel), "get_Rarity")]
    private static void CardModel_get_Rarity_Postfix(CardModel __instance, ref CardRarity __result)
    {
        if (RuntimeOverrideMetadata.TryGetEnum(ModStudioEntityKind.Card, __instance.Id.Entry, "rarity", out CardRarity rarity))
        {
            __result = rarity;
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(CardModel), "get_Pool")]
    private static bool CardModel_get_Pool_Prefix(CardModel __instance, ref CardPoolModel __result)
    {
        if (TryResolveCardPool(ModStudioEntityKind.Card, __instance.Id.Entry, "pool_id", out var pool))
        {
            __result = pool;
            return false;
        }

        if (TryResolveFallbackCardPool(__instance.Id.Entry, out pool))
        {
            __result = pool;
            return false;
        }

        return true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CardModel), "get_TargetType")]
    private static void CardModel_get_TargetType_Postfix(CardModel __instance, ref TargetType __result)
    {
        if (RuntimeOverrideMetadata.TryGetEnum(ModStudioEntityKind.Card, __instance.Id.Entry, "target_type", out TargetType targetType))
        {
            __result = targetType;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CardModel), "get_CanonicalEnergyCost")]
    private static void CardModel_get_CanonicalEnergyCost_Postfix(CardModel __instance, ref int __result)
    {
        if (RuntimeOverrideMetadata.TryGetInt(ModStudioEntityKind.Card, __instance.Id.Entry, "energy_cost", out var energyCost))
        {
            __result = Math.Max(0, energyCost);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CardModel), "get_HasEnergyCostX")]
    private static void CardModel_get_HasEnergyCostX_Postfix(CardModel __instance, ref bool __result)
    {
        if (RuntimeOverrideMetadata.TryGetBool(ModStudioEntityKind.Card, __instance.Id.Entry, "energy_cost_x", out var hasEnergyCostX))
        {
            __result = hasEnergyCostX;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CardModel), "get_CanonicalStarCost")]
    private static void CardModel_get_CanonicalStarCost_Postfix(CardModel __instance, ref int __result)
    {
        if (RuntimeOverrideMetadata.TryGetInt(ModStudioEntityKind.Card, __instance.Id.Entry, "canonical_star_cost", out var starCost))
        {
            __result = starCost;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CardModel), "get_HasStarCostX")]
    private static void CardModel_get_HasStarCostX_Postfix(CardModel __instance, ref bool __result)
    {
        if (RuntimeOverrideMetadata.TryGetBool(ModStudioEntityKind.Card, __instance.Id.Entry, "star_cost_x", out var hasStarCostX))
        {
            __result = hasStarCostX;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CardModel), "get_CanBeGeneratedInCombat")]
    private static void CardModel_get_CanBeGeneratedInCombat_Postfix(CardModel __instance, ref bool __result)
    {
        if (RuntimeOverrideMetadata.TryGetBool(ModStudioEntityKind.Card, __instance.Id.Entry, "can_be_generated_in_combat", out var canBeGeneratedInCombat))
        {
            __result = canBeGeneratedInCombat;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(RelicModel), "get_PackedIconPath")]
    private static void RelicModel_get_PackedIconPath_Postfix(RelicModel __instance, ref string __result)
    {
        if (TryGetOverriddenResourcePath(ModStudioEntityKind.Relic, __instance.Id.Entry, "icon_path", out var path))
        {
            __result = path;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(RelicModel), "get_IconPath")]
    private static void RelicModel_get_IconPath_Postfix(RelicModel __instance, ref string __result)
    {
        if (TryGetOverriddenResourcePath(ModStudioEntityKind.Relic, __instance.Id.Entry, "icon_path", out var path))
        {
            __result = path;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(RelicModel), "get_Icon")]
    private static void RelicModel_get_Icon_Postfix(RelicModel __instance, ref Texture2D __result)
    {
        if (TryLoadTextureOverride(ModStudioEntityKind.Relic, __instance.Id.Entry, "icon_path", out var texture))
        {
            __result = texture;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(RelicModel), "get_BigIcon")]
    private static void RelicModel_get_BigIcon_Postfix(RelicModel __instance, ref Texture2D __result)
    {
        if (TryLoadTextureOverride(ModStudioEntityKind.Relic, __instance.Id.Entry, "icon_path", out var texture))
        {
            __result = texture;
        }
    }

    internal static void ApplyRelicRarityOverride(RelicModel model, ref RelicRarity result)
    {
        if (RuntimeOverrideMetadata.TryGetEnum(ModStudioEntityKind.Relic, model.Id.Entry, "rarity", out RelicRarity rarity))
        {
            result = rarity;
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(RelicModel), "get_Pool")]
    private static bool RelicModel_get_Pool_Prefix(RelicModel __instance, ref RelicPoolModel __result)
    {
        if (TryResolveRelicPool(ModStudioEntityKind.Relic, __instance.Id.Entry, "pool_id", out var pool))
        {
            __result = pool;
            return false;
        }

        if (TryResolveFallbackRelicPool(__instance.Id.Entry, out pool))
        {
            __result = pool;
            return false;
        }

        return true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PotionModel), "get_ImagePath")]
    private static void PotionModel_get_ImagePath_Postfix(PotionModel __instance, ref string __result)
    {
        if (TryGetOverriddenResourcePath(ModStudioEntityKind.Potion, __instance.Id.Entry, "image_path", out var path))
        {
            __result = path;
        }
    }

    internal static void ApplyPotionRarityOverride(PotionModel model, ref PotionRarity result)
    {
        if (RuntimeOverrideMetadata.TryGetEnum(ModStudioEntityKind.Potion, model.Id.Entry, "rarity", out PotionRarity rarity))
        {
            result = rarity;
        }
    }

    internal static void ApplyPotionUsageOverride(PotionModel model, ref PotionUsage result)
    {
        if (RuntimeOverrideMetadata.TryGetEnum(ModStudioEntityKind.Potion, model.Id.Entry, "usage", out PotionUsage usage))
        {
            result = usage;
        }
    }

    internal static void ApplyPotionTargetTypeOverride(PotionModel model, ref TargetType result)
    {
        if (RuntimeOverrideMetadata.TryGetEnum(ModStudioEntityKind.Potion, model.Id.Entry, "target_type", out TargetType targetType))
        {
            result = targetType;
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(PotionModel), "get_Pool")]
    private static bool PotionModel_get_Pool_Prefix(PotionModel __instance, ref PotionPoolModel __result)
    {
        if (TryResolvePotionPool(ModStudioEntityKind.Potion, __instance.Id.Entry, "pool_id", out var pool))
        {
            __result = pool;
            return false;
        }

        if (TryResolveFallbackPotionPool(__instance.Id.Entry, out pool))
        {
            __result = pool;
            return false;
        }

        return true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PotionModel), "get_CanBeGeneratedInCombat")]
    private static void PotionModel_get_CanBeGeneratedInCombat_Postfix(PotionModel __instance, ref bool __result)
    {
        if (RuntimeOverrideMetadata.TryGetBool(ModStudioEntityKind.Potion, __instance.Id.Entry, "can_be_generated_in_combat", out var canBeGeneratedInCombat))
        {
            __result = canBeGeneratedInCombat;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PotionModel), "get_Image")]
    private static void PotionModel_get_Image_Postfix(PotionModel __instance, ref Texture2D __result)
    {
        if (TryLoadTextureOverride(ModStudioEntityKind.Potion, __instance.Id.Entry, "image_path", out var texture))
        {
            __result = texture;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(EnchantmentModel), "get_IconPath")]
    private static void EnchantmentModel_get_IconPath_Postfix(EnchantmentModel __instance, ref string __result)
    {
        if (TryGetOverriddenPath(ModStudioEntityKind.Enchantment, __instance.Id.Entry, "icon_path", out var path))
        {
            __result = path;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(EnchantmentModel), "get_HasExtraCardText")]
    private static void EnchantmentModel_get_HasExtraCardText_Postfix(EnchantmentModel __instance, ref bool __result)
    {
        if (RuntimeOverrideMetadata.TryGetBool(ModStudioEntityKind.Enchantment, __instance.Id.Entry, "has_extra_card_text", out var hasExtraCardText))
        {
            __result = hasExtraCardText;
            return;
        }

        if (RuntimeOverrideMetadata.HasMetadata(ModStudioEntityKind.Enchantment, __instance.Id.Entry, "extra_card_text"))
        {
            __result = true;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(EnchantmentModel), "get_ShowAmount")]
    private static void EnchantmentModel_get_ShowAmount_Postfix(EnchantmentModel __instance, ref bool __result)
    {
        if (RuntimeOverrideMetadata.TryGetBool(ModStudioEntityKind.Enchantment, __instance.Id.Entry, "show_amount", out var showAmount))
        {
            __result = showAmount;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(EventModel), "get_LayoutType")]
    private static void EventModel_get_LayoutType_Postfix(EventModel __instance, ref EventLayoutType __result)
    {
        if (RuntimeOverrideMetadata.TryGetEnum(ModStudioEntityKind.Event, __instance.Id.Entry, "layout_type", out EventLayoutType layoutType))
        {
            __result = layoutType;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(EventModel), "get_IsShared")]
    private static void EventModel_get_IsShared_Postfix(EventModel __instance, ref bool __result)
    {
        if (RuntimeOverrideMetadata.TryGetBool(ModStudioEntityKind.Event, __instance.Id.Entry, "is_shared", out var isShared))
        {
            __result = isShared;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(EventModel), nameof(EventModel.CreateInitialPortrait))]
    private static void EventModel_CreateInitialPortrait_Postfix(EventModel __instance, ref Texture2D __result)
    {
        if (TryLoadTextureOverride(ModStudioEntityKind.Event, __instance.Id.Entry, "portrait_path", out var texture) ||
            TryLoadTextureOverride(ModStudioEntityKind.Event, __instance.Id.Entry, "image_path", out texture))
        {
            __result = texture;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(EventModel), nameof(EventModel.GetAssetPaths))]
    private static void EventModel_GetAssetPaths_Postfix(EventModel __instance, ref IEnumerable<string> __result)
    {
        if (!TryGetOverriddenPath(ModStudioEntityKind.Event, __instance.Id.Entry, "portrait_path", out var portraitPath))
        {
            return;
        }

        var defaultPortraitPath = ImageHelper.GetImagePath($"events/{__instance.Id.Entry.ToLowerInvariant()}.png");
        __result = __result
            .Where(path => !string.Equals(path, defaultPortraitPath, StringComparison.OrdinalIgnoreCase))
            .Append(portraitPath)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(EventModel), "get_CanonicalEncounter")]
    private static void EventModel_get_CanonicalEncounter_Postfix(EventModel __instance, ref EncounterModel? __result)
    {
        if (TryResolveEncounter(ModStudioEntityKind.Event, __instance.Id.Entry, "encounter_id", out var encounter))
        {
            __result = encounter;
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(EventModel), "SetInitialEventState")]
    private static bool EventModel_SetInitialEventState_Prefix(EventModel __instance, bool isPreFinished)
    {
        return !RuntimeEventTemplateSupport.TryHandleSetInitialEventState(__instance, isPreFinished);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(EventModel), nameof(EventModel.Resume))]
    private static bool EventModel_Resume_Prefix(EventModel __instance, AbstractRoom exitedRoom, ref Task __result)
    {
        if (!RuntimeEventTemplateSupport.TryHandleResume(__instance, exitedRoom))
        {
            return true;
        }

        __result = Task.CompletedTask;
        return false;
    }

    private static void ApplyCharacterOverrides(CharacterModel character, Player player)
    {
        var characterId = character.Id.Entry;
        var hasDeckOverride = RuntimeOverrideMetadata.HasMetadata(ModStudioEntityKind.Character, characterId, "starting_deck_ids");
        var hasRelicOverride = RuntimeOverrideMetadata.HasMetadata(ModStudioEntityKind.Character, characterId, "starting_relic_ids");
        var hasPotionOverride = RuntimeOverrideMetadata.HasMetadata(ModStudioEntityKind.Character, characterId, "starting_potion_ids");
        if (hasDeckOverride || hasRelicOverride || hasPotionOverride)
        {
            Log.Info($"[ModStudio.Override] Applying character overrides for {characterId}: deck={hasDeckOverride} relics={hasRelicOverride} potions={hasPotionOverride}");
        }

        if (RuntimeOverrideMetadata.TryGetInt(ModStudioEntityKind.Character, characterId, "starting_hp", out var startingHp) && startingHp > 0)
        {
            player.Creature.SetMaxHpInternal(startingHp);
            player.Creature.SetCurrentHpInternal(startingHp);
        }

        if (RuntimeOverrideMetadata.TryGetInt(ModStudioEntityKind.Character, characterId, "starting_gold", out var startingGold))
        {
            player.Gold = startingGold;
        }

        if (RuntimeOverrideMetadata.TryGetInt(ModStudioEntityKind.Character, characterId, "max_energy", out var maxEnergy) && maxEnergy >= 0)
        {
            player.MaxEnergy = maxEnergy;
        }

        if (RuntimeOverrideMetadata.TryGetInt(ModStudioEntityKind.Character, characterId, "base_orb_slot_count", out var baseOrbSlotCount) && baseOrbSlotCount >= 0)
        {
            player.BaseOrbSlotCount = baseOrbSlotCount;
        }

        if (hasDeckOverride)
        {
            player.Deck.Clear(silent: true);
            foreach (var cardId in RuntimeOverrideMetadata.GetIdList(ModStudioEntityKind.Character, characterId, "starting_deck_ids"))
            {
                var card = ResolveCardModel(cardId)?.ToMutable();
                if (card == null)
                {
                    Log.Warn($"Mod Studio could not resolve starting deck card '{cardId}' for character '{characterId}'.");
                    continue;
                }

                card.FloorAddedToDeck = 1;
                player.Deck.AddInternal(card, -1, silent: true);
            }
        }

        if (hasRelicOverride)
        {
            foreach (var relic in player.Relics.ToList())
            {
                player.RemoveRelicInternal(relic, silent: true);
            }

            foreach (var relicId in RuntimeOverrideMetadata.GetIdList(ModStudioEntityKind.Character, characterId, "starting_relic_ids"))
            {
                var relic = ResolveRelicModel(relicId)?.ToMutable();
                if (relic == null)
                {
                    Log.Warn($"Mod Studio could not resolve starting relic '{relicId}' for character '{characterId}'.");
                    continue;
                }

                relic.FloorAddedToDeck = 1;
                SaveManager.Instance?.MarkRelicAsSeen(relic);
                player.AddRelicInternal(relic, -1, silent: true);
            }
        }

        if (hasPotionOverride)
        {
            foreach (var potion in player.PotionSlots.ToList())
            {
                if (potion != null)
                {
                    player.DiscardPotionInternal(potion, silent: true);
                }
            }

            var potionIds = RuntimeOverrideMetadata.GetIdList(ModStudioEntityKind.Character, characterId, "starting_potion_ids");
            if (potionIds.Count > player.MaxPotionCount)
            {
                player.AddToMaxPotionCount(potionIds.Count - player.MaxPotionCount);
            }

            foreach (var potionId in potionIds)
            {
                var potion = ResolvePotionModel(potionId)?.ToMutable();
                if (potion == null)
                {
                    Log.Warn($"Mod Studio could not resolve starting potion '{potionId}' for character '{characterId}'.");
                    continue;
                }

                player.AddPotionInternal(potion, -1, silent: true);
            }
        }

        if (hasDeckOverride)
        {
            Log.Info($"[ModStudio.Override] StartingDeck {characterId}: {string.Join(", ", player.Deck.Cards.Select(card => card.Id.Entry))}");
        }

        if (hasRelicOverride)
        {
            Log.Info($"[ModStudio.Override] StartingRelics {characterId}: {string.Join(", ", player.Relics.Select(relic => relic.Id.Entry))}");
        }

        if (hasPotionOverride)
        {
            Log.Info($"[ModStudio.Override] StartingPotions {characterId}: {string.Join(", ", player.PotionSlots.Where(potion => potion != null).Select(potion => potion!.Id.Entry))}");
        }
    }

    private static bool TryGetOverriddenPath(ModStudioEntityKind kind, string entityId, string metadataKey, out string path)
    {
        path = string.Empty;
        var raw = RuntimeOverrideMetadata.GetMetadataOrNull(kind, entityId, metadataKey);
        var normalized = raw == null ? null : RuntimeAssetLoader.GetOverriddenPathOrNull(raw);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        path = normalized;
        return true;
    }

    private static bool TryGetOverriddenResourcePath(ModStudioEntityKind kind, string entityId, string metadataKey, out string path)
    {
        path = string.Empty;
        if (!TryGetOverriddenPath(kind, entityId, metadataKey, out var normalized) ||
            !normalized.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        path = normalized;
        return true;
    }

    private static bool TryLoadTextureOverride(ModStudioEntityKind kind, string entityId, string metadataKey, out Texture2D texture)
    {
        texture = null!;
        if (!TryGetOverriddenPath(kind, entityId, metadataKey, out var path))
        {
            return false;
        }

        var loaded = RuntimeAssetLoader.LoadTexture(path);
        if (loaded == null)
        {
            return false;
        }

        texture = loaded;
        TryLogTextureOverrideApplied(kind, entityId, metadataKey, path, loaded);
        return true;
    }

    private static void TryLogTextureOverrideApplied(ModStudioEntityKind kind, string entityId, string metadataKey, string path, Texture2D texture)
    {
        var key = $"{kind}:{entityId}:{metadataKey}";
        if (!LoggedTextureOverrideApplications.Add(key))
        {
            return;
        }

        var size = texture.GetSize();
        Log.Info($"[ModStudio.Asset] Applied texture override {kind}:{entityId}:{metadataKey} -> {path} size={size.X:0}x{size.Y:0}");
    }

    private static bool TryResolveCardPool(ModStudioEntityKind kind, string entityId, string metadataKey, out CardPoolModel pool)
    {
        pool = null!;
        if (!RuntimeOverrideMetadata.TryGetMetadata(kind, entityId, metadataKey, out var poolId))
        {
            return false;
        }

        var resolved = ModelDb.AllCardPools.FirstOrDefault(candidate =>
            string.Equals(candidate.Id.Entry, poolId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(candidate.Title, poolId, StringComparison.OrdinalIgnoreCase));
        if (resolved == null)
        {
            return false;
        }

        pool = resolved;
        return true;
    }

    private static bool TryResolveRelicPool(ModStudioEntityKind kind, string entityId, string metadataKey, out RelicPoolModel pool)
    {
        pool = null!;
        if (!RuntimeOverrideMetadata.TryGetMetadata(kind, entityId, metadataKey, out var poolId))
        {
            return false;
        }

        var resolved = ModelDb.AllRelicPools.FirstOrDefault(candidate =>
            string.Equals(candidate.Id.Entry, poolId, StringComparison.OrdinalIgnoreCase));
        if (resolved == null)
        {
            return false;
        }

        pool = resolved;
        return true;
    }

    private static bool TryResolvePotionPool(ModStudioEntityKind kind, string entityId, string metadataKey, out PotionPoolModel pool)
    {
        pool = null!;
        if (!RuntimeOverrideMetadata.TryGetMetadata(kind, entityId, metadataKey, out var poolId))
        {
            return false;
        }

        var resolved = ModelDb.AllPotionPools.FirstOrDefault(candidate =>
            string.Equals(candidate.Id.Entry, poolId, StringComparison.OrdinalIgnoreCase));
        if (resolved == null)
        {
            return false;
        }

        pool = resolved;
        return true;
    }

    private static bool TryResolveEncounter(ModStudioEntityKind kind, string entityId, string metadataKey, out EncounterModel encounter)
    {
        encounter = null!;
        if (!RuntimeOverrideMetadata.TryGetMetadata(kind, entityId, metadataKey, out var encounterId))
        {
            return false;
        }

        var resolved = ModelDb.AllEncounters.FirstOrDefault(candidate =>
            string.Equals(candidate.Id.Entry, encounterId, StringComparison.OrdinalIgnoreCase));
        if (resolved == null)
        {
            return false;
        }

        encounter = resolved;
        return true;
    }

    private static bool TryResolveFallbackCardPool(string entityId, out CardPoolModel pool)
    {
        pool = null!;
        if (!ModStudioBootstrap.RuntimeDynamicContentRegistry.IsCustomActive(ModStudioEntityKind.Card, entityId))
        {
            return false;
        }

        var resolved = ModelDb.AllCharacterCardPools.FirstOrDefault() ?? ModelDb.AllCardPools.FirstOrDefault();
        if (resolved == null)
        {
            return false;
        }

        LogPoolFallback(ModStudioEntityKind.Card, entityId, resolved.Id.Entry);
        pool = resolved;
        return true;
    }

    private static bool TryResolveFallbackRelicPool(string entityId, out RelicPoolModel pool)
    {
        pool = null!;
        if (!ModStudioBootstrap.RuntimeDynamicContentRegistry.IsCustomActive(ModStudioEntityKind.Relic, entityId))
        {
            return false;
        }

        var resolved = ModelDb.AllRelicPools.FirstOrDefault();
        if (resolved == null)
        {
            return false;
        }

        LogPoolFallback(ModStudioEntityKind.Relic, entityId, resolved.Id.Entry);
        pool = resolved;
        return true;
    }

    private static bool TryResolveFallbackPotionPool(string entityId, out PotionPoolModel pool)
    {
        pool = null!;
        if (!ModStudioBootstrap.RuntimeDynamicContentRegistry.IsCustomActive(ModStudioEntityKind.Potion, entityId))
        {
            return false;
        }

        var resolved = ModelDb.AllPotionPools.FirstOrDefault();
        if (resolved == null)
        {
            return false;
        }

        LogPoolFallback(ModStudioEntityKind.Potion, entityId, resolved.Id.Entry);
        pool = resolved;
        return true;
    }

    private static void LogPoolFallback(ModStudioEntityKind kind, string entityId, string poolId)
    {
        var key = $"{kind}:{entityId}:{poolId}";
        if (!LoggedPoolFallbackApplications.Add(key))
        {
            return;
        }

        Log.Warn($"Mod Studio fell back to pool '{poolId}' for dynamic {kind} '{entityId}' because no explicit pool metadata could be resolved.");
    }

    private static CardModel? ResolveCardModel(string entityId)
    {
        if (ModStudioBootstrap.RuntimeDynamicContentRegistry.TryGetRegisteredModel<CardModel>(ModStudioEntityKind.Card, entityId, out var dynamicCard) &&
            dynamicCard != null)
        {
            return dynamicCard;
        }

        return ModelDb.AllCards.FirstOrDefault(model => model.Id.Entry == entityId);
    }

    private static RelicModel? ResolveRelicModel(string entityId)
    {
        if (ModStudioBootstrap.RuntimeDynamicContentRegistry.TryGetRegisteredModel<RelicModel>(ModStudioEntityKind.Relic, entityId, out var dynamicRelic) &&
            dynamicRelic != null)
        {
            return dynamicRelic;
        }

        return ModelDb.AllRelics.FirstOrDefault(model => model.Id.Entry == entityId);
    }

    private static PotionModel? ResolvePotionModel(string entityId)
    {
        if (ModStudioBootstrap.RuntimeDynamicContentRegistry.TryGetRegisteredModel<PotionModel>(ModStudioEntityKind.Potion, entityId, out var dynamicPotion) &&
            dynamicPotion != null)
        {
            return dynamicPotion;
        }

        return ModelDb.AllPotions.FirstOrDefault(model => model.Id.Entry == entityId);
    }
}
