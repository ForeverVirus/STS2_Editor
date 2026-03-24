using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using STS2_Editor.Scripts.Editor.Core.Models;

namespace STS2_Editor.Scripts.Editor.Graph;

public enum NativeBehaviorTranslationStatus
{
    Supported = 0,
    Partial = 1,
    Unsupported = 2
}

public sealed record NativeBehaviorTranslationCapability(
    string Key,
    string Title,
    NativeBehaviorTranslationStatus Status,
    string Description,
    string[] Notes);

public sealed class NativeBehaviorGraphSource
{
    public ModStudioEntityKind EntityKind { get; set; }

    public string GraphId { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = "Native Behavior Graph";

    public string Description { get; set; } = string.Empty;

    public string TriggerId { get; set; } = string.Empty;

    public List<NativeBehaviorStep> Steps { get; set; } = new();

    public Dictionary<string, string> Metadata { get; set; } = new(StringComparer.Ordinal);
}

public sealed class NativeBehaviorStep
{
    public string Kind { get; set; } = string.Empty;

    public Dictionary<string, string> Parameters { get; set; } = new(StringComparer.Ordinal);

    public List<NativeBehaviorStep> TrueBranch { get; set; } = new();

    public List<NativeBehaviorStep> FalseBranch { get; set; } = new();
}

public sealed class NativeBehaviorGraphTranslationResult
{
    public BehaviorGraphDefinition Graph { get; set; } = new();

    public bool IsPartial { get; set; }

    public List<string> UnsupportedStepKinds { get; } = new();

    public List<string> AppliedStepKinds { get; } = new();

    public List<string> Warnings { get; } = new();
}

public sealed class NativeBehaviorGraphTranslator
{
    private readonly GraphDescriptionGenerator _descriptionGenerator = new();

    private static readonly IReadOnlyList<NativeBehaviorTranslationCapability> Catalog = new[]
    {
        new NativeBehaviorTranslationCapability(
            "combat.damage",
            "Damage",
            NativeBehaviorTranslationStatus.Supported,
            "Deals damage to the selected target.",
            new[] { "amount", "target", "props" }),
        new NativeBehaviorTranslationCapability(
            "combat.gain_block",
            "Gain Block",
            NativeBehaviorTranslationStatus.Supported,
            "Grants block to the selected target.",
            new[] { "amount", "target", "props" }),
        new NativeBehaviorTranslationCapability(
            "combat.heal",
            "Heal",
            NativeBehaviorTranslationStatus.Supported,
            "Heals the selected target.",
            new[] { "amount", "target" }),
        new NativeBehaviorTranslationCapability(
            "combat.draw_cards",
            "Draw Cards",
            NativeBehaviorTranslationStatus.Supported,
            "Draws cards for the owner player.",
            new[] { "amount" }),
        new NativeBehaviorTranslationCapability(
            "player.gain_energy",
            "Gain Energy",
            NativeBehaviorTranslationStatus.Supported,
            "Adds energy to the owner player.",
            new[] { "amount" }),
        new NativeBehaviorTranslationCapability(
            "player.gain_gold",
            "Gain Gold",
            NativeBehaviorTranslationStatus.Supported,
            "Adds gold to the owner player.",
            new[] { "amount" }),
        new NativeBehaviorTranslationCapability(
            "player.gain_stars",
            "Gain Stars",
            NativeBehaviorTranslationStatus.Supported,
            "Adds stars to the owner player.",
            new[] { "amount" }),
        new NativeBehaviorTranslationCapability(
            "combat.apply_power",
            "Apply Power",
            NativeBehaviorTranslationStatus.Supported,
            "Applies a power model to the selected target.",
            new[] { "power_id", "amount", "target" }),
        new NativeBehaviorTranslationCapability(
            "combat.discard_cards",
            "Discard Cards",
            NativeBehaviorTranslationStatus.Partial,
            "Discards cards from hand or from an explicit card id list.",
            new[] { "amount", "card_ids", "target" }),
        new NativeBehaviorTranslationCapability(
            "combat.exhaust_cards",
            "Exhaust Cards",
            NativeBehaviorTranslationStatus.Partial,
            "Exhausts cards from hand or from an explicit card id list.",
            new[] { "amount", "card_ids", "target" }),
        new NativeBehaviorTranslationCapability(
            "combat.create_card",
            "Create Card",
            NativeBehaviorTranslationStatus.Partial,
            "Creates a card and adds it to the selected pile.",
            new[] { "card_id", "count", "target_pile" }),
        new NativeBehaviorTranslationCapability(
            "cardpile.move_cards",
            "Move Cards",
            NativeBehaviorTranslationStatus.Supported,
            "Moves existing cards from one pile to another using optional cost and type filters.",
            new[] { "source_pile", "target_pile", "count", "exact_energy_cost", "include_x_cost", "card_type_scope" }),
        new NativeBehaviorTranslationCapability(
            "combat.remove_card",
            "Remove Card",
            NativeBehaviorTranslationStatus.Partial,
            "Removes cards from the current pile or an explicit card id list.",
            new[] { "card_ids", "card_id", "target" }),
        new NativeBehaviorTranslationCapability(
            "combat.transform_card",
            "Transform Card",
            NativeBehaviorTranslationStatus.Partial,
            "Transforms a card into another card or a random result.",
            new[] { "card_id", "replacement_card_id", "random_replacement" }),
        new NativeBehaviorTranslationCapability(
            "card.select_cards",
            "Select Cards",
            NativeBehaviorTranslationStatus.Partial,
            "Selects cards from a pile and stores them for later graph steps.",
            new[] { "state_key", "selection_mode", "source_pile", "count", "prompt_kind" }),
        new NativeBehaviorTranslationCapability(
            "card.discard_and_draw",
            "Discard And Draw",
            NativeBehaviorTranslationStatus.Partial,
            "Discards cards and then draws a number of cards.",
            new[] { "card_state_key", "draw_count" }),
        new NativeBehaviorTranslationCapability(
            "card.apply_keyword",
            "Apply Card Keyword",
            NativeBehaviorTranslationStatus.Partial,
            "Adds a keyword to the selected cards.",
            new[] { "card_state_key", "keyword" }),
        new NativeBehaviorTranslationCapability(
            "card.upgrade",
            "Upgrade Card",
            NativeBehaviorTranslationStatus.Supported,
            "Upgrades the selected cards.",
            new[] { "card_state_key", "card_ids", "card_preview_style" }),
        new NativeBehaviorTranslationCapability(
            "card.downgrade",
            "Downgrade Card",
            NativeBehaviorTranslationStatus.Supported,
            "Downgrades the selected cards.",
            new[] { "card_state_key", "card_ids" }),
        new NativeBehaviorTranslationCapability(
            "card.enchant",
            "Enchant Card",
            NativeBehaviorTranslationStatus.Partial,
            "Applies an enchantment to the selected cards.",
            new[] { "card_state_key", "card_ids", "enchantment_id", "amount" }),
        new NativeBehaviorTranslationCapability(
            "card.autoplay",
            "Auto Play Card",
            NativeBehaviorTranslationStatus.Partial,
            "Automatically plays the selected cards.",
            new[] { "card_state_key", "card_ids", "target", "auto_play_type" }),
        new NativeBehaviorTranslationCapability(
            "card.apply_single_turn_sly",
            "Apply Single-Turn Sly",
            NativeBehaviorTranslationStatus.Supported,
            "Applies single-turn Sly to the selected cards.",
            new[] { "card_state_key", "card_ids" }),
        new NativeBehaviorTranslationCapability(
            "cardpile.auto_play_from_draw_pile",
            "Auto Play From Draw Pile",
            NativeBehaviorTranslationStatus.Partial,
            "Automatically plays cards from the draw pile.",
            new[] { "count", "position", "force_exhaust" }),
        new NativeBehaviorTranslationCapability(
            "orb.channel",
            "Channel Orb",
            NativeBehaviorTranslationStatus.Partial,
            "Channels an orb for the owner player.",
            new[] { "orb_id" }),
        new NativeBehaviorTranslationCapability(
            "orb.passive",
            "Orb Passive",
            NativeBehaviorTranslationStatus.Partial,
            "Triggers an orb passive effect.",
            new[] { "orb_id", "target" }),
        new NativeBehaviorTranslationCapability(
            "orb.add_slots",
            "Add Orb Slots",
            NativeBehaviorTranslationStatus.Supported,
            "Adds orb slots to the owner player.",
            new[] { "amount" }),
        new NativeBehaviorTranslationCapability(
            "orb.remove_slots",
            "Remove Orb Slots",
            NativeBehaviorTranslationStatus.Supported,
            "Removes orb slots from the owner player.",
            new[] { "amount" }),
        new NativeBehaviorTranslationCapability(
            "orb.evoke_next",
            "Evoke Next Orb",
            NativeBehaviorTranslationStatus.Partial,
            "Evokes the next orb in the owner's queue.",
            new[] { "dequeue" }),
        new NativeBehaviorTranslationCapability(
            "potion.procure",
            "Procure Potion",
            NativeBehaviorTranslationStatus.Partial,
            "Adds a potion to the owner's potion belt.",
            new[] { "potion_id" }),
        new NativeBehaviorTranslationCapability(
            "potion.discard",
            "Discard Potion",
            NativeBehaviorTranslationStatus.Partial,
            "Discards a potion from the owner.",
            new[] { "potion_id" }),
        new NativeBehaviorTranslationCapability(
            "relic.obtain",
            "Obtain Relic",
            NativeBehaviorTranslationStatus.Partial,
            "Gives a relic to the owner player.",
            new[] { "relic_id" }),
        new NativeBehaviorTranslationCapability(
            "relic.remove",
            "Remove Relic",
            NativeBehaviorTranslationStatus.Partial,
            "Removes a relic from the owner player.",
            new[] { "relic_id" }),
        new NativeBehaviorTranslationCapability(
            "relic.replace",
            "Replace Relic",
            NativeBehaviorTranslationStatus.Partial,
            "Replaces one relic with another.",
            new[] { "relic_id", "replacement_relic_id" }),
        new NativeBehaviorTranslationCapability(
            "relic.melt",
            "Melt Relic",
            NativeBehaviorTranslationStatus.Partial,
            "Melts a relic from the owner player.",
            new[] { "relic_id" }),
        new NativeBehaviorTranslationCapability(
            "player.add_pet",
            "Add Pet",
            NativeBehaviorTranslationStatus.Partial,
            "Summons or adds a pet for the owner player.",
            new[] { "monster_id" }),
        new NativeBehaviorTranslationCapability(
            "player.forge",
            "Forge",
            NativeBehaviorTranslationStatus.Partial,
            "Runs the forge flow for the owner player.",
            new[] { "amount" }),
        new NativeBehaviorTranslationCapability(
            "player.complete_quest",
            "Complete Quest",
            NativeBehaviorTranslationStatus.Partial,
            "Marks the current quest card as complete.",
            Array.Empty<string>()),
        new NativeBehaviorTranslationCapability(
            "player.rest_heal",
            "Rest Heal",
            NativeBehaviorTranslationStatus.Partial,
            "Applies the rest-site heal effect.",
            new[] { "play_sfx" }),
        new NativeBehaviorTranslationCapability(
            "player.end_turn",
            "End Turn",
            NativeBehaviorTranslationStatus.Partial,
            "Ends the current turn for the owner.",
            Array.Empty<string>()),
        new NativeBehaviorTranslationCapability(
            "combat.repeat",
            "Repeat",
            NativeBehaviorTranslationStatus.Partial,
            "Repeats the outgoing branch a number of times.",
            new[] { "count" }),
        new NativeBehaviorTranslationCapability(
            "player.lose_hp",
            "Lose HP",
            NativeBehaviorTranslationStatus.Supported,
            "Deals unblockable damage to the owner player.",
            new[] { "amount", "target", "props" }),
        new NativeBehaviorTranslationCapability(
            "player.gain_max_hp",
            "Gain Max HP",
            NativeBehaviorTranslationStatus.Supported,
            "Increases the target creature's max HP.",
            new[] { "amount", "target" }),
        new NativeBehaviorTranslationCapability(
            "creature.set_current_hp",
            "Set Current HP",
            NativeBehaviorTranslationStatus.Partial,
            "Sets the current HP of the selected creature.",
            new[] { "amount", "target" }),
        new NativeBehaviorTranslationCapability(
            "flow.branch",
            "Branch",
            NativeBehaviorTranslationStatus.Supported,
            "Splits execution into true and false branches.",
            new[] { "condition", "condition_key" }),
        new NativeBehaviorTranslationCapability(
            "event.reward",
            "Event Reward",
            NativeBehaviorTranslationStatus.Partial,
            "Can translate simple reward payloads such as gold, stars, energy, block, draw, heal, or damage.",
            new[] { "reward_kind", "amount", "target" }),
        new NativeBehaviorTranslationCapability(
            "reward.offer_custom",
            "Offer Custom Reward",
            NativeBehaviorTranslationStatus.Partial,
            "Offers a reward screen entry. Auto-import may still require manual reward-kind cleanup.",
            new[] { "reward_kind", "amount", "reward_count", "card_id", "relic_id", "potion_id" }),
        new NativeBehaviorTranslationCapability(
            "event.choice",
            "Event Choice",
            NativeBehaviorTranslationStatus.Partial,
            "Can translate simple binary choice branches.",
            new[] { "condition", "true_branch", "false_branch" }),
        new NativeBehaviorTranslationCapability(
            "scene.cutscene",
            "Cutscene / Scene Action",
            NativeBehaviorTranslationStatus.Unsupported,
            "Complex scene-driven or animated actions are not translated in Phase 1.",
            new[] { "custom_scene", "timeline", "animation" }),
        new NativeBehaviorTranslationCapability(
            "deck.mutation",
            "Deck / Card Pool Mutation",
            NativeBehaviorTranslationStatus.Unsupported,
            "Direct deck mutation and complex card pool operations need a later phase.",
            new[] { "add_card", "remove_card", "transform_card" }),
        new NativeBehaviorTranslationCapability(
            "map.mutation",
            "Map / Room Mutation",
            NativeBehaviorTranslationStatus.Unsupported,
            "Map flow and room mutations are out of scope for the first translator pass.",
            new[] { "move_room", "spawn_room", "unlock_map" }),
        new NativeBehaviorTranslationCapability(
            "monster.ai",
            "Monster AI",
            NativeBehaviorTranslationStatus.Unsupported,
            "Monster AI scripting is not auto-translated in Phase 1.",
            new[] { "intent", "turn_script", "behavior_tree" })
    };

    public IReadOnlyList<NativeBehaviorTranslationCapability> SupportCatalog => Catalog;

    public NativeBehaviorGraphTranslationResult Translate(NativeBehaviorGraphSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var result = new NativeBehaviorGraphTranslationResult();
        var graph = BehaviorGraphTemplateFactory.CreateDefaultScaffold(source.GraphId, source.EntityKind, source.Name, source.Description, source.TriggerId);
        graph.Name = string.IsNullOrWhiteSpace(source.Name) ? graph.Name : source.Name;
        graph.Description = string.IsNullOrWhiteSpace(source.Description) ? graph.Description : source.Description;

        if (source.Metadata.Count > 0)
        {
            foreach (var pair in source.Metadata)
            {
                graph.Metadata[pair.Key] = pair.Value;
            }
        }

        var entryNodeId = graph.EntryNodeId;
        var exitNodeId = graph.Nodes.FirstOrDefault(node => string.Equals(node.NodeType, "flow.exit", StringComparison.Ordinal))?.NodeId ?? "exit";
        graph.Nodes = graph.Nodes
            .Where(node => string.Equals(node.NodeType, "flow.entry", StringComparison.Ordinal) ||
                           string.Equals(node.NodeType, "flow.exit", StringComparison.Ordinal))
            .ToList();
        graph.Connections.Clear();

        if (source.Steps.Count == 0)
        {
            graph.Connections.Add(new BehaviorGraphConnectionDefinition
            {
                FromNodeId = entryNodeId,
                FromPortId = "next",
                ToNodeId = exitNodeId,
                ToPortId = "in"
            });
            result.Warnings.Add("No native effect steps were provided; generated a scaffold only.");
            graph.Description = _descriptionGenerator.ResolveDescription(graph, source.Description);
            result.Graph = graph;
            return result;
        }

        var nodeIndex = 0;
        var tailNodeId = entryNodeId;
        var tailPortId = "next";
        var stopAfterCurrentSequence = false;

        foreach (var step in source.Steps)
        {
            if (IsBranchStep(step.Kind))
            {
                AppendBranchStep(graph, step, tailNodeId, tailPortId, exitNodeId, result, ref nodeIndex);
                result.AppliedStepKinds.Add(step.Kind);
                stopAfterCurrentSequence = true;
                break;
            }

            var node = CreateActionNode(step, ref nodeIndex, result);
            result.AppliedStepKinds.Add(step.Kind);
            graph.Nodes.Add(node);
            graph.Connections.Add(new BehaviorGraphConnectionDefinition
            {
                FromNodeId = tailNodeId,
                FromPortId = tailPortId,
                ToNodeId = node.NodeId,
                ToPortId = "in"
            });
            tailNodeId = node.NodeId;
            tailPortId = "out";
        }

        if (!stopAfterCurrentSequence)
        {
            graph.Connections.Add(new BehaviorGraphConnectionDefinition
            {
                FromNodeId = tailNodeId,
                FromPortId = tailPortId,
                ToNodeId = exitNodeId,
                ToPortId = "in"
            });
        }

        graph.Description = _descriptionGenerator.ResolveDescription(graph, source.Description);
        result.Graph = graph;
        result.IsPartial = result.UnsupportedStepKinds.Count > 0;
        return result;
    }

    public static IReadOnlyList<NativeBehaviorTranslationCapability> GetSupportCatalog()
    {
        return Catalog;
    }

    private static BehaviorGraphNodeDefinition CreateActionNode(NativeBehaviorStep step, ref int nodeIndex, NativeBehaviorGraphTranslationResult result)
    {
        var normalizedKind = NormalizeStepKind(step.Kind);
        var nodeId = ResolveNodeId(step, normalizedKind, nodeIndex++);

        return normalizedKind switch
        {
            "combat.damage" => BuildNode(nodeId, "combat.damage", "Damage", step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = GetParameter(step, "amount", "0"),
                ["target"] = GetParameter(step, "target", "current_target"),
                ["props"] = GetParameter(step, "props", "none")
            }),
            "combat.gain_block" or "block" => BuildNode(nodeId, "combat.gain_block", "Gain Block", step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = GetParameter(step, "amount", "0"),
                ["target"] = GetParameter(step, "target", "self"),
                ["props"] = GetParameter(step, "props", "none")
            }),
            "combat.heal" => BuildNode(nodeId, "combat.heal", "Heal", step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = GetParameter(step, "amount", "0"),
                ["target"] = GetParameter(step, "target", "self")
            }),
            "combat.draw_cards" or "draw" => BuildNode(nodeId, "combat.draw_cards", "Draw Cards", step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = GetParameter(step, "amount", "1")
            }),
            "player.gain_energy" or "energy" => BuildNode(nodeId, "player.gain_energy", "Gain Energy", step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = GetParameter(step, "amount", "1")
            }),
            "player.gain_gold" or "gold" => BuildNode(nodeId, "player.gain_gold", "Gain Gold", step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = GetParameter(step, "amount", "1")
            }),
            "player.gain_stars" or "stars" => BuildNode(nodeId, "player.gain_stars", "Gain Stars", step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = GetParameter(step, "amount", "1")
            }),
            "combat.apply_power" or "power" => BuildNode(nodeId, "combat.apply_power", "Apply Power", step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["power_id"] = GetParameter(step, "power_id", string.Empty),
                ["amount"] = GetParameter(step, "amount", "1"),
                ["target"] = GetParameter(step, "target", "current_target")
            }),
            "combat.discard_cards" or "discard" => BuildNode(nodeId, "combat.discard_cards", "Discard Cards", step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = GetParameter(step, "amount", "1"),
                ["card_ids"] = GetParameter(step, "card_ids", string.Empty),
                ["target"] = GetParameter(step, "target", "hand")
            }),
            "combat.exhaust_cards" or "exhaust" => BuildNode(nodeId, "combat.exhaust_cards", "Exhaust Cards", step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = GetParameter(step, "amount", "1"),
                ["card_ids"] = GetParameter(step, "card_ids", string.Empty),
                ["target"] = GetParameter(step, "target", "hand")
            }),
            "combat.create_card" or "create_card" => BuildNode(nodeId, "combat.create_card", "Create Card", step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["card_id"] = GetParameter(step, "card_id", string.Empty),
                ["count"] = GetParameter(step, "count", "1"),
                ["target_pile"] = GetParameter(step, "target_pile", "hand")
            }),
            "cardpile.move_cards" or "move_cards" => BuildNode(nodeId, "cardpile.move_cards", "Move Cards", step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["source_pile"] = GetParameter(step, "source_pile", PileType.Discard.ToString()),
                ["target_pile"] = GetParameter(step, "target_pile", PileType.Hand.ToString()),
                ["count"] = GetParameter(step, "count", "0"),
                ["exact_energy_cost"] = GetParameter(step, "exact_energy_cost", "-1"),
                ["include_x_cost"] = GetParameter(step, "include_x_cost", bool.FalseString),
                ["card_type_scope"] = GetParameter(step, "card_type_scope", "any")
            }),
            "combat.remove_card" or "remove_card" => BuildNode(nodeId, "combat.remove_card", "Remove Card", step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["card_ids"] = GetParameter(step, "card_ids", string.Empty),
                ["card_id"] = GetParameter(step, "card_id", string.Empty),
                ["target"] = GetParameter(step, "target", "current")
            }),
            "combat.transform_card" or "transform_card" => BuildNode(nodeId, "combat.transform_card", "Transform Card", step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["card_id"] = GetParameter(step, "card_id", string.Empty),
                ["card_state_key"] = GetParameter(step, "card_state_key", "selected_cards"),
                ["replacement_card_id"] = GetParameter(step, "replacement_card_id", string.Empty),
                ["random_replacement"] = GetParameter(step, "random_replacement", "false")
            }),
            "card.select_cards" => BuildNode(nodeId, "card.select_cards", "Select Cards", step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["state_key"] = GetParameter(step, "state_key", "selected_cards"),
                ["selection_mode"] = GetParameter(step, "selection_mode", "simple_grid"),
                ["source_pile"] = GetParameter(step, "source_pile", PileType.Deck.ToString()),
                ["count"] = GetParameter(step, "count", "1"),
                ["prompt_kind"] = GetParameter(step, "prompt_kind", "generic"),
                ["allow_cancel"] = GetParameter(step, "allow_cancel", bool.FalseString)
            }),
            "card.discard_and_draw" => BuildNode(nodeId, "card.discard_and_draw", "Discard And Draw", step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["card_state_key"] = GetParameter(step, "card_state_key", "selected_cards"),
                ["draw_count"] = GetParameter(step, "draw_count", "0")
            }),
            "card.apply_keyword" => BuildNode(nodeId, "card.apply_keyword", "Apply Card Keyword", step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["card_state_key"] = GetParameter(step, "card_state_key", "selected_cards"),
                ["keyword"] = GetParameter(step, "keyword", string.Empty)
            }),
            "card.upgrade" => BuildNode(nodeId, "card.upgrade", "Upgrade Card", step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["card_state_key"] = GetParameter(step, "card_state_key", "selected_cards"),
                ["card_ids"] = GetParameter(step, "card_ids", string.Empty),
                ["card_preview_style"] = GetParameter(step, "card_preview_style", CardPreviewStyle.HorizontalLayout.ToString())
            }),
            "card.downgrade" => BuildNode(nodeId, "card.downgrade", "Downgrade Card", step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["card_state_key"] = GetParameter(step, "card_state_key", "selected_cards"),
                ["card_ids"] = GetParameter(step, "card_ids", string.Empty)
            }),
            "card.enchant" => BuildNode(nodeId, "card.enchant", "Enchant Card", step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["card_state_key"] = GetParameter(step, "card_state_key", "selected_cards"),
                ["card_ids"] = GetParameter(step, "card_ids", string.Empty),
                ["enchantment_id"] = GetParameter(step, "enchantment_id", string.Empty),
                ["amount"] = GetParameter(step, "amount", "1")
            }),
            "card.autoplay" => BuildNode(nodeId, "card.autoplay", "Auto Play Card", step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["card_state_key"] = GetParameter(step, "card_state_key", "selected_cards"),
                ["card_ids"] = GetParameter(step, "card_ids", string.Empty),
                ["target"] = GetParameter(step, "target", "current_target"),
                ["auto_play_type"] = GetParameter(step, "auto_play_type", AutoPlayType.Default.ToString()),
                ["skip_x_capture"] = GetParameter(step, "skip_x_capture", bool.FalseString),
                ["skip_card_pile_visuals"] = GetParameter(step, "skip_card_pile_visuals", bool.FalseString)
            }),
            "card.apply_single_turn_sly" => BuildNode(nodeId, "card.apply_single_turn_sly", "Apply Single-Turn Sly", step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["card_state_key"] = GetParameter(step, "card_state_key", "selected_cards"),
                ["card_ids"] = GetParameter(step, "card_ids", string.Empty)
            }),
            "cardpile.auto_play_from_draw_pile" => BuildNode(nodeId, "cardpile.auto_play_from_draw_pile", "Auto Play From Draw Pile", step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["count"] = GetParameter(step, "count", GetParameter(step, "amount", "1")),
                ["position"] = GetParameter(step, "position", CardPilePosition.Bottom.ToString()),
                ["force_exhaust"] = GetParameter(step, "force_exhaust", bool.FalseString)
            }),
            "orb.channel" => BuildNode(nodeId, "orb.channel", "Channel Orb", step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["orb_id"] = GetParameter(step, "orb_id", string.Empty)
            }),
            "orb.passive" => BuildNode(nodeId, "orb.passive", "Orb Passive", step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["orb_id"] = GetParameter(step, "orb_id", string.Empty),
                ["target"] = GetParameter(step, "target", "current_target")
            }),
            "orb.add_slots" => BuildNode(nodeId, "orb.add_slots", "Add Orb Slots", step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = GetParameter(step, "amount", "1")
            }),
            "orb.remove_slots" => BuildNode(nodeId, "orb.remove_slots", "Remove Orb Slots", step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = GetParameter(step, "amount", "1")
            }),
            "orb.evoke_next" => BuildNode(nodeId, "orb.evoke_next", "Evoke Next Orb", step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["dequeue"] = GetParameter(step, "dequeue", bool.TrueString)
            }),
            "potion.procure" => BuildNode(nodeId, "potion.procure", "Procure Potion", step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["potion_id"] = GetParameter(step, "potion_id", string.Empty)
            }),
            "potion.discard" => BuildNode(nodeId, "potion.discard", "Discard Potion", step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["potion_id"] = GetParameter(step, "potion_id", string.Empty)
            }),
            "relic.obtain" => BuildNode(nodeId, "relic.obtain", "Obtain Relic", step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["relic_id"] = GetParameter(step, "relic_id", string.Empty)
            }),
            "relic.remove" => BuildNode(nodeId, "relic.remove", "Remove Relic", step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["relic_id"] = GetParameter(step, "relic_id", string.Empty)
            }),
            "relic.replace" => BuildNode(nodeId, "relic.replace", "Replace Relic", step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["relic_id"] = GetParameter(step, "relic_id", string.Empty),
                ["replacement_relic_id"] = GetParameter(step, "replacement_relic_id", string.Empty)
            }),
            "relic.melt" => BuildNode(nodeId, "relic.melt", "Melt Relic", step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["relic_id"] = GetParameter(step, "relic_id", string.Empty)
            }),
            "player.add_pet" => BuildNode(nodeId, "player.add_pet", "Add Pet", step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["monster_id"] = GetParameter(step, "monster_id", string.Empty)
            }),
            "player.forge" => BuildNode(nodeId, "player.forge", "Forge", step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = GetParameter(step, "amount", "1")
            }),
            "player.complete_quest" => BuildNode(nodeId, "player.complete_quest", "Complete Quest", step, new Dictionary<string, string>(StringComparer.Ordinal)),
            "player.rest_heal" => BuildNode(nodeId, "player.rest_heal", "Rest Heal", step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["play_sfx"] = GetParameter(step, "play_sfx", bool.TrueString)
            }),
            "player.end_turn" => BuildNode(nodeId, "player.end_turn", "End Turn", step, new Dictionary<string, string>(StringComparer.Ordinal)),
            "combat.repeat" or "repeat" => BuildNode(nodeId, "combat.repeat", "Repeat", step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["count"] = GetParameter(step, "count", "1")
            }),
            "player.lose_hp" or "lose_hp" => BuildNode(nodeId, "player.lose_hp", "Lose HP", step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = GetParameter(step, "amount", "0"),
                ["target"] = GetParameter(step, "target", "self"),
                ["props"] = GetParameter(step, "props", "none")
            }),
            "player.gain_max_hp" or "gain_max_hp" => BuildNode(nodeId, "player.gain_max_hp", "Gain Max HP", step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = GetParameter(step, "amount", "0"),
                ["target"] = GetParameter(step, "target", "self")
            }),
            "creature.set_current_hp" => BuildNode(nodeId, "creature.set_current_hp", "Set Current HP", step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = GetParameter(step, "amount", "0"),
                ["target"] = GetParameter(step, "target", "self")
            }),
            "event.reward" => TranslateRewardStep(step, nodeId, result),
            "reward.offer_custom" => BuildNode(nodeId, "reward.offer_custom", "Offer Custom Reward", step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["reward_kind"] = GetParameter(step, "reward_kind", "custom"),
                ["amount"] = GetParameter(step, "amount", "0"),
                ["reward_count"] = GetParameter(step, "reward_count", "1"),
                ["card_id"] = GetParameter(step, "card_id", string.Empty),
                ["relic_id"] = GetParameter(step, "relic_id", string.Empty),
                ["potion_id"] = GetParameter(step, "potion_id", string.Empty)
            }),
            "event.page" or "event.option" or "event.goto_page" or "event.proceed" or "event.start_combat" => BuildNode(nodeId, normalizedKind, GetDisplayName(step, normalizedKind), step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["page_id"] = GetParameter(step, "page_id", GetParameter(step, "page", string.Empty)),
                ["option_id"] = GetParameter(step, "option_id", string.Empty),
                ["title"] = GetParameter(step, "title", string.Empty),
                ["description"] = GetParameter(step, "description", string.Empty),
                ["next_page_id"] = GetParameter(step, "next_page_id", string.Empty),
                ["encounter_id"] = GetParameter(step, "encounter_id", string.Empty),
                ["resume_page_id"] = GetParameter(step, "resume_page_id", string.Empty),
                ["is_proceed"] = GetParameter(step, "is_proceed", "false"),
                ["save_choice_to_history"] = GetParameter(step, "save_choice_to_history", "true"),
                ["reward_kind"] = GetParameter(step, "reward_kind", string.Empty),
                ["reward_amount"] = GetParameter(step, "reward_amount", string.Empty),
                ["reward_target"] = GetParameter(step, "reward_target", string.Empty),
                ["reward_props"] = GetParameter(step, "reward_props", string.Empty),
                ["reward_power_id"] = GetParameter(step, "reward_power_id", string.Empty),
                ["is_start"] = GetParameter(step, "is_start", "false"),
                ["option_order"] = GetParameter(step, "option_order", string.Empty)
            }),
            "debug.log" or "log" => BuildNode(nodeId, "debug.log", "Log Message", step, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["message"] = GetParameter(step, "message", "Native behavior step executed.")
            }),
            _ => TranslateUnsupportedStep(step, nodeId, result)
        };
    }

    private static BehaviorGraphNodeDefinition TranslateRewardStep(NativeBehaviorStep step, string nodeId, NativeBehaviorGraphTranslationResult result)
    {
        _ = result;
        return BuildNode(nodeId, "event.reward", "Event Reward", step, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["reward_kind"] = GetParameter(step, "reward_kind", "gold"),
            ["reward_amount"] = GetParameter(step, "amount", GetParameter(step, "reward_amount", "1")),
            ["reward_target"] = GetParameter(step, "target", GetParameter(step, "reward_target", "self")),
            ["reward_props"] = GetParameter(step, "props", GetParameter(step, "reward_props", "none")),
            ["reward_power_id"] = GetParameter(step, "power_id", GetParameter(step, "reward_power_id", string.Empty))
        });
    }

    private static void AppendBranchStep(
        BehaviorGraphDefinition graph,
        NativeBehaviorStep step,
        string tailNodeId,
        string tailPortId,
        string exitNodeId,
        NativeBehaviorGraphTranslationResult result,
        ref int nodeIndex)
    {
        var branchNodeId = ResolveNodeId(step, "flow.branch", nodeIndex++);
        var branchNode = BuildNode(branchNodeId, "flow.branch", GetDisplayName(step, "Branch"), step, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["condition"] = GetParameter(step, "condition", GetParameter(step, "condition_key", string.Empty))
        });
        graph.Nodes.Add(branchNode);
        graph.Connections.Add(new BehaviorGraphConnectionDefinition
        {
            FromNodeId = tailNodeId,
            FromPortId = tailPortId,
            ToNodeId = branchNode.NodeId,
            ToPortId = "in"
        });

        AppendBranchSequence(graph, step.TrueBranch, branchNode.NodeId, "true", exitNodeId, result, ref nodeIndex);
        AppendBranchSequence(graph, step.FalseBranch, branchNode.NodeId, "false", exitNodeId, result, ref nodeIndex);

        result.AppliedStepKinds.Add(step.Kind);
    }

    private static void AppendBranchSequence(
        BehaviorGraphDefinition graph,
        IReadOnlyList<NativeBehaviorStep> branchSteps,
        string previousNodeId,
        string previousPortId,
        string exitNodeId,
        NativeBehaviorGraphTranslationResult result,
        ref int nodeIndex)
    {
        if (branchSteps.Count == 0)
        {
            graph.Connections.Add(new BehaviorGraphConnectionDefinition
            {
                FromNodeId = previousNodeId,
                FromPortId = previousPortId,
                ToNodeId = exitNodeId,
                ToPortId = "in"
            });
            return;
        }

        var currentNodeId = previousNodeId;
        var currentPortId = previousPortId;
        foreach (var step in branchSteps)
        {
            if (IsBranchStep(step.Kind))
            {
                AppendBranchStep(graph, step, currentNodeId, currentPortId, exitNodeId, result, ref nodeIndex);
                return;
            }

            var node = CreateActionNode(step, ref nodeIndex, result);
            result.AppliedStepKinds.Add(step.Kind);
            graph.Nodes.Add(node);
            graph.Connections.Add(new BehaviorGraphConnectionDefinition
            {
                FromNodeId = currentNodeId,
                FromPortId = currentPortId,
                ToNodeId = node.NodeId,
                ToPortId = "in"
            });
            currentNodeId = node.NodeId;
            currentPortId = "out";
        }

        graph.Connections.Add(new BehaviorGraphConnectionDefinition
        {
            FromNodeId = currentNodeId,
            FromPortId = currentPortId,
            ToNodeId = exitNodeId,
            ToPortId = "in"
        });
    }

    private static BehaviorGraphNodeDefinition TranslateUnsupportedStep(NativeBehaviorStep step, string nodeId, NativeBehaviorGraphTranslationResult result)
    {
        var normalizedKind = NormalizeStepKind(step.Kind);
        result.UnsupportedStepKinds.Add(step.Kind);
        result.Warnings.Add($"Unsupported native step '{step.Kind}' was translated as a debug placeholder.");

        return BuildNode(nodeId, "debug.log", GetDisplayName(step, normalizedKind), step, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["message"] = $"Unsupported native step: {step.Kind}"
        });
    }

    private static BehaviorGraphNodeDefinition BuildNode(string nodeId, string nodeType, string displayName, NativeBehaviorStep step, Dictionary<string, string> properties)
    {
        return new BehaviorGraphNodeDefinition
        {
            NodeId = nodeId,
            NodeType = nodeType,
            DisplayName = displayName,
            Description = GetParameter(step, "description", string.Empty),
            Properties = properties,
            DynamicValues = BuildDynamicValues(step)
        };
    }

    private static Dictionary<string, DynamicValueDefinition> BuildDynamicValues(NativeBehaviorStep step)
    {
        var results = new Dictionary<string, DynamicValueDefinition>(StringComparer.Ordinal);
        foreach (var propertyKey in step.Parameters.Keys
                     .Where(key => key.EndsWith("_source_kind", StringComparison.OrdinalIgnoreCase))
                     .Select(key => key[..^"_source_kind".Length])
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var sourceKindText = GetParameter(step, $"{propertyKey}_source_kind", DynamicValueSourceKind.Literal.ToString());
            if (!Enum.TryParse<DynamicValueSourceKind>(sourceKindText, ignoreCase: true, out var sourceKind))
            {
                sourceKind = DynamicValueSourceKind.Literal;
            }

            var definition = new DynamicValueDefinition
            {
                SourceKind = sourceKind,
                LiteralValue = GetParameter(step, propertyKey, "0"),
                DynamicVarName = GetParameter(step, $"{propertyKey}_var_name", string.Empty),
                FormulaRef = GetParameter(step, $"{propertyKey}_formula_ref", string.Empty),
                TemplateText = GetParameter(step, $"{propertyKey}_template", string.Empty),
                PreviewFormat = GetParameter(step, $"{propertyKey}_preview_format", string.Empty),
                PreviewMultiplierKey = GetParameter(step, $"{propertyKey}_preview_multiplier_key", string.Empty),
                PreviewMultiplierValue = GetParameter(step, $"{propertyKey}_preview_multiplier_value", string.Empty)
            };

            if (Enum.TryParse<DynamicValueOverrideMode>(GetParameter(step, $"{propertyKey}_base_override_mode", DynamicValueOverrideMode.None.ToString()), ignoreCase: true, out var baseMode))
            {
                definition.BaseOverrideMode = baseMode;
            }

            if (Enum.TryParse<DynamicValueOverrideMode>(GetParameter(step, $"{propertyKey}_extra_override_mode", DynamicValueOverrideMode.None.ToString()), ignoreCase: true, out var extraMode))
            {
                definition.ExtraOverrideMode = extraMode;
            }

            definition.BaseOverrideValue = GetParameter(step, $"{propertyKey}_base_override_value", string.Empty);
            definition.ExtraOverrideValue = GetParameter(step, $"{propertyKey}_extra_override_value", string.Empty);
            results[propertyKey] = definition;
        }

        return results;
    }

    private static bool IsBranchStep(string? kind)
    {
        var normalized = NormalizeStepKind(kind);
        return normalized is "flow.branch" or "branch" or "event.choice";
    }

    private static string ResolveNodeId(NativeBehaviorStep step, string normalizedKind, int nodeIndex)
    {
        if (step.Parameters.TryGetValue("node_id", out var customNodeId) && !string.IsNullOrWhiteSpace(customNodeId))
        {
            return customNodeId.Trim();
        }

        return $"{normalizedKind.Replace('.', '_')}_{nodeIndex}";
    }

    private static string GetDisplayName(NativeBehaviorStep step, string fallback)
    {
        return GetParameter(step, "display_name", fallback);
    }

    private static string GetParameter(NativeBehaviorStep step, string key, string defaultValue)
    {
        return step.Parameters.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : defaultValue;
    }

    private static string NormalizeStepKind(string? kind)
    {
        return string.IsNullOrWhiteSpace(kind)
            ? string.Empty
            : kind.Trim().ToLowerInvariant();
    }

    public static IReadOnlyList<NativeBehaviorTranslationCapability> GetSupportCatalogSnapshot()
    {
        return Catalog;
    }
}
