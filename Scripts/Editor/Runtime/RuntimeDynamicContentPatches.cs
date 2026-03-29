using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Unlocks;

namespace STS2_Editor.Scripts.Editor.Runtime;

[HarmonyPatch]
internal static class RuntimeDynamicContentPatches
{
    private static readonly FieldInfo ActRoomsField = AccessTools.Field(typeof(ActModel), "_rooms")!;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CardPoolModel), "get_AllCards")]
    private static void CardPoolModel_get_AllCards_Postfix(CardPoolModel __instance, ref IEnumerable<CardModel> __result)
    {
        var customCards = ModStudioBootstrap.RuntimeDynamicContentRegistry.GetActiveCardsForPool(__instance.Id.Entry);
        __result = __result.Concat(customCards).DistinctBy(card => card.Id);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CardPoolModel), "get_AllCardIds")]
    private static void CardPoolModel_get_AllCardIds_Postfix(CardPoolModel __instance, ref IEnumerable<ModelId> __result)
    {
        var customIds = ModStudioBootstrap.RuntimeDynamicContentRegistry
            .GetActiveCardsForPool(__instance.Id.Entry)
            .Select(card => card.Id);
        __result = __result.Concat(customIds).Distinct();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(RelicPoolModel), "get_AllRelics")]
    private static void RelicPoolModel_get_AllRelics_Postfix(RelicPoolModel __instance, ref IEnumerable<RelicModel> __result)
    {
        var customRelics = ModStudioBootstrap.RuntimeDynamicContentRegistry.GetActiveRelicsForPool(__instance.Id.Entry);
        __result = __result.Concat(customRelics).DistinctBy(relic => relic.Id);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(RelicPoolModel), "get_AllRelicIds")]
    private static void RelicPoolModel_get_AllRelicIds_Postfix(RelicPoolModel __instance, ref HashSet<ModelId> __result)
    {
        var updatedIds = new HashSet<ModelId>(__result);
        foreach (var relicId in ModStudioBootstrap.RuntimeDynamicContentRegistry
                     .GetActiveRelicsForPool(__instance.Id.Entry)
                     .Select(relic => relic.Id))
        {
            updatedIds.Add(relicId);
        }

        __result = updatedIds;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PotionPoolModel), "get_AllPotions")]
    private static void PotionPoolModel_get_AllPotions_Postfix(PotionPoolModel __instance, ref IEnumerable<PotionModel> __result)
    {
        var customPotions = ModStudioBootstrap.RuntimeDynamicContentRegistry.GetActivePotionsForPool(__instance.Id.Entry);
        __result = __result.Concat(customPotions).DistinctBy(potion => potion.Id);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PotionPoolModel), "get_AllPotionIds")]
    private static void PotionPoolModel_get_AllPotionIds_Postfix(PotionPoolModel __instance, ref IEnumerable<ModelId> __result)
    {
        var customIds = ModStudioBootstrap.RuntimeDynamicContentRegistry
            .GetActivePotionsForPool(__instance.Id.Entry)
            .Select(potion => potion.Id);
        __result = __result.Concat(customIds).Distinct();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ModelDb), "get_AllSharedEvents")]
    private static void ModelDb_get_AllSharedEvents_Postfix(ref IEnumerable<EventModel> __result)
    {
        __result = __result
            .Concat(ModStudioBootstrap.RuntimeDynamicContentRegistry.GetActiveEvents(sharedOnly: true))
            .DistinctBy(evt => evt.Id);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ModelDb), "get_AllEvents")]
    private static void ModelDb_get_AllEvents_Postfix(ref IEnumerable<EventModel> __result)
    {
        __result = __result
            .Concat(ModStudioBootstrap.RuntimeDynamicContentRegistry.GetActiveEvents())
            .DistinctBy(evt => evt.Id);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ModelDb), "get_DebugEnchantments")]
    private static void ModelDb_get_DebugEnchantments_Postfix(ref IEnumerable<EnchantmentModel> __result)
    {
        __result = __result
            .Concat(ModStudioBootstrap.RuntimeDynamicContentRegistry.GetActiveEnchantments())
            .DistinctBy(enchantment => enchantment.Id);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ActModel), nameof(ActModel.GenerateRooms))]
    private static void ActModel_GenerateRooms_Postfix(ActModel __instance, Rng rng, UnlockState unlockState, bool isMultiplayer)
    {
        if (ActRoomsField.GetValue(__instance) is not RoomSet rooms)
        {
            return;
        }

        var customEvents = ModStudioBootstrap.RuntimeDynamicContentRegistry
            .GetActiveEvents(__instance.Id.Entry)
            .OrderBy(_ => rng.NextFloat())
            .ToList();

        if (customEvents.Count == 0)
        {
            return;
        }

        rooms.events.AddRange(customEvents.Where(custom => rooms.events.All(existing => existing.Id != custom.Id)));
    }
}
