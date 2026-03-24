using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2_Editor.Scripts.Editor.Core.Models;
using STS2_Editor.Scripts.Editor.Graph;

namespace STS2_Editor.Scripts.Editor.Runtime;

internal static class RuntimeGraphPreviewService
{
    public static void ApplyCardPreview(CardModel card, CardPreviewMode previewMode, Creature? target, DynamicVarSet dynamicVars)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(dynamicVars);

        if (!ReferenceEquals(dynamicVars, card.DynamicVars) ||
            !RuntimeGraphOverrides.TryGetExecutableGraph(ModStudioEntityKind.Card, card.Id.Entry, out _, out var graph) ||
            graph == null)
        {
            return;
        }

        var previewContext = BuildCardPreviewContext(card, target, upgradedPreview: previewMode == CardPreviewMode.Upgrade);
        ApplyGraphPreview(graph, card, previewContext, dynamicVars);
    }

    public static DynamicPreviewContext BuildCardPreviewContext(CardModel card, Creature? target, bool upgradedPreview)
    {
        var combat = card.Owner?.PlayerCombatState;
        var handCount = ResolveHandCount(card, combat);
        return new DynamicPreviewContext
        {
            EntityKind = ModStudioEntityKind.Card,
            EntityId = card.Id.Entry,
            Upgraded = upgradedPreview || card.IsUpgraded,
            TargetSelector = target == null ? "current_target" : "current_target",
            CurrentBlock = card.Owner?.Creature?.Block ?? 0,
            CurrentStars = combat?.Stars ?? 0,
            CurrentEnergy = combat?.Energy ?? card.EnergyCost.GetWithModifiers(CostModifiers.All),
            HandCount = handCount,
            DrawPileCount = combat?.DrawPile?.Cards.Count ?? 0,
            DiscardPileCount = combat?.DiscardPile?.Cards.Count ?? 0,
            ExhaustPileCount = combat?.ExhaustPile?.Cards.Count ?? 0,
            MissingHp = Math.Max((card.Owner?.Creature?.MaxHp ?? 0) - (card.Owner?.Creature?.CurrentHp ?? 0), 0),
            FormulaMultipliers = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            {
                ["hand_count"] = handCount,
                ["cards"] = handCount,
                ["stars"] = combat?.Stars ?? 0,
                ["energy"] = combat?.Energy ?? 0,
                ["current_block"] = card.Owner?.Creature?.Block ?? 0,
                ["draw_pile"] = combat?.DrawPile?.Cards.Count ?? 0,
                ["discard_pile"] = combat?.DiscardPile?.Cards.Count ?? 0,
                ["exhaust_pile"] = combat?.ExhaustPile?.Cards.Count ?? 0,
                ["missing_hp"] = Math.Max((card.Owner?.Creature?.MaxHp ?? 0) - (card.Owner?.Creature?.CurrentHp ?? 0), 0)
            }
        };
    }

    private static int ResolveHandCount(CardModel card, PlayerCombatState? combat)
    {
        var handCount = combat?.Hand?.Cards.Count ?? 0;
        if (card.Pile?.Type == PileType.Hand && combat?.Hand?.Cards.Contains(card) == true)
        {
            handCount = Math.Max(0, handCount - 1);
        }

        return handCount;
    }

    private static void ApplyGraphPreview(
        BehaviorGraphDefinition graph,
        CardModel card,
        DynamicPreviewContext previewContext,
        DynamicVarSet dynamicVars)
    {
        foreach (var node in GraphDescriptionSupport.BuildPrimaryActionSequence(graph))
        {
            switch (node.NodeType)
            {
                case "combat.damage":
                    ApplyAmountPreview(node, "amount", card, previewContext, dynamicVars, "Damage", "CalculatedDamage");
                    break;
                case "combat.gain_block":
                    ApplyAmountPreview(node, "amount", card, previewContext, dynamicVars, "Block", "CalculatedBlock");
                    break;
                case "combat.heal":
                    ApplyAmountPreview(node, "amount", card, previewContext, dynamicVars, "Heal");
                    break;
                case "combat.draw_cards":
                    ApplyAmountPreview(node, "count", card, previewContext, dynamicVars, "Cards");
                    break;
                case "combat.repeat":
                    ApplyAmountPreview(node, "count", card, previewContext, dynamicVars, "Repeat");
                    break;
                case "player.gain_energy":
                case "player.lose_energy":
                    ApplyAmountPreview(node, "amount", card, previewContext, dynamicVars, "Energy");
                    break;
                case "player.gain_gold":
                case "player.lose_gold":
                    ApplyAmountPreview(node, "amount", card, previewContext, dynamicVars, "Gold");
                    break;
                case "player.gain_stars":
                    ApplyAmountPreview(node, "amount", card, previewContext, dynamicVars, "Stars");
                    break;
                case "player.lose_hp":
                    ApplyAmountPreview(node, "amount", card, previewContext, dynamicVars, "HpLoss");
                    break;
                case "player.gain_max_hp":
                case "player.lose_max_hp":
                    ApplyAmountPreview(node, "amount", card, previewContext, dynamicVars, "MaxHp");
                    break;
                case "combat.apply_power":
                    ApplyPowerPreview(node, card, previewContext, dynamicVars);
                    break;
            }
        }
    }

    private static void ApplyPowerPreview(
        BehaviorGraphNodeDefinition node,
        CardModel card,
        DynamicPreviewContext previewContext,
        DynamicVarSet dynamicVars)
    {
        ApplyAmountPreview(node, "amount", card, previewContext, dynamicVars, "Power");

        if (!node.Properties.TryGetValue("power_id", out var powerId) || string.IsNullOrWhiteSpace(powerId))
        {
            return;
        }

        var mappedPowerKey = TryMapPowerDynamicVarKey(powerId);
        if (!string.IsNullOrWhiteSpace(mappedPowerKey))
        {
            ApplyAmountPreview(node, "amount", card, previewContext, dynamicVars, mappedPowerKey);
        }
    }

    private static void ApplyAmountPreview(
        BehaviorGraphNodeDefinition node,
        string propertyKey,
        CardModel card,
        DynamicPreviewContext previewContext,
        DynamicVarSet dynamicVars,
        params string[] dynamicVarKeys)
    {
        var preview = DynamicValueEvaluator.EvaluatePreview(node, propertyKey, card, previewContext, 0m);
        foreach (var dynamicVarKey in dynamicVarKeys)
        {
            if (dynamicVars.TryGetValue(dynamicVarKey, out var dynamicVar))
            {
                dynamicVar.PreviewValue = preview.Value;
            }
        }

        if (string.Equals(node.NodeType, "combat.damage", StringComparison.Ordinal) &&
            node.DynamicValues.TryGetValue(propertyKey, out var definition) &&
            definition.SourceKind == DynamicValueSourceKind.FormulaRef)
        {
            if (dynamicVars.TryGetValue("CalculationBase", out var baseVar))
            {
                baseVar.PreviewValue = preview.BaseValue;
            }

            if (dynamicVars.TryGetValue("CalculationExtra", out var extraVar))
            {
                extraVar.PreviewValue = preview.ExtraValue;
            }
        }
    }

    private static string? TryMapPowerDynamicVarKey(string powerId)
    {
        var trimmed = powerId.Trim();
        if (trimmed.EndsWith("_POWER", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^"_POWER".Length];
        }

        var segments = trimmed
            .Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(segment => char.ToUpperInvariant(segment[0]) + segment[1..].ToLowerInvariant());
        var baseName = string.Concat(segments);
        return string.IsNullOrWhiteSpace(baseName) ? null : $"{baseName}Power";
    }
}
