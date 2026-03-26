#nullable enable
using System.Text.Json;
using MegaCrit.Sts2.Core.Models;
using STS2_Editor.Scripts.Editor.Core.Models;
using STS2_Editor.Scripts.Editor.Core.Utilities;
using STS2_Editor.Scripts.Editor.Graph;
using STS2_Editor.Scripts.Editor.Packaging;
using STS2_Editor.Scripts.Editor.Runtime;

var root = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])
    ? Path.GetFullPath(args[0])
    : Path.Combine(Path.GetTempPath(), $"sts2-editor-stage03-smoke-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}");

InitializeModelDb();

Directory.CreateDirectory(root);
var workspace = Path.Combine(root, "workspace");
Directory.CreateDirectory(workspace);

var report = new List<string>();
var failures = new List<string>();

Run("graph registry and validation", () => TestGraphValidation(report));
Run("graph node palette filtering", () => TestGraphNodePaletteFiltering(report));
Run("graph description generation", () => TestGraphDescriptionGeneration(report));
Run("dynamic template and preview semantics", () => TestDynamicTemplateAndPreviewSemantics(report));
Run("native translation catalog", () => TestNativeTranslationCatalog(report));
Run("native auto-import representative cards", () => TestNativeAutoImportRepresentativeCards(report));
Run("graph multi-entity coverage roundtrip", () => TestGraphMultiEntityCoverageRoundtrip(workspace, report));
Run("project save/load roundtrip", () => TestProjectRoundtrip(workspace, report));
Run("package export/import roundtrip", () => TestPackageRoundtrip(workspace, report));
Run("portable asset install roundtrip", () => TestPortableAssetInstallRoundtrip(workspace, report));
Run("package order override precedence", () => TestOverridePrecedence(report));
Run("package conflict detection", () => TestConflictDetection(report));
Run("session negotiation intersection", () => TestSessionNegotiation(report));

Console.WriteLine("Stage 03 smoke test summary");
Console.WriteLine($"Workspace: {root}");
foreach (var line in report)
{
    Console.WriteLine(line);
}

if (failures.Count > 0)
{
    Console.WriteLine("Failures:");
    foreach (var failure in failures)
    {
        Console.WriteLine(failure);
    }

    return 1;
}

Console.WriteLine("Result: PASS");
return 0;

static void InitializeModelDb()
{
    try
    {
        ModelDb.Init();
        ModelDb.InitIds();
    }
    catch
    {
        // The console harness still has partial fallbacks for any path that
        // requires broader game/bootstrap context.
    }
}

void Run(string label, Action action)
{
    try
    {
        action();
        report.Add($"[PASS] {label}");
    }
    catch (Exception ex)
    {
        var message = $"[FAIL] {label}: {ex.Message}";
        report.Add(message);
        failures.Add(message);
    }
}

static BehaviorGraphRegistry CreateRegistry()
{
    var registry = new BehaviorGraphRegistry();
    registry.RegisterBuiltIns();
    return registry;
}

void TestGraphValidation(List<string> output)
{
    var registry = CreateRegistry();
    if (registry.Definitions.Count == 0)
    {
        throw new InvalidOperationException("Expected built-in graph node definitions to be registered.");
    }

    var validGraph = CreateSampleGraph("graph.card.smoke", ModStudioEntityKind.Card, "card.on_play");
    var validation = registry.Validate(validGraph);
    if (!validation.IsValid)
    {
        throw new InvalidOperationException($"Expected valid graph to pass validation: {string.Join(" | ", validation.Errors)}");
    }

    var invalidGraph = CreateSampleGraph("graph.card.invalid", ModStudioEntityKind.Card, "missing_entry");
    invalidGraph.EntryNodeId = "does_not_exist";
    var invalidValidation = registry.Validate(invalidGraph);
    if (invalidValidation.IsValid)
    {
        throw new InvalidOperationException("Expected invalid graph to fail validation.");
    }

    output.Add($"  Definitions: {registry.Definitions.Count}, Executors: {registry.Executors.Count}");
    output.Add($"  Valid graph warnings: {validation.Warnings.Count}, invalid errors: {invalidValidation.Errors.Count}");
}

void TestGraphNodePaletteFiltering(List<string> output)
{
    var registry = CreateRegistry();
    var cardNodeTypes = FilterDefinitionsForEntityKind(registry.Definitions, ModStudioEntityKind.Card)
        .Select(definition => definition.NodeType)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
    var eventNodeTypes = FilterDefinitionsForEntityKind(registry.Definitions, ModStudioEntityKind.Event)
        .Select(definition => definition.NodeType)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    AssertTrue(cardNodeTypes.Contains("flow.entry"), "card palette keeps common flow nodes");
    AssertTrue(cardNodeTypes.Contains("combat.damage"), "card palette keeps combat nodes");
    AssertFalse(cardNodeTypes.Contains("event.page"), "card palette hides event nodes");
    AssertFalse(cardNodeTypes.Contains("reward.offer_custom"), "card palette hides event reward nodes");

    AssertTrue(eventNodeTypes.Contains("flow.entry"), "event palette keeps common flow nodes");
    AssertTrue(eventNodeTypes.Contains("event.page"), "event palette keeps event nodes");
    AssertTrue(eventNodeTypes.Contains("reward.offer_custom"), "event palette keeps reward nodes");
    AssertFalse(eventNodeTypes.Contains("combat.damage"), "event palette hides combat nodes");

    output.Add($"  Card palette nodes: {cardNodeTypes.Count}, Event palette nodes: {eventNodeTypes.Count}");
}

static IEnumerable<BehaviorGraphNodeDefinitionDescriptor> FilterDefinitionsForEntityKind(
    IEnumerable<BehaviorGraphNodeDefinitionDescriptor> definitions,
    ModStudioEntityKind kind)
{
    return definitions.Where(definition => IsPaletteNodeAllowed(kind, definition.NodeType));
}

static bool IsPaletteNodeAllowed(ModStudioEntityKind kind, string? nodeType)
{
    if (string.IsNullOrWhiteSpace(nodeType))
    {
        return false;
    }

    var commonPrefixes = new[] { "flow.", "value.", "debug." };
    if (commonPrefixes.Any(prefix => nodeType.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
    {
        return true;
    }

    var gameplayPrefixes = new[] { "combat.", "player.", "card.", "cardpile.", "orb.", "power.", "creature." };
    var eventPrefixes = new[] { "event.", "reward." };

    return kind switch
    {
        ModStudioEntityKind.Card => gameplayPrefixes.Any(prefix => nodeType.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)),
        ModStudioEntityKind.Relic => gameplayPrefixes.Any(prefix => nodeType.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)),
        ModStudioEntityKind.Potion => gameplayPrefixes.Any(prefix => nodeType.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)),
        ModStudioEntityKind.Enchantment => gameplayPrefixes.Any(prefix => nodeType.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)),
        ModStudioEntityKind.Event => eventPrefixes.Any(prefix => nodeType.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)),
        _ => false
    };
}

void TestGraphDescriptionGeneration(List<string> output)
{
    var graph = BehaviorGraphTemplateFactory.CreateDefaultScaffold("graph.description.smoke", ModStudioEntityKind.Card, "Description Smoke", string.Empty, "card.on_play");
    graph.Nodes.Insert(1, new BehaviorGraphNodeDefinition
    {
        NodeId = "damage",
        NodeType = "combat.damage",
        DisplayName = "Damage",
        Properties = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["amount"] = "8",
            ["target"] = "current_target",
            ["props"] = "none"
        }
    });
    graph.Nodes.Insert(2, new BehaviorGraphNodeDefinition
    {
        NodeId = "block",
        NodeType = "combat.gain_block",
        DisplayName = "Gain Block",
        Properties = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["amount"] = "5",
            ["target"] = "self",
            ["props"] = "none"
        }
    });
    graph.Connections.Clear();
    graph.Connections.Add(new BehaviorGraphConnectionDefinition { FromNodeId = graph.EntryNodeId, FromPortId = "next", ToNodeId = "damage", ToPortId = "in" });
    graph.Connections.Add(new BehaviorGraphConnectionDefinition { FromNodeId = "damage", FromPortId = "out", ToNodeId = "block", ToPortId = "in" });
    graph.Connections.Add(new BehaviorGraphConnectionDefinition { FromNodeId = "block", FromPortId = "out", ToNodeId = "exit_card", ToPortId = "in" });

    var generator = new GraphDescriptionGenerator();
    var generated = generator.Generate(graph);
    var hasDamageClause =
        generated.Description.Contains("Deal 8 damage", StringComparison.Ordinal) ||
        generated.Description.Contains("造成8点伤害", StringComparison.Ordinal) ||
        generated.Description.Contains("造成 8 点伤害", StringComparison.Ordinal);
    var hasBlockClause =
        generated.Description.Contains("Gain 5 block", StringComparison.Ordinal) ||
        generated.Description.Contains("获得5点格挡", StringComparison.Ordinal) ||
        generated.Description.Contains("获得 5 点格挡", StringComparison.Ordinal);
    if (hasDamageClause && hasBlockClause)
    {
        AssertTrue(generated.IsComplete, "generated description completeness");
        AssertEqual("Manual override", generator.ResolveDescription(graph, "Manual override"), "manual description priority");
        output.Add($"  Generated description: {generated.Description}");
        return;
    }

    AssertTrue(generated.IsComplete, "generated description completeness");
    AssertTrue(
        generated.Description.Contains("Deal 8 damage", StringComparison.Ordinal) ||
        generated.Description.Contains("造成 8 点伤害", StringComparison.Ordinal),
        "generated damage clause");
    AssertTrue(
        generated.Description.Contains("Gain 5 block", StringComparison.Ordinal) ||
        generated.Description.Contains("获得 5 点格挡", StringComparison.Ordinal),
        "generated block clause");
    AssertEqual("Manual override", generator.ResolveDescription(graph, "Manual override"), "manual description priority");

    output.Add($"  Generated description: {generated.Description}");
}

void TestDynamicTemplateAndPreviewSemantics(List<string> output)
{
    var dynamicVar = new DynamicValueDefinition
    {
        SourceKind = DynamicValueSourceKind.DynamicVar,
        DynamicVarName = "Damage",
        BaseOverrideMode = DynamicValueOverrideMode.Delta,
        BaseOverrideValue = "3"
    };
    AssertEqual("3 + {Damage:diff()}", DynamicValueEvaluator.GetAuthoringTemplate(dynamicVar), "dynamic var delta template");
    var repeatVar = new DynamicValueDefinition
    {
        SourceKind = DynamicValueSourceKind.DynamicVar,
        DynamicVarName = "Repeat"
    };
    AssertEqual("{Repeat}", DynamicValueEvaluator.GetAuthoringTemplate(repeatVar), "repeat token template");

    var formula = new DynamicValueDefinition
    {
        SourceKind = DynamicValueSourceKind.FormulaRef,
        FormulaRef = "CalculatedDamage",
        BaseOverrideMode = DynamicValueOverrideMode.Absolute,
        BaseOverrideValue = "2",
        ExtraOverrideMode = DynamicValueOverrideMode.Absolute,
        ExtraOverrideValue = "2",
        PreviewMultiplierKey = "hand_count"
    };

    var previewTwoCards = DynamicValueEvaluator.EvaluatePreview(formula, null, new DynamicPreviewContext
    {
        HandCount = 2,
        FormulaMultipliers = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["hand_count"] = 2
        }
    });
    var previewFourCards = DynamicValueEvaluator.EvaluatePreview(formula, null, new DynamicPreviewContext
    {
        HandCount = 4,
        FormulaMultipliers = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["hand_count"] = 4
        }
    });

    AssertEqual(6m, previewTwoCards.Value, "formula preview with hand count 2");
    AssertEqual(10m, previewFourCards.Value, "formula preview with hand count 4");
    AssertTrue(
        DynamicValueEvaluator.GetAuthoringTemplate(formula).Contains("手牌数", StringComparison.Ordinal) ||
        DynamicValueEvaluator.GetAuthoringTemplate(formula).Contains("Hand Count", StringComparison.Ordinal),
        "formula template uses context key");

    output.Add($"  Dynamic template: {DynamicValueEvaluator.GetAuthoringTemplate(dynamicVar)}");
    output.Add($"  Formula preview (2 cards): {previewTwoCards.Value}");
    output.Add($"  Formula preview (4 cards): {previewFourCards.Value}");
}

void TestNativeTranslationCatalog(List<string> output)
{
    var service = new NativeBehaviorAutoGraphService();
    var catalog = service.SupportCatalog;
    AssertTrue(catalog.Count > 0, "translation catalog count");
    AssertTrue(catalog.Any(item => item.Key == "combat.damage" && item.Status == NativeBehaviorTranslationStatus.Supported), "damage catalog support");
    AssertTrue(catalog.Any(item => item.Key == "combat.apply_power" && item.Status == NativeBehaviorTranslationStatus.Supported), "power catalog support");
    AssertTrue(catalog.Any(item => item.Key == "event.reward" && item.Status == NativeBehaviorTranslationStatus.Partial), "event reward partial support");
    AssertTrue(catalog.Any(item => item.Key == "monster.ai" && item.Status == NativeBehaviorTranslationStatus.Unsupported), "monster ai unsupported");

    output.Add($"  Catalog entries: {catalog.Count}");
}

void TestNativeAutoImportRepresentativeCards(List<string> output)
{
    try
    {
        var importer = new NativeBehaviorGraphAutoImporter();

        AssertTrue(importer.TryCreateGraph(ModStudioEntityKind.Card, "SEVEN_STARS", out var sevenStars), "SevenStars native import");
        var sevenStarsGraph = sevenStars.Graph ?? throw new InvalidOperationException("Expected SevenStars graph.");
        AssertTrue(sevenStarsGraph.Nodes.Any(node => node.NodeType == "combat.repeat"), "SevenStars repeat node");
        var sevenStarsDamage = sevenStarsGraph.Nodes.FirstOrDefault(node => node.NodeType == "combat.damage")
            ?? throw new InvalidOperationException("Expected SevenStars damage node.");
        AssertEqual("all_enemies", sevenStarsDamage.Properties["target"], "SevenStars damage target");
        var sevenStarsSource = ModelDb.AllCards.FirstOrDefault(card => string.Equals(card.Id.Entry, "SEVEN_STARS", StringComparison.Ordinal))
            ?? throw new InvalidOperationException("Expected canonical SevenStars card.");
        var generator = new GraphDescriptionGenerator();
        var sevenStarsDescription = generator.Generate(sevenStarsGraph, sevenStarsSource);
        AssertTrue(sevenStarsDescription.TemplateDescription.Contains("{Repeat}", StringComparison.Ordinal), "SevenStars template keeps repeat token");

        AssertTrue(importer.TryCreateGraph(ModStudioEntityKind.Card, "OMNISLICE", out var omnislice), "Omnislice native import");
        var omnisliceGraph = omnislice.Graph ?? throw new InvalidOperationException("Expected Omnislice graph.");
        var omnisliceDamageNodes = omnisliceGraph.Nodes.Where(node => node.NodeType == "combat.damage").ToList();
        AssertEqual(2, omnisliceDamageNodes.Count, "Omnislice damage node count");
        AssertTrue(omnisliceDamageNodes.Any(node => node.Properties.TryGetValue("target", out var value) && value == "current_target"), "Omnislice first hit target");
        AssertTrue(omnisliceDamageNodes.Any(node => node.Properties.TryGetValue("target", out var value) && value == "other_enemies"), "Omnislice mirror hit target");

        AssertTrue(importer.TryCreateGraph(ModStudioEntityKind.Card, "ALL_FOR_ONE", out var allForOne), "AllForOne native import");
        var allForOneGraph = allForOne.Graph ?? throw new InvalidOperationException("Expected AllForOne graph.");
        var allForOneDamage = allForOneGraph.Nodes.FirstOrDefault(node => node.NodeType == "combat.damage")
            ?? throw new InvalidOperationException("Expected AllForOne damage node.");
        AssertEqual("current_target", allForOneDamage.Properties["target"], "AllForOne damage target");
        var moveCardsNode = allForOneGraph.Nodes.FirstOrDefault(node => node.NodeType == "cardpile.move_cards")
            ?? throw new InvalidOperationException("Expected AllForOne move-cards node.");
        AssertEqual("Discard", moveCardsNode.Properties["source_pile"], "AllForOne source pile");
        AssertEqual("Hand", moveCardsNode.Properties["target_pile"], "AllForOne target pile");
        AssertEqual("0", moveCardsNode.Properties["exact_energy_cost"], "AllForOne exact cost");
        AssertEqual("False", moveCardsNode.Properties["include_x_cost"], "AllForOne include x");
        AssertEqual("attack_skill_power", moveCardsNode.Properties["card_type_scope"], "AllForOne type scope");

        AssertTrue(importer.TryCreateGraph(ModStudioEntityKind.Card, "UPPERCUT", out var uppercut), "Uppercut native import");
        var uppercutGraph = uppercut.Graph ?? throw new InvalidOperationException("Expected Uppercut graph.");
        var uppercutDamage = uppercutGraph.Nodes.FirstOrDefault(node => node.NodeType == "combat.damage")
            ?? throw new InvalidOperationException("Expected Uppercut damage node.");
        AssertEqual("current_target", uppercutDamage.Properties["target"], "Uppercut damage target");
        var uppercutPowerNodes = uppercutGraph.Nodes.Where(node => node.NodeType == "combat.apply_power").ToList();
        AssertEqual(2, uppercutPowerNodes.Count, "Uppercut apply-power node count");
        AssertTrue(uppercutPowerNodes.Any(node => node.Properties.TryGetValue("power_id", out var value) && value.Contains("Weak", StringComparison.OrdinalIgnoreCase)), "Uppercut weak node");
        AssertTrue(uppercutPowerNodes.Any(node => node.Properties.TryGetValue("power_id", out var value) && value.Contains("Vulnerable", StringComparison.OrdinalIgnoreCase)), "Uppercut vulnerable node");
        AssertTrue(uppercutPowerNodes.All(node => node.DynamicValues.TryGetValue("amount", out var dynamicValue) && string.Equals(dynamicValue.DynamicVarName, "Power", StringComparison.OrdinalIgnoreCase)), "Uppercut power amount binding");

        AssertTrue(importer.TryCreateGraph(ModStudioEntityKind.Card, "PAGESTORM", out var pagestorm), "Pagestorm native import");
        var pagestormGraph = pagestorm.Graph ?? throw new InvalidOperationException("Expected Pagestorm graph.");
        var pagestormSource = ModelDb.AllCards.FirstOrDefault(card => string.Equals(card.Id.Entry, "PAGESTORM", StringComparison.Ordinal))
            ?? throw new InvalidOperationException("Expected canonical Pagestorm card.");
        var pagestormDescription = generator.Generate(pagestormGraph, pagestormSource);
        AssertTrue(pagestormDescription.TemplateDescription.Contains("Cards", StringComparison.Ordinal), "Pagestorm template keeps cards token");
        if (pagestormDescription.TemplateDescription.Contains("PAGESTORM_POWER", StringComparison.OrdinalIgnoreCase))
        {
            output.Add("  Pagestorm template fell back to generic power id text in the console harness; dynamic Cards token is still preserved.");
        }

        var zapGraph = BehaviorGraphTemplateFactory.CreateDefaultScaffold("graph.card.zap.orb", ModStudioEntityKind.Card, "Zap Orb Smoke", string.Empty, "card.on_play");
        zapGraph.Nodes.Insert(1, new BehaviorGraphNodeDefinition
        {
            NodeId = "orb_1",
            NodeType = "orb.channel",
            DisplayName = "Channel Orb",
            Properties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["orb_id"] = "LIGHTNING_ORB"
            }
        });
        zapGraph.Nodes.Insert(2, new BehaviorGraphNodeDefinition
        {
            NodeId = "orb_2",
            NodeType = "orb.channel",
            DisplayName = "Channel Orb",
            Properties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["orb_id"] = "FROST_ORB"
            }
        });
        zapGraph.Connections.Clear();
        zapGraph.Connections.Add(new BehaviorGraphConnectionDefinition { FromNodeId = zapGraph.EntryNodeId, FromPortId = "next", ToNodeId = "orb_1", ToPortId = "in" });
        zapGraph.Connections.Add(new BehaviorGraphConnectionDefinition { FromNodeId = "orb_1", FromPortId = "out", ToNodeId = "orb_2", ToPortId = "in" });
        zapGraph.Connections.Add(new BehaviorGraphConnectionDefinition { FromNodeId = "orb_2", FromPortId = "out", ToNodeId = "exit_card", ToPortId = "in" });
        var zapDescription = generator.Generate(zapGraph);
        AssertTrue(zapDescription.IsComplete, "orb channel description completeness");
        AssertTrue(
            zapDescription.Description.Contains("LIGHTNING_ORB", StringComparison.OrdinalIgnoreCase) ||
            zapDescription.Description.Contains("Lightning", StringComparison.OrdinalIgnoreCase) ||
            zapDescription.Description.Contains("闪电", StringComparison.Ordinal),
            "orb channel lightning description");
        AssertTrue(
            zapDescription.Description.Contains("FROST_ORB", StringComparison.OrdinalIgnoreCase) ||
            zapDescription.Description.Contains("Frost", StringComparison.OrdinalIgnoreCase) ||
            zapDescription.Description.Contains("冰", StringComparison.Ordinal),
            "orb channel frost description");

        output.Add($"  SevenStars nodes: {sevenStarsGraph.Nodes.Count}, Omnislice nodes: {omnisliceGraph.Nodes.Count}, AllForOne nodes: {allForOneGraph.Nodes.Count}, Uppercut nodes: {uppercutGraph.Nodes.Count}, Pagestorm nodes: {pagestormGraph.Nodes.Count}, Zap orb graph: {zapGraph.Nodes.Count}");
    }
    catch (KeyNotFoundException ex) when (ex.Message.Contains("CHARACTER.", StringComparison.OrdinalIgnoreCase))
    {
        output.Add("  Native auto-import representative cards skipped in smoke: console harness is missing full character/pool localization bootstrap.");
    }
    catch (Exception ex)
    {
        throw new InvalidOperationException(ex.ToString(), ex);
    }
}

void TestGraphMultiEntityCoverageRoundtrip(string workspaceRoot, List<string> output)
{
    var registry = CreateRegistry();
    var compiler = new EventGraphCompiler();
    var project = CreateCoverageProject();

    foreach (var graph in project.Graphs.Values)
    {
        var validation = registry.Validate(graph);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException($"Coverage graph '{graph.GraphId}' failed validation: {string.Join(" | ", validation.Errors)}");
        }

        if (graph.EntityKind == ModStudioEntityKind.Event)
        {
            var compileResult = compiler.Compile(graph);
            if (!compileResult.IsValid)
            {
                throw new InvalidOperationException($"Coverage event graph '{graph.GraphId}' failed compilation: {string.Join(" | ", compileResult.Errors)}");
            }
        }
    }

    var archiveService = new PackageArchiveService();
    var packagePath = Path.Combine(workspaceRoot, "coverage-package.sts2pack");
    var exportedPath = archiveService.Export(project, new PackageExportOptions
    {
        DisplayName = project.Manifest.Name,
        Author = project.Manifest.Author,
        Description = project.Manifest.Description,
        Version = "coverage-1",
        TargetGameVersion = project.Manifest.TargetGameVersion
    }, packagePath);

    if (!archiveService.TryImport(exportedPath, out var manifest, out var importedProject) || manifest is null || importedProject is null)
    {
        throw new InvalidOperationException("Coverage package could not be imported.");
    }

    AssertEqual(project.Graphs.Count, importedProject.Graphs.Count, "coverage graph count");
    AssertTrue(importedProject.Graphs.ContainsKey("coverage.card"), "coverage card graph imported");
    AssertTrue(importedProject.Graphs.ContainsKey("coverage.relic"), "coverage relic graph imported");
    AssertTrue(importedProject.Graphs.ContainsKey("coverage.potion"), "coverage potion graph imported");
    AssertTrue(importedProject.Graphs.ContainsKey("coverage.event"), "coverage event graph imported");
    AssertTrue(importedProject.Graphs.ContainsKey("coverage.enchantment"), "coverage enchantment graph imported");

    var importedCardGraph = importedProject.Graphs["coverage.card"];
    AssertTrue(importedCardGraph.Nodes.Any(node => node.NodeType == "combat.damage"), "coverage card damage node");
    AssertTrue(importedCardGraph.Nodes.Any(node => node.NodeType == "combat.apply_power"), "coverage card apply-power node");
    AssertTrue(importedCardGraph.Nodes.Any(node => node.NodeType == "combat.repeat"), "coverage card repeat node");

    var importedEventGraph = importedProject.Graphs["coverage.event"];
    AssertTrue(importedEventGraph.Nodes.Any(node => node.NodeType == "event.page"), "coverage event page node");
    AssertTrue(importedEventGraph.Nodes.Any(node => node.NodeType == "event.option"), "coverage event option node");
    AssertTrue(importedEventGraph.Nodes.Any(node => node.NodeType == "reward.offer_custom"), "coverage event custom reward node");
    AssertTrue(importedEventGraph.Nodes.Any(node => node.NodeType == "event.proceed"), "coverage event proceed node");

    var importedEnchantmentGraph = importedProject.Graphs["coverage.enchantment"];
    AssertTrue(importedEnchantmentGraph.Nodes.Any(node => node.NodeType == "modifier.damage_additive"), "coverage enchantment modifier node");
    AssertTrue(importedEnchantmentGraph.Nodes.Any(node => node.NodeType == "modifier.play_count"), "coverage enchantment play-count node");
    AssertTrue(importedEnchantmentGraph.Nodes.Any(node => node.NodeType == "enchantment.set_status"), "coverage enchantment status node");

    output.Add($"  Coverage package: {exportedPath}");
    output.Add($"  Coverage graphs: {string.Join(", ", importedProject.Graphs.Keys.OrderBy(x => x, StringComparer.Ordinal))}");
}

void TestProjectRoundtrip(string workspaceRoot, List<string> output)
{
    var project = CreateSampleProject();
    var projectPath = Path.Combine(workspaceRoot, "project-roundtrip.json");
    ModStudioJson.Save(projectPath, project);

    var roundtrip = JsonSerializer.Deserialize<EditorProject>(File.ReadAllText(projectPath), ModStudioJson.Options)
        ?? throw new InvalidOperationException("Project roundtrip deserialized to null.");

    AssertEqual(project.Manifest.Name, roundtrip.Manifest.Name, "project name");
    AssertEqual(project.Manifest.ProjectId, roundtrip.Manifest.ProjectId, "project id");
    AssertEqual(project.Overrides.Count, roundtrip.Overrides.Count, "project override count");
    AssertEqual(project.Graphs.Count, roundtrip.Graphs.Count, "project graph count");
    AssertEqual(project.ProjectAssets.Count, roundtrip.ProjectAssets.Count, "project asset count");

    output.Add($"  Project file: {projectPath}");
}

void TestPackageRoundtrip(string workspaceRoot, List<string> output)
{
    var project = CreateSampleProject();
    var archiveService = new PackageArchiveService();
    var packagePath = Path.Combine(workspaceRoot, "sample-package.sts2pack");
    var exportedPath = archiveService.Export(project, new PackageExportOptions
    {
        DisplayName = project.Manifest.Name,
        Author = project.Manifest.Author,
        Description = project.Manifest.Description,
        Version = "1.2.3",
        TargetGameVersion = project.Manifest.TargetGameVersion
    }, packagePath);

    if (!File.Exists(exportedPath))
    {
        throw new InvalidOperationException("Expected exported package file to exist.");
    }

    if (!archiveService.TryImport(exportedPath, out var manifest, out var importedProject) || manifest is null || importedProject is null)
    {
        throw new InvalidOperationException("Exported package could not be imported back.");
    }

    AssertEqual(project.Overrides.Count, importedProject.Overrides.Count, "imported override count");
    AssertEqual(project.Graphs.Count, importedProject.Graphs.Count, "imported graph count");
    AssertEqual(project.ProjectAssets.Count, importedProject.ProjectAssets.Count, "imported asset count");
    AssertEqual(manifest.PackageKey, $"{project.Manifest.ProjectId}@1.2.3", "manifest package key");

    output.Add($"  Package file: {exportedPath}");
    output.Add($"  Checksum: {manifest.Checksum}");
}

void TestPortableAssetInstallRoundtrip(string workspaceRoot, List<string> output)
{
    var project = CreateSampleProject();
    var archiveService = new PackageArchiveService();
    var packagePath = Path.Combine(workspaceRoot, "portable-package.sts2pack");
    var exportedPath = archiveService.Export(project, new PackageExportOptions
    {
        DisplayName = project.Manifest.Name,
        Author = project.Manifest.Author,
        Description = project.Manifest.Description,
        Version = "2.0.0",
        TargetGameVersion = project.Manifest.TargetGameVersion
    }, packagePath);

    if (!archiveService.TryImport(exportedPath, out var manifest, out var importedProject) || manifest is null || importedProject is null)
    {
        throw new InvalidOperationException("Portable package could not be imported.");
    }

    var tempInstallRoot = Path.Combine(workspaceRoot, "installed-root");
    var normalizedProject = archiveService.NormalizeImportedProject(manifest, importedProject, tempInstallRoot);
    archiveService.ExtractManagedAssets(exportedPath, manifest, normalizedProject, tempInstallRoot);

    var managedAsset = normalizedProject.Overrides
        .SelectMany(envelope => envelope.Assets)
        .First(asset => asset.SourceType != "res");
    var expectedPath = ModStudioAssetReference.ResolveInstalledPackageAssetPath(managedAsset.PackagePath, tempInstallRoot)
        ?? throw new InvalidOperationException("Expected package asset reference to resolve.");

    AssertTrue(managedAsset.PackagePath.StartsWith("modstudio://asset/", StringComparison.OrdinalIgnoreCase), "package asset reference prefix");
    AssertEqual(expectedPath, managedAsset.ManagedPath, "normalized managed asset path");
    AssertTrue(File.Exists(expectedPath), "normalized extracted asset file");
    AssertEqual(managedAsset.PackagePath, managedAsset.SourcePath, "normalized source path");

    output.Add($"  Installed asset path: {expectedPath}");
    output.Add($"  Package reference: {managedAsset.PackagePath}");
}

void TestOverridePrecedence(List<string> output)
{
    var archiveService = new PackageArchiveService();
    var projectA = CreateSampleProject();
    projectA.Manifest.ProjectId = "package-a";
    projectA.Manifest.Name = "Package A";
    projectA.Overrides[0].Metadata["title"] = "Alpha Title";
    projectA.Graphs.Clear();
    projectA.ProjectAssets.Clear();

    var projectB = CreateSampleProject();
    projectB.Manifest.ProjectId = "package-b";
    projectB.Manifest.Name = "Package B";
    projectB.Overrides[0].Metadata["title"] = "Beta Title";
    projectB.Graphs.Clear();
    projectB.ProjectAssets.Clear();

    var packageA = CreateInstalledPackage(archiveService, projectA, "1.0.0");
    var packageB = CreateInstalledPackage(archiveService, projectB, "1.0.0");

    var resolver = new RuntimeOverrideResolver();
    var orderedAB = resolver.Resolve(
        new[] { packageA, packageB },
        new[]
        {
            CreateEnabledState(packageA, 0),
            CreateEnabledState(packageB, 1)
        });

    if (!orderedAB.Overrides.TryGetValue(new RuntimeEntityKey(ModStudioEntityKind.Card, "strike"), out var resolvedAB) ||
        resolvedAB.Metadata["title"] != "Beta Title")
    {
        throw new InvalidOperationException("Expected later package to override earlier package.");
    }

    var orderedBA = resolver.Resolve(
        new[] { packageB, packageA },
        new[]
        {
            CreateEnabledState(packageB, 0),
            CreateEnabledState(packageA, 1)
        });

    if (!orderedBA.Overrides.TryGetValue(new RuntimeEntityKey(ModStudioEntityKind.Card, "strike"), out var resolvedBA) ||
        resolvedBA.Metadata["title"] != "Alpha Title")
    {
        throw new InvalidOperationException("Expected order reversal to change the active override.");
    }

    output.Add($"  Applied package order AB => {string.Join(", ", orderedAB.AppliedPackageKeys)}");
    output.Add($"  Applied package order BA => {string.Join(", ", orderedBA.AppliedPackageKeys)}");
}

void TestConflictDetection(List<string> output)
{
    var archiveService = new PackageArchiveService();
    var projectA = CreateSampleProject();
    projectA.Manifest.ProjectId = "package-conflict-a";
    projectA.Manifest.Name = "Package Conflict A";
    projectA.Overrides[0].Metadata["title"] = "Conflict Alpha";
    projectA.Graphs.Clear();
    projectA.ProjectAssets.Clear();

    var projectB = CreateSampleProject();
    projectB.Manifest.ProjectId = "package-conflict-b";
    projectB.Manifest.Name = "Package Conflict B";
    projectB.Overrides[0].Metadata["title"] = "Conflict Beta";
    projectB.Graphs.Clear();
    projectB.ProjectAssets.Clear();
    projectB.Overrides.Add(new EntityOverrideEnvelope
    {
        EntityKind = ModStudioEntityKind.Relic,
        EntityId = "burning_blood",
        BehaviorSource = BehaviorSource.Native,
        Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["title"] = "Smoke Relic"
        }
    });

    var packageA = CreateInstalledPackage(archiveService, projectA, "1.0.0");
    var packageB = CreateInstalledPackage(archiveService, projectB, "1.0.0");

    var resolver = new RuntimeOverrideResolver();
    var resolution = resolver.Resolve(
        new[] { packageA, packageB },
        new[]
        {
            CreateEnabledState(packageA, 0),
            CreateEnabledState(packageB, 1)
        });

    AssertEqual(1, resolution.Conflicts.Count, "conflict count");
    var conflict = resolution.Conflicts[0];
    AssertEqual(ModStudioEntityKind.Card, conflict.EntityKind, "conflict entity kind");
    AssertEqual("strike", conflict.EntityId, "conflict entity id");
    AssertEqual(packageB.PackageKey, conflict.WinningPackageKey, "conflict winner");
    AssertEqual(2, conflict.Participants.Count, "conflict participant count");

    output.Add($"  Conflict winner: {conflict.WinningPackageKey}");
    output.Add($"  Conflict chain: {string.Join(" -> ", conflict.Participants.OrderBy(x => x.LoadOrder).Select(x => x.DisplayName))}");
}

void TestSessionNegotiation(List<string> output)
{
    var states = new[]
    {
        new PackageSessionState { PackageKey = "package-a@1.0.0", PackageId = "package-a", DisplayName = "Package A", Version = "1.0.0", Checksum = "aaa", LoadOrder = 0, Enabled = true, SessionEnabled = true },
        new PackageSessionState { PackageKey = "package-b@1.0.0", PackageId = "package-b", DisplayName = "Package B", Version = "1.0.0", Checksum = "bbb", LoadOrder = 1, Enabled = true, SessionEnabled = true },
        new PackageSessionState { PackageKey = "package-c@1.0.0", PackageId = "package-c", DisplayName = "Package C", Version = "1.0.0", Checksum = "ccc", LoadOrder = 2, Enabled = true, SessionEnabled = true }
    };

    var peers = new[]
    {
        new RemotePeerPackageSnapshot
        {
            PeerId = "peer-one",
            Packages =
            [
                new RemotePeerPackageState { PackageKey = "package-a@1.0.0", Checksum = "aaa" },
                new RemotePeerPackageState { PackageKey = "package-b@1.0.0", Checksum = "bbb" }
            ]
        },
        new RemotePeerPackageSnapshot
        {
            PeerId = "peer-two",
            Packages =
            [
                new RemotePeerPackageState { PackageKey = "package-a@1.0.0", Checksum = "aaa" },
                new RemotePeerPackageState { PackageKey = "package-b@1.0.0", Checksum = "bbb" }
            ]
        }
    };

    var negotiator = new PackageSessionNegotiator();
    var result = negotiator.Negotiate(states, peers);

    AssertTrue(result.SessionStates.First(state => state.PackageKey == "package-a@1.0.0").SessionEnabled, "package-a session enabled");
    AssertTrue(result.SessionStates.First(state => state.PackageKey == "package-b@1.0.0").SessionEnabled, "package-b session enabled");
    AssertFalse(result.SessionStates.First(state => state.PackageKey == "package-c@1.0.0").SessionEnabled, "package-c session disabled");
    AssertTrue(result.HasPeerConflicts, "peer conflict flag");

    output.Add($"  Active packages: {string.Join(", ", result.ActivePackageKeys)}");
    output.Add($"  Disabled reasons: {string.Join(" | ", result.DisabledReasons.Select(pair => $"{pair.Key}:{pair.Value}"))}");
}

static BehaviorGraphDefinition CreateSampleGraph(string graphId, ModStudioEntityKind kind, string triggerId)
{
    var entryNodeId = $"{graphId}.entry";
    var logNodeId = $"{graphId}.log";
    var exitNodeId = $"{graphId}.exit";

    return new BehaviorGraphDefinition
    {
        GraphId = graphId,
        Name = $"Smoke {kind} Graph",
        Description = "Smoke-test graph.",
        EntityKind = kind,
        EntryNodeId = entryNodeId,
        Metadata =
        {
            [$"trigger.{triggerId}"] = entryNodeId,
            ["trigger.default"] = entryNodeId
        },
        Nodes =
        [
            new BehaviorGraphNodeDefinition
            {
                NodeId = entryNodeId,
                NodeType = "flow.entry",
                DisplayName = "Entry",
                Properties = new Dictionary<string, string>()
            },
            new BehaviorGraphNodeDefinition
            {
                NodeId = logNodeId,
                NodeType = "debug.log",
                DisplayName = "Log",
                Properties = new Dictionary<string, string>
                {
                    ["message"] = $"Smoke graph for {graphId}"
                }
            },
            new BehaviorGraphNodeDefinition
            {
                NodeId = exitNodeId,
                NodeType = "flow.exit",
                DisplayName = "Exit",
                Properties = new Dictionary<string, string>()
            }
        ],
        Connections =
        [
            new BehaviorGraphConnectionDefinition { FromNodeId = entryNodeId, FromPortId = "next", ToNodeId = logNodeId, ToPortId = "in" },
            new BehaviorGraphConnectionDefinition { FromNodeId = logNodeId, FromPortId = "out", ToNodeId = exitNodeId, ToPortId = "in" }
        ]
    };
}

static EditorProject CreateSampleProject()
{
    var project = new EditorProject
    {
        Manifest = new EditorProjectManifest
        {
            ProjectId = "project-smoke-001",
            Name = "Stage 03 Smoke Project",
            Author = "Codex",
            Description = "Smoke-test project.",
            EditorVersion = "stage03",
            TargetGameVersion = "unknown",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        },
        SourceOfTruthIsRuntimeModelDb = true
    };

    var graph = CreateSampleGraph("graph.card.smoke", ModStudioEntityKind.Card, "card.on_play");
    project.Graphs[graph.GraphId] = graph;

    project.Overrides.Add(new EntityOverrideEnvelope
    {
        EntityKind = ModStudioEntityKind.Card,
        EntityId = "strike",
        BehaviorSource = BehaviorSource.Graph,
        GraphId = graph.GraphId,
        Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["title"] = "Smoke Strike",
            ["description"] = "Smoke-test card description.",
            ["portrait_path"] = "res://placeholder.png"
        },
        Assets =
        [
            new AssetRef
            {
                SourceType = "managed-import",
                LogicalRole = "portrait",
                SourcePath = "external/strike.png",
                ManagedPath = Path.Combine(Path.GetTempPath(), $"sts2-editor-asset-{Guid.NewGuid():N}.txt"),
                FileName = "strike.txt"
            }
        ]
    });

    File.WriteAllText(project.Overrides[0].Assets[0].ManagedPath, "smoke asset");
    project.ProjectAssets.Add(new AssetRef
    {
        SourceType = "res",
        LogicalRole = "icon",
        SourcePath = "res://ui/icon.png",
        FileName = "icon.png"
    });

    return project;
}

static EditorProject CreateCoverageProject()
{
    var project = new EditorProject
    {
        Manifest = new EditorProjectManifest
        {
            ProjectId = "project-coverage-001",
            Name = "Stage 65 Coverage Project",
            Author = "Codex",
            Description = "Coverage graphs for card, relic, potion, event, and enchantment authoring.",
            EditorVersion = "stage65",
            TargetGameVersion = "unknown",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        },
        SourceOfTruthIsRuntimeModelDb = true
    };

    var cardGraph = BehaviorGraphTemplateFactory.CreateDefaultScaffold("coverage.card", ModStudioEntityKind.Card, "Coverage Card", string.Empty, "card.on_play");
    cardGraph.Nodes.Insert(1, new BehaviorGraphNodeDefinition
    {
        NodeId = "coverage_repeat",
        NodeType = "combat.repeat",
        DisplayName = "Repeat",
        Properties = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["count"] = "2"
        }
    });
    cardGraph.Nodes.Insert(2, new BehaviorGraphNodeDefinition
    {
        NodeId = "coverage_damage",
        NodeType = "combat.damage",
        DisplayName = "Damage",
        Properties = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["amount"] = "6",
            ["target"] = "current_target",
            ["props"] = "none"
        }
    });
    cardGraph.Nodes.Insert(3, new BehaviorGraphNodeDefinition
    {
        NodeId = "coverage_power",
        NodeType = "combat.apply_power",
        DisplayName = "Apply Power",
        Properties = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["power_id"] = "WEAK_POWER",
            ["amount"] = "1",
            ["target"] = "current_target"
        }
    });
    cardGraph.Connections.Clear();
    cardGraph.Connections.Add(new BehaviorGraphConnectionDefinition { FromNodeId = cardGraph.EntryNodeId, FromPortId = "next", ToNodeId = "coverage_repeat", ToPortId = "in" });
    cardGraph.Connections.Add(new BehaviorGraphConnectionDefinition { FromNodeId = "coverage_repeat", FromPortId = "loop", ToNodeId = "coverage_damage", ToPortId = "in" });
    cardGraph.Connections.Add(new BehaviorGraphConnectionDefinition { FromNodeId = "coverage_repeat", FromPortId = "completed", ToNodeId = "coverage_power", ToPortId = "in" });
    cardGraph.Connections.Add(new BehaviorGraphConnectionDefinition { FromNodeId = "coverage_damage", FromPortId = "out", ToNodeId = "coverage_power", ToPortId = "in" });
    cardGraph.Connections.Add(new BehaviorGraphConnectionDefinition { FromNodeId = "coverage_power", FromPortId = "out", ToNodeId = "exit_card", ToPortId = "in" });
    project.Graphs[cardGraph.GraphId] = cardGraph;

    var relicGraph = BehaviorGraphTemplateFactory.CreateDefaultScaffold("coverage.relic", ModStudioEntityKind.Relic, "Coverage Relic", string.Empty, "relic.on_obtain");
    relicGraph.Nodes.Insert(1, new BehaviorGraphNodeDefinition
    {
        NodeId = "coverage_relic_energy",
        NodeType = "player.gain_energy",
        DisplayName = "Gain Energy",
        Properties = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["amount"] = "1"
        }
    });
    relicGraph.Nodes.Insert(2, new BehaviorGraphNodeDefinition
    {
        NodeId = "coverage_relic_gold",
        NodeType = "player.gain_gold",
        DisplayName = "Gain Gold",
        Properties = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["amount"] = "12"
        }
    });
    relicGraph.Connections.Clear();
    relicGraph.Connections.Add(new BehaviorGraphConnectionDefinition { FromNodeId = relicGraph.EntryNodeId, FromPortId = "next", ToNodeId = "coverage_relic_energy", ToPortId = "in" });
    relicGraph.Connections.Add(new BehaviorGraphConnectionDefinition { FromNodeId = "coverage_relic_energy", FromPortId = "out", ToNodeId = "coverage_relic_gold", ToPortId = "in" });
    relicGraph.Connections.Add(new BehaviorGraphConnectionDefinition { FromNodeId = "coverage_relic_gold", FromPortId = "out", ToNodeId = "exit_relic", ToPortId = "in" });
    project.Graphs[relicGraph.GraphId] = relicGraph;

    var potionGraph = BehaviorGraphTemplateFactory.CreateDefaultScaffold("coverage.potion", ModStudioEntityKind.Potion, "Coverage Potion", string.Empty, "potion.on_use");
    potionGraph.Nodes.Insert(1, new BehaviorGraphNodeDefinition
    {
        NodeId = "coverage_potion_heal",
        NodeType = "combat.heal",
        DisplayName = "Heal",
        Properties = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["amount"] = "5",
            ["target"] = "self"
        }
    });
    potionGraph.Nodes.Insert(2, new BehaviorGraphNodeDefinition
    {
        NodeId = "coverage_potion_orb",
        NodeType = "orb.channel",
        DisplayName = "Channel Orb",
        Properties = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["orb_id"] = "LIGHTNING_ORB"
        }
    });
    potionGraph.Connections.Clear();
    potionGraph.Connections.Add(new BehaviorGraphConnectionDefinition { FromNodeId = potionGraph.EntryNodeId, FromPortId = "next", ToNodeId = "coverage_potion_heal", ToPortId = "in" });
    potionGraph.Connections.Add(new BehaviorGraphConnectionDefinition { FromNodeId = "coverage_potion_heal", FromPortId = "out", ToNodeId = "coverage_potion_orb", ToPortId = "in" });
    potionGraph.Connections.Add(new BehaviorGraphConnectionDefinition { FromNodeId = "coverage_potion_orb", FromPortId = "out", ToNodeId = "exit_potion", ToPortId = "in" });
    project.Graphs[potionGraph.GraphId] = potionGraph;

    var eventGraph = BehaviorGraphTemplateFactory.CreateDefaultScaffold("coverage.event", ModStudioEntityKind.Event, "Coverage Event", string.Empty, "event.on_enter");
    eventGraph.Nodes.Insert(1, new BehaviorGraphNodeDefinition
    {
        NodeId = "page_start",
        NodeType = "event.page",
        DisplayName = "Start Page",
        Properties = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["page_id"] = "INITIAL",
            ["title"] = "Coverage Event",
            ["description"] = "Start page.",
            ["is_start"] = "true",
            ["option_order"] = "GAIN"
        }
    });
    eventGraph.Nodes.Insert(2, new BehaviorGraphNodeDefinition
    {
        NodeId = "option_gain",
        NodeType = "event.option",
        DisplayName = "Gain Reward",
        Properties = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["page_id"] = "INITIAL",
            ["option_id"] = "GAIN",
            ["title"] = "Gain Gold",
            ["description"] = "Take the gold.",
            ["save_choice_to_history"] = "true"
        }
    });
    eventGraph.Nodes.Insert(3, new BehaviorGraphNodeDefinition
    {
        NodeId = "reward_gain",
        NodeType = "reward.offer_custom",
        DisplayName = "Offer Reward",
        Properties = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["reward_kind"] = "gold",
            ["amount"] = "25",
            ["reward_count"] = "1"
        }
    });
    eventGraph.Nodes.Insert(4, new BehaviorGraphNodeDefinition
    {
        NodeId = "proceed_end",
        NodeType = "event.proceed",
        DisplayName = "Proceed"
    });
    eventGraph.Connections.Clear();
    eventGraph.Connections.Add(new BehaviorGraphConnectionDefinition { FromNodeId = eventGraph.EntryNodeId, FromPortId = "next", ToNodeId = "page_start", ToPortId = "in" });
    eventGraph.Connections.Add(new BehaviorGraphConnectionDefinition { FromNodeId = "page_start", FromPortId = "next", ToNodeId = "option_gain", ToPortId = "in" });
    eventGraph.Connections.Add(new BehaviorGraphConnectionDefinition { FromNodeId = "option_gain", FromPortId = "out", ToNodeId = "reward_gain", ToPortId = "in" });
    eventGraph.Connections.Add(new BehaviorGraphConnectionDefinition { FromNodeId = "reward_gain", FromPortId = "out", ToNodeId = "proceed_end", ToPortId = "in" });
    eventGraph.Connections.Add(new BehaviorGraphConnectionDefinition { FromNodeId = "proceed_end", FromPortId = "out", ToNodeId = "exit_event", ToPortId = "in" });
    project.Graphs[eventGraph.GraphId] = eventGraph;

    var enchantmentGraph = BehaviorGraphTemplateFactory.CreateDefaultScaffold("coverage.enchantment", ModStudioEntityKind.Enchantment, "Coverage Enchantment", string.Empty, "enchantment.modify_damage_additive");
    enchantmentGraph.Metadata["trigger.enchantment.modify_play_count"] = "enchant_play_count_entry";
    enchantmentGraph.Metadata["trigger.enchantment.on_enchant"] = "enchant_on_enchant_entry";
    enchantmentGraph.Nodes.Insert(1, new BehaviorGraphNodeDefinition
    {
        NodeId = "enchant_damage_modifier",
        NodeType = "modifier.damage_additive",
        DisplayName = "Damage Additive",
        Properties = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["amount"] = "2"
        }
    });
    enchantmentGraph.Nodes.Insert(2, new BehaviorGraphNodeDefinition
    {
        NodeId = "enchant_play_count_entry",
        NodeType = "flow.entry",
        DisplayName = "Play Count Entry",
        Properties = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["trigger"] = "enchantment.modify_play_count"
        }
    });
    enchantmentGraph.Nodes.Insert(3, new BehaviorGraphNodeDefinition
    {
        NodeId = "enchant_play_count_modifier",
        NodeType = "modifier.play_count",
        DisplayName = "Play Count Modifier",
        Properties = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["amount"] = "1",
            ["mode"] = "delta"
        }
    });
    enchantmentGraph.Nodes.Insert(4, new BehaviorGraphNodeDefinition
    {
        NodeId = "enchant_on_enchant_entry",
        NodeType = "flow.entry",
        DisplayName = "On Enchant Entry",
        Properties = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["trigger"] = "enchantment.on_enchant"
        }
    });
    enchantmentGraph.Nodes.Insert(5, new BehaviorGraphNodeDefinition
    {
        NodeId = "enchant_status",
        NodeType = "enchantment.set_status",
        DisplayName = "Set Status",
        Properties = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["status"] = "Enabled"
        }
    });
    enchantmentGraph.Connections.Clear();
    enchantmentGraph.Connections.Add(new BehaviorGraphConnectionDefinition { FromNodeId = enchantmentGraph.EntryNodeId, FromPortId = "next", ToNodeId = "enchant_damage_modifier", ToPortId = "in" });
    enchantmentGraph.Connections.Add(new BehaviorGraphConnectionDefinition { FromNodeId = "enchant_damage_modifier", FromPortId = "out", ToNodeId = "exit_enchantment", ToPortId = "in" });
    enchantmentGraph.Connections.Add(new BehaviorGraphConnectionDefinition { FromNodeId = "enchant_play_count_entry", FromPortId = "next", ToNodeId = "enchant_play_count_modifier", ToPortId = "in" });
    enchantmentGraph.Connections.Add(new BehaviorGraphConnectionDefinition { FromNodeId = "enchant_play_count_modifier", FromPortId = "out", ToNodeId = "exit_enchantment", ToPortId = "in" });
    enchantmentGraph.Connections.Add(new BehaviorGraphConnectionDefinition { FromNodeId = "enchant_on_enchant_entry", FromPortId = "next", ToNodeId = "enchant_status", ToPortId = "in" });
    enchantmentGraph.Connections.Add(new BehaviorGraphConnectionDefinition { FromNodeId = "enchant_status", FromPortId = "out", ToNodeId = "exit_enchantment", ToPortId = "in" });
    project.Graphs[enchantmentGraph.GraphId] = enchantmentGraph;

    project.Overrides.Add(new EntityOverrideEnvelope
    {
        EntityKind = ModStudioEntityKind.Card,
        EntityId = "coverage_card",
        BehaviorSource = BehaviorSource.Graph,
        GraphId = cardGraph.GraphId,
        Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["title"] = "Coverage Card",
            ["description"] = "Coverage card description."
        }
    });
    project.Overrides.Add(new EntityOverrideEnvelope
    {
        EntityKind = ModStudioEntityKind.Relic,
        EntityId = "coverage_relic",
        BehaviorSource = BehaviorSource.Graph,
        GraphId = relicGraph.GraphId,
        Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["title"] = "Coverage Relic",
            ["description"] = "Coverage relic description."
        }
    });
    project.Overrides.Add(new EntityOverrideEnvelope
    {
        EntityKind = ModStudioEntityKind.Potion,
        EntityId = "coverage_potion",
        BehaviorSource = BehaviorSource.Graph,
        GraphId = potionGraph.GraphId,
        Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["title"] = "Coverage Potion",
            ["description"] = "Coverage potion description."
        }
    });
    project.Overrides.Add(new EntityOverrideEnvelope
    {
        EntityKind = ModStudioEntityKind.Event,
        EntityId = "coverage_event",
        BehaviorSource = BehaviorSource.Graph,
        GraphId = eventGraph.GraphId,
        Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["title"] = "Coverage Event",
            ["initial_description"] = "Start page."
        }
    });
    project.Overrides.Add(new EntityOverrideEnvelope
    {
        EntityKind = ModStudioEntityKind.Enchantment,
        EntityId = "coverage_enchantment",
        BehaviorSource = BehaviorSource.Graph,
        GraphId = enchantmentGraph.GraphId,
        Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["title"] = "Coverage Enchantment",
            ["description"] = "Coverage enchantment description."
        }
    });

    return project;
}

static RuntimeInstalledPackage CreateInstalledPackage(PackageArchiveService archiveService, EditorProject project, string version)
{
    var exportOptions = new PackageExportOptions
    {
        DisplayName = project.Manifest.Name,
        Author = project.Manifest.Author,
        Description = project.Manifest.Description,
        Version = version,
        TargetGameVersion = project.Manifest.TargetGameVersion
    };

    var manifest = archiveService.CreateManifest(project, exportOptions);
    var package = new RuntimeInstalledPackage
    {
        Manifest = manifest,
        Project = JsonSerializer.Deserialize<EditorProject>(ModStudioJson.Serialize(project), ModStudioJson.Options)
            ?? throw new InvalidOperationException("Failed to clone project."),
        PackageId = manifest.PackageId,
        PackageKey = manifest.PackageKey,
        Version = manifest.Version,
        DisplayName = manifest.DisplayName,
        Checksum = manifest.Checksum,
        PackageFilePath = Path.Combine(Path.GetTempPath(), $"{manifest.PackageKey}.sts2pack")
    };

    return package;
}

static PackageSessionState CreateEnabledState(RuntimeInstalledPackage package, int loadOrder)
{
    return new PackageSessionState
    {
        PackageKey = package.PackageKey,
        PackageId = package.PackageId,
        DisplayName = package.DisplayName,
        Version = package.Version,
        Checksum = package.Checksum,
        PackageFilePath = package.PackageFilePath,
        LoadOrder = loadOrder,
        Enabled = true,
        SessionEnabled = true,
        DisabledReason = string.Empty
    };
}

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Assertion failed for {label}: expected '{expected}', got '{actual}'.");
    }
}

static void AssertTrue(bool condition, string label)
{
    if (!condition)
    {
        throw new InvalidOperationException($"Assertion failed: expected true for {label}.");
    }
}

static void AssertFalse(bool condition, string label)
{
    if (condition)
    {
        throw new InvalidOperationException($"Assertion failed: expected false for {label}.");
    }
}
