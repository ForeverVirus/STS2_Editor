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
            "starting_hp" => Dual("初始生命", "Starting HP"),
            "starting_gold" => Dual("初始金币", "Starting Gold"),
            "max_energy" => Dual("初始能量", "Max Energy"),
            "base_orb_slot_count" => Dual("基础球槽", "Base Orb Slots"),
            "starting_deck_ids" => Dual("初始卡组", "Starting Deck"),
            "starting_relic_ids" => Dual("初始遗物", "Starting Relics"),
            "starting_potion_ids" => Dual("初始药水", "Starting Potions"),
            "pool_id" => Dual("所属角色池 ID", "Pool Id"),
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
            "target" => Dual("目标", "Target"),
            "props" => Dual("属性标记", "Props"),
            "power_id" => Dual("能力 ID", "Power Id"),
            "condition" => Dual("条件", "Condition"),
            "condition_key" => Dual("条件键", "Condition Key"),
            "message" => Dual("消息", "Message"),
            "reward_kind" => Dual("奖励类型", "Reward Kind"),
            "add_card" => Dual("添加卡牌", "Add Card"),
            "remove_card" => Dual("移除卡牌", "Remove Card"),
            "transform_card" => Dual("变形卡牌", "Transform Card"),
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
                "all_enemies" => Dual("全体敌人", "All Enemies"),
                "all_allies" => Dual("全体友方", "All Allies"),
                "all_targets" => Dual("所有目标", "All Targets"),
                _ => value
            },
            "props" => value switch
            {
                "none" => Dual("无", "None"),
                "Unblockable" => Dual("不可格挡", "Unblockable"),
                "Unpowered" => Dual("不受力量影响", "Unpowered"),
                "Move" => Dual("位移", "Move"),
                "SkipHurtAnim" => Dual("跳过受击动画", "Skip Hurt Anim"),
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
