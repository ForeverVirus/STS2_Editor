namespace STS2_Editor.Scripts.Editor.Graph;

public sealed class GraphDescriptionTemplateGenerator
{
    public string BuildTemplate(BehaviorGraphDefinition graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var orderedNodes = GraphDescriptionSupport.BuildPrimaryActionSequence(graph);
        var fragments = new List<string>();
        for (var index = 0; index < orderedNodes.Count; index++)
        {
            var node = orderedNodes[index];
            if (string.Equals(node.NodeType, "combat.repeat", StringComparison.Ordinal) && index + 1 < orderedNodes.Count)
            {
                var repeated = BuildRepeatedTemplate(node, orderedNodes[index + 1]);
                if (!string.IsNullOrWhiteSpace(repeated))
                {
                    fragments.Add(repeated);
                    index++;
                    continue;
                }
            }

            var fragment = BuildNodeTemplate(node);
            if (!string.IsNullOrWhiteSpace(fragment))
            {
                fragments.Add(fragment);
            }
        }

        return string.Join(Environment.NewLine, fragments);
    }

    private string BuildNodeTemplate(BehaviorGraphNodeDefinition node)
    {
        return node.NodeType switch
        {
            "flow.entry" or "flow.exit" or "flow.sequence" or "flow.branch" => string.Empty,
            "value.set" or "value.add" or "value.multiply" or "value.compare" => string.Empty,
            "combat.damage" => BuildAmountTemplate(node, "amount", "造成", "点伤害", "Deal", "damage"),
            "combat.gain_block" => BuildAmountTemplate(node, "amount", "获得", "点格挡", "Gain", "block"),
            "combat.heal" => BuildAmountTemplate(node, "amount", "恢复", "点生命", "Heal", "HP"),
            "combat.draw_cards" => BuildSimpleTemplate(node, "amount", "抽", "张牌", "Draw", "cards"),
            "combat.apply_power" => BuildApplyPowerTemplate(node),
            "combat.lose_block" => BuildAmountTemplate(node, "amount", "失去", "点格挡", "Lose", "block"),
            "combat.discard_cards" => BuildSimpleTemplate(node, "amount", "弃掉", "张牌", "Discard", "cards"),
            "combat.exhaust_cards" => BuildSimpleTemplate(node, "amount", "消耗", "张牌", "Exhaust", "cards"),
            "combat.create_card" => GraphDescriptionSupport.Dual("创建卡牌。", "Create card(s)."),
            "cardpile.move_cards" => BuildMoveCardsTemplate(node),
            "combat.remove_card" => GraphDescriptionSupport.Dual("移除卡牌。", "Remove card(s)."),
            "combat.transform_card" => GraphDescriptionSupport.Dual("转化卡牌。", "Transform card(s)."),
            "card.select_cards" => GraphDescriptionSupport.Dual("选择卡牌。", "Select card(s)."),
            "card.discard_and_draw" => GraphDescriptionSupport.Dual("弃牌后抽牌。", "Discard and then draw cards."),
            "card.apply_keyword" => GraphDescriptionSupport.Dual("为卡牌施加关键字。", "Apply a keyword to card(s)."),
            "card.remove_keyword" => GraphDescriptionSupport.Dual("移除卡牌关键字。", "Remove a keyword from card(s)."),
            "card.set_cost_delta" => GraphDescriptionSupport.Dual("调整卡牌费用。", "Adjust card cost."),
            "card.set_cost_absolute" => GraphDescriptionSupport.Dual("设置卡牌费用。", "Set card cost."),
            "card.set_cost_this_combat" => GraphDescriptionSupport.Dual("在本场战斗中设置卡牌费用。", "Set card cost for this combat."),
            "card.add_cost_until_played" => GraphDescriptionSupport.Dual("在打出前增加卡牌费用。", "Increase card cost until played."),
            "card.upgrade" => GraphDescriptionSupport.Dual("升级卡牌。", "Upgrade card(s)."),
            "card.downgrade" => GraphDescriptionSupport.Dual("降级卡牌。", "Downgrade card(s)."),
            "card.enchant" => GraphDescriptionSupport.Dual("附魔卡牌。", "Enchant card(s)."),
            "card.autoplay" => GraphDescriptionSupport.Dual("自动打出卡牌。", "Auto-play card(s)."),
            "card.apply_single_turn_sly" => GraphDescriptionSupport.Dual("施加单回合狡诈。", "Apply single-turn Sly."),
            "cardpile.auto_play_from_draw_pile" => GraphDescriptionSupport.Dual("从抽牌堆自动打出卡牌。", "Auto-play from draw pile."),
            "player.lose_hp" => BuildAmountTemplate(node, "amount", "失去", "点生命", "Lose", "HP"),
            "player.gain_max_hp" => BuildAmountTemplate(node, "amount", "获得", "点最大生命", "Gain", "max HP"),
            "player.lose_max_hp" => BuildAmountTemplate(node, "amount", "失去", "点最大生命", "Lose", "max HP"),
            "player.gain_energy" => BuildSimpleTemplate(node, "amount", "获得", "点能量", "Gain", "energy"),
            "player.lose_energy" => BuildSimpleTemplate(node, "amount", "失去", "点能量", "Lose", "energy"),
            "player.gain_gold" => BuildSimpleTemplate(node, "amount", "获得", "金币", "Gain", "gold"),
            "player.lose_gold" => BuildSimpleTemplate(node, "amount", "失去", "金币", "Lose", "gold"),
            "player.gain_stars" => BuildSimpleTemplate(node, "amount", "获得", "点星数", "Gain", "stars"),
            "player.gain_max_potion_count" => BuildSimpleTemplate(node, "amount", "获得", "点药水上限", "Gain", "max potion count"),
            "player.add_pet" => GraphDescriptionSupport.Dual("召唤宠物。", "Add a pet."),
            "player.forge" => BuildSimpleTemplate(node, "amount", "锻造", string.Empty, "Forge", string.Empty),
            "player.complete_quest" => GraphDescriptionSupport.Dual("完成任务。", "Complete the quest."),
            "player.rest_heal" => GraphDescriptionSupport.Dual("执行休息治疗。", "Perform rest heal."),
            "player.end_turn" => GraphDescriptionSupport.Dual("结束回合。", "End the turn."),
            "creature.set_current_hp" => BuildSimpleTemplate(node, "amount", "将当前生命设为", string.Empty, "Set current HP to", string.Empty),
            "creature.kill" => GraphDescriptionSupport.Dual("击杀目标。", "Kill the target."),
            "creature.stun" => GraphDescriptionSupport.Dual("击晕目标。", "Stun the target."),
            "orb.channel" => BuildChannelOrbTemplate(node),
            "orb.passive" => GraphDescriptionSupport.Dual("触发充能球被动。", "Trigger an orb passive."),
            "orb.add_slots" => BuildSimpleTemplate(node, "amount", "获得", "个充能球槽位", "Gain", "orb slots"),
            "orb.remove_slots" => BuildSimpleTemplate(node, "amount", "失去", "个充能球槽位", "Lose", "orb slots"),
            "orb.evoke_next" => GraphDescriptionSupport.Dual("激发下一个充能球。", "Evoke the next orb."),
            "potion.procure" => GraphDescriptionSupport.Dual("获得药水。", "Procure a potion."),
            "potion.discard" => GraphDescriptionSupport.Dual("丢弃药水。", "Discard a potion."),
            "relic.obtain" => GraphDescriptionSupport.Dual("获得遗物。", "Obtain a relic."),
            "relic.remove" => GraphDescriptionSupport.Dual("移除遗物。", "Remove a relic."),
            "relic.replace" => GraphDescriptionSupport.Dual("替换遗物。", "Replace a relic."),
            "relic.melt" => GraphDescriptionSupport.Dual("熔化遗物。", "Melt a relic."),
            "combat.repeat" => BuildStandaloneRepeatTemplate(node),
            "power.remove" => GraphDescriptionSupport.Dual("移除能力。", "Remove a power."),
            "power.modify_amount" => GraphDescriptionSupport.Dual("调整能力层数。", "Modify power amount."),
            "cardpile.shuffle" => GraphDescriptionSupport.Dual("洗牌。", "Shuffle the card pile."),
            "modifier.damage_additive" => GraphDescriptionSupport.Dual("提供伤害加成。", "Provide additive damage modifier."),
            "modifier.damage_multiplicative" => GraphDescriptionSupport.Dual("提供伤害倍率。", "Provide multiplicative damage modifier."),
            "modifier.block_additive" => GraphDescriptionSupport.Dual("提供格挡加成。", "Provide additive block modifier."),
            "modifier.block_multiplicative" => GraphDescriptionSupport.Dual("提供格挡倍率。", "Provide multiplicative block modifier."),
            "modifier.play_count" => GraphDescriptionSupport.Dual("修改打出次数。", "Modify play count."),
            "modifier.hand_draw" => GraphDescriptionSupport.Dual("修改抽牌数量。", "Modify hand draw."),
            "modifier.x_value" => GraphDescriptionSupport.Dual("修改 X 数值。", "Modify X value."),
            "modifier.max_energy" => GraphDescriptionSupport.Dual("修改最大能量。", "Modify max energy."),
            "enchantment.set_status" => GraphDescriptionSupport.Dual("设置附魔状态。", "Set enchantment status."),
            "event.page" => GraphDescriptionSupport.Dual("事件页面。", "Event page."),
            "event.option" => GraphDescriptionSupport.Dual("事件选项。", "Event option."),
            "event.goto_page" => GraphDescriptionSupport.Dual("跳转事件页面。", "Go to event page."),
            "event.proceed" => GraphDescriptionSupport.Dual("离开当前事件。", "Proceed out of the current event."),
            "event.start_combat" => GraphDescriptionSupport.Dual("开始战斗。", "Start combat."),
            "event.reward" => GraphDescriptionSupport.Dual("发放事件奖励。", "Grant event reward."),
            "reward.offer_custom" => GraphDescriptionSupport.Dual("提供自定义奖励。", "Offer a custom reward."),
            "reward.card_options_upgrade" => GraphDescriptionSupport.Dual("升级卡牌奖励选项。", "Upgrade card reward options."),
            "reward.card_options_enchant" => GraphDescriptionSupport.Dual("附魔卡牌奖励选项。", "Enchant card reward options."),
            "reward.mark_card_rewards_rerollable" => GraphDescriptionSupport.Dual("将卡牌奖励标记为可重掷。", "Mark card rewards as rerollable."),
            "map.replace_generated" => GraphDescriptionSupport.Dual("替换生成的地图。", "Replace the generated map."),
            "map.remove_unknown_room_type" => GraphDescriptionSupport.Dual("从未知地图点移除一种房间类型。", "Remove a room type from unknown map points."),
            "debug.log" => GraphDescriptionSupport.Dual("记录调试信息。", "Log a debug message."),
            _ => string.Empty
        };
    }

    private string BuildRepeatedTemplate(BehaviorGraphNodeDefinition repeatNode, BehaviorGraphNodeDefinition repeatedNode)
    {
        var repeatText = GraphDescriptionSupport.GetCountTemplate(repeatNode, "count", "1");
        return repeatedNode.NodeType switch
        {
            "combat.damage" => BuildRepeatedDamageTemplate(repeatedNode, repeatText),
            _ => GraphDescriptionSupport.Dual(
                $"重复执行 {repeatText} 次：{GraphDescriptionSupport.TrimSentenceEnding(BuildNodeTemplate(repeatedNode))}。",
                $"Repeat {repeatText} times: {GraphDescriptionSupport.TrimSentenceEnding(BuildNodeTemplate(repeatedNode))}.")
        };
    }

    private string BuildRepeatedDamageTemplate(BehaviorGraphNodeDefinition damageNode, string repeatText)
    {
        var amountText = GraphDescriptionSupport.GetCountTemplate(damageNode, "amount", "0");
        var target = GraphDescriptionSupport.GetProperty(damageNode, "target", "current_target");
        return target switch
        {
            "all_enemies" => GraphDescriptionSupport.Dual(
                $"对所有敌人造成{amountText}点伤害，重复{repeatText}次。",
                $"Deal {amountText} damage to all enemies {repeatText} times."),
            "other_enemies" => GraphDescriptionSupport.Dual(
                $"对所有其他敌人造成{amountText}点伤害，重复{repeatText}次。",
                $"Deal {amountText} damage to all other enemies {repeatText} times."),
            "self" => GraphDescriptionSupport.Dual(
                $"对自身造成{amountText}点伤害，重复{repeatText}次。",
                $"Deal {amountText} damage to self {repeatText} times."),
            _ => GraphDescriptionSupport.Dual(
                $"造成{amountText}点伤害，重复{repeatText}次。",
                $"Deal {amountText} damage {repeatText} times.")
        };
    }

    private string BuildAmountTemplate(BehaviorGraphNodeDefinition node, string propertyKey, string zhVerb, string zhUnit, string enVerb, string enUnit)
    {
        var amountText = GraphDescriptionSupport.GetCountTemplate(node, propertyKey, "0");
        var target = GraphDescriptionSupport.GetProperty(node, "target", "current_target");

        if (string.Equals(node.NodeType, "combat.damage", StringComparison.Ordinal))
        {
            return target switch
            {
                "all_enemies" => GraphDescriptionSupport.Dual($"对所有敌人造成{amountText}点伤害。", $"Deal {amountText} damage to all enemies."),
                "other_enemies" => GraphDescriptionSupport.Dual($"对所有其他敌人造成{amountText}点伤害。", $"Deal {amountText} damage to all other enemies."),
                "self" => GraphDescriptionSupport.Dual($"对自身造成{amountText}点伤害。", $"Deal {amountText} damage to self."),
                _ => GraphDescriptionSupport.Dual($"造成{amountText}点伤害。", $"Deal {amountText} damage.")
            };
        }

        if (string.Equals(target, "self", StringComparison.Ordinal))
        {
            return GraphDescriptionSupport.Dual($"{zhVerb}{amountText}{zhUnit}。", $"{enVerb} {amountText} {enUnit}.");
        }

        return GraphDescriptionSupport.Dual(
            $"{zhVerb}{amountText}{zhUnit}，目标为{GraphDescriptionSupport.DescribeTarget(target)}。",
            $"{enVerb} {amountText} {enUnit} to {GraphDescriptionSupport.DescribeTarget(target)}.");
    }

    private string BuildSimpleTemplate(BehaviorGraphNodeDefinition node, string propertyKey, string zhPrefix, string zhSuffix, string enPrefix, string enSuffix)
    {
        var valueText = GraphDescriptionSupport.GetCountTemplate(node, propertyKey, "0");
        if (string.IsNullOrWhiteSpace(zhSuffix))
        {
            return GraphDescriptionSupport.Dual(
                $"{zhPrefix}{valueText}。",
                $"{enPrefix} {valueText}{(string.IsNullOrWhiteSpace(enSuffix) ? "." : $" {enSuffix}.")}");
        }

        return GraphDescriptionSupport.Dual(
            $"{zhPrefix}{valueText}{zhSuffix}。",
            $"{enPrefix} {valueText} {enSuffix}.");
    }

    private string BuildApplyPowerTemplate(BehaviorGraphNodeDefinition node)
    {
        if (GraphDescriptionSupport.TryBuildPowerTemplate(node, out var template))
        {
            return template;
        }

        var amountText = GraphDescriptionSupport.GetCountTemplate(node, "amount", "1");
        var powerId = GraphDescriptionSupport.GetProperty(node, "power_id", "power");
        var target = GraphDescriptionSupport.DescribeTarget(GraphDescriptionSupport.GetProperty(node, "target", "current_target"));
        return GraphDescriptionSupport.Dual(
            $"给予{target}{powerId} {amountText}层。",
            $"Apply {powerId} x{amountText} to {target}.");
    }

    private string BuildStandaloneRepeatTemplate(BehaviorGraphNodeDefinition node)
    {
        var countText = GraphDescriptionSupport.GetCountTemplate(node, "count", "1");
        return GraphDescriptionSupport.Dual($"重复执行{countText}次。", $"Repeat {countText} times.");
    }

    private string BuildChannelOrbTemplate(BehaviorGraphNodeDefinition node)
    {
        return GraphDescriptionSupport.BuildChannelOrbDescription(
            GraphDescriptionSupport.GetProperty(node, "orb_id", string.Empty));
    }

    private string BuildMoveCardsTemplate(BehaviorGraphNodeDefinition node)
    {
        var sourcePile = GraphDescriptionSupport.GetProperty(node, "source_pile", "Discard");
        var targetPile = GraphDescriptionSupport.GetProperty(node, "target_pile", "Hand");
        var typeScope = GraphDescriptionSupport.GetProperty(node, "card_type_scope", "any");
        var exactCost = GraphDescriptionSupport.GetProperty(node, "exact_energy_cost", "-1");
        var countText = GraphDescriptionSupport.GetProperty(node, "count", "0");
        var countDisplay = countText == "0" ? GraphDescriptionSupport.Dual("所有", "all") : countText;
        var costDisplay = exactCost == "-1"
            ? GraphDescriptionSupport.Dual("不限费用", "any cost")
            : GraphDescriptionSupport.Dual($"{exactCost}费", $"{exactCost}-cost");

        return GraphDescriptionSupport.Dual(
            $"将{sourcePile}中符合条件的{typeScope}牌移动到{targetPile}，数量为{countDisplay}（{costDisplay}）。",
            $"Move {countDisplay} matching {typeScope} card(s) from {sourcePile} to {targetPile} ({costDisplay}).");
    }
}
