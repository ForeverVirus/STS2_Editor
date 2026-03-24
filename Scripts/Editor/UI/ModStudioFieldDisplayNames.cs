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
            "portrait_path" => Dual("立绘路径", "Portrait Path"),
            "icon_path" => Dual("图标路径", "Icon Path"),
            "image_path" => Dual("图片路径", "Image Path"),
            "amount" => Dual("数值", "Amount"),
            "count" => Dual("数量上限", "Count Limit"),
            "target" => Dual("目标", "Target"),
            "source_pile" => Dual("来源牌堆", "Source Pile"),
            "target_pile" => Dual("目标牌堆", "Target Pile"),
            "exact_energy_cost" => Dual("精确费用", "Exact Cost"),
            "include_x_cost" => Dual("包含 X 费", "Include X Cost"),
            "card_type_scope" => Dual("卡牌类型筛选", "Card Type Scope"),
            "position" => Dual("抽取位置", "Draw Position"),
            "card_id" => Dual("卡牌 ID", "Card Id"),
            "replacement_card_id" => Dual("替换卡牌 ID", "Replacement Card Id"),
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

    public static string FormatGraphPropertyValue(string? key, string? value)
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

    private static string Dual(string zh, string en)
    {
        return ModStudioLocalization.IsChinese ? zh : en;
    }
}
