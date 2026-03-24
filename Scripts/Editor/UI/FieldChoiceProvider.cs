using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.ValueProps;
using STS2_Editor.Scripts.Editor.Core.Utilities;
using STS2_Editor.Scripts.Editor.Graph;

namespace STS2_Editor.Scripts.Editor.UI;

internal static class FieldChoiceProvider
{
    public static IReadOnlyList<(string Value, string Display)> GetBasicChoices(string key)
    {
        return key switch
        {
            "type" => Enum.GetNames<CardType>()
                .Select(value => (value, ModStudioFieldDisplayNames.FormatGraphPropertyValue(key, value)))
                .ToList(),
            "rarity" => GetRarityChoices(),
            "target_type" => Enum.GetNames<TargetType>()
                .Select(value => (value, ModStudioFieldDisplayNames.FormatGraphPropertyValue(key, value)))
                .ToList(),
            "usage" => Enum.GetNames<PotionUsage>()
                .Select(value => (value, ModStudioFieldDisplayNames.FormatGraphPropertyValue(key, value)))
                .ToList(),
            "layout_type" => Enum.GetNames<EventLayoutType>()
                .Select(value => (value, ModStudioFieldDisplayNames.FormatValue(value)))
                .ToList(),
            "pool_id" => GetPoolChoices(),
            "card_id" => GetCardChoices(),
            "replacement_card_id" => GetCardChoices(),
            "relic_id" => GetRelicChoices(),
            "replacement_relic_id" => GetRelicChoices(),
            "potion_id" => GetPotionChoices(),
            "monster_id" => GetMonsterChoices(),
            "act_ids" => ModelDb.Acts
                .Select(act => (act.Id.Entry, $"{SafeLocText(act.Title)} [{act.Id.Entry}]"))
                .OrderBy(pair => pair.Item2, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            "enchantment_id" => GetEnchantmentChoices(),
            _ => Array.Empty<(string, string)>()
        };
    }

    public static IReadOnlyList<(string Value, string Display)> GetGraphChoices(string key)
    {
        return key switch
        {
            "target" => new[]
            {
                ("self", ModStudioFieldDisplayNames.FormatGraphPropertyValue(key, "self")),
                ("current_target", ModStudioFieldDisplayNames.FormatGraphPropertyValue(key, "current_target")),
                ("other_enemies", ModStudioFieldDisplayNames.FormatGraphPropertyValue(key, "other_enemies")),
                ("all_enemies", ModStudioFieldDisplayNames.FormatGraphPropertyValue(key, "all_enemies")),
                ("all_allies", ModStudioFieldDisplayNames.FormatGraphPropertyValue(key, "all_allies")),
                ("all_targets", ModStudioFieldDisplayNames.FormatGraphPropertyValue(key, "all_targets"))
            },
            "props" => new[] { ("none", ModStudioFieldDisplayNames.FormatGraphPropertyValue(key, "none")) }
                .Concat(Enum.GetNames<ValueProp>()
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .Select(name => (name, ModStudioFieldDisplayNames.FormatGraphPropertyValue(key, name))))
                .ToList(),
            "reward_kind" => GetRewardKindChoicesV2(),
            "reward_target" => GetGraphChoices("target"),
            "reward_props" => GetGraphChoices("props"),
            "reward_power_id" => GetPowerChoices(),
            "encounter_id" => GetEncounterChoices(),
            "operator" => GetOperatorChoices(),
            "dynamic_source_kind" => GetDynamicSourceKindChoices(),
            "base_override_mode" => GetOverrideModeChoices(),
            "extra_override_mode" => GetOverrideModeChoices(),
            "dynamic_var_name" => GetDynamicVarChoices(),
            "formula_ref" => GetFormulaChoices(),
            "preview_multiplier_key" => GetPreviewMultiplierChoices(),
            "power_id" => GetPowerChoices(),
            "card_type_scope" => GetCardTypeScopeChoices(),
            "target_pile" => GetPileChoices(),
            "position" => GetCardPilePositionChoices(),
            "auto_play_type" => GetAutoPlayTypeChoices(),
            "gold_loss_type" => GetGoldLossTypeChoices(),
            "card_preview_style" => GetCardPreviewStyleChoices(),
            "card_id" => GetCardChoices(),
            "replacement_card_id" => GetCardChoices(),
            "relic_id" => GetRelicChoices(),
            "replacement_relic_id" => GetRelicChoices(),
            "potion_id" => GetPotionChoices(),
            "monster_id" => GetMonsterChoices(),
            "orb_id" => GetOrbChoices(),
            "enchantment_id" => GetEnchantmentChoices(),
            "keyword" => GetCardKeywordChoices(),
            "selection_mode" => GetSelectionModeChoices(),
            "source_pile" => GetPileChoices(),
            "prompt_kind" => GetPromptKindChoices(),
            _ => Array.Empty<(string, string)>()
        };
    }

    public static IReadOnlyList<(string Value, string Display)> GetDynamicVarChoices(AbstractModel? model = null)
    {
        return EnumerateDynamicVarNames(model)
            .Concat(GetCommonDynamicVarNames())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Select(name => (name, ModStudioFieldDisplayNames.FormatGraphPropertyValue("dynamic_var_name", name)))
            .ToList();
    }

    public static IReadOnlyList<(string Value, string Display)> GetFormulaChoices(AbstractModel? model = null)
    {
        return EnumerateFormulaVarNames(model)
            .Concat(GetCommonFormulaRefs())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Select(name => (name, ModStudioFieldDisplayNames.FormatGraphPropertyValue("formula_ref", name)))
            .ToList();
    }

    private static IReadOnlyList<(string Value, string Display)> GetPoolChoices()
    {
        var results = new List<(string, string)>();
        results.AddRange(ModelDb.AllCardPools.Select(pool => (pool.Id.Entry, $"{pool.Title} [{pool.Id.Entry}]")));
        results.AddRange(ModelDb.AllRelicPools.Select(pool => (pool.Id.Entry, $"{pool.Id.Entry} [{pool.GetType().Name}]")));
        results.AddRange(ModelDb.AllPotionPools.Select(pool => (pool.Id.Entry, $"{pool.Id.Entry} [{pool.GetType().Name}]")));
        return results
            .GroupBy(pair => pair.Item1, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(pair => pair.Item2, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<(string Value, string Display)> GetPowerChoices()
    {
        return ModelDb.AllPowers
            .Select(power => (power.Id.Entry, $"{SafeLocText(power.Title)} [{power.Id.Entry}]"))
            .OrderBy(pair => pair.Item2, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<(string Value, string Display)> GetEncounterChoices()
    {
        return ModelDb.AllEncounters
            .Select(encounter => (encounter.Id.Entry, $"{SafeLocText(encounter.Title)} [{encounter.Id.Entry}]"))
            .OrderBy(pair => pair.Item2, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<(string Value, string Display)> GetRelicChoices()
    {
        return ModelDb.AllRelics
            .Select(relic => (relic.Id.Entry, $"{SafeLocText(relic.Title)} [{relic.Id.Entry}]"))
            .OrderBy(pair => pair.Item2, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<(string Value, string Display)> GetPotionChoices()
    {
        return ModelDb.AllPotions
            .Select(potion => (potion.Id.Entry, $"{SafeLocText(potion.Title)} [{potion.Id.Entry}]"))
            .OrderBy(pair => pair.Item2, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<(string Value, string Display)> GetMonsterChoices()
    {
        return ModelDb.Monsters
            .Select(monster => (monster.Id.Entry, $"{SafeLocText(monster.Title)} [{monster.Id.Entry}]"))
            .OrderBy(pair => pair.Item2, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<(string Value, string Display)> GetOrbChoices()
    {
        return ModelDb.Orbs
            .Select(orb => (orb.Id.Entry, $"{SafeLocText(orb.Title)} [{orb.Id.Entry}]"))
            .OrderBy(pair => pair.Item2, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<(string Value, string Display)> GetCardChoices()
    {
        return ModelDb.AllCards
            .Select(card => (card.Id.Entry, $"{SafeLocText(card.TitleLocString)} [{card.Id.Entry}]"))
            .OrderBy(pair => pair.Item2, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<(string Value, string Display)> GetEnchantmentChoices()
    {
        return ModelDb.DebugEnchantments
            .Select(enchantment => (enchantment.Id.Entry, $"{SafeLocText(enchantment.Title)} [{enchantment.Id.Entry}]"))
            .OrderBy(pair => pair.Item2, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<(string Value, string Display)> GetRarityChoices()
    {
        var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in Enum.GetNames<CardRarity>()) values.Add(name);
        foreach (var name in Enum.GetNames<RelicRarity>()) values.Add(name);
        foreach (var name in Enum.GetNames<PotionRarity>()) values.Add(name);
        return values
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .Select(value => (value, ModStudioFieldDisplayNames.FormatValue(value)))
            .ToList();
    }

    private static IReadOnlyList<(string Value, string Display)> GetPileChoices()
    {
        return Enum.GetNames<PileType>()
            .Select(value => (value, ModStudioFieldDisplayNames.FormatGraphPropertyValue("target_pile", value)))
            .ToList();
    }

    private static IReadOnlyList<(string Value, string Display)> GetAutoPlayTypeChoices()
    {
        return Enum.GetNames<AutoPlayType>()
            .Select(value => (value, ModStudioFieldDisplayNames.FormatGraphPropertyValue("auto_play_type", value)))
            .ToList();
    }

    private static IReadOnlyList<(string Value, string Display)> GetGoldLossTypeChoices()
    {
        return Enum.GetNames<GoldLossType>()
            .Select(value => (value, ModStudioFieldDisplayNames.FormatGraphPropertyValue("gold_loss_type", value)))
            .ToList();
    }

    private static IReadOnlyList<(string Value, string Display)> GetCardPreviewStyleChoices()
    {
        return Enum.GetNames<CardPreviewStyle>()
            .Select(value => (value, ModStudioFieldDisplayNames.FormatGraphPropertyValue("card_preview_style", value)))
            .ToList();
    }

    private static IReadOnlyList<(string Value, string Display)> GetCardKeywordChoices()
    {
        return Enum.GetNames<CardKeyword>()
            .Select(value => (value, ModStudioFieldDisplayNames.FormatGraphPropertyValue("keyword", value)))
            .ToList();
    }

    private static IReadOnlyList<(string Value, string Display)> GetSelectionModeChoices()
    {
        return new[]
        {
            ("simple_grid", Dual("网格选择", "Simple Grid")),
            ("simple_grid_rewards", Dual("奖励网格选择", "Rewards Grid")),
            ("hand", Dual("手牌选择", "Hand")),
            ("hand_for_discard", Dual("手牌弃牌选择", "Hand For Discard")),
            ("hand_for_upgrade", Dual("手牌升级选择", "Hand For Upgrade")),
            ("choose_a_card_screen", Dual("三选一卡", "Choose A Card")),
            ("choose_bundle", Dual("卡包选择", "Choose Bundle")),
            ("deck_for_upgrade", Dual("牌库升级选择", "Deck For Upgrade")),
            ("deck_for_enchantment", Dual("牌库附魔选择", "Deck For Enchantment")),
            ("deck_for_transformation", Dual("牌库变形选择", "Deck For Transformation")),
            ("deck_for_removal", Dual("牌库移除选择", "Deck For Removal"))
        };
    }

    private static IReadOnlyList<(string Value, string Display)> GetCardTypeScopeChoices()
    {
        return new[]
        {
            ("any", Dual("任意类型", "Any Type")),
            ("attack", Dual("攻击牌", "Attack")),
            ("skill", Dual("技能牌", "Skill")),
            ("power", Dual("能力牌", "Power")),
            ("status", Dual("状态牌", "Status")),
            ("curse", Dual("诅咒牌", "Curse")),
            ("attack_skill", Dual("攻击 / 技能", "Attack / Skill")),
            ("attack_power", Dual("攻击 / 能力", "Attack / Power")),
            ("skill_power", Dual("技能 / 能力", "Skill / Power")),
            ("attack_skill_power", Dual("攻击 / 技能 / 能力", "Attack / Skill / Power")),
            ("non_status", Dual("非状态战斗牌", "Non-status Combat Cards"))
        };
    }

    private static IReadOnlyList<(string Value, string Display)> GetCardPilePositionChoices()
    {
        return Enum.GetNames<CardPilePosition>()
            .Select(value => (value, ModStudioFieldDisplayNames.FormatGraphPropertyValue("position", value)))
            .ToList();
    }

    private static IReadOnlyList<(string Value, string Display)> GetPromptKindChoices()
    {
        return new[]
        {
            ("generic", Dual("通用", "Generic")),
            ("discard", Dual("弃牌", "Discard")),
            ("exhaust", Dual("消耗", "Exhaust")),
            ("transform", Dual("变形", "Transform")),
            ("upgrade", Dual("升级", "Upgrade")),
            ("remove", Dual("移除", "Remove")),
            ("enchant", Dual("附魔", "Enchant"))
        };
    }

    private static IReadOnlyList<(string Value, string Display)> GetRewardKindChoices()
    {
        return new[]
        {
            ("gold", Dual("金币", "Gold")),
            ("energy", Dual("能量", "Energy")),
            ("stars", Dual("星数", "Stars")),
            ("draw", Dual("抽牌", "Draw")),
            ("block", Dual("格挡", "Block")),
            ("heal", Dual("治疗", "Heal")),
            ("damage", Dual("伤害", "Damage")),
            ("power", Dual("能力", "Power")),
            ("relic", Dual("遗物奖励", "Relic Reward")),
            ("potion", Dual("药水奖励", "Potion Reward")),
            ("special_card", Dual("特殊卡牌奖励", "Special Card Reward")),
            ("card_removal", Dual("移除卡牌奖励", "Card Removal Reward")),
            ("custom", Dual("自定义占位", "Custom Placeholder"))
        };
    }

    private static IReadOnlyList<(string Value, string Display)> GetOperatorChoices()
    {
        return new[]
        {
            ("eq", Dual("等于", "=")),
            ("ne", Dual("不等于", "!=")),
            ("lt", Dual("小于", "<")),
            ("lte", Dual("小于等于", "<=")),
            ("gt", Dual("大于", ">")),
            ("gte", Dual("大于等于", ">="))
        };
    }

    private static IReadOnlyList<(string Value, string Display)> GetDynamicSourceKindChoices()
    {
        return new[]
        {
            (DynamicValueSourceKind.Literal.ToString(), ModStudioLocalizationCatalog.T("graph.source.literal")),
            (DynamicValueSourceKind.DynamicVar.ToString(), ModStudioLocalizationCatalog.T("graph.source.dynamic_var")),
            (DynamicValueSourceKind.FormulaRef.ToString(), ModStudioLocalizationCatalog.T("graph.source.formula"))
        };
    }

    private static IReadOnlyList<(string Value, string Display)> GetOverrideModeChoices()
    {
        return new[]
        {
            (DynamicValueOverrideMode.None.ToString(), ModStudioLocalizationCatalog.T("graph.override.none")),
            (DynamicValueOverrideMode.Absolute.ToString(), ModStudioLocalizationCatalog.T("graph.override.absolute")),
            (DynamicValueOverrideMode.Delta.ToString(), ModStudioLocalizationCatalog.T("graph.override.delta"))
        };
    }

    private static IEnumerable<string> EnumerateDynamicVarNames(AbstractModel? model)
    {
        return model switch
        {
            CardModel card => card.DynamicVars.Values.Select(value => value.Name),
            PotionModel potion => potion.DynamicVars.Values.Select(value => value.Name),
            RelicModel relic => relic.DynamicVars.Values.Select(value => value.Name),
            _ => Array.Empty<string>()
        };
    }

    private static IEnumerable<string> EnumerateFormulaVarNames(AbstractModel? model)
    {
        return model switch
        {
            CardModel card => card.DynamicVars.Values
                .Where(value => value is MegaCrit.Sts2.Core.Localization.DynamicVars.CalculatedVar)
                .Select(value => value.Name),
            _ => Array.Empty<string>()
        };
    }

    private static IEnumerable<string> GetCommonDynamicVarNames()
    {
        return new[]
        {
            "Damage",
            "Block",
            "Heal",
            "Energy",
            "Gold",
            "Stars",
            "Cards",
            "Power",
            "CalculationBase",
            "CalculationExtra"
        };
    }

    private static IEnumerable<string> GetCommonFormulaRefs()
    {
        return new[]
        {
            "CalculatedDamage",
            "CalculatedBlock"
        };
    }

    private static IReadOnlyList<(string Value, string Display)> GetPreviewMultiplierChoices()
    {
        return new[]
        {
            ("hand_count", Dual("手牌数", "Hand Count")),
            ("cards", Dual("手牌数", "Hand Count")),
            ("stars", Dual("星数", "Stars")),
            ("energy", Dual("能量", "Energy")),
            ("current_block", Dual("当前格挡", "Current Block")),
            ("draw_pile", Dual("抽牌堆数量", "Draw Pile Count")),
            ("discard_pile", Dual("弃牌堆数量", "Discard Pile Count")),
            ("exhaust_pile", Dual("消耗堆数量", "Exhaust Pile Count")),
            ("missing_hp", Dual("已损生命", "Missing HP"))
        };
    }

    #if false
    private static IReadOnlyList<(string Value, string Display)> GetRewardKindChoicesV2()
    {
        return new[]
        {
            ("gold", Dual("閲戝竵", "Gold")),
            ("energy", Dual("鑳介噺", "Energy")),
            ("stars", Dual("鏄熸暟", "Stars")),
            ("draw", Dual("鎶界墝", "Draw")),
            ("block", Dual("鏍兼尅", "Block")),
            ("heal", Dual("娌荤枟", "Heal")),
            ("damage", Dual("浼ゅ", "Damage")),
            ("max_hp", Dual("鏈€澶х敓鍛?, "Max HP")),
            ("power", Dual("鑳藉姏", "Power")),
            ("card", Dual("鍗＄墝濂栧姳", "Card Reward")),
            ("relic", Dual("閬楃墿濂栧姳", "Relic Reward")),
            ("potion", Dual("鑽按濂栧姳", "Potion Reward")),
            ("special_card", Dual("鐗规畩鍗＄墝濂栧姳", "Special Card Reward")),
            ("remove_card", Dual("绉婚櫎鍗＄墝濂栧姳", "Card Removal Reward")),
            ("custom", Dual("鑷畾涔夊崰浣?, "Custom Placeholder"))
        };
    }

    #endif

    private static IReadOnlyList<(string Value, string Display)> GetRewardKindChoicesV2()
    {
        return new[]
        {
            ("gold", Dual("金币", "Gold")),
            ("energy", Dual("能量", "Energy")),
            ("stars", Dual("星数", "Stars")),
            ("draw", Dual("抽牌", "Draw")),
            ("block", Dual("格挡", "Block")),
            ("heal", Dual("治疗", "Heal")),
            ("damage", Dual("伤害", "Damage")),
            ("max_hp", Dual("最大生命", "Max HP")),
            ("power", Dual("能力", "Power")),
            ("card", Dual("卡牌奖励", "Card Reward")),
            ("relic", Dual("遗物奖励", "Relic Reward")),
            ("potion", Dual("药水奖励", "Potion Reward")),
            ("special_card", Dual("特殊卡牌奖励", "Special Card Reward")),
            ("remove_card", Dual("移除卡牌奖励", "Card Removal Reward")),
            ("custom", Dual("自定义占位", "Custom Placeholder"))
        };
    }

    private static string SafeLocText(LocString? locString)
    {
        if (locString == null)
        {
            return string.Empty;
        }

        try
        {
            return locString.GetRawText();
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string Dual(string zh, string en)
    {
        return ModStudioLocalization.IsChinese ? zh : en;
    }
}
