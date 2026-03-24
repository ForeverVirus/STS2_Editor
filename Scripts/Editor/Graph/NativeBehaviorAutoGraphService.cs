using System.Text.RegularExpressions;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2_Editor.Scripts.Editor.Core.Models;

namespace STS2_Editor.Scripts.Editor.Graph;

public enum NativeBehaviorAutoGraphStrategy
{
    ReflectionImport = 0,
    DescriptionFallback = 1
}

public sealed class NativeBehaviorAutoGraphResult
{
    public BehaviorGraphDefinition Graph { get; init; } = new();

    public bool IsPartial { get; init; }

    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> SupportedStepKinds { get; init; } = Array.Empty<string>();

    public NativeBehaviorAutoGraphStrategy Strategy { get; init; }

    public string Summary { get; init; } = string.Empty;
}

public sealed class NativeBehaviorAutoGraphService
{
    private static readonly Regex DamageRegex = new(@"(?:deal|deals|造成)\s*(?<amount>\d+)\s*(?:damage|点伤害|點傷害|伤害|傷害)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex BlockRegex = new(@"(?:gain|gains|获得|獲得)\s*(?<amount>\d+)\s*(?:block|格挡|格擋)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex HealRegex = new(@"(?:heal|heals|恢复|恢復|回复|回復)\s*(?<amount>\d+)\s*(?:hp|health|生命|生命值)?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DrawRegex = new(@"(?:draw|draws|抽)\s*(?<amount>\d+)\s*(?:cards?|张牌|張牌)?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex EnergyRegex = new(@"(?:gain|gains|获得|獲得)\s*(?<amount>\d+)\s*(?:energy|能量)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex GoldRegex = new(@"(?:gain|gains|获得|獲得)\s*(?<amount>\d+)\s*(?:gold|金币|金幣)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex StarsRegex = new(@"(?:gain|gains|获得|獲得)\s*(?<amount>\d+)\s*(?:stars?|星星)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ApplyPowerRegex = new(@"(?:apply|applies|施加)\s*(?<amount>\d+)\s*(?:layers?\s+of\s+|层)?(?<power>[\p{L}\p{IsCJKUnifiedIdeographs}\s]+?)(?:\.|。|,|，|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly string[] UnsupportedKeywords =
    [
        "channel",
        "stance",
        "orb",
        "summon",
        "discard",
        "exhaust",
        "retain",
        "scry",
        "transform",
        "random",
        "choose",
        "generate",
        "create",
        "shuffle",
        "evoke",
        "灌注",
        "姿态",
        "宝珠",
        "召唤",
        "弃牌",
        "消耗",
        "保留",
        "占卜",
        "变化",
        "随机",
        "选择",
        "生成",
        "洗入"
    ];

    private readonly NativeBehaviorGraphTranslator _translator = new();
    private readonly NativeBehaviorGraphAutoImporter _importer = new();

    public IReadOnlyList<NativeBehaviorTranslationCapability> SupportCatalog => NativeBehaviorGraphTranslator.GetSupportCatalog();

    public bool TryCreateGraph(ModStudioEntityKind kind, string entityId, out NativeBehaviorAutoGraphResult? result)
    {
        result = null;
        if (TryCreateViaReflection(kind, entityId, out result))
        {
            return true;
        }

        return kind switch
        {
            ModStudioEntityKind.Card => TryCreateCardGraphFallback(entityId, out result),
            ModStudioEntityKind.Potion => TryCreatePotionGraphFallback(entityId, out result),
            ModStudioEntityKind.Relic => TryCreateRelicGraphFallback(entityId, out result),
            ModStudioEntityKind.Event => TryCreateEventGraphFallback(entityId, out result),
            _ => false
        };
    }

    private bool TryCreateViaReflection(ModStudioEntityKind kind, string entityId, out NativeBehaviorAutoGraphResult? result)
    {
        result = null;
        if (!_importer.TryCreateGraph(kind, entityId, out var importResult) ||
            !importResult.IsSupported ||
            importResult.Graph == null)
        {
            return false;
        }

        var notes = importResult.UnsupportedCalls
            .Concat(importResult.Warnings)
            .Where(note => !string.IsNullOrWhiteSpace(note))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        result = new NativeBehaviorAutoGraphResult
        {
            Graph = importResult.Graph,
            IsPartial = importResult.IsPartial,
            Notes = notes,
            SupportedStepKinds = importResult.Graph.Nodes
                .Select(node => node.NodeType)
                .Where(nodeType => !string.IsNullOrWhiteSpace(nodeType) && nodeType is not "flow.entry" and not "flow.exit")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Strategy = NativeBehaviorAutoGraphStrategy.ReflectionImport,
            Summary = string.Join(
                Environment.NewLine,
                new[]
                {
                    "Strategy: reflection-import",
                    importResult.Summary
                }.Where(line => !string.IsNullOrWhiteSpace(line)))
        };
        return true;
    }

    private bool TryCreateCardGraphFallback(string entityId, out NativeBehaviorAutoGraphResult? result)
    {
        result = null;
        var card = ModelDb.AllCards.FirstOrDefault(candidate => string.Equals(candidate.Id.Entry, entityId, StringComparison.Ordinal));
        if (card == null)
        {
            return false;
        }

        var description = NormalizeDescription(TryGetCardDescription(card));
        var steps = ParseCommonSteps(description, ModStudioEntityKind.Card, ResolveDefaultTarget(card.TargetType));
        if (steps.Count == 0)
        {
            return false;
        }

        var notes = FindUnsupportedNotes(description);
        var source = new NativeBehaviorGraphSource
        {
            EntityKind = ModStudioEntityKind.Card,
            GraphId = $"auto_card_{entityId}",
            Name = $"{card.Title} Native Import",
            Description = description,
            TriggerId = "card.on_play",
            Steps = steps
        };

        var translation = _translator.Translate(source);
        translation.Graph.GraphId = source.GraphId;
        translation.Graph.Name = source.Name;
        translation.Graph.Description = _translator.Translate(source).Graph.Description;
        result = BuildFallbackResult(translation, "card.on_play", notes);
        return true;
    }

    private bool TryCreatePotionGraphFallback(string entityId, out NativeBehaviorAutoGraphResult? result)
    {
        result = null;
        var potion = ModelDb.AllPotions.FirstOrDefault(candidate => string.Equals(candidate.Id.Entry, entityId, StringComparison.Ordinal));
        if (potion == null)
        {
            return false;
        }

        var description = NormalizeDescription(TryGetLocText(potion.DynamicDescription));
        var defaultTarget = potion.TargetType.IsSingleTarget() ? "current_target" : ResolveDefaultTarget(potion.TargetType);
        var steps = ParseCommonSteps(description, ModStudioEntityKind.Potion, defaultTarget);
        if (steps.Count == 0)
        {
            return false;
        }

        var notes = FindUnsupportedNotes(description);
        var source = new NativeBehaviorGraphSource
        {
            EntityKind = ModStudioEntityKind.Potion,
            GraphId = $"auto_potion_{entityId}",
            Name = $"{TryGetLocText(potion.Title)} Native Import",
            Description = description,
            TriggerId = "potion.on_use",
            Steps = steps
        };

        var translation = _translator.Translate(source);
        translation.Graph.GraphId = source.GraphId;
        translation.Graph.Name = source.Name;
        result = BuildFallbackResult(translation, "potion.on_use", notes);
        return true;
    }

    private bool TryCreateRelicGraphFallback(string entityId, out NativeBehaviorAutoGraphResult? result)
    {
        result = null;
        var relic = ModelDb.AllRelics.FirstOrDefault(candidate => string.Equals(candidate.Id.Entry, entityId, StringComparison.Ordinal));
        if (relic == null)
        {
            return false;
        }

        var description = NormalizeDescription(TryGetLocText(relic.DynamicDescription));
        if (!TryResolveRelicTrigger(description, out var triggerId))
        {
            return false;
        }

        var steps = ParseCommonSteps(description, ModStudioEntityKind.Relic, "self");
        if (steps.Count == 0)
        {
            return false;
        }

        var notes = FindUnsupportedNotes(description);
        var source = new NativeBehaviorGraphSource
        {
            EntityKind = ModStudioEntityKind.Relic,
            GraphId = $"auto_relic_{entityId}",
            Name = $"{TryGetLocText(relic.Title)} Native Import",
            Description = description,
            TriggerId = triggerId,
            Steps = steps
        };

        var translation = _translator.Translate(source);
        translation.Graph.GraphId = source.GraphId;
        translation.Graph.Name = source.Name;
        result = BuildFallbackResult(translation, triggerId, notes);
        return true;
    }

    private bool TryCreateEventGraphFallback(string entityId, out NativeBehaviorAutoGraphResult? result)
    {
        result = null;
        var eventModel = ModelDb.AllEvents.FirstOrDefault(candidate => string.Equals(candidate.Id.Entry, entityId, StringComparison.Ordinal));
        if (eventModel == null)
        {
            return false;
        }

        var graph = BehaviorGraphTemplateFactory.CreateDefaultScaffold(
            $"auto_event_{entityId}",
            ModStudioEntityKind.Event,
            $"{TryGetLocText(eventModel.Title)} Native Import",
            NormalizeDescription(TryGetLocText(eventModel.InitialDescription)),
            "event.on_begin");

        var exitNodeId = graph.Nodes.First(node => string.Equals(node.NodeType, "flow.exit", StringComparison.Ordinal)).NodeId;
        graph.Connections.Clear();

        var pageNodeId = "event_page_initial";
        var optionIds = new List<string>();
        var pageNode = new BehaviorGraphNodeDefinition
        {
            NodeId = pageNodeId,
            NodeType = "event.page",
            DisplayName = "Initial Page",
            Properties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["page_id"] = "INITIAL",
                ["title"] = TryGetLocText(eventModel.Title),
                ["description"] = NormalizeDescription(TryGetLocText(eventModel.InitialDescription)),
                ["is_start"] = bool.TrueString
            }
        };
        graph.Nodes.Add(pageNode);
        graph.Connections.Add(new BehaviorGraphConnectionDefinition
        {
            FromNodeId = graph.EntryNodeId,
            FromPortId = "next",
            ToNodeId = pageNodeId,
            ToPortId = "in"
        });

        var optionIndex = 0;
        foreach (var optionTitle in eventModel.GameInfoOptions.Select(TryGetLocText).Where(text => !string.IsNullOrWhiteSpace(text)))
        {
            var optionId = $"OPTION_{optionIndex + 1}";
            optionIds.Add(optionId);
            var optionNodeId = $"event_option_{optionIndex + 1}";
            var proceedNodeId = $"event_proceed_{optionIndex + 1}";
            graph.Nodes.Add(new BehaviorGraphNodeDefinition
            {
                NodeId = optionNodeId,
                NodeType = "event.option",
                DisplayName = $"Option {optionIndex + 1}",
                Properties = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["page_id"] = "INITIAL",
                    ["option_id"] = optionId,
                    ["title"] = optionTitle,
                    ["description"] = string.Empty,
                    ["is_proceed"] = bool.TrueString
                }
            });
            graph.Nodes.Add(new BehaviorGraphNodeDefinition
            {
                NodeId = proceedNodeId,
                NodeType = "event.proceed",
                DisplayName = "Proceed"
            });
            graph.Connections.Add(new BehaviorGraphConnectionDefinition
            {
                FromNodeId = pageNodeId,
                FromPortId = "out",
                ToNodeId = optionNodeId,
                ToPortId = "in"
            });
            graph.Connections.Add(new BehaviorGraphConnectionDefinition
            {
                FromNodeId = optionNodeId,
                FromPortId = "out",
                ToNodeId = proceedNodeId,
                ToPortId = "in"
            });
            graph.Connections.Add(new BehaviorGraphConnectionDefinition
            {
                FromNodeId = proceedNodeId,
                FromPortId = "out",
                ToNodeId = exitNodeId,
                ToPortId = "in"
            });
            optionIndex++;
        }

        pageNode.Properties["option_order"] = string.Join(",", optionIds);
        graph.Metadata["event_start_page_id"] = "INITIAL";

        result = new NativeBehaviorAutoGraphResult
        {
            Graph = graph,
            IsPartial = true,
            Notes = new[]
            {
                "Event auto-graph scaffolded from the initial page and option localization only.",
                "Option outcomes still require manual review or later event reflection support."
            },
            SupportedStepKinds = graph.Nodes
                .Select(node => node.NodeType)
                .Where(nodeType => !string.IsNullOrWhiteSpace(nodeType) && nodeType is not "flow.entry" and not "flow.exit")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Strategy = NativeBehaviorAutoGraphStrategy.DescriptionFallback,
            Summary = "Strategy: event-scaffold-fallback"
        };
        return true;
    }

    private NativeBehaviorAutoGraphResult BuildFallbackResult(NativeBehaviorGraphTranslationResult translation, string triggerId, IReadOnlyList<string> notes)
    {
        var summaryLines = new List<string>
        {
            "Strategy: description-fallback",
            $"Trigger: {triggerId}",
            $"Applied steps: {(translation.AppliedStepKinds.Count == 0 ? "-" : string.Join(", ", translation.AppliedStepKinds.Distinct(StringComparer.OrdinalIgnoreCase)))}"
        };

        if (translation.UnsupportedStepKinds.Count > 0)
        {
            summaryLines.Add($"Unsupported steps: {string.Join(", ", translation.UnsupportedStepKinds.Distinct(StringComparer.OrdinalIgnoreCase))}");
        }

        foreach (var note in notes)
        {
            summaryLines.Add($"Note: {note}");
        }

        return new NativeBehaviorAutoGraphResult
        {
            Graph = translation.Graph,
            IsPartial = translation.IsPartial || notes.Count > 0,
            Notes = notes.Concat(translation.Warnings).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            SupportedStepKinds = translation.AppliedStepKinds.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Strategy = NativeBehaviorAutoGraphStrategy.DescriptionFallback,
            Summary = string.Join(Environment.NewLine, summaryLines)
        };
    }

    private List<NativeBehaviorStep> ParseCommonSteps(string description, ModStudioEntityKind kind, string defaultTarget)
    {
        var matches = new List<StepMatch>();
        CollectMatches(matches, DamageRegex, description, match => BuildStep("combat.damage", match.Groups["amount"].Value, ResolveTarget(match.Value, defaultTarget), ResolveDamageProps(kind)));
        CollectMatches(matches, BlockRegex, description, match => BuildStep("combat.gain_block", match.Groups["amount"].Value, ResolveTarget(match.Value, defaultTarget), "none"));
        CollectMatches(matches, HealRegex, description, match => BuildStep("combat.heal", match.Groups["amount"].Value, ResolveTarget(match.Value, defaultTarget)));
        CollectMatches(matches, DrawRegex, description, match => BuildStep("combat.draw_cards", match.Groups["amount"].Value));
        CollectMatches(matches, EnergyRegex, description, match => BuildStep("player.gain_energy", match.Groups["amount"].Value));
        CollectMatches(matches, GoldRegex, description, match => BuildStep("player.gain_gold", match.Groups["amount"].Value));
        CollectMatches(matches, StarsRegex, description, match => BuildStep("player.gain_stars", match.Groups["amount"].Value));
        CollectMatches(matches, ApplyPowerRegex, description, match => BuildApplyPowerStep(match, defaultTarget));

        return matches
            .OrderBy(match => match.Index)
            .Select(match => match.Step)
            .Where(step => step != null)
            .Cast<NativeBehaviorStep>()
            .ToList();
    }

    private void CollectMatches(List<StepMatch> matches, Regex regex, string description, Func<Match, NativeBehaviorStep?> factory)
    {
        foreach (Match match in regex.Matches(description))
        {
            if (!match.Success)
            {
                continue;
            }

            var step = factory(match);
            if (step == null)
            {
                continue;
            }

            matches.Add(new StepMatch(match.Index, step));
        }
    }

    private NativeBehaviorStep BuildStep(string kind, string amount)
    {
        return new NativeBehaviorStep
        {
            Kind = kind,
            Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = amount
            }
        };
    }

    private NativeBehaviorStep BuildStep(string kind, string amount, string target, string? props = null)
    {
        var step = BuildStep(kind, amount);
        step.Parameters["target"] = target;
        if (!string.IsNullOrWhiteSpace(props))
        {
            step.Parameters["props"] = props;
        }

        return step;
    }

    private NativeBehaviorStep? BuildApplyPowerStep(Match match, string defaultTarget)
    {
        var powerName = match.Groups["power"].Value.Trim();
        var powerId = ResolvePowerId(powerName);
        if (string.IsNullOrWhiteSpace(powerId))
        {
            return null;
        }

        var step = BuildStep("combat.apply_power", match.Groups["amount"].Value, ResolveTarget(match.Value, defaultTarget));
        step.Parameters["power_id"] = powerId;
        return step;
    }

    private static string ResolveTarget(string fragment, string defaultTarget)
    {
        var normalized = fragment.ToLowerInvariant();
        if (normalized.Contains("all enemies", StringComparison.Ordinal) || normalized.Contains("所有敌人", StringComparison.Ordinal))
        {
            return "all_enemies";
        }

        if (normalized.Contains("all allies", StringComparison.Ordinal) || normalized.Contains("所有友军", StringComparison.Ordinal))
        {
            return "all_allies";
        }

        return defaultTarget;
    }

    private static string ResolveDefaultTarget(TargetType targetType)
    {
        return targetType switch
        {
            TargetType.AnyEnemy => "current_target",
            TargetType.AnyAlly or TargetType.AnyPlayer => "current_target",
            TargetType.AllEnemies => "all_enemies",
            TargetType.AllAllies => "all_allies",
            _ => "self"
        };
    }

    private static string ResolveDamageProps(ModStudioEntityKind kind)
    {
        return kind == ModStudioEntityKind.Card ? ValueProp.Move.ToString() : "none";
    }

    private static bool TryResolveRelicTrigger(string description, out string triggerId)
    {
        var normalized = description.ToLowerInvariant();
        if (normalized.Contains("after you play a card", StringComparison.Ordinal) ||
            normalized.Contains("when you play a card", StringComparison.Ordinal) ||
            normalized.Contains("每当你打出一张牌", StringComparison.Ordinal))
        {
            triggerId = "relic.after_card_played";
            return true;
        }

        if (normalized.Contains("before you play a card", StringComparison.Ordinal) ||
            normalized.Contains("在你打出一张牌前", StringComparison.Ordinal))
        {
            triggerId = "relic.before_card_played";
            return true;
        }

        if (normalized.Contains("after you use a potion", StringComparison.Ordinal) ||
            normalized.Contains("when you use a potion", StringComparison.Ordinal) ||
            normalized.Contains("每当你使用药水", StringComparison.Ordinal))
        {
            triggerId = "relic.after_potion_used";
            return true;
        }

        if (normalized.Contains("before you use a potion", StringComparison.Ordinal) ||
            normalized.Contains("在你使用药水前", StringComparison.Ordinal))
        {
            triggerId = "relic.before_potion_used";
            return true;
        }

        triggerId = string.Empty;
        return false;
    }

    private static IReadOnlyList<string> FindUnsupportedNotes(string description)
    {
        return UnsupportedKeywords
            .Where(keyword => description.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .Select(keyword => $"Contains unsupported keyword '{keyword}'.")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string TryGetCardDescription(CardModel card)
    {
        try
        {
            return card.GetDescriptionForPile(PileType.None);
        }
        catch
        {
            return TryGetLocText(card.Description);
        }
    }

    private static string TryGetLocText(LocString locString)
    {
        try
        {
            return locString.GetFormattedText() ?? string.Empty;
        }
        catch
        {
            try
            {
                return locString.GetRawText();
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    private static string NormalizeDescription(string? rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return string.Empty;
        }

        var text = Regex.Replace(rawText, @"\[/?[^\]]+\]", " ");
        text = text.Replace('\n', ' ').Replace('\r', ' ');
        text = Regex.Replace(text, @"\s+", " ");
        return text.Trim();
    }

    private static string? ResolvePowerId(string powerName)
    {
        if (string.IsNullOrWhiteSpace(powerName))
        {
            return null;
        }

        var normalized = NormalizePowerName(powerName);
        foreach (var power in ModelDb.AllPowers)
        {
            var title = NormalizePowerName(TryGetLocText(power.Title));
            if (string.Equals(title, normalized, StringComparison.OrdinalIgnoreCase))
            {
                return power.Id.Entry;
            }
        }

        return null;
    }

    private static string NormalizePowerName(string value)
    {
        return Regex.Replace(value, @"\s+", " ").Trim().Trim('.', '。', ',', '，').ToLowerInvariant();
    }

    private sealed record StepMatch(int Index, NativeBehaviorStep Step);
}
