using STS2_Editor.Scripts.Editor.Core.Utilities;

namespace STS2_Editor.Scripts.Editor.UI;

internal static class ModStudioLocalizationCatalog
{
    public static string T(string key)
    {
        return key switch
        {
            "graph.value_source" => Dual("值来源", "Value Source"),
            "graph.preview_context" => Dual("预览上下文", "Preview Context"),
            "graph.preview_result" => Dual("预览结果", "Preview Result"),
            "graph.preview_template" => Dual("动态模板", "Template"),
            "graph.preview_text" => Dual("预览文本", "Preview Text"),
            "graph.preview_context.upgraded" => Dual("按升级后预览", "Preview As Upgraded"),
            "graph.preview_context.target" => Dual("预览目标", "Preview Target"),
            "graph.preview_context.current_block" => Dual("当前格挡", "Current Block"),
            "graph.preview_context.current_stars" => Dual("当前星数", "Current Stars"),
            "graph.preview_context.current_energy" => Dual("当前能量", "Current Energy"),
            "graph.preview_context.hand_count" => Dual("当前手牌数", "Hand Count"),
            "graph.preview_context.draw_pile_count" => Dual("抽牌堆数量", "Draw Pile Count"),
            "graph.preview_context.discard_pile_count" => Dual("弃牌堆数量", "Discard Pile Count"),
            "graph.preview_context.exhaust_pile_count" => Dual("消耗堆数量", "Exhaust Pile Count"),
            "graph.preview_context.missing_hp" => Dual("已损生命", "Missing HP"),
            "graph.base_override_mode" => Dual("基础值覆盖方式", "Base Override Mode"),
            "graph.base_override_value" => Dual("基础值覆盖值", "Base Override"),
            "graph.extra_override_mode" => Dual("额外值覆盖方式", "Extra Override Mode"),
            "graph.extra_override_value" => Dual("额外值覆盖值", "Extra Override"),
            "graph.dynamic_var_name" => Dual("动态变量", "Dynamic Variable"),
            "graph.formula_ref" => Dual("原版公式引用", "Formula Reference"),
            "graph.preview_multiplier" => Dual("预览乘数", "Preview Multiplier"),
            "graph.override.none" => Dual("不覆盖", "None"),
            "graph.override.absolute" => Dual("绝对值", "Absolute"),
            "graph.override.delta" => Dual("增量", "Delta"),
            "graph.source.literal" => Dual("固定值", "Literal"),
            "graph.source.dynamic_var" => Dual("原版动态变量", "Dynamic Var"),
            "graph.source.formula" => Dual("原版公式", "Formula Ref"),
            "event.page" => Dual("事件页面", "Event Page"),
            "event.option" => Dual("事件选项", "Event Option"),
            "event.goto_page" => Dual("跳转页面", "Go To Page"),
            "event.proceed" => Dual("结束事件", "Proceed"),
            "event.start_combat" => Dual("开始战斗", "Start Combat"),
            "event.reward" => Dual("事件奖励", "Event Reward"),
            "tab.asset_runtime" => Dual("游戏内素材", "Game Assets"),
            "tab.asset_project" => Dual("导入的外部素材", "Imported Assets"),
            "label.node_properties" => Dual("节点属性", "Node Properties"),
            "placeholder.graph_node_properties_empty" => Dual("当前节点没有额外属性。", "The current node has no extra properties."),
            _ => key
        };
    }

    private static string Dual(string zh, string en)
    {
        return ModStudioLocalization.IsChinese ? zh : en;
    }
}
