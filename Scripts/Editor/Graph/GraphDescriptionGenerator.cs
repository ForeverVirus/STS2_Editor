using MegaCrit.Sts2.Core.Models;

namespace STS2_Editor.Scripts.Editor.Graph;

public sealed class GraphDescriptionGenerationResult
{
    public string Description { get; set; } = string.Empty;

    public string TemplateDescription { get; set; } = string.Empty;

    public string PreviewDescription { get; set; } = string.Empty;

    public List<string> UsedNodeTypes { get; } = new();

    public List<string> UnsupportedNodeTypes { get; } = new();

    public bool IsComplete => UnsupportedNodeTypes.Count == 0;
}

public sealed class GraphDescriptionGenerator
{
    private readonly GraphDescriptionTemplateGenerator _templateGenerator = new();
    private readonly DynamicPreviewService _previewService = new();

    private static readonly IReadOnlyList<string> SupportedNodeTypes =
    [
        "flow.sequence",
        "flow.branch",
        "combat.damage",
        "combat.gain_block",
        "combat.heal",
        "combat.draw_cards",
        "combat.apply_power",
        "combat.lose_block",
        "combat.discard_cards",
        "combat.exhaust_cards",
        "combat.create_card",
        "cardpile.move_cards",
        "combat.remove_card",
        "combat.transform_card",
        "combat.repeat",
        "card.upgrade",
        "card.downgrade",
        "card.enchant",
        "card.autoplay",
        "card.apply_single_turn_sly",
        "cardpile.auto_play_from_draw_pile",
        "player.lose_hp",
        "player.gain_max_hp",
        "player.lose_energy",
        "player.lose_gold",
        "player.gain_max_potion_count",
        "player.gain_energy",
        "player.gain_gold",
        "player.gain_stars",
        "player.forge",
        "player.lose_max_hp",
        "creature.set_current_hp",
        "creature.kill",
        "creature.stun",
        "orb.channel",
        "orb.passive",
        "power.remove",
        "power.modify_amount",
        "cardpile.shuffle",
        "debug.log",
        "event.page",
        "event.option",
        "event.goto_page",
        "event.proceed",
        "event.start_combat",
        "event.reward",
        "reward.offer_custom"
    ];

    public IReadOnlyList<string> SupportedNodeKinds => SupportedNodeTypes;

    public GraphDescriptionGenerationResult Generate(BehaviorGraphDefinition graph, AbstractModel? sourceModel = null, DynamicPreviewContext? previewContext = null)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var result = new GraphDescriptionGenerationResult
        {
            TemplateDescription = _templateGenerator.BuildTemplate(graph)
        };

        var orderedNodes = GraphDescriptionSupport.BuildPrimaryActionSequence(graph);
        var fragments = new List<string>();
        for (var index = 0; index < orderedNodes.Count; index++)
        {
            var node = orderedNodes[index];
            if (string.Equals(node.NodeType, "combat.repeat", StringComparison.Ordinal) && index + 1 < orderedNodes.Count)
            {
                var repeated = DescribeCombinedRepeat(node, orderedNodes[index + 1], result, sourceModel, previewContext);
                if (!string.IsNullOrWhiteSpace(repeated))
                {
                    fragments.Add(repeated);
                    index++;
                    continue;
                }
            }

            var fragment = DescribeNode(node, result, sourceModel, previewContext);
            if (!string.IsNullOrWhiteSpace(fragment))
            {
                fragments.Add(fragment);
            }
        }

        if (fragments.Count == 0)
        {
            result.PreviewDescription = string.IsNullOrWhiteSpace(graph.Description) ? graph.Name : graph.Description;
            result.Description = result.PreviewDescription;
            return result;
        }

        result.PreviewDescription = string.Join(Environment.NewLine, fragments);
        result.Description = result.PreviewDescription;
        return result;
    }

    public string ResolveDescription(BehaviorGraphDefinition graph, string? manualDescription = null, AbstractModel? sourceModel = null, DynamicPreviewContext? previewContext = null)
    {
        if (!string.IsNullOrWhiteSpace(manualDescription))
        {
            return manualDescription.Trim();
        }

        return Generate(graph, sourceModel, previewContext).PreviewDescription;
    }

    public bool CanGenerateAutomatically(BehaviorGraphDefinition graph, AbstractModel? sourceModel = null, DynamicPreviewContext? previewContext = null)
    {
        return Generate(graph, sourceModel, previewContext).IsComplete;
    }

    private string DescribeCombinedRepeat(
        BehaviorGraphNodeDefinition repeatNode,
        BehaviorGraphNodeDefinition repeatedNode,
        GraphDescriptionGenerationResult result,
        AbstractModel? sourceModel,
        DynamicPreviewContext? previewContext)
    {
        result.UsedNodeTypes.Add("combat.repeat");
        result.UsedNodeTypes.Add(Normalize(repeatedNode.NodeType));

        var repeatPreview = _previewService.Evaluate(repeatNode, "count", sourceModel, previewContext, 1m).PreviewText;
        return repeatedNode.NodeType switch
        {
            "combat.damage" => DescribeRepeatedDamage(repeatedNode, repeatPreview, sourceModel, previewContext),
            "combat.draw_cards" => GraphDescriptionSupport.Dual(
                $"重复执行 {repeatPreview} 次：{GraphDescriptionSupport.TrimSentenceEnding(DescribeNode(repeatedNode, result, sourceModel, previewContext))}。",
                $"Repeat {repeatPreview} times: {GraphDescriptionSupport.TrimSentenceEnding(DescribeNode(repeatedNode, result, sourceModel, previewContext))}."),
            _ => GraphDescriptionSupport.Dual(
                $"重复执行 {repeatPreview} 次：{GraphDescriptionSupport.TrimSentenceEnding(DescribeNode(repeatedNode, result, sourceModel, previewContext))}。",
                $"Repeat {repeatPreview} times: {GraphDescriptionSupport.TrimSentenceEnding(DescribeNode(repeatedNode, result, sourceModel, previewContext))}.")
        };
    }

    private string DescribeNode(BehaviorGraphNodeDefinition node, GraphDescriptionGenerationResult result, AbstractModel? sourceModel, DynamicPreviewContext? previewContext)
    {
        var kind = Normalize(node.NodeType);
        if (string.IsNullOrWhiteSpace(kind) || kind is "flow.entry" or "flow.exit" or "flow.sequence")
        {
            return string.Empty;
        }

        result.UsedNodeTypes.Add(kind);
        return kind switch
        {
            "flow.branch" => Dual("根据条件进入不同分支。", "Branch based on a condition."),
            "combat.damage" => DescribeDamage(node, sourceModel, previewContext),
            "combat.gain_block" => DescribeAmountNode(node, "amount", "获得", "点格挡", "Gain", "block", sourceModel, previewContext),
            "combat.heal" => DescribeAmountNode(node, "amount", "恢复", "点生命", "Heal", "HP", sourceModel, previewContext),
            "combat.draw_cards" => DescribeSimpleAmount(node, "amount", "抽", "张牌", "Draw", "cards", sourceModel, previewContext),
            "combat.apply_power" => DescribeApplyPower(node, sourceModel, previewContext),
            "combat.lose_block" => DescribeAmountNode(node, "amount", "失去", "点格挡", "Lose", "block", sourceModel, previewContext),
            "combat.discard_cards" => DescribeSimpleAmount(node, "amount", "弃掉", "张牌", "Discard", "cards", sourceModel, previewContext),
            "combat.exhaust_cards" => DescribeSimpleAmount(node, "amount", "消耗", "张牌", "Exhaust", "cards", sourceModel, previewContext),
            "combat.create_card" => DescribeCreateCard(node, sourceModel, previewContext),
            "cardpile.move_cards" => DescribeMoveCards(node, sourceModel, previewContext),
            "combat.remove_card" => Dual("移除选中的卡牌。", "Remove the selected cards."),
            "combat.transform_card" => DescribeTransformCard(node),
            "combat.repeat" => DescribeStandaloneRepeat(node, sourceModel, previewContext),
            "card.upgrade" => Dual("升级选中的卡牌。", "Upgrade the selected card(s)."),
            "card.downgrade" => Dual("降级选中的卡牌。", "Downgrade the selected card(s)."),
            "card.enchant" => DescribeEnchant(node, sourceModel, previewContext),
            "card.autoplay" => DescribeAutoPlay(node),
            "card.apply_single_turn_sly" => Dual("为选中的卡牌施加单回合狡诈。", "Apply single-turn Sly to the selected card(s)."),
            "cardpile.auto_play_from_draw_pile" => DescribeAutoPlayFromDrawPile(node, sourceModel, previewContext),
            "player.lose_hp" => DescribeAmountNode(node, "amount", "失去", "点生命", "Lose", "HP", sourceModel, previewContext),
            "player.gain_max_hp" => DescribeAmountNode(node, "amount", "获得", "点最大生命", "Gain", "max HP", sourceModel, previewContext),
            "player.lose_energy" => DescribeSimpleAmount(node, "amount", "失去", "点能量", "Lose", "energy", sourceModel, previewContext),
            "player.lose_gold" => DescribeSimpleAmount(node, "amount", "失去", "金币", "Lose", "gold", sourceModel, previewContext),
            "player.gain_max_potion_count" => DescribeSimpleAmount(node, "amount", "获得", "点药水上限", "Gain", "max potion count", sourceModel, previewContext),
            "player.gain_energy" => DescribeSimpleAmount(node, "amount", "获得", "点能量", "Gain", "energy", sourceModel, previewContext),
            "player.gain_gold" => DescribeSimpleAmount(node, "amount", "获得", "金币", "Gain", "gold", sourceModel, previewContext),
            "player.gain_stars" => DescribeSimpleAmount(node, "amount", "获得", "点星数", "Gain", "stars", sourceModel, previewContext),
            "player.forge" => DescribeSimpleAmount(node, "amount", "铸造", string.Empty, "Forge", string.Empty, sourceModel, previewContext),
            "player.lose_max_hp" => DescribeAmountNode(node, "amount", "失去", "点最大生命", "Lose", "max HP", sourceModel, previewContext),
            "creature.set_current_hp" => DescribeSimpleAmount(node, "amount", "将当前生命设为", string.Empty, "Set current HP to", string.Empty, sourceModel, previewContext),
            "creature.kill" => Dual("击杀目标。", "Kill the target."),
            "creature.stun" => Dual("击晕目标。", "Stun the target."),
            "orb.passive" => Dual("触发一个充能球的被动效果。", "Trigger an orb passive."),
            "orb.channel" => DescribeChannelOrb(node),
            "power.remove" => DescribePowerRemove(node),
            "power.modify_amount" => DescribePowerModifyAmount(node, sourceModel, previewContext),
            "cardpile.shuffle" => Dual("洗牌。", "Shuffle the card pile."),
            "event.page" => DescribeEventPage(node),
            "event.option" => DescribeEventOption(node),
            "event.goto_page" => DescribeEventGoto(node),
            "event.proceed" => Dual("结束当前事件。", "Proceed out of the current event."),
            "event.start_combat" => DescribeEventStartCombat(node),
            "event.reward" => DescribeEventReward(node, sourceModel, previewContext),
            "reward.offer_custom" => DescribeOfferCustomReward(node, sourceModel, previewContext),
            "debug.log" => DescribeLog(node),
            _ => MarkUnsupported(node, result)
        };
    }

    private string DescribeDamage(BehaviorGraphNodeDefinition node, AbstractModel? sourceModel, DynamicPreviewContext? previewContext)
    {
        var preview = _previewService.Evaluate(node, "amount", sourceModel, previewContext, 0m);
        var target = GraphDescriptionSupport.GetProperty(node, "target", "current_target");
        return target switch
        {
            "all_enemies" => Dual($"对所有敌人造成{preview.PreviewText}点伤害。", $"Deal {preview.PreviewText} damage to all enemies."),
            "other_enemies" => Dual($"对所有其他敌人造成{preview.PreviewText}点伤害。", $"Deal {preview.PreviewText} damage to all other enemies."),
            "self" => Dual($"对自身造成{preview.PreviewText}点伤害。", $"Deal {preview.PreviewText} damage to self."),
            _ => Dual($"造成{preview.PreviewText}点伤害。", $"Deal {preview.PreviewText} damage.")
        };
    }

    private string DescribeRepeatedDamage(BehaviorGraphNodeDefinition damageNode, string repeatPreview, AbstractModel? sourceModel, DynamicPreviewContext? previewContext)
    {
        var damagePreview = _previewService.Evaluate(damageNode, "amount", sourceModel, previewContext, 0m).PreviewText;
        var target = GraphDescriptionSupport.GetProperty(damageNode, "target", "current_target");
        return target switch
        {
            "all_enemies" => Dual($"对所有敌人造成{damagePreview}点伤害{repeatPreview}次。", $"Deal {damagePreview} damage to all enemies {repeatPreview} times."),
            "other_enemies" => Dual($"对所有其他敌人造成{damagePreview}点伤害{repeatPreview}次。", $"Deal {damagePreview} damage to all other enemies {repeatPreview} times."),
            "self" => Dual($"对自身造成{damagePreview}点伤害{repeatPreview}次。", $"Deal {damagePreview} damage to self {repeatPreview} times."),
            _ => Dual($"造成{damagePreview}点伤害{repeatPreview}次。", $"Deal {damagePreview} damage {repeatPreview} times.")
        };
    }

    private string DescribeChannelOrb(BehaviorGraphNodeDefinition node)
    {
        return GraphDescriptionSupport.BuildChannelOrbDescription(
            GraphDescriptionSupport.GetProperty(node, "orb_id", string.Empty));
    }

    private string DescribeAmountNode(
        BehaviorGraphNodeDefinition node,
        string propertyKey,
        string zhVerb,
        string zhUnit,
        string enVerb,
        string enUnit,
        AbstractModel? sourceModel,
        DynamicPreviewContext? previewContext)
    {
        var preview = _previewService.Evaluate(node, propertyKey, sourceModel, previewContext, 0m);
        var target = GraphDescriptionSupport.GetProperty(node, "target", "self");
        if (string.Equals(target, "self", StringComparison.Ordinal))
        {
            return Dual($"{zhVerb}{preview.PreviewText}{zhUnit}。", $"{enVerb} {preview.PreviewText} {enUnit}.");
        }

        return Dual(
            $"{zhVerb}{preview.PreviewText}{zhUnit}，目标为{GraphDescriptionSupport.DescribeTarget(target)}。",
            $"{enVerb} {preview.PreviewText} {enUnit} to {GraphDescriptionSupport.DescribeTarget(target)}.");
    }

    private string DescribeSimpleAmount(
        BehaviorGraphNodeDefinition node,
        string propertyKey,
        string zhPrefix,
        string zhSuffix,
        string enPrefix,
        string enSuffix,
        AbstractModel? sourceModel,
        DynamicPreviewContext? previewContext)
    {
        var preview = _previewService.Evaluate(node, propertyKey, sourceModel, previewContext, 0m);
        if (string.IsNullOrWhiteSpace(zhSuffix))
        {
            return Dual($"{zhPrefix}{preview.PreviewText}。", $"{enPrefix} {preview.PreviewText}{(string.IsNullOrWhiteSpace(enSuffix) ? "." : $" {enSuffix}.")}");
        }

        return Dual($"{zhPrefix}{preview.PreviewText}{zhSuffix}。", $"{enPrefix} {preview.PreviewText} {enSuffix}.");
    }

    private string DescribeApplyPower(BehaviorGraphNodeDefinition node, AbstractModel? sourceModel, DynamicPreviewContext? previewContext)
    {
        var preview = _previewService.Evaluate(node, "amount", sourceModel, previewContext, 1m);
        if (GraphDescriptionSupport.TryBuildPowerPreview(node, preview, out var powerText))
        {
            return powerText;
        }

        var powerId = GraphDescriptionSupport.GetProperty(node, "power_id", "power");
        var target = GraphDescriptionSupport.DescribeTarget(GraphDescriptionSupport.GetProperty(node, "target", "current_target"));
        return Dual($"给予{target}{powerId} {preview.PreviewText}层。", $"Apply {powerId} x{preview.PreviewText} to {target}.");
    }

    private string DescribeCreateCard(BehaviorGraphNodeDefinition node, AbstractModel? sourceModel, DynamicPreviewContext? previewContext)
    {
        var preview = _previewService.Evaluate(node, "count", sourceModel, previewContext, 1m);
        var cardId = GraphDescriptionSupport.GetProperty(node, "card_id", "card");
        var targetPile = GraphDescriptionSupport.GetProperty(node, "target_pile", "Hand");
        return Dual($"将{cardId} x{preview.PreviewText} 放入{targetPile}。", $"Create {cardId} x{preview.PreviewText} into {targetPile}.");
    }

    private string DescribeMoveCards(BehaviorGraphNodeDefinition node, AbstractModel? sourceModel, DynamicPreviewContext? previewContext)
    {
        var sourcePile = GraphDescriptionSupport.GetProperty(node, "source_pile", "Discard");
        var targetPile = GraphDescriptionSupport.GetProperty(node, "target_pile", "Hand");
        var typeScope = GraphDescriptionSupport.GetProperty(node, "card_type_scope", "any");
        var exactCost = GraphDescriptionSupport.GetProperty(node, "exact_energy_cost", "-1");
        var countPreview = _previewService.Evaluate(node, "count", sourceModel, previewContext, 0m).PreviewText;
        var countText = countPreview is "0" or "0.0" or "0.00"
            ? Dual("所有", "all")
            : countPreview;
        var costText = exactCost == "-1"
            ? Dual("不限费用", "any cost")
            : Dual($"{exactCost}费", $"{exactCost}-cost");
        return Dual(
            $"将{sourcePile}中符合条件的{typeScope}牌移动到{targetPile}，数量{countText}（{costText}）。",
            $"Move {countText} matching {typeScope} card(s) from {sourcePile} to {targetPile} ({costText}).");
    }

    private string DescribeTransformCard(BehaviorGraphNodeDefinition node)
    {
        var replacement = GraphDescriptionSupport.GetProperty(node, "replacement_card_id", "card");
        return Dual($"将选中的卡牌变化为{replacement}。", $"Transform the selected card(s) into {replacement}.");
    }

    private string DescribeStandaloneRepeat(BehaviorGraphNodeDefinition node, AbstractModel? sourceModel, DynamicPreviewContext? previewContext)
    {
        var preview = _previewService.Evaluate(node, "count", sourceModel, previewContext, 1m);
        return Dual($"重复执行{preview.PreviewText}次。", $"Repeat {preview.PreviewText} times.");
    }

    private string DescribeEnchant(BehaviorGraphNodeDefinition node, AbstractModel? sourceModel, DynamicPreviewContext? previewContext)
    {
        var preview = _previewService.Evaluate(node, "amount", sourceModel, previewContext, 1m);
        var enchantmentId = GraphDescriptionSupport.GetProperty(node, "enchantment_id", "enchantment");
        return Dual($"为卡牌施加{enchantmentId} x{preview.PreviewText}。", $"Enchant the selected card(s) with {enchantmentId} x{preview.PreviewText}.");
    }

    private string DescribeAutoPlay(BehaviorGraphNodeDefinition node)
    {
        var target = GraphDescriptionSupport.DescribeTarget(GraphDescriptionSupport.GetProperty(node, "target", "current_target"));
        return Dual($"自动打出选中的牌，目标为{target}。", $"Auto play the selected card(s) toward {target}.");
    }

    private string DescribeAutoPlayFromDrawPile(BehaviorGraphNodeDefinition node, AbstractModel? sourceModel, DynamicPreviewContext? previewContext)
    {
        var preview = _previewService.Evaluate(node, "count", sourceModel, previewContext, 1m);
        return Dual($"从抽牌堆自动打出{preview.PreviewText}张牌。", $"Auto play {preview.PreviewText} card(s) from the draw pile.");
    }

    private string DescribePowerRemove(BehaviorGraphNodeDefinition node)
    {
        var powerId = GraphDescriptionSupport.GetProperty(node, "power_id", "power");
        var target = GraphDescriptionSupport.DescribeTarget(GraphDescriptionSupport.GetProperty(node, "target", "current_target"));
        return Dual($"移除{target}身上的{powerId}。", $"Remove {powerId} from {target}.");
    }

    private string DescribePowerModifyAmount(BehaviorGraphNodeDefinition node, AbstractModel? sourceModel, DynamicPreviewContext? previewContext)
    {
        var powerId = GraphDescriptionSupport.GetProperty(node, "power_id", "power");
        var preview = _previewService.Evaluate(node, "amount", sourceModel, previewContext, 1m);
        var target = GraphDescriptionSupport.DescribeTarget(GraphDescriptionSupport.GetProperty(node, "target", "current_target"));
        return Dual($"{target}的{powerId}变化{preview.PreviewText}。", $"Modify {powerId} by {preview.PreviewText} on {target}.");
    }

    private string DescribeEventPage(BehaviorGraphNodeDefinition node)
    {
        var pageId = GraphDescriptionSupport.GetProperty(node, "page_id", node.NodeId);
        var title = GraphDescriptionSupport.GetProperty(node, "title", pageId);
        return Dual($"事件页面：{title} [{pageId}]。", $"Event page: {title} [{pageId}].");
    }

    private string DescribeEventOption(BehaviorGraphNodeDefinition node)
    {
        var optionId = GraphDescriptionSupport.GetProperty(node, "option_id", node.NodeId);
        var title = GraphDescriptionSupport.GetProperty(node, "title", optionId);
        return Dual($"事件选项：{title} [{optionId}]。", $"Event option: {title} [{optionId}].");
    }

    private string DescribeEventGoto(BehaviorGraphNodeDefinition node)
    {
        var pageId = GraphDescriptionSupport.GetProperty(node, "next_page_id", "INITIAL");
        return Dual($"跳转到页面 {pageId}。", $"Go to page {pageId}.");
    }

    private string DescribeEventStartCombat(BehaviorGraphNodeDefinition node)
    {
        var encounterId = GraphDescriptionSupport.GetProperty(node, "encounter_id", string.Empty);
        var resumePageId = GraphDescriptionSupport.GetProperty(node, "resume_page_id", string.Empty);
        return string.IsNullOrWhiteSpace(resumePageId)
            ? Dual($"开始战斗 {encounterId}。", $"Start combat {encounterId}.")
            : Dual($"开始战斗 {encounterId}，战斗后返回 {resumePageId}。", $"Start combat {encounterId} and resume at {resumePageId}.");
    }

    private string DescribeEventReward(BehaviorGraphNodeDefinition node, AbstractModel? sourceModel, DynamicPreviewContext? previewContext)
    {
        var rewardKind = GraphDescriptionSupport.GetProperty(node, "reward_kind", "gold");
        var preview = _previewService.Evaluate(node, "amount", sourceModel, previewContext, 1m);
        return rewardKind switch
        {
            "gold" => Dual($"奖励{preview.PreviewText}金币。", $"Reward {preview.PreviewText} gold."),
            "energy" => Dual($"奖励{preview.PreviewText}点能量。", $"Reward {preview.PreviewText} energy."),
            "stars" => Dual($"奖励{preview.PreviewText}点星数。", $"Reward {preview.PreviewText} stars."),
            "draw" => Dual($"奖励抽{preview.PreviewText}张牌。", $"Reward draw {preview.PreviewText} cards."),
            "block" => Dual($"奖励{preview.PreviewText}点格挡。", $"Reward {preview.PreviewText} block."),
            "heal" => Dual($"奖励{preview.PreviewText}点治疗。", $"Reward {preview.PreviewText} healing."),
            "damage" => Dual($"奖励{preview.PreviewText}点伤害。", $"Reward {preview.PreviewText} damage."),
            "power" => Dual($"奖励能力 {GraphDescriptionSupport.GetProperty(node, "power_id", "power")} x{preview.PreviewText}。", $"Reward power {GraphDescriptionSupport.GetProperty(node, "power_id", "power")} x{preview.PreviewText}."),
            _ => Dual("发放事件奖励。", "Grant an event reward.")
        };
    }

    private string DescribeOfferCustomReward(BehaviorGraphNodeDefinition node, AbstractModel? sourceModel, DynamicPreviewContext? previewContext)
    {
        var rewardKind = GraphDescriptionSupport.GetProperty(node, "reward_kind", "custom");
        var preview = _previewService.Evaluate(node, "amount", sourceModel, previewContext, 0m);
        return rewardKind switch
        {
            "gold" => Dual($"提供{preview.PreviewText}金币奖励。", $"Offer a {preview.PreviewText} gold reward."),
            "relic" => Dual($"提供遗物奖励 {GraphDescriptionSupport.GetProperty(node, "relic_id", "relic")}。", $"Offer relic reward {GraphDescriptionSupport.GetProperty(node, "relic_id", "relic")}."),
            "potion" => Dual($"提供药水奖励 {GraphDescriptionSupport.GetProperty(node, "potion_id", "potion")}。", $"Offer potion reward {GraphDescriptionSupport.GetProperty(node, "potion_id", "potion")}."),
            "special_card" => Dual($"提供特殊卡牌奖励 {GraphDescriptionSupport.GetProperty(node, "card_id", "card")}。", $"Offer special card reward {GraphDescriptionSupport.GetProperty(node, "card_id", "card")}."),
            "card_removal" => Dual("提供移除卡牌奖励。", "Offer card removal reward."),
            _ => Dual("提供自定义奖励。", "Offer a custom reward.")
        };
    }

    private string DescribeLog(BehaviorGraphNodeDefinition node)
    {
        var message = GraphDescriptionSupport.GetProperty(node, "message");
        return string.IsNullOrWhiteSpace(message) ? string.Empty : Dual($"记录日志：{message}。", $"Log: {message}.");
    }

    private string MarkUnsupported(BehaviorGraphNodeDefinition node, GraphDescriptionGenerationResult result)
    {
        result.UnsupportedNodeTypes.Add(node.NodeType);
        return string.Empty;
    }

    private static string Normalize(string? nodeType)
    {
        return string.IsNullOrWhiteSpace(nodeType) ? string.Empty : nodeType.Trim();
    }

    private static string Dual(string zh, string en)
    {
        return GraphDescriptionSupport.Dual(zh, en);
    }
}
