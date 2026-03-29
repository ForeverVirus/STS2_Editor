using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Enchantments;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Audio;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using STS2_Editor.Scripts.Editor.Core.Models;
using STS2_Editor.Scripts.Editor.Core.Utilities;

namespace STS2_Editor.Scripts.Editor.UI;

internal static class ModStudioFieldDisplayNames
{
    public static string Get(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        return key switch
        {
            "left" => Dual("左值", "Left"),
            "right" => Dual("右值", "Right"),
            "key" => Dual("状态键", "State Key"),
            "value" => Dual("状态值", "State Value"),
            "delta" => Dual("增量", "Delta"),
            "factor" => Dual("倍率", "Factor"),
            "result_key" => Dual("结果键", "Result Key"),
            "status" => Dual("状态", "Status"),
            "title" => Dual("名称", "Title"),
            "description" => Dual("描述", "Description"),
            "initial_description" => Dual("初始描述", "Initial Description"),
            "starting_hp" => Dual("初始血量", "Starting HP"),
            "starting_gold" => Dual("初始金币", "Starting Gold"),
            "max_energy" => Dual("初始能量", "Max Energy"),
            "base_orb_slot_count" => Dual("基础槽位数", "Base Orb Slots"),
            "starting_deck_ids" => Dual("初始卡组", "Starting Deck"),
            "starting_relic_ids" => Dual("初始遗物", "Starting Relics"),
            "starting_potion_ids" => Dual("初始药水", "Starting Potions"),
            "pool_id" => Dual("所属池 ID", "Pool Id"),
            "type" => Dual("类型", "Type"),
            "rarity" => Dual("稀有度", "Rarity"),
            "target_type" => Dual("目标类型", "Target Type"),
            "energy_cost" => Dual("能量费用", "Energy Cost"),
            "energy_cost_x" => Dual("X 费用", "X Cost"),
            "canonical_star_cost" => Dual("星数费用", "Star Cost"),
            "star_cost_x" => Dual("星数 X 费用", "Star X Cost"),
            "can_be_generated_in_combat" => Dual("可在战斗中生成", "Can Generate In Combat"),
            "usage" => Dual("使用方式", "Usage"),
            "layout_type" => Dual("布局类型", "Layout Type"),
            "is_shared" => Dual("共享事件", "Shared Event"),
            "show_amount" => Dual("显示数值", "Show Amount"),
            "has_extra_card_text" => Dual("额外卡牌文本", "Has Extra Card Text"),
            "extra_card_text" => Dual("额外卡牌文本内容", "Extra Card Text"),
            "min_initial_hp" => Dual("最小初始血量", "Min Initial HP"),
            "max_initial_hp" => Dual("最大初始血量", "Max Initial HP"),
            "is_health_bar_visible" => Dual("显示生命条", "Is Health Bar Visible"),
            "hp_bar_size_reduction" => Dual("生命条缩减", "HP Bar Size Reduction"),
            "bestiary_attack_anim_id" => Dual("图鉴攻击动画", "Bestiary Attack Anim Id"),
            "take_damage_sfx_type" => Dual("受击音效类型", "Take Damage Sfx Type"),
            "take_damage_sfx" => Dual("受击音效", "Take Damage Sfx"),
            "death_sfx" => Dual("死亡音效", "Death Sfx"),
            "has_death_sfx" => Dual("有死亡音效", "Has Death Sfx"),
            "hurt_sfx" => Dual("受伤音效", "Hurt Sfx"),
            "has_hurt_sfx" => Dual("有受伤音效", "Has Hurt Sfx"),
            "should_fade_after_death" => Dual("死亡后淡出", "Should Fade After Death"),
            "should_disappear_from_doom" => Dual("末日后消失", "Should Disappear From Doom"),
            "death_anim_length_override" => Dual("死亡动画时长", "Death Anim Length Override"),
            "can_change_scale" => Dual("可缩放", "Can Change Scale"),
            "extra_death_vfx_padding_x" => Dual("额外死亡特效偏移 X", "Extra Death VFX Padding X"),
            "extra_death_vfx_padding_y" => Dual("额外死亡特效偏移 Y", "Extra Death VFX Padding Y"),
            "portrait_path" => Dual("立绘路径", "Portrait Path"),
            "icon_path" => Dual("图标路径", "Icon Path"),
            "image_path" => Dual("图片路径", "Image Path"),
            "amount" => Dual("数值", "Amount"),
            "count" => Dual("数量上限", "Count Limit"),
            "hit_count" => Dual("连击次数", "Hit Count"),
            "target" => Dual("目标", "Target"),
            "variable_name" => Dual("变量名", "Variable Name"),
            "animation_id" => Dual("动画 ID", "Animation Id"),
            "wait_duration" => Dual("等待时长", "Wait Duration"),
            "sfx_path" => Dual("音效路径", "Sfx Path"),
            "duration" => Dual("持续时长", "Duration"),
            "source_pile" => Dual("来源牌堆", "Source Pile"),
            "target_pile" => Dual("目标牌堆", "Target Pile"),
            "exact_energy_cost" => Dual("精确费用", "Exact Cost"),
            "include_x_cost" => Dual("包含 X 费", "Include X Cost"),
            "card_type_scope" => Dual("卡牌类型筛选", "Card Type Scope"),
            "position" => Dual("抽取位置", "Draw Position"),
            "card_id" => Dual("卡牌 ID", "Card Id"),
            "replacement_card_id" => Dual("替换卡牌 ID", "Replacement Card Id"),
            "monster_id" => Dual("怪物 ID", "Monster Id"),
            "enchantment_id" => Dual("附魔 ID", "Enchantment Id"),
            "auto_play_type" => Dual("自动打出类型", "Auto Play Type"),
            "force_exhaust" => Dual("强制消耗", "Force Exhaust"),
            "gold_loss_type" => Dual("金币损失类型", "Gold Loss Type"),
            "card_preview_style" => Dual("卡牌预览风格", "Card Preview Style"),
            "props" => Dual("属性标记", "Props"),
            "power_id" => Dual("能力 ID", "Power Id"),
            "dynamic_source_kind" => Dual("动态值来源", "Dynamic Source"),
            "dynamic_var_name" => Dual("动态变量", "Dynamic Variable"),
            "formula_ref" => Dual("原版公式", "Formula Ref"),
            "base_override_mode" => Dual("基础值覆盖方式", "Base Override Mode"),
            "base_override_value" => Dual("基础值覆盖", "Base Override Value"),
            "extra_override_mode" => Dual("额外值覆盖方式", "Extra Override Mode"),
            "extra_override_value" => Dual("额外值覆盖", "Extra Override Value"),
            "preview_multiplier_key" => Dual("公式上下文来源", "Formula Context Key"),
            "preview_multiplier_value" => Dual("预览系数（兼容）", "Preview Scale (Legacy)"),
            "template_text" => Dual("模板文本", "Template Text"),
            "preview_format" => Dual("预览格式", "Preview Format"),
            "condition" => Dual("条件", "Condition"),
            "condition_key" => Dual("条件键", "Condition Key"),
            "message" => Dual("消息", "Message"),
            "reward_kind" => Dual("奖励类型", "Reward Kind"),
            "reward_count" => Dual("奖励数量", "Reward Count"),
            "operator" => Dual("运算符", "Operator"),
            "phase_kind" => Dual("阶段类型", "Phase Kind"),
            "repeat_type" => Dual("重复规则", "Repeat Type"),
            "intent_type" => Dual("意图类型", "Intent Type"),
            "hook_type" => Dual("生命周期钩子", "Lifecycle Hook"),
            "event_kind" => Dual("事件类型", "Event Kind"),
            "state_variable_type" => Dual("状态变量类型", "State Variable Type"),
            "turn_id" => Dual("回合 ID", "Turn Id"),
            "move_id" => Dual("招式 ID", "Move Id"),
            "phase_id" => Dual("阶段 ID", "Phase Id"),
            "branch_id" => Dual("分支 ID", "Branch Id"),
            "graph_id" => Dual("Graph ID", "Graph Id"),
            "initial_value" => Dual("初始值", "Initial Value"),
            "filter_monster_id" => Dual("过滤怪物 ID", "Filter Monster Id"),
            "target_phase_id" => Dual("目标阶段 ID", "Target Phase Id"),
            "target_turn_id" => Dual("目标回合 ID", "Target Turn Id"),
            "max_repeats" => Dual("最大重复次数", "Max Repeats"),
            "cooldown" => Dual("冷却回合", "Cooldown"),
            "weight" => Dual("权重", "Weight"),
            "page_id" => Dual("页面 ID", "Page Id"),
            "option_id" => Dual("选项 ID", "Option Id"),
            "next_page_id" => Dual("下一页面 ID", "Next Page Id"),
            "resume_page_id" => Dual("返回页面 ID", "Resume Page Id"),
            "encounter_id" => Dual("遭遇 ID", "Encounter Id"),
            "add_card" => Dual("添加卡牌", "Add Card"),
            "remove_card" => Dual("移除卡牌", "Remove Card"),
            "transform_card" => Dual("转化卡牌", "Transform Card"),
            "move_room" => Dual("移动房间", "Move Room"),
            "spawn_room" => Dual("生成房间", "Spawn Room"),
            "unlock_map" => Dual("解锁地图", "Unlock Map"),
            _ => key
        };
    }

    public static string FormatValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (bool.TryParse(value, out var booleanValue))
        {
            return booleanValue ? Dual("是", "True") : Dual("否", "False");
        }

        return value;
    }

    public static string FormatPropertyValue(string? key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (bool.TryParse(value, out var booleanValue))
        {
            return FormatValue(booleanValue.ToString());
        }

        return (key ?? string.Empty).ToLowerInvariant() switch
        {
            "type" => value switch
            {
                nameof(CardType.None) => Dual("无", "None"),
                nameof(CardType.Attack) => Dual("攻击", "Attack"),
                nameof(CardType.Skill) => Dual("技能", "Skill"),
                nameof(CardType.Power) => Dual("能力", "Power"),
                nameof(CardType.Status) => Dual("状态", "Status"),
                nameof(CardType.Curse) => Dual("诅咒", "Curse"),
                nameof(CardType.Quest) => Dual("任务", "Quest"),
                _ => value
            },
            "target_type" => value switch
            {
                nameof(TargetType.None) => Dual("无", "None"),
                nameof(TargetType.Self) => Dual("自身", "Self"),
                nameof(TargetType.AnyEnemy) => Dual("任意敌人", "Any Enemy"),
                nameof(TargetType.AllEnemies) => Dual("所有敌人", "All Enemies"),
                nameof(TargetType.RandomEnemy) => Dual("随机敌人", "Random Enemy"),
                nameof(TargetType.AnyPlayer) => Dual("任意玩家", "Any Player"),
                nameof(TargetType.AnyAlly) => Dual("任意友方", "Any Ally"),
                nameof(TargetType.AllAllies) => Dual("所有友方", "All Allies"),
                nameof(TargetType.TargetedNoCreature) => Dual("非生物目标", "Targeted Non-Creature"),
                nameof(TargetType.Osty) => "Osty",
                _ => value
            },
            "rarity" => value switch
            {
                nameof(CardRarity.None) => Dual("无", "None"),
                nameof(CardRarity.Basic) => Dual("基础", "Basic"),
                nameof(CardRarity.Common) => Dual("普通", "Common"),
                nameof(CardRarity.Uncommon) => Dual("非凡", "Uncommon"),
                nameof(CardRarity.Rare) => Dual("稀有", "Rare"),
                nameof(CardRarity.Ancient) => Dual("远古", "Ancient"),
                nameof(CardRarity.Event) => Dual("事件", "Event"),
                nameof(CardRarity.Token) => Dual("衍生", "Token"),
                nameof(CardRarity.Status) => Dual("状态", "Status"),
                nameof(CardRarity.Curse) => Dual("诅咒", "Curse"),
                nameof(CardRarity.Quest) => Dual("任务", "Quest"),
                nameof(RelicRarity.Starter) => Dual("起始", "Starter"),
                nameof(RelicRarity.Shop) => Dual("商店", "Shop"),
                _ => value
            },
            "take_damage_sfx_type" => value switch
            {
                nameof(DamageSfxType.None) => Dual("无", "None"),
                nameof(DamageSfxType.Armor) => Dual("护甲", "Armor"),
                nameof(DamageSfxType.ArmorBig) => Dual("厚重护甲", "Armor Big"),
                nameof(DamageSfxType.Fur) => Dual("毛皮", "Fur"),
                nameof(DamageSfxType.Insect) => Dual("昆虫", "Insect"),
                nameof(DamageSfxType.Magic) => Dual("魔法", "Magic"),
                nameof(DamageSfxType.Plant) => Dual("植物", "Plant"),
                nameof(DamageSfxType.Slime) => Dual("史莱姆", "Slime"),
                nameof(DamageSfxType.Stone) => Dual("石头", "Stone"),
                _ => value
            },
            "usage" => value switch
            {
                nameof(PotionUsage.None) => Dual("无", "None"),
                nameof(PotionUsage.CombatOnly) => Dual("仅战斗", "Combat Only"),
                nameof(PotionUsage.AnyTime) => Dual("任意时机", "Any Time"),
                nameof(PotionUsage.Automatic) => Dual("自动触发", "Automatic"),
                _ => value
            },
            "layout_type" => value switch
            {
                nameof(EventLayoutType.Default) => Dual("默认", "Default"),
                nameof(EventLayoutType.Combat) => Dual("战斗", "Combat"),
                nameof(EventLayoutType.Ancient) => Dual("远古", "Ancient"),
                nameof(EventLayoutType.Custom) => Dual("自定义", "Custom"),
                _ => value
            },
            "phase_kind" => value switch
            {
                nameof(MonsterPhaseKind.Sequential) => Dual("顺序", "Sequential"),
                nameof(MonsterPhaseKind.RandomBranch) => Dual("随机分支", "Random Branch"),
                nameof(MonsterPhaseKind.ConditionalBranch) => Dual("条件分支", "Conditional Branch"),
                _ => value
            },
            "repeat_type" => value switch
            {
                "CanRepeatForever" => Dual("可无限重复", "Can Repeat Forever"),
                "CanRepeatXTimes" => Dual("可重复固定次数", "Can Repeat X Times"),
                "CannotRepeat" => Dual("不能连续重复", "Cannot Repeat"),
                "UseOnlyOnce" => Dual("仅一次", "Use Only Once"),
                _ => value
            },
            "intent_type" => value switch
            {
                nameof(MonsterIntentType.Unknown) => Dual("未知", "Unknown"),
                nameof(MonsterIntentType.SingleAttack) => Dual("单段攻击", "Single Attack"),
                nameof(MonsterIntentType.MultiAttack) => Dual("多段攻击", "Multi Attack"),
                nameof(MonsterIntentType.Buff) => Dual("增益", "Buff"),
                nameof(MonsterIntentType.Debuff) => Dual("减益", "Debuff"),
                nameof(MonsterIntentType.Defend) => Dual("防御", "Defend"),
                nameof(MonsterIntentType.Summon) => Dual("召唤", "Summon"),
                nameof(MonsterIntentType.Status) => Dual("状态牌", "Status"),
                nameof(MonsterIntentType.Heal) => Dual("治疗", "Heal"),
                nameof(MonsterIntentType.CardDebuff) => Dual("卡牌减益", "Card Debuff"),
                _ => value
            },
            "hook_type" => value switch
            {
                nameof(MonsterLifecycleHookType.AfterAddedToRoom) => Dual("进入房间后", "After Added To Room"),
                nameof(MonsterLifecycleHookType.BeforeRemovedFromRoom) => Dual("离开房间前", "Before Removed From Room"),
                nameof(MonsterLifecycleHookType.OnDieToDoom) => Dual("死于末日时", "On Die To Doom"),
                nameof(MonsterLifecycleHookType.BeforeDeath) => Dual("死亡前", "Before Death"),
                nameof(MonsterLifecycleHookType.AfterCurrentHpChanged) => Dual("生命变化后", "After Current HP Changed"),
                _ => value
            },
            "event_kind" => value switch
            {
                nameof(MonsterEventTriggerKind.AllyDied) => Dual("友军死亡", "Ally Died"),
                nameof(MonsterEventTriggerKind.HpChanged) => Dual("生命变化", "HP Changed"),
                _ => value
            },
            "state_variable_type" => value switch
            {
                nameof(MonsterStateVariableType.Integer) => Dual("整数", "Integer"),
                nameof(MonsterStateVariableType.Boolean) => Dual("布尔", "Boolean"),
                nameof(MonsterStateVariableType.Float) => Dual("浮点", "Float"),
                nameof(MonsterStateVariableType.String) => Dual("字符串", "String"),
                _ => value
            },
            "pool_id" => FormatPoolValue(value),
            "behavior_source" => value switch
            {
                "Native" => Dual("原版", "Native"),
                "Graph" => "Graph",
                _ => value
            },
            "room_type" or "reward_room_type" => value switch
            {
                nameof(RoomType.Unassigned) => Dual("未分配", "Unassigned"),
                nameof(RoomType.Monster) => Dual("怪物", "Monster"),
                nameof(RoomType.Elite) => Dual("精英", "Elite"),
                nameof(RoomType.Boss) => Dual("Boss", "Boss"),
                nameof(RoomType.Treasure) => Dual("宝箱", "Treasure"),
                nameof(RoomType.Shop) => Dual("商店", "Shop"),
                nameof(RoomType.Event) => Dual("事件", "Event"),
                nameof(RoomType.RestSite) => Dual("休息点", "Rest Site"),
                nameof(RoomType.Map) => Dual("地图", "Map"),
                _ => value
            },
            "map_kind" => value switch
            {
                "golden_path" => Dual("黄金路线", "Golden Path"),
                _ => value
            },
            "selection_mode" => value switch
            {
                "simple_grid" => Dual("网格选择", "Simple Grid"),
                "simple_grid_rewards" => Dual("奖励网格选择", "Rewards Grid"),
                "hand" => Dual("手牌选择", "Hand"),
                "hand_for_discard" => Dual("手牌弃牌选择", "Hand For Discard"),
                "hand_for_upgrade" => Dual("手牌升级选择", "Hand For Upgrade"),
                "choose_a_card_screen" => Dual("三选一卡", "Choose A Card"),
                "choose_bundle" => Dual("卡包选择", "Choose Bundle"),
                "deck_for_upgrade" => Dual("牌库升级选择", "Deck For Upgrade"),
                "deck_for_enchantment" => Dual("牌库附魔选择", "Deck For Enchantment"),
                "deck_for_transformation" => Dual("牌库变形选择", "Deck For Transformation"),
                "deck_for_removal" => Dual("牌库移除选择", "Deck For Removal"),
                _ => value
            },
            "prompt_kind" => value switch
            {
                "generic" => Dual("通用", "Generic"),
                "discard" => Dual("弃牌", "Discard"),
                "exhaust" => Dual("消耗", "Exhaust"),
                "transform" => Dual("变形", "Transform"),
                "upgrade" => Dual("升级", "Upgrade"),
                "remove" => Dual("移除", "Remove"),
                "enchant" => Dual("附魔", "Enchant"),
                _ => value
            },
            _ => FormatGraphPropertyValue(key, value)
        };
    }

    public static string FormatGraphPropertyValue(string? key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return (key ?? string.Empty).ToLowerInvariant() switch
        {
            "left" or "right" or "condition" or "value" => FormatGraphExpression(value),
            "key" or "result_key" or "condition_key" => FormatStateKey(value),
            "status" => FormatEnchantmentStatus(value),
            "target" => value switch
            {
                "self" => Dual("自身", "Self"),
                "current_target" => Dual("当前目标", "Current Target"),
                "other_enemies" => Dual("其他敌人", "Other Enemies"),
                "all_enemies" => Dual("所有敌人", "All Enemies"),
                "all_allies" => Dual("所有友方", "All Allies"),
                "all_targets" => Dual("所有目标", "All Targets"),
                _ => value
            },
            "target_pile" => value switch
            {
                "None" => Dual("无", "None"),
                "Draw" => Dual("抽牌堆", "Draw Pile"),
                "Hand" => Dual("手牌", "Hand"),
                "Discard" => Dual("弃牌堆", "Discard Pile"),
                "Exhaust" => Dual("消耗堆", "Exhaust Pile"),
                "Play" => Dual("打出堆", "Play Pile"),
                "Deck" => Dual("牌库", "Deck"),
                _ => value
            },
            "source_pile" => FormatGraphPropertyValue("target_pile", value),
            "position" => value switch
            {
                "Top" => Dual("顶部", "Top"),
                "Bottom" => Dual("底部", "Bottom"),
                "Random" => Dual("随机", "Random"),
                _ => value
            },
            "card_preview_style" => value switch
            {
                "None" => Dual("无", "None"),
                "HorizontalLayout" => Dual("横向", "Horizontal"),
                "MessyLayout" => Dual("杂乱", "Messy"),
                "EventLayout" => Dual("事件", "Event"),
                "GridLayout" => Dual("网格", "Grid"),
                _ => value
            },
            "auto_play_type" => value switch
            {
                "None" => Dual("无", "None"),
                "Default" => Dual("默认", "Default"),
                "SlyDiscard" => Dual("灵巧弃牌", "Sly Discard"),
                _ => value
            },
            "gold_loss_type" => value switch
            {
                "None" => Dual("无", "None"),
                "Spent" => Dual("花费", "Spent"),
                "Lost" => Dual("丢失", "Lost"),
                "Stolen" => Dual("偷走", "Stolen"),
                _ => value
            },
            "props" => value switch
            {
                "none" => Dual("无", "None"),
                "Unblockable" => Dual("不可格挡", "Unblockable"),
                "Unpowered" => Dual("不受强化影响", "Unpowered"),
                "Move" => Dual("位移", "Move"),
                "SkipHurtAnim" => Dual("跳过受击动画", "Skip Hurt Anim"),
                _ => value
            },
            "dynamic_source_kind" => value switch
            {
                "Literal" => Dual("固定值", "Literal"),
                "DynamicVar" => Dual("动态变量", "Dynamic Var"),
                "FormulaRef" => Dual("原版公式", "Formula Ref"),
                _ => value
            },
            "base_override_mode" or "extra_override_mode" => value switch
            {
                "None" => Dual("不覆盖", "None"),
                "Absolute" => Dual("绝对值", "Absolute"),
                "Delta" => Dual("增量", "Delta"),
                _ => value
            },
            "preview_multiplier_key" => value switch
            {
                "hand_count" or "cards" => Dual("手牌数", "Hand Count"),
                "stars" => Dual("星数", "Stars"),
                "energy" => Dual("能量", "Energy"),
                "current_block" => Dual("当前格挡", "Current Block"),
                "draw_pile" => Dual("抽牌堆数量", "Draw Pile Count"),
                "discard_pile" => Dual("弃牌堆数量", "Discard Pile Count"),
                "exhaust_pile" => Dual("消耗堆数量", "Exhaust Pile Count"),
                "missing_hp" => Dual("已损生命", "Missing HP"),
                _ => value
            },
            "target_type" or "type" or "rarity" or "usage" or "layout_type" or "pool_id" or "behavior_source" or "room_type" or "reward_room_type" or "map_kind" or "selection_mode" or "prompt_kind" or "take_damage_sfx_type" => FormatPropertyValue(key, value),
            "card_type_scope" => value switch
            {
                "any" => Dual("任意类型", "Any Type"),
                "attack" => Dual("攻击牌", "Attack"),
                "skill" => Dual("技能牌", "Skill"),
                "power" => Dual("能力牌", "Power"),
                "status" => Dual("状态牌", "Status"),
                "curse" => Dual("诅咒牌", "Curse"),
                "attack_skill" => Dual("攻击 / 技能", "Attack / Skill"),
                "attack_power" => Dual("攻击 / 能力", "Attack / Power"),
                "skill_power" => Dual("技能 / 能力", "Skill / Power"),
                "attack_skill_power" => Dual("攻击 / 技能 / 能力", "Attack / Skill / Power"),
                "non_status" => Dual("非状态战斗牌", "Non-status Combat Cards"),
                _ => value
            },
            "reward_kind" => value switch
            {
                "gold" => Dual("金币", "Gold"),
                "energy" => Dual("能量", "Energy"),
                "stars" => Dual("星数", "Stars"),
                "draw" => Dual("抽牌", "Draw"),
                "block" => Dual("格挡", "Block"),
                "heal" => Dual("治疗", "Heal"),
                "damage" => Dual("伤害", "Damage"),
                "power" => Dual("能力", "Power"),
                _ => value
            },
            "operator" => value switch
            {
                "eq" => Dual("等于", "="),
                "ne" => Dual("不等于", "!="),
                "lt" => Dual("小于", "<"),
                "lte" => Dual("小于等于", "<="),
                "gt" => Dual("大于", ">"),
                "gte" => Dual("大于等于", ">="),
                _ => value
            },
            _ => FormatValue(value)
        };
    }

    internal static string FormatGraphExpression(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        if (trimmed.StartsWith("$", StringComparison.Ordinal))
        {
            return FormatReference(trimmed[1..]);
        }

        if (bool.TryParse(trimmed, out var booleanValue))
        {
            return FormatValue(booleanValue.ToString());
        }

        return trimmed;
    }

    internal static string FormatStateKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        if (trimmed.StartsWith("$state.", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed["$state.".Length..];
        }
        else if (trimmed.StartsWith("state.", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed["state.".Length..];
        }

        return trimmed switch
        {
            "hook_result" => $"{Dual("状态", "State")}.hook_result [{Dual("Hook 返回值", "Hook Return")}]",
            "Status" => $"{Dual("状态", "State")}.Status [{Dual("原版状态字段", "Native Status Field")}]",
            _ => $"{Dual("状态", "State")}.{trimmed}"
        };
    }

    private static string FormatReference(string reference)
    {
        if (reference.StartsWith("state.", StringComparison.OrdinalIgnoreCase))
        {
            return $"{Dual("状态", "State")}.{reference["state.".Length..]}";
        }

        return reference.ToLowerInvariant() switch
        {
            "trigger" => Dual("触发器", "Trigger"),
            "source_model" => Dual("源模型", "Source Model"),
            "card" => Dual("卡牌", "Card"),
            "card_play" => Dual("出牌动作", "Card Play"),
            "potion" => Dual("药水", "Potion"),
            "relic" => Dual("遗物", "Relic"),
            "event" => Dual("事件", "Event"),
            "enchantment" => Dual("附魔", "Enchantment"),
            "owner" or "owner_player" => Dual("拥有者", "Owner"),
            "owner_creature" or "source_creature" or "self" => Dual("拥有者生物", "Owner Creature"),
            "target" or "current_target" => Dual("当前目标", "Current Target"),
            "combat_state" => Dual("战斗状态", "Combat State"),
            "run_state" => Dual("运行状态", "Run State"),
            "choice_context" => Dual("选择上下文", "Choice Context"),
            _ => reference
        };
    }

    private static string FormatEnchantmentStatus(string value)
    {
        return value switch
        {
            nameof(EnchantmentStatus.Disabled) => Dual("未启用", "Disabled"),
            nameof(EnchantmentStatus.Normal) => Dual("正常", "Normal"),
            "Enabled" => Dual("已启用", "Enabled"),
            _ => value
        };
    }

    private static string FormatPoolValue(string value)
    {
        var cardPool = ModelDb.AllCardPools.FirstOrDefault(pool => string.Equals(pool.Id.Entry, value, StringComparison.OrdinalIgnoreCase));
        if (cardPool != null)
        {
            var title = cardPool.Title;
            return string.IsNullOrWhiteSpace(title) || string.Equals(title, value, StringComparison.OrdinalIgnoreCase)
                ? value
                : $"{title} [{value}]";
        }

        var relicPool = ModelDb.AllRelicPools.FirstOrDefault(pool => string.Equals(pool.Id.Entry, value, StringComparison.OrdinalIgnoreCase));
        if (relicPool != null)
        {
            return $"{value} [{Dual("遗物池", "Relic Pool")}]";
        }

        var potionPool = ModelDb.AllPotionPools.FirstOrDefault(pool => string.Equals(pool.Id.Entry, value, StringComparison.OrdinalIgnoreCase));
        if (potionPool != null)
        {
            return $"{value} [{Dual("药水池", "Potion Pool")}]";
        }

        return value;
    }

    private static string Dual(string zh, string en)
    {
        return ModStudioLocalization.IsChinese ? zh : en;
    }
}
