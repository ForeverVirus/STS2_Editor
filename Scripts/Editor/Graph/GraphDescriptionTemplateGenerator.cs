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
            "combat.damage" => BuildAmountTemplate(node, "amount", "造成", "点伤害", "Deal", "damage"),
            "combat.gain_block" => BuildAmountTemplate(node, "amount", "获得", "点格挡", "Gain", "block"),
            "combat.heal" => BuildAmountTemplate(node, "amount", "恢复", "点生命", "Heal", "HP"),
            "combat.draw_cards" => BuildSimpleTemplate(node, "amount", "抽", "张牌", "Draw", "cards"),
            "combat.apply_power" => BuildApplyPowerTemplate(node),
            "cardpile.move_cards" => BuildMoveCardsTemplate(node),
            "player.gain_energy" => BuildSimpleTemplate(node, "amount", "获得", "点能量", "Gain", "energy"),
            "player.gain_gold" => BuildSimpleTemplate(node, "amount", "金币", string.Empty, "Gain", "gold"),
            "player.gain_stars" => BuildSimpleTemplate(node, "amount", "获得", "点星数", "Gain", "stars"),
            "player.forge" => BuildSimpleTemplate(node, "amount", "铸造", string.Empty, "Forge", string.Empty),
            "creature.set_current_hp" => BuildSimpleTemplate(node, "amount", "将当前生命设为", string.Empty, "Set current HP to", string.Empty),
            "orb.channel" => BuildChannelOrbTemplate(node),
            "combat.repeat" => BuildStandaloneRepeatTemplate(node),
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
                $"对所有敌人造成{amountText}点伤害{repeatText}次。",
                $"Deal {amountText} damage to all enemies {repeatText} times."),
            "other_enemies" => GraphDescriptionSupport.Dual(
                $"对所有其他敌人造成{amountText}点伤害{repeatText}次。",
                $"Deal {amountText} damage to all other enemies {repeatText} times."),
            "self" => GraphDescriptionSupport.Dual(
                $"对自身造成{amountText}点伤害{repeatText}次。",
                $"Deal {amountText} damage to self {repeatText} times."),
            _ => GraphDescriptionSupport.Dual(
                $"造成{amountText}点伤害{repeatText}次。",
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
                "all_enemies" => GraphDescriptionSupport.Dual(
                    $"对所有敌人造成{amountText}点伤害。",
                    $"Deal {amountText} damage to all enemies."),
                "other_enemies" => GraphDescriptionSupport.Dual(
                    $"对所有其他敌人造成{amountText}点伤害。",
                    $"Deal {amountText} damage to all other enemies."),
                "self" => GraphDescriptionSupport.Dual(
                    $"对自身造成{amountText}点伤害。",
                    $"Deal {amountText} damage to self."),
                _ => GraphDescriptionSupport.Dual(
                    $"造成{amountText}点伤害。",
                    $"Deal {amountText} damage.")
            };
        }

        if (string.Equals(target, "self", StringComparison.Ordinal))
        {
            return GraphDescriptionSupport.Dual(
                $"{zhVerb}{amountText}{zhUnit}。",
                $"{enVerb} {amountText} {enUnit}.");
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
            return GraphDescriptionSupport.Dual($"{zhPrefix}{valueText}。", $"{enPrefix} {valueText}{(string.IsNullOrWhiteSpace(enSuffix) ? "." : $" {enSuffix}.")}");
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
            $"将{sourcePile}中符合条件的{typeScope}牌移动到{targetPile}，数量{countDisplay}（{costDisplay}）。",
            $"Move {countDisplay} matching {typeScope} card(s) from {sourcePile} to {targetPile} ({costDisplay}).");
    }
}
