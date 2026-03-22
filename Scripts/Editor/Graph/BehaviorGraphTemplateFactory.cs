using STS2_Editor.Scripts.Editor.Core.Models;

namespace STS2_Editor.Scripts.Editor.Graph;

public static class BehaviorGraphTemplateFactory
{
    private static readonly IReadOnlyList<BehaviorGraphTemplateDescriptor> Templates =
    [
        new BehaviorGraphTemplateDescriptor
        {
            TemplateId = "card.basic_damage",
            EntityKind = ModStudioEntityKind.Card,
            DisplayName = "Card: Basic Damage",
            Description = "On play, deal damage to the current target.",
            TriggerId = "card.on_play",
            DefaultAmount = "6"
        },
        new BehaviorGraphTemplateDescriptor
        {
            TemplateId = "card.gain_block",
            EntityKind = ModStudioEntityKind.Card,
            DisplayName = "Card: Gain Block",
            Description = "On play, gain block for the owner.",
            TriggerId = "card.on_play",
            DefaultAmount = "5"
        },
        new BehaviorGraphTemplateDescriptor
        {
            TemplateId = "card.draw_cards",
            EntityKind = ModStudioEntityKind.Card,
            DisplayName = "Card: Draw Cards",
            Description = "On play, draw cards for the owner.",
            TriggerId = "card.on_play",
            DefaultAmount = "1"
        },
        new BehaviorGraphTemplateDescriptor
        {
            TemplateId = "potion.heal_self",
            EntityKind = ModStudioEntityKind.Potion,
            DisplayName = "Potion: Heal Self",
            Description = "On use, heal the owner.",
            TriggerId = "potion.on_use",
            DefaultAmount = "10"
        },
        new BehaviorGraphTemplateDescriptor
        {
            TemplateId = "potion.damage_target",
            EntityKind = ModStudioEntityKind.Potion,
            DisplayName = "Potion: Damage Target",
            Description = "On use, damage the selected target.",
            TriggerId = "potion.on_use",
            DefaultAmount = "20"
        },
        new BehaviorGraphTemplateDescriptor
        {
            TemplateId = "relic.after_card_played_gain_block",
            EntityKind = ModStudioEntityKind.Relic,
            DisplayName = "Relic: After Card Played Gain Block",
            Description = "After a card is played, gain block for the owner.",
            TriggerId = "relic.after_card_played",
            DefaultAmount = "1"
        },
        new BehaviorGraphTemplateDescriptor
        {
            TemplateId = "enchantment.on_play_damage",
            EntityKind = ModStudioEntityKind.Enchantment,
            DisplayName = "Enchantment: On Play Damage",
            Description = "When the enchanted card is played, deal damage to the current target.",
            TriggerId = "enchantment.on_play",
            DefaultAmount = "2"
        }
    ];

    public static IReadOnlyList<BehaviorGraphTemplateDescriptor> GetTemplates(ModStudioEntityKind entityKind)
    {
        return Templates
            .Where(template => template.EntityKind == entityKind)
            .OrderBy(template => template.DisplayName, StringComparer.Ordinal)
            .ToList();
    }

    public static bool TryCreateTemplate(string templateId, string graphId, ModStudioEntityKind entityKind, out BehaviorGraphDefinition? graph)
    {
        graph = null;
        var template = Templates.FirstOrDefault(item => string.Equals(item.TemplateId, templateId, StringComparison.Ordinal));
        if (template == null || template.EntityKind != entityKind)
        {
            return false;
        }

        graph = template.TemplateId switch
        {
            "card.basic_damage" => CreateLinearTemplate(
                graphId,
                entityKind,
                template.DisplayName,
                template.Description,
                template.TriggerId,
                new BehaviorGraphNodeDefinition
                {
                    NodeId = "damage",
                    NodeType = "combat.damage",
                    DisplayName = "Damage",
                    Properties = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["amount"] = template.DefaultAmount,
                        ["target"] = "current_target",
                        ["props"] = "none"
                    }
                }),
            "card.gain_block" => CreateLinearTemplate(
                graphId,
                entityKind,
                template.DisplayName,
                template.Description,
                template.TriggerId,
                new BehaviorGraphNodeDefinition
                {
                    NodeId = "gain_block",
                    NodeType = "combat.gain_block",
                    DisplayName = "Gain Block",
                    Properties = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["amount"] = template.DefaultAmount,
                        ["target"] = "self",
                        ["props"] = "none"
                    }
                }),
            "card.draw_cards" => CreateLinearTemplate(
                graphId,
                entityKind,
                template.DisplayName,
                template.Description,
                template.TriggerId,
                new BehaviorGraphNodeDefinition
                {
                    NodeId = "draw_cards",
                    NodeType = "combat.draw_cards",
                    DisplayName = "Draw Cards",
                    Properties = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["amount"] = template.DefaultAmount
                    }
                }),
            "potion.heal_self" => CreateLinearTemplate(
                graphId,
                entityKind,
                template.DisplayName,
                template.Description,
                template.TriggerId,
                new BehaviorGraphNodeDefinition
                {
                    NodeId = "heal",
                    NodeType = "combat.heal",
                    DisplayName = "Heal",
                    Properties = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["amount"] = template.DefaultAmount,
                        ["target"] = "self"
                    }
                }),
            "potion.damage_target" => CreateLinearTemplate(
                graphId,
                entityKind,
                template.DisplayName,
                template.Description,
                template.TriggerId,
                new BehaviorGraphNodeDefinition
                {
                    NodeId = "damage",
                    NodeType = "combat.damage",
                    DisplayName = "Damage",
                    Properties = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["amount"] = template.DefaultAmount,
                        ["target"] = "current_target",
                        ["props"] = "none"
                    }
                }),
            "relic.after_card_played_gain_block" => CreateLinearTemplate(
                graphId,
                entityKind,
                template.DisplayName,
                template.Description,
                template.TriggerId,
                new BehaviorGraphNodeDefinition
                {
                    NodeId = "gain_block",
                    NodeType = "combat.gain_block",
                    DisplayName = "Gain Block",
                    Properties = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["amount"] = template.DefaultAmount,
                        ["target"] = "self",
                        ["props"] = "none"
                    }
                }),
            "enchantment.on_play_damage" => CreateLinearTemplate(
                graphId,
                entityKind,
                template.DisplayName,
                template.Description,
                template.TriggerId,
                new BehaviorGraphNodeDefinition
                {
                    NodeId = "damage",
                    NodeType = "combat.damage",
                    DisplayName = "Damage",
                    Properties = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["amount"] = template.DefaultAmount,
                        ["target"] = "current_target",
                        ["props"] = "none"
                    }
                }),
            _ => CreateDefaultScaffold(graphId, entityKind, template.DisplayName, template.Description, template.TriggerId)
        };

        return true;
    }

    public static BehaviorGraphDefinition CreateDefaultScaffold(string graphId, ModStudioEntityKind entityKind, string? name = null, string? description = null, string? triggerId = null)
    {
        var entryNodeId = $"entry_{entityKind.ToString().ToLowerInvariant()}";
        var exitNodeId = $"exit_{entityKind.ToString().ToLowerInvariant()}";
        var graph = new BehaviorGraphDefinition
        {
            GraphId = graphId,
            Name = string.IsNullOrWhiteSpace(name) ? $"{entityKind} Override Graph" : name,
            Description = string.IsNullOrWhiteSpace(description)
                ? "Phase 1 default graph scaffold generated by Mod Studio."
                : description,
            EntityKind = entityKind,
            EntryNodeId = entryNodeId,
            Nodes =
            [
                new BehaviorGraphNodeDefinition { NodeId = entryNodeId, NodeType = "flow.entry", DisplayName = "Entry" },
                new BehaviorGraphNodeDefinition { NodeId = exitNodeId, NodeType = "flow.exit", DisplayName = "Exit" }
            ],
            Connections =
            [
                new BehaviorGraphConnectionDefinition { FromNodeId = entryNodeId, FromPortId = "next", ToNodeId = exitNodeId, ToPortId = "in" }
            ],
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [$"trigger.{triggerId ?? GetDefaultTrigger(entityKind)}"] = entryNodeId,
                ["trigger.default"] = entryNodeId
            }
        };
        return graph;
    }

    private static BehaviorGraphDefinition CreateLinearTemplate(
        string graphId,
        ModStudioEntityKind entityKind,
        string name,
        string description,
        string triggerId,
        BehaviorGraphNodeDefinition actionNode)
    {
        var entryNodeId = $"entry_{entityKind.ToString().ToLowerInvariant()}";
        var exitNodeId = $"exit_{entityKind.ToString().ToLowerInvariant()}";
        return new BehaviorGraphDefinition
        {
            GraphId = graphId,
            Name = name,
            Description = description,
            EntityKind = entityKind,
            EntryNodeId = entryNodeId,
            Nodes =
            [
                new BehaviorGraphNodeDefinition { NodeId = entryNodeId, NodeType = "flow.entry", DisplayName = "Entry" },
                actionNode,
                new BehaviorGraphNodeDefinition { NodeId = exitNodeId, NodeType = "flow.exit", DisplayName = "Exit" }
            ],
            Connections =
            [
                new BehaviorGraphConnectionDefinition { FromNodeId = entryNodeId, FromPortId = "next", ToNodeId = actionNode.NodeId, ToPortId = "in" },
                new BehaviorGraphConnectionDefinition { FromNodeId = actionNode.NodeId, FromPortId = "out", ToNodeId = exitNodeId, ToPortId = "in" }
            ],
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [$"trigger.{triggerId}"] = entryNodeId,
                ["trigger.default"] = entryNodeId
            }
        };
    }

    private static string GetDefaultTrigger(ModStudioEntityKind entityKind)
    {
        return entityKind switch
        {
            ModStudioEntityKind.Card => "card.on_play",
            ModStudioEntityKind.Potion => "potion.on_use",
            ModStudioEntityKind.Relic => "relic.after_card_played",
            ModStudioEntityKind.Enchantment => "enchantment.on_play",
            _ => "default"
        };
    }
}
