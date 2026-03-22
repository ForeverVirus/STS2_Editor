using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Entities.Relics;

namespace STS2_Editor.Scripts.Editor.Runtime;

[HarmonyPatch]
internal static class RuntimeConcreteRelicRarityOverridePatches
{
    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        return RuntimeConcretePropertyPatchHelpers.GetConcreteGetterTargets(typeof(RelicModel), nameof(RelicModel.Rarity));
    }

    [HarmonyPostfix]
    private static void Postfix(RelicModel __instance, ref RelicRarity __result)
    {
        RuntimeOverridePatches.ApplyRelicRarityOverride(__instance, ref __result);
    }
}

[HarmonyPatch]
internal static class RuntimeConcretePotionRarityOverridePatches
{
    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        return RuntimeConcretePropertyPatchHelpers.GetConcreteGetterTargets(typeof(PotionModel), nameof(PotionModel.Rarity));
    }

    [HarmonyPostfix]
    private static void Postfix(PotionModel __instance, ref PotionRarity __result)
    {
        RuntimeOverridePatches.ApplyPotionRarityOverride(__instance, ref __result);
    }
}

[HarmonyPatch]
internal static class RuntimeConcretePotionUsageOverridePatches
{
    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        return RuntimeConcretePropertyPatchHelpers.GetConcreteGetterTargets(typeof(PotionModel), nameof(PotionModel.Usage));
    }

    [HarmonyPostfix]
    private static void Postfix(PotionModel __instance, ref PotionUsage __result)
    {
        RuntimeOverridePatches.ApplyPotionUsageOverride(__instance, ref __result);
    }
}

[HarmonyPatch]
internal static class RuntimeConcretePotionTargetTypeOverridePatches
{
    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        return RuntimeConcretePropertyPatchHelpers.GetConcreteGetterTargets(typeof(PotionModel), nameof(PotionModel.TargetType));
    }

    [HarmonyPostfix]
    private static void Postfix(PotionModel __instance, ref TargetType __result)
    {
        RuntimeOverridePatches.ApplyPotionTargetTypeOverride(__instance, ref __result);
    }
}

internal static class RuntimeConcretePropertyPatchHelpers
{
    public static IEnumerable<MethodBase> GetConcreteGetterTargets(Type baseType, string propertyName)
    {
        return ModelDb.AllAbstractModelSubtypes
            .Where(type => type.IsSubclassOf(baseType) && !type.IsAbstract)
            .Select(type => type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetMethod)
            .Where(method => method != null && !method.IsAbstract)
            .Distinct()!;
    }
}
