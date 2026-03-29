using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Enchantments;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using STS2_Editor.Scripts.Editor.Core.Models;
using STS2_Editor.Scripts.Editor.Core.Utilities;
using STS2_Editor.Scripts.Editor.Graph;

namespace STS2_Editor.Scripts.Editor.UI;

internal static class FieldChoiceProvider
{
    private static readonly Dictionary<string, IReadOnlyList<(string Value, string Display)>> BasicChoiceCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, IReadOnlyList<(string Value, string Display)>> GraphChoiceCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<ModStudioEntityKind, IReadOnlyList<(string Value, string Display)>> GraphTriggerChoiceCache = new();
    private static bool? _cachedLanguageIsChinese;
    private static EditorProject? _currentProject;

    public static EditorProject? CurrentProject => _currentProject;

    public static void SetCurrentProject(EditorProject? project)
    {
        if (ReferenceEquals(_currentProject, project))
        {
            return;
        }

        _currentProject = project;
        InvalidateProjectChoices();
    }

    public static void InvalidateProjectChoices()
    {
        // Clear cached entries that include project entities
        foreach (var key in BasicChoiceCache.Keys.Where(k =>
            k.EndsWith(":card_id", StringComparison.OrdinalIgnoreCase) ||
            k.EndsWith(":replacement_card_id", StringComparison.OrdinalIgnoreCase) ||
            k.EndsWith(":relic_id", StringComparison.OrdinalIgnoreCase) ||
            k.EndsWith(":replacement_relic_id", StringComparison.OrdinalIgnoreCase) ||
            k.EndsWith(":potion_id", StringComparison.OrdinalIgnoreCase) ||
            k.EndsWith(":enchantment_id", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            BasicChoiceCache.Remove(key);
        }

        GraphChoiceCache.Remove("card_id");
        GraphChoiceCache.Remove("replacement_card_id");
        GraphChoiceCache.Remove("relic_id");
        GraphChoiceCache.Remove("replacement_relic_id");
        GraphChoiceCache.Remove("potion_id");
        GraphChoiceCache.Remove("enchantment_id");

        ModStudioBasicEditor.InvalidateListOptionCache();
    }

    public static IReadOnlyList<(string Value, string Display)> GetBasicChoices(ModStudioEntityKind kind, string key)
    {
        EnsureCacheLanguage();
        var cacheKey = $"{kind}:{key}";
        if (BasicChoiceCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var result = key switch
        {
            "type" => Enum.GetNames<CardType>()
                .Select(value => (value, ModStudioFieldDisplayNames.FormatPropertyValue(key, value)))
                .ToList(),
            "rarity" => GetRarityChoices(kind),
            "target_type" => Enum.GetNames<TargetType>()
                .Select(value => (value, ModStudioFieldDisplayNames.FormatPropertyValue(key, value)))
                .ToList(),
            "usage" => Enum.GetNames<PotionUsage>()
                .Select(value => (value, ModStudioFieldDisplayNames.FormatPropertyValue(key, value)))
                .ToList(),
            "layout_type" => Enum.GetNames<EventLayoutType>()
                .Select(value => (value, ModStudioFieldDisplayNames.FormatPropertyValue(key, value)))
                .ToList(),
            "phase_kind" => Enum.GetNames<MonsterPhaseKind>()
                .Select(value => (value, ModStudioFieldDisplayNames.FormatPropertyValue(key, value)))
                .ToList(),
            "repeat_type" => Enum.GetNames<MoveRepeatType>()
                .Select(value => (value, ModStudioFieldDisplayNames.FormatPropertyValue(key, value)))
                .ToList(),
            "intent_type" => Enum.GetNames<MonsterIntentType>()
                .Select(value => (value, ModStudioFieldDisplayNames.FormatPropertyValue(key, value)))
                .ToList(),
            "hook_type" => Enum.GetNames<MonsterLifecycleHookType>()
                .Select(value => (value, ModStudioFieldDisplayNames.FormatPropertyValue(key, value)))
                .ToList(),
            "event_kind" => Enum.GetNames<MonsterEventTriggerKind>()
                .Select(value => (value, ModStudioFieldDisplayNames.FormatPropertyValue(key, value)))
                .ToList(),
            "state_variable_type" => Enum.GetNames<MonsterStateVariableType>()
                .Select(value => (value, ModStudioFieldDisplayNames.FormatPropertyValue(key, value)))
                .ToList(),
            "pool_id" => GetPoolChoices(kind),
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

        BasicChoiceCache[cacheKey] = result;
        return result;
    }

    public static IReadOnlyList<(string Value, string Display)> GetGraphChoices(string key)
    {
        EnsureCacheLanguage();
        if (GraphChoiceCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var result = key switch
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
            "status" => GetEnchantmentStatusChoices(),
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
            "reward_room_type" => GetRoomTypeChoices(),
            "room_type" => GetRoomTypeChoices(),
            "map_kind" => GetMapKindChoices(),
            _ => Array.Empty<(string, string)>()
        };

        GraphChoiceCache[key] = result;
        return result;
    }

    public static IReadOnlyList<(string Value, string Display)> GetGraphTriggerChoices(ModStudioEntityKind kind)
    {
        EnsureCacheLanguage();
        if (GraphTriggerChoiceCache.TryGetValue(kind, out var cached))
        {
            return cached;
        }

        var result = kind switch
        {
            ModStudioEntityKind.Card => BuildTriggerChoices(
            [
                ("card.on_play", Dual("打出卡牌时", "On Play"))
            ]),
            ModStudioEntityKind.Potion => BuildTriggerChoices(
            [
                ("potion.on_use", Dual("使用药水时", "On Use"))
            ]),
            ModStudioEntityKind.Relic => BuildTriggerChoices(
            [
                ("relic.after_obtained", Dual("获得遗物后", "After Obtained")),
                ("relic.after_room_entered", Dual("进入房间后", "After Room Entered")),
                ("relic.before_combat_start", Dual("战斗开始前", "Before Combat Start")),
                ("relic.before_combat_start_late", Dual("战斗开始前 Late", "Before Combat Start Late")),
                ("relic.after_player_turn_start_early", Dual("玩家回合开始后 Early", "After Player Turn Start Early")),
                ("relic.after_player_turn_start", Dual("玩家回合开始后", "After Player Turn Start")),
                ("relic.after_player_turn_start_late", Dual("玩家回合开始后 Late", "After Player Turn Start Late")),
                ("relic.before_turn_end_very_early", Dual("回合结束前 Very Early", "Before Turn End Very Early")),
                ("relic.before_turn_end_early", Dual("回合结束前 Early", "Before Turn End Early")),
                ("relic.before_turn_end", Dual("回合结束前", "Before Turn End")),
                ("relic.after_turn_end", Dual("回合结束后", "After Turn End")),
                ("relic.after_turn_end_late", Dual("回合结束后 Late", "After Turn End Late")),
                ("relic.before_card_played", Dual("打牌前", "Before Card Played")),
                ("relic.after_card_played", Dual("打牌后", "After Card Played")),
                ("relic.after_card_played_late", Dual("打牌后 Late", "After Card Played Late")),
                ("relic.after_card_drawn", Dual("抽到卡牌后", "After Card Drawn")),
                ("relic.after_card_discarded", Dual("弃牌后", "After Card Discarded")),
                ("relic.after_card_exhausted", Dual("卡牌消耗后", "After Card Exhausted")),
                ("relic.after_card_changed_piles", Dual("卡牌换牌堆后", "After Card Changed Piles")),
                ("relic.after_card_changed_piles_late", Dual("卡牌换牌堆后 Late", "After Card Changed Piles Late")),
                ("relic.before_hand_draw", Dual("手牌抽取前", "Before Hand Draw")),
                ("relic.before_hand_draw_late", Dual("手牌抽取前 Late", "Before Hand Draw Late")),
                ("relic.modify_hand_draw", Dual("修改抽牌数量", "Modify Hand Draw")),
                ("relic.modify_hand_draw_late", Dual("修改抽牌数量 Late", "Modify Hand Draw Late")),
                ("relic.modify_max_energy", Dual("修改最大能量", "Modify Max Energy")),
                ("relic.after_energy_reset", Dual("能量重置后", "After Energy Reset")),
                ("relic.after_energy_reset_late", Dual("能量重置后 Late", "After Energy Reset Late")),
                ("relic.should_player_reset_energy", Dual("是否重置玩家能量", "Should Player Reset Energy")),
                ("relic.before_attack", Dual("攻击前", "Before Attack")),
                ("relic.after_attack", Dual("攻击后", "After Attack")),
                ("relic.before_potion_used", Dual("使用药水前", "Before Potion Used")),
                ("relic.after_potion_used", Dual("使用药水后", "After Potion Used")),
                ("relic.after_shuffle", Dual("洗牌后", "After Shuffle")),
                ("relic.after_hand_emptied", Dual("手牌打空后", "After Hand Emptied")),
                ("relic.after_stars_spent", Dual("消耗星数后", "After Stars Spent")),
                ("relic.after_combat_end", Dual("战斗结束后", "After Combat End")),
                ("relic.after_combat_victory_early", Dual("战斗胜利后 Early", "After Combat Victory Early")),
                ("relic.after_combat_victory", Dual("战斗胜利后", "After Combat Victory")),
                ("relic.modify_rewards", Dual("修改奖励", "Modify Rewards")),
                ("relic.modify_rewards_late", Dual("修改奖励 Late", "Modify Rewards Late")),
                ("relic.modify_card_reward_options", Dual("修改卡牌奖励选项", "Modify Card Reward Options")),
                ("relic.modify_card_reward_options_late", Dual("修改卡牌奖励选项 Late", "Modify Card Reward Options Late")),
                ("relic.modify_rest_site_heal_rewards", Dual("修改休息点治疗奖励", "Modify Rest Site Heal Rewards")),
                ("relic.should_disable_remaining_rest_site_options", Dual("是否禁用剩余休息点选项", "Should Disable Remaining Rest Site Options")),
                ("relic.should_flush", Dual("是否清空手牌", "Should Flush")),
                ("relic.should_force_potion_reward", Dual("是否强制药水奖励", "Should Force Potion Reward")),
                ("relic.should_gain_gold", Dual("是否获得金币", "Should Gain Gold")),
                ("relic.should_procure_potion", Dual("是否获得药水", "Should Procure Potion")),
                ("relic.should_refill_merchant_entry", Dual("是否补货商店物品", "Should Refill Merchant Entry")),
                ("relic.modify_generated_map", Dual("修改地图生成", "Modify Generated Map")),
                ("relic.modify_generated_map_late", Dual("修改地图生成 Late", "Modify Generated Map Late")),
                ("relic.modify_unknown_map_point_room_types", Dual("修改未知地图点房间类型", "Modify Unknown Map Point Room Types")),
                ("relic.modify_x_value", Dual("修改 X 值", "Modify X Value")),
                ("relic.modify_damage_additive", Dual("修改伤害加算", "Modify Damage Additive")),
                ("relic.modify_damage_multiplicative", Dual("修改伤害乘算", "Modify Damage Multiplicative")),
                ("relic.modify_block_multiplicative", Dual("修改格挡乘算", "Modify Block Multiplicative")),
                ("relic.modify_hp_lost_before_osty", Dual("修改失去生命前置", "Modify HP Lost Before Osty")),
                ("relic.modify_hp_lost_after_osty", Dual("修改失去生命后置", "Modify HP Lost After Osty")),
                ("relic.before_play_phase_start", Dual("出牌阶段开始前", "Before Play Phase Start")),
                ("relic.before_side_turn_start", Dual("阵营回合开始前", "Before Side Turn Start")),
                ("relic.after_side_turn_start", Dual("阵营回合开始后", "After Side Turn Start"))
            ]),
            ModStudioEntityKind.Enchantment => BuildTriggerChoices(
            [
                ("enchantment.on_enchant", Dual("附魔生效时", "On Enchant")),
                ("enchantment.on_play", Dual("附魔卡牌打出时", "On Play")),
                ("enchantment.after_card_played", Dual("打牌后", "After Card Played")),
                ("enchantment.after_card_drawn", Dual("抽到卡牌后", "After Card Drawn")),
                ("enchantment.after_player_turn_start", Dual("玩家回合开始后", "After Player Turn Start")),
                ("enchantment.before_flush", Dual("洗切前", "Before Flush")),
                ("enchantment.modify_damage_additive", Dual("修改伤害加算", "Modify Damage Additive")),
                ("enchantment.modify_damage_multiplicative", Dual("修改伤害乘算", "Modify Damage Multiplicative")),
                ("enchantment.modify_block_additive", Dual("修改格挡加算", "Modify Block Additive")),
                ("enchantment.modify_block_multiplicative", Dual("修改格挡乘算", "Modify Block Multiplicative")),
                ("enchantment.modify_play_count", Dual("修改打出次数", "Modify Play Count"))
            ]),
            _ => Array.Empty<(string, string)>()
        };

        GraphTriggerChoiceCache[kind] = result;
        return result;
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

    private static IReadOnlyList<(string Value, string Display)> GetPoolChoices(ModStudioEntityKind kind)
    {
        return kind switch
        {
            ModStudioEntityKind.Card => ModelDb.AllCardPools
                .Select(pool => (pool.Id.Entry, ModStudioFieldDisplayNames.FormatPropertyValue("pool_id", pool.Id.Entry)))
                .OrderBy(pair => pair.Item2, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            ModStudioEntityKind.Relic => ModelDb.AllRelicPools
                .Select(pool => (pool.Id.Entry, ModStudioFieldDisplayNames.FormatPropertyValue("pool_id", pool.Id.Entry)))
                .OrderBy(pair => pair.Item2, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            ModStudioEntityKind.Potion => ModelDb.AllPotionPools
                .Select(pool => (pool.Id.Entry, ModStudioFieldDisplayNames.FormatPropertyValue("pool_id", pool.Id.Entry)))
                .OrderBy(pair => pair.Item2, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            _ => Array.Empty<(string, string)>()
        };
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
        var items = ModelDb.AllRelics
            .Select(relic =>
            {
                var title = ResolveEntityTitle(ModStudioEntityKind.Relic, relic.Id.Entry, SafeLocText(relic.Title));
                return (relic.Id.Entry, $"{title} [{relic.Id.Entry}]");
            })
            .ToList();

        AppendProjectEntities(items, ModStudioEntityKind.Relic);

        return items.OrderBy(pair => pair.Item2, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static IReadOnlyList<(string Value, string Display)> GetPotionChoices()
    {
        var items = ModelDb.AllPotions
            .Select(potion =>
            {
                var title = ResolveEntityTitle(ModStudioEntityKind.Potion, potion.Id.Entry, SafeLocText(potion.Title));
                return (potion.Id.Entry, $"{title} [{potion.Id.Entry}]");
            })
            .ToList();

        AppendProjectEntities(items, ModStudioEntityKind.Potion);

        return items.OrderBy(pair => pair.Item2, StringComparer.OrdinalIgnoreCase).ToList();
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
        var items = ModelDb.AllCards
            .Select(card =>
            {
                var title = ResolveEntityTitle(ModStudioEntityKind.Card, card.Id.Entry, SafeLocText(card.TitleLocString));
                return (card.Id.Entry, $"{title} [{card.Id.Entry}]");
            })
            .ToList();

        AppendProjectEntities(items, ModStudioEntityKind.Card);

        return items.OrderBy(pair => pair.Item2, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static IReadOnlyList<(string Value, string Display)> GetEnchantmentChoices()
    {
        var items = ModelDb.DebugEnchantments
            .Select(enchantment =>
            {
                var title = ResolveEntityTitle(ModStudioEntityKind.Enchantment, enchantment.Id.Entry, SafeLocText(enchantment.Title));
                return (enchantment.Id.Entry, $"{title} [{enchantment.Id.Entry}]");
            })
            .ToList();

        AppendProjectEntities(items, ModStudioEntityKind.Enchantment);

        return items.OrderBy(pair => pair.Item2, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static IReadOnlyList<(string Value, string Display)> GetRarityChoices(ModStudioEntityKind kind)
    {
        IEnumerable<string> values = kind switch
        {
            ModStudioEntityKind.Card => Enum.GetNames<CardRarity>(),
            ModStudioEntityKind.Relic => Enum.GetNames<RelicRarity>(),
            ModStudioEntityKind.Potion => Enum.GetNames<PotionRarity>(),
            _ => Array.Empty<string>()
        };

        return values
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .Select(value => (value, ModStudioFieldDisplayNames.FormatPropertyValue("rarity", value)))
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

    private static IReadOnlyList<(string Value, string Display)> GetRoomTypeChoices()
    {
        return Enum.GetNames<RoomType>()
            .Select(value => (value, ModStudioFieldDisplayNames.FormatPropertyValue("room_type", value)))
            .ToList();
    }

    private static IReadOnlyList<(string Value, string Display)> GetMapKindChoices()
    {
        return
        [
            ("golden_path", ModStudioFieldDisplayNames.FormatPropertyValue("map_kind", "golden_path"))
        ];
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

    private static IReadOnlyList<(string Value, string Display)> GetEnchantmentStatusChoices()
    {
        return Enum.GetNames<EnchantmentStatus>()
            .Select(value => (value, ModStudioFieldDisplayNames.FormatGraphPropertyValue("status", value)))
            .ToList();
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

    private static IReadOnlyList<(string Value, string Display)> BuildTriggerChoices((string Value, string Display)[] choices)
    {
        return choices.ToList();
    }

    private static void AppendProjectEntities(List<(string Value, string Display)> items, ModStudioEntityKind kind)
    {
        if (_currentProject == null)
        {
            return;
        }

        var existingIds = new HashSet<string>(items.Select(i => i.Value), StringComparer.OrdinalIgnoreCase);

        foreach (var envelope in _currentProject.Overrides)
        {
            if (envelope.EntityKind != kind || existingIds.Contains(envelope.EntityId))
            {
                continue;
            }

            var title = ResolveEntityTitle(kind, envelope.EntityId, envelope.EntityId);
            items.Add((envelope.EntityId, $"{title} [{envelope.EntityId}]"));
        }
    }

    internal static string ResolveEntityTitle(ModStudioEntityKind kind, string entityId, string fallbackTitle)
    {
        if (_currentProject == null)
        {
            return string.IsNullOrWhiteSpace(fallbackTitle) ? entityId : fallbackTitle;
        }

        var envelope = _currentProject.Overrides.FirstOrDefault(candidate =>
            candidate.EntityKind == kind &&
            string.Equals(candidate.EntityId, entityId, StringComparison.Ordinal));
        if (envelope?.Metadata != null &&
            envelope.Metadata.TryGetValue("title", out var overriddenTitle) &&
            !string.IsNullOrWhiteSpace(overriddenTitle))
        {
            return overriddenTitle;
        }

        return string.IsNullOrWhiteSpace(fallbackTitle) ? entityId : fallbackTitle;
    }

    private static void EnsureCacheLanguage()
    {
        if (_cachedLanguageIsChinese == ModStudioLocalization.IsChinese)
        {
            return;
        }

        _cachedLanguageIsChinese = ModStudioLocalization.IsChinese;
        BasicChoiceCache.Clear();
        GraphChoiceCache.Clear();
        GraphTriggerChoiceCache.Clear();
    }
}
