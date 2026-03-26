#nullable enable
using System.Text.Json;
using STS2_Editor.Scripts.Editor.Core.Models;
using STS2_Editor.Scripts.Editor.Core.Utilities;
using STS2_Editor.Scripts.Editor.Graph;
using STS2_Editor.Scripts.Editor.Packaging;
using STS2_Editor.Scripts.Editor.Runtime;

var repoRoot = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])
    ? Path.GetFullPath(args[0])
    : FindRepoRoot(Environment.CurrentDirectory);

var coverageRoot = Path.Combine(repoRoot, "coverage");
var aggregateRoot = Path.Combine(coverageRoot, "aggregate");
var kindRoots = new Dictionary<ModStudioEntityKind, string>
{
    [ModStudioEntityKind.Card] = Path.Combine(coverageRoot, "cards"),
    [ModStudioEntityKind.Relic] = Path.Combine(coverageRoot, "relics"),
    [ModStudioEntityKind.Potion] = Path.Combine(coverageRoot, "potions"),
    [ModStudioEntityKind.Event] = Path.Combine(coverageRoot, "events"),
    [ModStudioEntityKind.Enchantment] = Path.Combine(coverageRoot, "enchantments")
};

Directory.CreateDirectory(coverageRoot);
Directory.CreateDirectory(aggregateRoot);
foreach (var root in kindRoots.Values)
{
    Directory.CreateDirectory(root);
}

var registry = CreateRegistry();
var compiler = new EventGraphCompiler();
var archiveService = new PackageArchiveService();
var project = CreateCoverageProject();
var reportLines = new List<string>();

ValidateProject(project, registry, compiler);

var aggregateProjectPath = Path.Combine(aggregateRoot, "coverage-project.json");
ModStudioJson.Save(aggregateProjectPath, project);
var aggregatePackagePath = archiveService.Export(project, new PackageExportOptions
{
    DisplayName = project.Manifest.Name,
    Author = project.Manifest.Author,
    Description = project.Manifest.Description,
    Version = "coverage-1.0.0",
    TargetGameVersion = project.Manifest.TargetGameVersion
}, Path.Combine(aggregateRoot, "coverage-package.sts2pack"));

if (!archiveService.TryImport(aggregatePackagePath, out var aggregateManifest, out var aggregateImportedProject) ||
    aggregateManifest is null ||
    aggregateImportedProject is null)
{
    throw new InvalidOperationException("Aggregate coverage package could not be re-imported.");
}

var aggregateJsonOptions = new JsonSerializerOptions(ModStudioJson.Options)
{
    WriteIndented = true
};

reportLines.Add("# Stage 65 Coverage Artifacts");
reportLines.Add("");
reportLines.Add($"Generated at UTC `{DateTimeOffset.UtcNow:O}`");
reportLines.Add("");
reportLines.Add("## Aggregate");
reportLines.Add($"- Project: `{Path.GetRelativePath(repoRoot, aggregateProjectPath).Replace('\\', '/')}`");
reportLines.Add($"- Package: `{Path.GetRelativePath(repoRoot, aggregatePackagePath).Replace('\\', '/')}`");
reportLines.Add($"- Graph Count: `{project.Graphs.Count}`");
reportLines.Add($"- Override Count: `{project.Overrides.Count}`");
reportLines.Add("");
reportLines.Add("## Per Kind");

foreach (var graph in project.Graphs.Values.OrderBy(graph => graph.EntityKind?.ToString() ?? string.Empty, StringComparer.Ordinal).ThenBy(graph => graph.GraphId, StringComparer.Ordinal))
{
    if (graph.EntityKind is not { } entityKind || !kindRoots.TryGetValue(entityKind, out var kindRoot))
    {
        continue;
    }

    var graphJsonPath = Path.Combine(kindRoot, $"{graph.GraphId}.graph.json");
    File.WriteAllText(graphJsonPath, JsonSerializer.Serialize(graph, aggregateJsonOptions));

    var singleProject = CreateSingleGraphProject(project, graph.GraphId);
    var singleProjectPath = Path.Combine(kindRoot, $"{graph.GraphId}.project.json");
    ModStudioJson.Save(singleProjectPath, singleProject);

    var singlePackagePath = archiveService.Export(singleProject, new PackageExportOptions
    {
        DisplayName = singleProject.Manifest.Name,
        Author = singleProject.Manifest.Author,
        Description = singleProject.Manifest.Description,
        Version = "coverage-1.0.0",
        TargetGameVersion = singleProject.Manifest.TargetGameVersion
    }, Path.Combine(kindRoot, $"{graph.GraphId}.sts2pack"));

    if (!archiveService.TryImport(singlePackagePath, out var singleManifest, out var singleImportedProject) ||
        singleManifest is null ||
        singleImportedProject is null ||
        !singleImportedProject.Graphs.ContainsKey(graph.GraphId))
    {
        throw new InvalidOperationException($"Single coverage package '{graph.GraphId}' failed re-import validation.");
    }

    var validation = registry.Validate(graph);
    EventGraphValidationResult? compileResult = null;
    if (graph.EntityKind == ModStudioEntityKind.Event)
    {
        compileResult = compiler.Compile(graph);
    }

    var kindReportPath = Path.Combine(kindRoot, "report.md");
    File.WriteAllText(kindReportPath, BuildKindReport(repoRoot, graph, validation, compileResult, graphJsonPath, singleProjectPath, singlePackagePath));

    reportLines.Add($"- `{graph.EntityKind}` -> `{Path.GetRelativePath(repoRoot, kindReportPath).Replace('\\', '/')}`");
}

var aggregateReportPath = Path.Combine(aggregateRoot, "report.md");
File.WriteAllText(aggregateReportPath, string.Join(Environment.NewLine, reportLines));

Console.WriteLine("Stage 65 coverage artifacts generated");
Console.WriteLine($"Aggregate report: {aggregateReportPath}");
foreach (var kindRoot in kindRoots.Values.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
{
    Console.WriteLine($"Kind root: {kindRoot}");
}

return 0;

static string FindRepoRoot(string startDirectory)
{
    var directory = new DirectoryInfo(startDirectory);
    while (directory != null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "STS2_Editor.csproj")))
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    throw new DirectoryNotFoundException("Could not locate repository root containing STS2_Editor.csproj.");
}

static BehaviorGraphRegistry CreateRegistry()
{
    var registry = new BehaviorGraphRegistry();
    registry.RegisterBuiltIns();
    return registry;
}

static void ValidateProject(EditorProject project, BehaviorGraphRegistry registry, EventGraphCompiler compiler)
{
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
}

static string BuildKindReport(
    string repoRoot,
    BehaviorGraphDefinition graph,
    BehaviorGraphValidationResult validation,
    EventGraphValidationResult? compileResult,
    string graphJsonPath,
    string projectJsonPath,
    string packagePath)
{
    var lines = new List<string>
    {
        $"# {graph.EntityKind} Coverage Sample",
        "",
        $"Graph Id: `{graph.GraphId}`",
        $"Entity Kind: `{graph.EntityKind}`",
        $"Node Count: `{graph.Nodes.Count}`",
        $"Validation: `{(validation.IsValid ? "PASS" : "FAIL")}`",
        $"Graph JSON: `{Path.GetRelativePath(repoRoot, graphJsonPath).Replace('\\', '/')}`",
        $"Project JSON: `{Path.GetRelativePath(repoRoot, projectJsonPath).Replace('\\', '/')}`",
        $"Package: `{Path.GetRelativePath(repoRoot, packagePath).Replace('\\', '/')}`",
        "",
        "## Node Types"
    };

    foreach (var nodeType in graph.Nodes.Select(node => node.NodeType).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
    {
        lines.Add($"- `{nodeType}`");
    }

    lines.Add("");
    lines.Add("## Validation");
    lines.Add(validation.Warnings.Count == 0
        ? "- warnings: none"
        : $"- warnings: {string.Join(" | ", validation.Warnings)}");

    if (compileResult != null)
    {
        lines.Add("");
        lines.Add("## Event Compiler");
        lines.Add($"- valid: `{compileResult.IsValid}`");
        lines.Add(compileResult.Warnings.Count == 0
            ? "- warnings: none"
            : $"- warnings: {string.Join(" | ", compileResult.Warnings)}");
    }

    return string.Join(Environment.NewLine, lines);
}

static EditorProject CreateSingleGraphProject(EditorProject sourceProject, string graphId)
{
    if (!sourceProject.Graphs.TryGetValue(graphId, out var graph))
    {
        throw new InvalidOperationException($"Coverage graph '{graphId}' was not found in source project.");
    }

    var project = new EditorProject
    {
        Manifest = new EditorProjectManifest
        {
            ProjectId = $"{sourceProject.Manifest.ProjectId}-{(graph.EntityKind?.ToString() ?? "unknown").ToLowerInvariant()}",
            Name = $"{graph.EntityKind} Coverage Project",
            Author = sourceProject.Manifest.Author,
            Description = $"Single-graph coverage package for {graph.EntityKind}.",
            EditorVersion = sourceProject.Manifest.EditorVersion,
            TargetGameVersion = sourceProject.Manifest.TargetGameVersion,
            CreatedAtUtc = sourceProject.Manifest.CreatedAtUtc,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        },
        SourceOfTruthIsRuntimeModelDb = sourceProject.SourceOfTruthIsRuntimeModelDb
    };

    project.Graphs[graphId] = JsonSerializer.Deserialize<BehaviorGraphDefinition>(
        JsonSerializer.Serialize(graph, ModStudioJson.Options),
        ModStudioJson.Options) ?? throw new InvalidOperationException($"Failed to clone graph '{graphId}'.");

    foreach (var envelope in sourceProject.Overrides.Where(envelope => string.Equals(envelope.GraphId, graphId, StringComparison.Ordinal)))
    {
        project.Overrides.Add(JsonSerializer.Deserialize<EntityOverrideEnvelope>(
            JsonSerializer.Serialize(envelope, ModStudioJson.Options),
            ModStudioJson.Options) ?? throw new InvalidOperationException($"Failed to clone override for graph '{graphId}'."));
    }

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
