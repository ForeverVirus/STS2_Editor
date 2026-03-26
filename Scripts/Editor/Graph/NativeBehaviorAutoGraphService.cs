using System.Globalization;
using System.Text.RegularExpressions;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Events;
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
        try
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
                ModStudioEntityKind.Enchantment => TryCreateEnchantmentGraphFallback(entityId, out result),
                _ => false
            };
        }
        catch
        {
            var triggerId = kind switch
            {
                ModStudioEntityKind.Card => "card.on_play",
                ModStudioEntityKind.Potion => "potion.on_use",
                ModStudioEntityKind.Relic => "relic.passive",
                ModStudioEntityKind.Event => "event.on_begin",
                ModStudioEntityKind.Enchantment => "enchantment.on_play",
                _ => "flow.entry"
            };
            result = BuildDebugFallbackResult(
                kind,
                $"auto_{kind.ToString().ToLowerInvariant()}_{entityId}",
                $"{entityId} Native Import",
                string.Empty,
                triggerId,
                $"{kind} auto-graph threw in the console harness; generated an explicit debug fallback graph.");
            return true;
        }
    }

    private bool TryCreateViaReflection(ModStudioEntityKind kind, string entityId, out NativeBehaviorAutoGraphResult? result)
    {
        result = null;
        NativeBehaviorGraphAutoImportResult? importResult;
        try
        {
            if (!_importer.TryCreateGraph(kind, entityId, out importResult) ||
                importResult == null ||
                !importResult.IsSupported ||
                importResult.Graph == null)
            {
                return false;
            }
        }
        catch
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
        try
        {
            result = null;
            var card = ResolveCardModel(entityId);
            if (card == null)
            {
                result = BuildDebugFallbackResult(
                    ModStudioEntityKind.Card,
                    $"auto_card_{entityId}",
                    $"{entityId} Native Import",
                    string.Empty,
                    "card.on_play",
                    "Card model could not be resolved in the console harness; generated an explicit fallback graph.");
                return true;
            }

            if (TryCreateSpecialCardFallback(entityId, card, out result))
            {
                return true;
            }

            var description = NormalizeDescription(TryGetCardDescription(card));
            var steps = ParseCommonSteps(description, ModStudioEntityKind.Card, ResolveDefaultTarget(card.TargetType));
            if (steps.Count == 0)
            {
                result = BuildDebugFallbackResult(
                    ModStudioEntityKind.Card,
                    $"auto_card_{entityId}",
                    $"{card.Title} Native Import",
                    description,
                    "card.on_play",
                    "Card did not expose a parseable on-play effect in the current fallback importer.");
                return true;
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
        catch
        {
            result = BuildDebugFallbackResult(
                ModStudioEntityKind.Card,
                $"auto_card_{entityId}",
                $"{entityId} Native Import",
                string.Empty,
                "card.on_play",
                "Card fallback threw in the console harness; generated an explicit debug fallback graph.");
            return true;
        }
    }

    private bool TryCreatePotionGraphFallback(string entityId, out NativeBehaviorAutoGraphResult? result)
    {
        result = null;
        var potion = ResolvePotionModel(entityId);
        if (potion == null)
        {
            result = BuildDebugFallbackResult(
                ModStudioEntityKind.Potion,
                $"auto_potion_{entityId}",
                $"{entityId} Native Import",
                string.Empty,
                "potion.on_use",
                "Potion model could not be resolved in the console harness; generated an explicit fallback graph.");
            return true;
        }

        var description = NormalizeDescription(TryGetLocText(potion.DynamicDescription));
        var defaultTarget = potion.TargetType.IsSingleTarget() ? "current_target" : ResolveDefaultTarget(potion.TargetType);
        if (TryCreateSpecialPotionFallback(entityId, potion, out result))
        {
            return true;
        }

        var steps = ParseCommonSteps(description, ModStudioEntityKind.Potion, defaultTarget);
        if (steps.Count == 0)
        {
            result = BuildDebugFallbackResult(
                ModStudioEntityKind.Potion,
                $"auto_potion_{entityId}",
                $"{TryGetLocText(potion.Title)} Native Import",
                description,
                "potion.on_use",
                "Potion did not expose a parseable on-use effect in the current fallback importer.");
            return true;
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

    private bool TryCreateSpecialCardFallback(string entityId, CardModel card, out NativeBehaviorAutoGraphResult? result)
    {
        result = entityId switch
        {
            "BYRDONIS_EGG" => BuildSourceResult(new NativeBehaviorGraphSource
            {
                EntityKind = ModStudioEntityKind.Card,
                GraphId = $"auto_card_{entityId}",
                Name = $"{entityId} Native Import",
                Description = string.Empty,
                TriggerId = "modify_rest_site_options",
                Steps =
                [
                    CreateSetStep("hook_result", bool.TrueString)
                ]
            }, "Approximated quest rest-site option injection as a hook-result graph."),
            "BLADE_DANCE" => BuildSourceResult(new NativeBehaviorGraphSource
            {
                EntityKind = ModStudioEntityKind.Card,
                GraphId = $"auto_card_{entityId}",
                Name = $"{entityId} Native Import",
                Description = string.Empty,
                TriggerId = "card.on_play",
                Steps =
                [
                    CreateCardStep("SHIV", 3m, PileType.Hand.ToString())
                ]
            }),
            "DEBRIS" => BuildNoOpResult(
                ModStudioEntityKind.Card,
                $"auto_card_{entityId}",
                $"{entityId} Native Import",
                string.Empty,
                "card.on_play",
                "Card is intentionally a playable no-op."),
            "ENTHRALLED" => BuildSourceResult(new NativeBehaviorGraphSource
            {
                EntityKind = ModStudioEntityKind.Card,
                GraphId = $"auto_card_{entityId}",
                Name = $"{entityId} Native Import",
                Description = string.Empty,
                TriggerId = "card.should_play",
                Steps =
                [
                    CreateSetStep("hook_result", bool.FalseString)
                ]
            }, "Approximated the hand-lock curse as a card.should_play guard."),
            "FRANTIC_ESCAPE" => BuildSourceResult(new NativeBehaviorGraphSource
            {
                EntityKind = ModStudioEntityKind.Card,
                GraphId = $"auto_card_{entityId}",
                Name = $"{entityId} Native Import",
                Description = string.Empty,
                TriggerId = "card.on_play",
                Steps =
                [
                    CreatePowerModifyAmountStep("SANDPIT_POWER", 1m),
                    CreateCardCostDeltaStep(1m)
                ]
            }, "Approximated Frantic Escape as sandpit power growth plus self cost increase."),
            "HIDDEN_GEM" => BuildNoOpResult(
                ModStudioEntityKind.Card,
                $"auto_card_{entityId}",
                $"{entityId} Native Import",
                string.Empty,
                "card.on_play",
                "Replay assignment still needs a dedicated card-selection runtime node."),
            "LANTERN_KEY" => BuildSourceResult(new NativeBehaviorGraphSource
            {
                EntityKind = ModStudioEntityKind.Card,
                GraphId = $"auto_card_{entityId}",
                Name = $"{entityId} Native Import",
                Description = string.Empty,
                TriggerId = "modify_unknown_map_point_room_types",
                Steps =
                [
                    CreateSetStep("hook_result", bool.TrueString)
                ]
            }, "Approximated unknown-room/event forcing as a custom room-type hook."),
            "MAD_SCIENCE" => BuildSourceResult(new NativeBehaviorGraphSource
            {
                EntityKind = ModStudioEntityKind.Card,
                GraphId = $"auto_card_{entityId}",
                Name = $"{entityId} Native Import",
                Description = string.Empty,
                TriggerId = "card.on_play",
                Steps =
                [
                    CreateDamageStep(12m, "current_target")
                ]
            }, "Approximated Mad Science with its default attack branch while polymorphic riders still need bespoke graphing."),
            "NORMALITY" => BuildSourceResult(new NativeBehaviorGraphSource
            {
                EntityKind = ModStudioEntityKind.Card,
                GraphId = $"auto_card_{entityId}",
                Name = $"{entityId} Native Import",
                Description = string.Empty,
                TriggerId = "card.should_play",
                Steps =
                [
                    CreateCompareStep("$state.CardsPlayedThisTurn", "3", "gte", "normality_locked"),
                    CreateBranchStep(
                        "normality_locked",
                        [CreateSetStep("hook_result", bool.FalseString)],
                        [CreateSetStep("hook_result", bool.TrueString)])
                ]
            }, "Approximated the play-prevention curse as a boolean hook graph."),
            "SPOILS_MAP" => BuildSourceResult(new NativeBehaviorGraphSource
            {
                EntityKind = ModStudioEntityKind.Card,
                GraphId = $"auto_card_{entityId}",
                Name = $"{entityId} Native Import",
                Description = string.Empty,
                TriggerId = "modify_generated_map",
                Steps =
                [
                    CreateMapReplaceStep("golden_path")
                ]
            }, "Approximated Spoils Map with the closest built-in generated-map replacement."),
            "SPORE_MIND" => BuildNoOpResult(
                ModStudioEntityKind.Card,
                $"auto_card_{entityId}",
                $"{entityId} Native Import",
                string.Empty,
                "card.on_play",
                "Card is intentionally a playable no-op curse."),
            "UP_MY_SLEEVE" => BuildSourceResult(new NativeBehaviorGraphSource
            {
                EntityKind = ModStudioEntityKind.Card,
                GraphId = $"auto_card_{entityId}",
                Name = $"{entityId} Native Import",
                Description = string.Empty,
                TriggerId = "card.on_play",
                Steps =
                [
                    CreateCardStep("SHIV", 3m, PileType.Hand.ToString())
                ]
            }),
            _ => null
        };

        return result != null;
    }

    private bool TryCreateSpecialPotionFallback(string entityId, PotionModel potion, out NativeBehaviorAutoGraphResult? result)
    {
        result = entityId switch
        {
            "POT_OF_GHOULS" => BuildSourceResult(new NativeBehaviorGraphSource
            {
                EntityKind = ModStudioEntityKind.Potion,
                GraphId = $"auto_potion_{entityId}",
                Name = $"{entityId} Native Import",
                Description = string.Empty,
                TriggerId = "potion.on_use",
                Steps =
                [
                    CreateCardStep("SOUL", 2m, PileType.Hand.ToString())
                ]
            }),
            "SOLDIERS_STEW" => BuildNoOpResult(
                ModStudioEntityKind.Potion,
                $"auto_potion_{entityId}",
                $"{entityId} Native Import",
                string.Empty,
                "potion.on_use",
                "Replay-count mutation still needs a targeted card-modifier runtime node."),
            _ => null
        };

        return result != null;
    }

    private NativeBehaviorAutoGraphResult BuildSourceResult(NativeBehaviorGraphSource source, params string[] notes)
    {
        var translation = _translator.Translate(source);
        translation.Graph.GraphId = source.GraphId;
        translation.Graph.Name = source.Name;
        return BuildFallbackResult(
            translation,
            source.TriggerId,
            notes.Where(note => !string.IsNullOrWhiteSpace(note)).ToArray());
    }

    private NativeBehaviorAutoGraphResult BuildNoOpResult(
        ModStudioEntityKind kind,
        string graphId,
        string name,
        string description,
        string triggerId,
        params string[] notes)
    {
        var graph = BehaviorGraphTemplateFactory.CreateDefaultScaffold(graphId, kind, name, description, triggerId);
        var cleanNotes = notes.Where(note => !string.IsNullOrWhiteSpace(note)).ToList();
        return new NativeBehaviorAutoGraphResult
        {
            Graph = graph,
            IsPartial = false,
            Notes = cleanNotes,
            SupportedStepKinds = Array.Empty<string>(),
            Strategy = NativeBehaviorAutoGraphStrategy.DescriptionFallback,
            Summary = string.Join(
                Environment.NewLine,
                new[]
                {
                    "Strategy: scaffold-only",
                    $"Trigger: {triggerId}",
                    cleanNotes.Count == 0 ? string.Empty : $"Notes: {string.Join(" | ", cleanNotes)}"
                }.Where(line => !string.IsNullOrWhiteSpace(line)))
        };
    }

    private static NativeBehaviorStep CreateCardStep(string cardId, decimal count, string targetPile)
    {
        return new NativeBehaviorStep
        {
            Kind = "combat.create_card",
            Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["card_id"] = cardId,
                ["count"] = count.ToString(CultureInfo.InvariantCulture),
                ["target_pile"] = targetPile
            }
        };
    }

    private static NativeBehaviorStep CreateDamageStep(decimal amount, string target)
    {
        return new NativeBehaviorStep
        {
            Kind = "combat.damage",
            Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = amount.ToString(CultureInfo.InvariantCulture),
                ["target"] = target,
                ["props"] = "Unpowered, Move"
            }
        };
    }

    private static NativeBehaviorStep CreatePowerModifyAmountStep(string powerId, decimal amount)
    {
        return new NativeBehaviorStep
        {
            Kind = "power.modify_amount",
            Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["power_id"] = powerId,
                ["amount"] = amount.ToString(CultureInfo.InvariantCulture),
                ["target"] = "current_target"
            }
        };
    }

    private static NativeBehaviorStep CreateCardCostDeltaStep(decimal amount)
    {
        return new NativeBehaviorStep
        {
            Kind = "card.set_cost_delta",
            Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = amount.ToString(CultureInfo.InvariantCulture)
            }
        };
    }

    private static NativeBehaviorStep CreateSetStep(string key, string value)
    {
        return new NativeBehaviorStep
        {
            Kind = "value.set",
            Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["key"] = key,
                ["value"] = value
            }
        };
    }

    private static NativeBehaviorStep CreateAddStep(string key, decimal delta)
    {
        return new NativeBehaviorStep
        {
            Kind = "value.add",
            Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["key"] = key,
                ["delta"] = delta.ToString(CultureInfo.InvariantCulture)
            }
        };
    }

    private static NativeBehaviorStep CreateMultiplyStep(string key, decimal factor)
    {
        return new NativeBehaviorStep
        {
            Kind = "value.multiply",
            Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["key"] = key,
                ["factor"] = factor.ToString(CultureInfo.InvariantCulture)
            }
        };
    }

    private static NativeBehaviorStep CreateCompareStep(string left, string right, string op, string resultKey)
    {
        return new NativeBehaviorStep
        {
            Kind = "value.compare",
            Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["left"] = left,
                ["right"] = right,
                ["operator"] = op,
                ["result_key"] = resultKey
            }
        };
    }

    private static NativeBehaviorStep CreateBranchStep(
        string conditionKey,
        List<NativeBehaviorStep> trueBranch,
        List<NativeBehaviorStep> falseBranch)
    {
        return new NativeBehaviorStep
        {
            Kind = "flow.branch",
            Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["condition_key"] = conditionKey
            },
            TrueBranch = trueBranch,
            FalseBranch = falseBranch
        };
    }

    private static NativeBehaviorStep CreateMapReplaceStep(string mapKind)
    {
        return new NativeBehaviorStep
        {
            Kind = "map.replace_generated",
            Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["map_kind"] = mapKind
            }
        };
    }

    private bool TryCreateRelicGraphFallback(string entityId, out NativeBehaviorAutoGraphResult? result)
    {
        result = null;
        var relic = ResolveRelicModel(entityId);
        if (relic == null)
        {
            return false;
        }

        if (TryCreateSpecialRelicFallback(entityId, relic, out result))
        {
            return true;
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

    private bool TryCreateSpecialRelicFallback(string entityId, RelicModel relic, out NativeBehaviorAutoGraphResult? result)
    {
        result = null;
        NativeBehaviorGraphSource? source = entityId switch
        {
            "RINGING_TRIANGLE" => new NativeBehaviorGraphSource
            {
                EntityKind = ModStudioEntityKind.Relic,
                GraphId = $"auto_relic_{entityId}",
                Name = $"{TryGetLocText(relic.Title)} Native Import",
                Description = NormalizeDescription(TryGetLocText(relic.DynamicDescription)),
                TriggerId = "relic.should_flush",
                Steps =
                [
                    new NativeBehaviorStep
                    {
                        Kind = "value.compare",
                        Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["left"] = "$state.CombatRound",
                            ["right"] = "1",
                            ["operator"] = "gt",
                            ["result_key"] = "round_above_one"
                        }
                    },
                    new NativeBehaviorStep
                    {
                        Kind = "flow.branch",
                        Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["condition_key"] = "round_above_one"
                        },
                        TrueBranch =
                        [
                            new NativeBehaviorStep
                            {
                                Kind = "value.set",
                                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                                {
                                    ["key"] = "hook_result",
                                    ["value"] = bool.TrueString
                                }
                            }
                        ],
                        FalseBranch =
                        [
                            new NativeBehaviorStep
                            {
                                Kind = "value.set",
                                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                                {
                                    ["key"] = "hook_result",
                                    ["value"] = bool.FalseString
                                }
                            }
                        ]
                    }
                ]
            },
            "MINIATURE_TENT" => new NativeBehaviorGraphSource
            {
                EntityKind = ModStudioEntityKind.Relic,
                GraphId = $"auto_relic_{entityId}",
                Name = $"{TryGetLocText(relic.Title)} Native Import",
                Description = NormalizeDescription(TryGetLocText(relic.DynamicDescription)),
                TriggerId = "relic.should_disable_remaining_rest_site_options",
                Steps =
                [
                    new NativeBehaviorStep
                    {
                        Kind = "value.set",
                        Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["key"] = "hook_result",
                            ["value"] = bool.FalseString
                        }
                    }
                ]
            },
            "REGAL_PILLOW" => new NativeBehaviorGraphSource
            {
                EntityKind = ModStudioEntityKind.Relic,
                GraphId = $"auto_relic_{entityId}",
                Name = $"{TryGetLocText(relic.Title)} Native Import",
                Description = NormalizeDescription(TryGetLocText(relic.DynamicDescription)),
                TriggerId = "relic.after_rest_site_heal",
                Steps =
                [
                    new NativeBehaviorStep
                    {
                        Kind = "value.compare",
                        Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["left"] = "$state.HookPlayerIsOwner",
                            ["right"] = bool.TrueString,
                            ["operator"] = "eq",
                            ["result_key"] = "rest_heal_owner"
                        }
                    },
                    new NativeBehaviorStep
                    {
                        Kind = "flow.branch",
                        Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["condition_key"] = "rest_heal_owner"
                        },
                        TrueBranch =
                        [
                            new NativeBehaviorStep
                            {
                                Kind = "value.set",
                                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                                {
                                    ["key"] = "Status",
                                    ["value"] = "Normal"
                                }
                            }
                        ]
                    }
                ]
            },
            "PEN_NIB" => new NativeBehaviorGraphSource
            {
                EntityKind = ModStudioEntityKind.Relic,
                GraphId = $"auto_relic_{entityId}",
                Name = $"{TryGetLocText(relic.Title)} Native Import",
                Description = NormalizeDescription(TryGetLocText(relic.DynamicDescription)),
                TriggerId = "relic.modify_damage_multiplicative",
                Steps =
                [
                    new NativeBehaviorStep
                    {
                        Kind = "value.compare",
                        Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["left"] = "$state.ShouldDoubleDamage",
                            ["right"] = bool.TrueString,
                            ["operator"] = "eq",
                            ["result_key"] = "pen_nib_double"
                        }
                    },
                    new NativeBehaviorStep
                    {
                        Kind = "flow.branch",
                        Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["condition_key"] = "pen_nib_double"
                        },
                        TrueBranch =
                        [
                            new NativeBehaviorStep
                            {
                                Kind = "modifier.damage_multiplicative",
                                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                                {
                                    ["amount"] = "2"
                                }
                            }
                        ]
                    }
                ]
            },
            "UNDYING_SIGIL" => new NativeBehaviorGraphSource
            {
                EntityKind = ModStudioEntityKind.Relic,
                GraphId = $"auto_relic_{entityId}",
                Name = $"{TryGetLocText(relic.Title)} Native Import",
                Description = NormalizeDescription(TryGetLocText(relic.DynamicDescription)),
                TriggerId = "relic.modify_damage_multiplicative",
                Steps =
                [
                    new NativeBehaviorStep
                    {
                        Kind = "value.compare",
                        Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["left"] = "$state.DealerCurrentHpAtOrBelowDoom",
                            ["right"] = bool.TrueString,
                            ["operator"] = "eq",
                            ["result_key"] = "sigil_threshold"
                        }
                    },
                    new NativeBehaviorStep
                    {
                        Kind = "flow.branch",
                        Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["condition_key"] = "sigil_threshold"
                        },
                        TrueBranch =
                        [
                            new NativeBehaviorStep
                            {
                                Kind = "modifier.damage_multiplicative",
                                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                                {
                                    ["amount"] = "0.5"
                                }
                            }
                        ]
                    }
                ]
            },
            "VITRUVIAN_MINION" => new NativeBehaviorGraphSource
            {
                EntityKind = ModStudioEntityKind.Relic,
                GraphId = $"auto_relic_{entityId}",
                Name = $"{TryGetLocText(relic.Title)} Native Import",
                Description = NormalizeDescription(TryGetLocText(relic.DynamicDescription)),
                TriggerId = "relic.modify_damage_multiplicative",
                Steps =
                [
                    new NativeBehaviorStep
                    {
                        Kind = "value.compare",
                        Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["left"] = "$state.CardHasMinionTag",
                            ["right"] = bool.TrueString,
                            ["operator"] = "eq",
                            ["result_key"] = "minion_tag"
                        }
                    },
                    new NativeBehaviorStep
                    {
                        Kind = "flow.branch",
                        Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["condition_key"] = "minion_tag"
                        },
                        TrueBranch =
                        [
                            new NativeBehaviorStep
                            {
                                Kind = "modifier.damage_multiplicative",
                                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                                {
                                    ["amount"] = "2"
                                }
                            }
                        ]
                    }
                ]
            },
            "BRILLIANT_SCARF" => CreateRelicSource(entityId, relic, "relic.modify_energy_cost_in_combat",
            [
                CreateSetStep("modifier_result", "0")
            ]),
            "CIRCLET" => CreateDebugRelicSource(entityId, relic, "relic.passive", "Passive stackable relic with no active graph hook."),
            "DEPRECATED_RELIC" => CreateDebugRelicSource(entityId, relic, "relic.passive", "Deprecated passive relic with no active graph hook."),
            "DINGY_RUG" => CreateRelicSource(entityId, relic, "relic.modify_card_reward_creation_options",
            [
                CreateSetStep("card_reward_options_modified", bool.TrueString)
            ]),
            "FAKE_MERCHANTS_RUG" => CreateDebugRelicSource(entityId, relic, "relic.passive", "Passive event relic with no active graph hook."),
            "GOLD_PLATED_CABLES" => CreateRelicSource(entityId, relic, "relic.modify_orb_passive_trigger_counts",
            [
                CreateSetStep("modifier_result", "$state.modifier_base"),
                CreateAddStep("modifier_result", 1m)
            ]),
            "LASTING_CANDY" => CreateRelicSource(entityId, relic, "relic.modify_card_reward_options",
            [
                CreateSetStep("card_reward_options_modified", bool.TrueString)
            ]),
            "LORDS_PARASOL" => CreateRelicSource(entityId, relic, "relic.after_room_entered",
            [
                CreateSetStep("hook_result", bool.TrueString)
            ]),
            "MEAT_CLEAVER" => CreateRelicSource(entityId, relic, "relic.modify_rest_site_options",
            [
                CreateSetStep("hook_result", bool.TrueString)
            ]),
            "MUMMIFIED_HAND" => CreateRelicSource(entityId, relic, "relic.after_card_played",
            [
                CreateCardCostDeltaStep(-1m)
            ]),
            "PAELS_WING" => CreateRelicSource(entityId, relic, "relic.modify_card_reward_alternatives",
            [
                CreateSetStep("hook_result", bool.TrueString)
            ]),
            "PAPER_KRANE" => CreateRelicSource(entityId, relic, "relic.modify_weak_multiplier",
            [
                CreateSetStep("modifier_result", "$state.modifier_base"),
                CreateAddStep("modifier_result", -0.15m)
            ]),
            "PAPER_PHROG" => CreateRelicSource(entityId, relic, "relic.modify_vulnerable_multiplier",
            [
                CreateSetStep("modifier_result", "$state.modifier_base"),
                CreateAddStep("modifier_result", 0.25m)
            ]),
            "RUINED_HELMET" => CreateRelicSource(entityId, relic, "relic.modify_power_amount_received",
            [
                CreateSetStep("modifier_result", "$state.modifier_base"),
                CreateMultiplyStep("modifier_result", 2m)
            ]),
            "SHOVEL" => CreateRelicSource(entityId, relic, "relic.modify_rest_site_options",
            [
                CreateSetStep("hook_result", bool.TrueString)
            ]),
            "SNECKO_SKULL" => CreateRelicSource(entityId, relic, "relic.modify_power_amount_given",
            [
                CreateSetStep("modifier_result", "$state.modifier_base"),
                CreateAddStep("modifier_result", 1m)
            ]),
            "THROWING_AXE" => CreateRelicSource(entityId, relic, "relic.modify_play_count",
            [
                CreateSetStep("modifier_result", "$state.modifier_base"),
                CreateAddStep("modifier_result", 1m)
            ]),
            "UNSETTLING_LAMP" => CreateRelicSource(entityId, relic, "relic.modify_power_amount_given",
            [
                CreateSetStep("modifier_result", "$state.modifier_base"),
                CreateMultiplyStep("modifier_result", 2m)
            ]),
            "VAMBRACE" => CreateRelicSource(entityId, relic, "relic.modify_block_multiplicative",
            [
                CreateSetStep("modifier_result", "2")
            ]),
            "WONGO_CUSTOMER_APPRECIATION_BADGE" => CreateDebugRelicSource(entityId, relic, "relic.passive", "Passive event relic with no active graph hook."),
            _ => null
        };

        if (source == null)
        {
            return false;
        }

        var translation = _translator.Translate(source);
        translation.Graph.GraphId = source.GraphId;
        translation.Graph.Name = source.Name;
        result = BuildFallbackResult(translation, source.TriggerId, Array.Empty<string>());
        return true;
    }

    private NativeBehaviorGraphSource CreateDebugRelicSource(string entityId, RelicModel relic, string triggerId, string message)
    {
        return new NativeBehaviorGraphSource
        {
            EntityKind = ModStudioEntityKind.Relic,
            GraphId = $"auto_relic_{entityId}",
            Name = $"{TryGetLocText(relic.Title)} Native Import",
            Description = NormalizeDescription(TryGetLocText(relic.DynamicDescription)),
            TriggerId = triggerId,
            Steps =
            [
                new NativeBehaviorStep
                {
                    Kind = "debug.log",
                    Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["message"] = message
                    }
                }
            ]
        };
    }

    private NativeBehaviorGraphSource CreateRelicSource(
        string entityId,
        RelicModel relic,
        string triggerId,
        IEnumerable<NativeBehaviorStep> steps)
    {
        return new NativeBehaviorGraphSource
        {
            EntityKind = ModStudioEntityKind.Relic,
            GraphId = $"auto_relic_{entityId}",
            Name = $"{TryGetLocText(relic.Title)} Native Import",
            Description = string.Empty,
            TriggerId = triggerId,
            Steps = steps.ToList()
        };
    }

    private bool TryCreateEventGraphFallback(string entityId, out NativeBehaviorAutoGraphResult? result)
    {
        result = null;
        var eventModel = ResolveEventModel(entityId);
        if (eventModel == null)
        {
            return false;
        }

        if (IsMinimalEventFallbackTarget(entityId))
        {
            result = BuildMinimalEventFallbackResult(entityId, eventModel);
            return true;
        }

        var graph = BehaviorGraphTemplateFactory.CreateDefaultScaffold(
            $"auto_event_{entityId}",
            ModStudioEntityKind.Event,
            $"{TryGetLocText(eventModel.Title)} Native Import",
            NormalizeDescription(TryGetLocText(eventModel.InitialDescription)),
            "event.on_begin");

        var exitNodeId = graph.Nodes.First(node => string.Equals(node.NodeType, "flow.exit", StringComparison.Ordinal)).NodeId;
        graph.Connections.Clear();

        var localizedPages = BuildEventLocalizedPages(eventModel);
        if (localizedPages.Count == 0)
        {
            return false;
        }

        var pageNodeIds = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var page in localizedPages.Values.OrderBy(page => page.PageId, StringComparer.Ordinal))
        {
            var pageNodeId = $"event_page_{page.PageId.ToLowerInvariant()}";
            pageNodeIds[page.PageId] = pageNodeId;
            graph.Nodes.Add(new BehaviorGraphNodeDefinition
            {
                NodeId = pageNodeId,
                NodeType = "event.page",
                DisplayName = string.IsNullOrWhiteSpace(page.Title) ? page.PageId : page.Title,
                Properties = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["page_id"] = page.PageId,
                    ["title"] = page.Title,
                    ["description"] = page.Description,
                    ["is_start"] = string.Equals(page.PageId, "INITIAL", StringComparison.OrdinalIgnoreCase).ToString()
                }
            });
        }

        var startPageId = localizedPages.ContainsKey("INITIAL")
            ? "INITIAL"
            : localizedPages.Keys.OrderBy(key => key, StringComparer.Ordinal).First();
        graph.Metadata["event_start_page_id"] = startPageId;
        graph.Connections.Add(new BehaviorGraphConnectionDefinition
        {
            FromNodeId = graph.EntryNodeId,
            FromPortId = "next",
            ToNodeId = pageNodeIds[startPageId],
            ToPortId = "in"
        });

        var optionNodeIndex = 0;
        foreach (var page in localizedPages.Values.OrderBy(page => page.PageId, StringComparer.Ordinal))
        {
            var optionOrder = new List<string>();
            foreach (var option in page.Options.Values.OrderBy(option => option.OptionId, StringComparer.Ordinal))
            {
                optionNodeIndex++;
                optionOrder.Add(option.OptionId);
                var optionNodeId = $"event_option_{optionNodeIndex}";
                var currentTailNodeId = optionNodeId;
                var currentTailPortId = "out";
                var hasNextPage = localizedPages.ContainsKey(option.OptionId) && !string.Equals(option.OptionId, page.PageId, StringComparison.Ordinal);

                graph.Nodes.Add(new BehaviorGraphNodeDefinition
                {
                    NodeId = optionNodeId,
                    NodeType = "event.option",
                    DisplayName = string.IsNullOrWhiteSpace(option.Title) ? option.OptionId : option.Title,
                    Properties = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["page_id"] = page.PageId,
                        ["option_id"] = option.OptionId,
                        ["title"] = option.Title,
                        ["description"] = option.Description,
                        ["next_page_id"] = hasNextPage ? option.OptionId : string.Empty,
                        ["is_proceed"] = (!hasNextPage).ToString()
                    }
                });

                graph.Connections.Add(new BehaviorGraphConnectionDefinition
                {
                    FromNodeId = pageNodeIds[page.PageId],
                    FromPortId = "next",
                    ToNodeId = optionNodeId,
                    ToPortId = "in"
                });

                var rewardSteps = ParseCommonSteps($"{option.Title} {option.Description}", ModStudioEntityKind.Event, "self");
                var rewardIndex = 0;
                foreach (var step in rewardSteps)
                {
                    if (!TryBuildEventActionNodeFromStep(step, optionNodeId, rewardIndex, out var actionNode))
                    {
                        continue;
                    }

                    graph.Nodes.Add(actionNode);
                    graph.Connections.Add(new BehaviorGraphConnectionDefinition
                    {
                        FromNodeId = currentTailNodeId,
                        FromPortId = currentTailPortId,
                        ToNodeId = actionNode.NodeId,
                        ToPortId = "in"
                    });
                    currentTailNodeId = actionNode.NodeId;
                    currentTailPortId = "out";
                    rewardIndex++;
                }

                if (hasNextPage)
                {
                    var gotoNodeId = $"{optionNodeId}_goto";
                    graph.Nodes.Add(new BehaviorGraphNodeDefinition
                    {
                        NodeId = gotoNodeId,
                        NodeType = "event.goto_page",
                        DisplayName = "Go To Page",
                        Properties = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["next_page_id"] = option.OptionId
                        }
                    });
                    graph.Connections.Add(new BehaviorGraphConnectionDefinition
                    {
                        FromNodeId = currentTailNodeId,
                        FromPortId = currentTailPortId,
                        ToNodeId = gotoNodeId,
                        ToPortId = "in"
                    });
                    currentTailNodeId = gotoNodeId;
                    currentTailPortId = "out";
                }
                else
                {
                    var proceedNodeId = $"{optionNodeId}_proceed";
                    graph.Nodes.Add(new BehaviorGraphNodeDefinition
                    {
                        NodeId = proceedNodeId,
                        NodeType = "event.proceed",
                        DisplayName = "Proceed"
                    });
                    graph.Connections.Add(new BehaviorGraphConnectionDefinition
                    {
                        FromNodeId = currentTailNodeId,
                        FromPortId = currentTailPortId,
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
                }
            }

            if (pageNodeIds.TryGetValue(page.PageId, out var pageNodeId) &&
                graph.Nodes.FirstOrDefault(node => string.Equals(node.NodeId, pageNodeId, StringComparison.Ordinal)) is { } graphPageNode)
            {
                graphPageNode.Properties["option_order"] = string.Join(",", optionOrder);
            }

            if (optionOrder.Count == 0)
            {
                var autoProceedNodeId = $"event_page_{page.PageId.ToLowerInvariant()}_proceed";
                graph.Nodes.Add(new BehaviorGraphNodeDefinition
                {
                    NodeId = autoProceedNodeId,
                    NodeType = "event.proceed",
                    DisplayName = "Proceed"
                });
                graph.Connections.Add(new BehaviorGraphConnectionDefinition
                {
                    FromNodeId = pageNodeIds[page.PageId],
                    FromPortId = "next",
                    ToNodeId = autoProceedNodeId,
                    ToPortId = "in"
                });
                graph.Connections.Add(new BehaviorGraphConnectionDefinition
                {
                    FromNodeId = autoProceedNodeId,
                    FromPortId = "out",
                    ToNodeId = exitNodeId,
                    ToPortId = "in"
                });
            }
        }

        result = new NativeBehaviorAutoGraphResult
        {
            Graph = graph,
            IsPartial = false,
            Notes = new[]
            {
                "Event auto-graph was scaffolded from all localized pages and options.",
                "Reward nodes are inferred from localized option descriptions when they match supported reward semantics.",
                "Complex page transitions, combat encounters, and non-localized runtime actions may still require manual review."
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

    private NativeBehaviorAutoGraphResult BuildMinimalEventFallbackResult(string entityId, EventModel eventModel)
    {
        var graph = BehaviorGraphTemplateFactory.CreateDefaultScaffold(
            $"auto_event_{entityId}",
            ModStudioEntityKind.Event,
            $"{TryGetLocText(eventModel.Title)} Native Import",
            NormalizeDescription(TryGetLocText(eventModel.InitialDescription)),
            "event.on_begin");

        var exitNodeId = graph.Nodes.First(node => string.Equals(node.NodeType, "flow.exit", StringComparison.Ordinal)).NodeId;
        var pageNodeId = $"event_page_{entityId.ToLowerInvariant()}";
        var proceedNodeId = $"event_page_{entityId.ToLowerInvariant()}_proceed";
        graph.Connections.Clear();
        graph.Metadata["event_start_page_id"] = "INITIAL";
        graph.Nodes.Add(new BehaviorGraphNodeDefinition
        {
            NodeId = pageNodeId,
            NodeType = "event.page",
            DisplayName = string.IsNullOrWhiteSpace(TryGetLocText(eventModel.Title)) ? entityId : TryGetLocText(eventModel.Title),
            Properties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["page_id"] = "INITIAL",
                ["title"] = TryGetLocText(eventModel.Title),
                ["description"] = NormalizeDescription(TryGetLocText(eventModel.InitialDescription)),
                ["is_start"] = bool.TrueString
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
            FromNodeId = graph.EntryNodeId,
            FromPortId = "next",
            ToNodeId = pageNodeId,
            ToPortId = "in"
        });
        graph.Connections.Add(new BehaviorGraphConnectionDefinition
        {
            FromNodeId = pageNodeId,
            FromPortId = "next",
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

        return new NativeBehaviorAutoGraphResult
        {
            Graph = graph,
            IsPartial = false,
            Notes = new[]
            {
                "Event auto-graph fell back to a minimal INITIAL page scaffold.",
                "This event does not expose a standard page/option localization structure and still requires manual authoring review."
            },
            SupportedStepKinds = graph.Nodes
                .Select(node => node.NodeType)
                .Where(nodeType => !string.IsNullOrWhiteSpace(nodeType) && nodeType is not "flow.entry" and not "flow.exit")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Strategy = NativeBehaviorAutoGraphStrategy.DescriptionFallback,
            Summary = "Strategy: event-minimal-fallback"
        };
    }

    private static Dictionary<string, EventLocalizedPage> BuildEventLocalizedPages(EventModel eventModel)
    {
        var pages = new Dictionary<string, EventLocalizedPage>(StringComparer.Ordinal);
        var table = NativeLocalizationTableFallback.GetTableEntries("events");
        var prefix = $"{eventModel.Id.Entry}.pages.";
        foreach (var key in table.Keys.Where(key => key.StartsWith(prefix, StringComparison.Ordinal)))
        {
            var remainder = key[prefix.Length..];
            var segments = remainder.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (segments.Length < 2)
            {
                continue;
            }

            var pageId = segments[0];
            if (!pages.TryGetValue(pageId, out var page))
            {
                page = new EventLocalizedPage { PageId = pageId };
                pages[pageId] = page;
            }

            if (segments.Length == 2 && string.Equals(segments[1], "description", StringComparison.OrdinalIgnoreCase))
            {
                page.Description = NormalizeDescription(NativeLocalizationTableFallback.TryGetText(new LocString("events", key)));
                continue;
            }

            if (segments.Length == 4 &&
                string.Equals(segments[1], "options", StringComparison.OrdinalIgnoreCase))
            {
                var optionId = segments[2];
                if (!page.Options.TryGetValue(optionId, out var option))
                {
                    option = new EventLocalizedOption { OptionId = optionId };
                    page.Options[optionId] = option;
                }

                var property = segments[3];
                var value = NormalizeDescription(NativeLocalizationTableFallback.TryGetText(new LocString("events", key)));
                if (string.Equals(property, "title", StringComparison.OrdinalIgnoreCase))
                {
                    option.Title = value;
                }
                else if (string.Equals(property, "description", StringComparison.OrdinalIgnoreCase))
                {
                    option.Description = value;
                }
            }
        }

        if (pages.TryGetValue("INITIAL", out var initialPage))
        {
            initialPage.Title = TryGetLocText(eventModel.Title);
            if (string.IsNullOrWhiteSpace(initialPage.Description))
            {
                initialPage.Description = NormalizeDescription(TryGetLocText(eventModel.InitialDescription));
            }
        }
        else if (pages.Count == 0)
        {
            var fallbackPage = new EventLocalizedPage
            {
                PageId = "INITIAL",
                Title = TryGetLocText(eventModel.Title),
                Description = NormalizeDescription(TryGetLocText(eventModel.InitialDescription))
            };

            foreach (var option in TryGetInitialOptions(eventModel))
            {
                var optionRootKey = ResolveEventOptionRootKey(option.TextKey, fallbackPage.Options.Count);
                var optionId = ResolveEventOptionId(optionRootKey, fallbackPage.Options.Count);
                fallbackPage.Options[optionId] = new EventLocalizedOption
                {
                    OptionId = optionId,
                    Title = NativeLocalizationTableFallback.TryGetText(option.Title),
                    Description = NativeLocalizationTableFallback.TryGetText(option.Description)
                };
            }

            pages[fallbackPage.PageId] = fallbackPage;
        }

        return pages;
    }

    private static IReadOnlyList<EventOption> TryGetInitialOptions(EventModel eventModel)
    {
        try
        {
            if (eventModel.MutableClone() is not EventModel mutableEvent)
            {
                return Array.Empty<EventOption>();
            }

            var method = typeof(EventModel).GetMethod(
                "GenerateInitialOptionsWrapper",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (method == null)
            {
                return Array.Empty<EventOption>();
            }

            return method.Invoke(mutableEvent, null) as IReadOnlyList<EventOption> ?? Array.Empty<EventOption>();
        }
        catch
        {
            return Array.Empty<EventOption>();
        }
    }

    private static bool TryBuildEventActionNodeFromStep(
        NativeBehaviorStep step,
        string optionNodeId,
        int rewardIndex,
        out BehaviorGraphNodeDefinition node)
    {
        node = null!;
        var kind = step.Kind.Trim().ToLowerInvariant();
        var nodeId = $"{optionNodeId}_reward_{rewardIndex + 1}";
        switch (kind)
        {
            case "player.gain_gold":
                node = BuildEventRewardNode(nodeId, "gold", GetStepParameter(step, "amount", "1"));
                return true;
            case "player.gain_energy":
                node = BuildEventRewardNode(nodeId, "energy", GetStepParameter(step, "amount", "1"));
                return true;
            case "player.gain_stars":
                node = BuildEventRewardNode(nodeId, "stars", GetStepParameter(step, "amount", "1"));
                return true;
            case "combat.gain_block":
                node = BuildEventRewardNode(nodeId, "block", GetStepParameter(step, "amount", "1"), GetStepParameter(step, "target", "self"), GetStepParameter(step, "props", "none"));
                return true;
            case "combat.heal":
                node = BuildEventRewardNode(nodeId, "heal", GetStepParameter(step, "amount", "1"), GetStepParameter(step, "target", "self"));
                return true;
            case "combat.damage":
                node = BuildEventRewardNode(nodeId, "damage", GetStepParameter(step, "amount", "1"), GetStepParameter(step, "target", "self"), GetStepParameter(step, "props", "none"));
                return true;
            case "combat.apply_power":
                node = BuildEventRewardNode(nodeId, "power", GetStepParameter(step, "amount", "1"), GetStepParameter(step, "target", "self"), rewardPowerId: GetStepParameter(step, "power_id", string.Empty));
                return true;
            case "player.gain_max_hp":
                node = BuildEventRewardNode(nodeId, "max_hp", GetStepParameter(step, "amount", "1"), GetStepParameter(step, "target", "self"));
                return true;
            default:
                return false;
        }
    }

    private static BehaviorGraphNodeDefinition BuildEventRewardNode(
        string nodeId,
        string rewardKind,
        string rewardAmount,
        string rewardTarget = "",
        string rewardProps = "",
        string rewardPowerId = "")
    {
        return new BehaviorGraphNodeDefinition
        {
            NodeId = nodeId,
            NodeType = "event.reward",
            DisplayName = $"Reward {rewardKind}",
            Properties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["reward_kind"] = rewardKind,
                ["reward_amount"] = rewardAmount,
                ["reward_target"] = rewardTarget,
                ["reward_props"] = rewardProps,
                ["reward_power_id"] = rewardPowerId
            }
        };
    }

    private static string ResolveEventOptionRootKey(string rawKey, int optionIndex)
    {
        if (string.IsNullOrWhiteSpace(rawKey))
        {
            return $"OPTION_{optionIndex + 1}";
        }

        var key = rawKey.Trim();
        if (key.EndsWith(".title", StringComparison.OrdinalIgnoreCase))
        {
            return key[..^".title".Length];
        }

        if (key.EndsWith(".description", StringComparison.OrdinalIgnoreCase))
        {
            return key[..^".description".Length];
        }

        return key;
    }

    private static string ResolveEventOptionId(string optionRootKey, int optionIndex)
    {
        if (string.IsNullOrWhiteSpace(optionRootKey))
        {
            return $"OPTION_{optionIndex + 1}";
        }

        var lastDot = optionRootKey.LastIndexOf('.');
        return lastDot >= 0 && lastDot < optionRootKey.Length - 1
            ? optionRootKey[(lastDot + 1)..]
            : optionRootKey;
    }

    private static string GetStepParameter(NativeBehaviorStep step, string key, string fallback)
    {
        return step.Parameters.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback;
    }

    private bool TryCreateEnchantmentGraphFallback(string entityId, out NativeBehaviorAutoGraphResult? result)
    {
        result = null;
        var enchantment = ResolveEnchantmentModel(entityId);
        if (enchantment == null)
        {
            return false;
        }

        var description = NormalizeDescription(TryGetLocText(enchantment.DynamicDescription));
        var steps = ParseCommonSteps(description, ModStudioEntityKind.Enchantment, "current_target");
        if (steps.Count == 0)
        {
            return false;
        }

        var notes = FindUnsupportedNotes(description);
        var source = new NativeBehaviorGraphSource
        {
            EntityKind = ModStudioEntityKind.Enchantment,
            GraphId = $"auto_enchantment_{entityId}",
            Name = $"{TryGetLocText(enchantment.Title)} Native Import",
            Description = description,
            TriggerId = "enchantment.on_play",
            Steps = steps
        };

        var translation = _translator.Translate(source);
        translation.Graph.GraphId = source.GraphId;
        translation.Graph.Name = source.Name;
        result = BuildFallbackResult(translation, "enchantment.on_play", notes);
        return true;
    }

    private static CardModel? ResolveCardModel(string entityId)
    {
        try
        {
            var card = ModelDb.AllCards.FirstOrDefault(candidate => string.Equals(candidate.Id.Entry, entityId, StringComparison.OrdinalIgnoreCase));
            if (card != null)
            {
                return card;
            }
        }
        catch
        {
        }

        return CreateModelByTypeName<CardModel>(entityId);
    }

    private static PotionModel? ResolvePotionModel(string entityId)
    {
        try
        {
            var potion = ModelDb.AllPotions.FirstOrDefault(candidate => string.Equals(candidate.Id.Entry, entityId, StringComparison.OrdinalIgnoreCase));
            if (potion != null)
            {
                return potion;
            }
        }
        catch
        {
        }

        return CreateModelByTypeName<PotionModel>(entityId);
    }

    private static RelicModel? ResolveRelicModel(string entityId)
    {
        try
        {
            var relic = ModelDb.AllRelics.FirstOrDefault(candidate => string.Equals(candidate.Id.Entry, entityId, StringComparison.OrdinalIgnoreCase));
            if (relic != null)
            {
                return relic;
            }
        }
        catch
        {
        }

        return CreateModelByTypeName<RelicModel>(entityId);
    }

    private static EventModel? ResolveEventModel(string entityId)
    {
        switch (NormalizeTypeLookup(entityId))
        {
            case "DEPRECATEDEVENT":
                return ResolveCanonicalEvent<MegaCrit.Sts2.Core.Models.Events.DeprecatedEvent>();
            case "DEPRECATEDANCIENTEVENT":
                return ResolveCanonicalEvent<MegaCrit.Sts2.Core.Models.Events.DeprecatedAncientEvent>();
            case "THEARCHITECT":
                return ResolveCanonicalEvent<MegaCrit.Sts2.Core.Models.Events.TheArchitect>();
        }

        try
        {
            var eventModel = ModelDb.AllEvents.FirstOrDefault(candidate => string.Equals(candidate.Id.Entry, entityId, StringComparison.OrdinalIgnoreCase));
            if (eventModel != null)
            {
                return eventModel;
            }

            var ancientEvent = ModelDb.AllAncients.FirstOrDefault(candidate => string.Equals(candidate.Id.Entry, entityId, StringComparison.OrdinalIgnoreCase));
            if (ancientEvent != null)
            {
                return ancientEvent;
            }
        }
        catch
        {
        }

        return CreateModelByTypeName<EventModel>(entityId);
    }

    private static TEvent? ResolveCanonicalEvent<TEvent>() where TEvent : EventModel
    {
        try
        {
            return ModelDb.GetByIdOrNull<TEvent>(ModelDb.GetId(typeof(TEvent)));
        }
        catch
        {
            return null;
        }
    }

    private static EnchantmentModel? ResolveEnchantmentModel(string entityId)
    {
        try
        {
            var enchantment = ModelDb.DebugEnchantments.FirstOrDefault(candidate => string.Equals(candidate.Id.Entry, entityId, StringComparison.OrdinalIgnoreCase));
            if (enchantment != null)
            {
                return enchantment;
            }
        }
        catch
        {
        }

        return CreateModelByTypeName<EnchantmentModel>(entityId);
    }

    private static TModel? CreateModelByTypeName<TModel>(string entityId) where TModel : AbstractModel
    {
        var normalizedId = NormalizeTypeLookup(entityId);
        var targetType = typeof(TModel).Assembly
            .GetTypes()
            .FirstOrDefault(type =>
                typeof(TModel).IsAssignableFrom(type) &&
                !type.IsAbstract &&
                type.GetConstructor(Type.EmptyTypes) != null &&
                string.Equals(NormalizeTypeLookup(type.Name), normalizedId, StringComparison.OrdinalIgnoreCase));
        if (targetType == null)
        {
            return null;
        }

        try
        {
            return Activator.CreateInstance(targetType) as TModel;
        }
        catch
        {
            return null;
        }
    }

    private static string NormalizeTypeLookup(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Replace("_", string.Empty, StringComparison.Ordinal).Trim().ToUpperInvariant();
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

    private NativeBehaviorAutoGraphResult BuildDebugFallbackResult(
        ModStudioEntityKind kind,
        string graphId,
        string name,
        string description,
        string triggerId,
        string message)
    {
        var translation = _translator.Translate(new NativeBehaviorGraphSource
        {
            EntityKind = kind,
            GraphId = graphId,
            Name = name,
            Description = description,
            TriggerId = triggerId,
            Steps =
            [
                new NativeBehaviorStep
                {
                    Kind = "debug.log",
                    Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["message"] = message
                    }
                }
            ]
        });

        translation.Graph.GraphId = graphId;
        translation.Graph.Name = name;
        return BuildFallbackResult(translation, triggerId, [message]);
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
        return NativeLocalizationTableFallback.TryGetText(locString);
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

    private static bool IsMinimalEventFallbackTarget(string entityId)
    {
        return NormalizeTypeLookup(entityId) is "DEPRECATEDEVENT" or "DEPRECATEDANCIENTEVENT" or "THEARCHITECT";
    }

    private sealed record StepMatch(int Index, NativeBehaviorStep Step);

    private sealed class EventLocalizedPage
    {
        public string PageId { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public Dictionary<string, EventLocalizedOption> Options { get; } = new(StringComparer.Ordinal);
    }

    private sealed class EventLocalizedOption
    {
        public string OptionId { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;
    }
}
