using System.Text.Json;
using MegaCrit.Sts2.Core.Helpers;
using STS2_Editor.Scripts.Editor.Core.Models;
using STS2_Editor.Scripts.Editor.Core.Utilities;
using STS2_Editor.Scripts.Editor.Graph;
using STS2_Editor.Scripts.Editor.Packaging;
using STS2_Editor.Scripts.Editor.Runtime;

const string GameplayProofProjectId = "stage09_custom_content_proof";
const string GameplayProofVersion = "1.0.0";
const string CustomCardId = "ed_stage09__card_001";
const string CustomPotionId = "ed_stage09__potion_001";
const string CustomRelicId = "ed_stage09__relic_001";

var root = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])
    ? Path.GetFullPath(args[0])
    : Path.Combine(Path.GetTempPath(), $"sts2-editor-stage09-proof-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}");

Directory.CreateDirectory(root);
var workspace = Path.Combine(root, "workspace");
Directory.CreateDirectory(workspace);

var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
var gameUserRoot = Path.Combine(appData, "SlayTheSpire2");
var editorRoot = Path.Combine(gameUserRoot, "sts2_editor");
var projectsRoot = Path.Combine(editorRoot, "projects");
var exportsRoot = Path.Combine(editorRoot, "exports");
var installedRoot = Path.Combine(editorRoot, "packages", "installed");
var reportPath = Path.Combine(workspace, "stage09-custom-content-proof-report.txt");
var packagePath = Path.Combine(workspace, "stage09-custom-content-proof.sts2pack");
var exportedCopyPath = Path.Combine(exportsRoot, "stage09-custom-content-proof.sts2pack");
var projectDirectory = Path.Combine(projectsRoot, GameplayProofProjectId);
var projectFilePath = Path.Combine(projectDirectory, "project.json");

Directory.CreateDirectory(gameUserRoot);
Directory.CreateDirectory(editorRoot);
Directory.CreateDirectory(projectsRoot);
Directory.CreateDirectory(exportsRoot);
Directory.CreateDirectory(installedRoot);

var report = new List<string>
{
    "Stage 09 Custom Content Gameplay Proof Package Generator",
    $"Workspace: {workspace}",
    $"Game user root: {gameUserRoot}",
    $"Editor root: {editorRoot}"
};

var project = CreateGameplayProofProject();
var archiveService = new PackageArchiveService();
var exportOptions = new PackageExportOptions
{
    PackageId = project.Manifest.ProjectId,
    DisplayName = project.Manifest.Name,
    Author = project.Manifest.Author,
    Description = project.Manifest.Description,
    Version = GameplayProofVersion,
    EditorVersion = project.Manifest.EditorVersion,
    TargetGameVersion = project.Manifest.TargetGameVersion
};

Directory.CreateDirectory(projectDirectory);
ModStudioJson.Save(projectFilePath, project);
report.Add($"Saved editor project: {projectFilePath}");

var exportedPath = archiveService.Export(project, exportOptions, packagePath);
File.Copy(exportedPath, exportedCopyPath, overwrite: true);
report.Add($"Exported package: {exportedPath}");
report.Add($"Copied package to editor exports: {exportedCopyPath}");

if (!archiveService.TryImport(exportedPath, out var manifest, out var importedProject) || manifest is null || importedProject is null)
{
    throw new InvalidOperationException("Failed to import the freshly exported custom-content proof package.");
}

InstallPackage(archiveService, exportedPath, manifest, importedProject, installedRoot, report);
WriteReport(reportPath, report);

Console.WriteLine("Stage 09 custom-content gameplay proof package prepared.");
foreach (var line in report)
{
    Console.WriteLine(line);
}

return 0;

static void InstallPackage(
    PackageArchiveService archiveService,
    string packageFilePath,
    EditorPackageManifest manifest,
    EditorProject importedProject,
    string installedPackagesRoot,
    IList<string> report)
{
    var packageDirectory = Path.Combine(installedPackagesRoot, manifest.PackageKey);
    if (Directory.Exists(packageDirectory))
    {
        Directory.Delete(packageDirectory, recursive: true);
    }

    Directory.CreateDirectory(packageDirectory);
    var normalizedProject = archiveService.NormalizeImportedProject(manifest, importedProject, installedPackagesRoot);
    archiveService.ExtractManagedAssets(packageFilePath, manifest, normalizedProject, installedPackagesRoot);

    ModStudioJson.Save(Path.Combine(packageDirectory, "manifest.json"), manifest);
    ModStudioJson.Save(Path.Combine(packageDirectory, "project.json"), normalizedProject);

    var sessionPath = Path.Combine(installedPackagesRoot, "session.json");
    var states = File.Exists(sessionPath)
        ? JsonSerializer.Deserialize<List<PackageSessionState>>(File.ReadAllText(sessionPath), ModStudioJson.Options) ?? new List<PackageSessionState>()
        : new List<PackageSessionState>();

    states.RemoveAll(state => string.Equals(state.PackageKey, manifest.PackageKey, StringComparison.Ordinal));
    var nextLoadOrder = states.Count == 0 ? 0 : states.Max(state => state.LoadOrder) + 1;
    states.Add(new PackageSessionState
    {
        PackageKey = manifest.PackageKey,
        PackageId = manifest.PackageId,
        DisplayName = manifest.DisplayName,
        Version = manifest.Version,
        Checksum = manifest.Checksum,
        PackageFilePath = packageDirectory,
        LoadOrder = nextLoadOrder,
        Enabled = true,
        SessionEnabled = true,
        DisabledReason = string.Empty
    });

    states = states
        .OrderBy(state => state.LoadOrder)
        .ThenBy(state => state.PackageKey, StringComparer.Ordinal)
        .Select((state, index) =>
        {
            state.LoadOrder = index;
            return state;
        })
        .ToList();

    ModStudioJson.Save(sessionPath, states);

    report.Add($"Installed package: {packageDirectory}");
    report.Add($"Session file: {sessionPath}");
    report.Add($"Package key: {manifest.PackageKey}");
    report.Add($"Checksum: {manifest.Checksum}");
    report.Add("Expected autoslay evidence:");
    report.Add($"  - Godot log contains '[INFO] [ModStudio.Graph] STAGE09_CARD_OK'");
    report.Add($"  - Godot log contains '[INFO] [ModStudio.Graph] STAGE09_POTION_OK'");
    report.Add($"  - Godot log contains '[INFO] [ModStudio.Graph] STAGE09_RELIC_OK'");
    report.Add($"  - AutoSlay log should mention custom ids {CustomCardId} and {CustomPotionId}");
}

static void WriteReport(string reportPath, IEnumerable<string> lines)
{
    File.WriteAllLines(reportPath, lines);
}

static EditorProject CreateGameplayProofProject()
{
    var characters = new[]
    {
        new CharacterSetup("IRONCLAD", ["BURNING_BLOOD"]),
        new CharacterSetup("SILENT", ["RING_OF_THE_SNAKE"]),
        new CharacterSetup("REGENT", ["DIVINE_RIGHT"]),
        new CharacterSetup("NECROBINDER", ["BOUND_PHYLACTERY"]),
        new CharacterSetup("DEFECT", ["CRACKED_CORE"])
    };

    const string sampleCardPoolId = "SILENT_CARD_POOL";
    var sampleCardPortraitPath = ImageHelper.GetImagePath("atlases/card_atlas.sprites/silent/neutralize.tres");
    const string samplePotionPoolId = "SILENT_POTION_POOL";
    var samplePotionImagePath = ImageHelper.GetImagePath("atlases/potion_atlas.sprites/block_potion.tres");
    const string sampleRelicPoolId = "IRONCLAD_RELIC_POOL";
    var sampleRelicIconPath = ImageHelper.GetImagePath("atlases/relic_atlas.sprites/anchor.tres");

    var project = new EditorProject
    {
        Manifest = new EditorProjectManifest
        {
            ProjectId = GameplayProofProjectId,
            Name = "Stage 09 Custom Content Proof",
            Author = "Codex",
            Description = "Controlled package for real-game autoslay verification of brand-new Mod Studio content.",
            EditorVersion = "stage09",
            TargetGameVersion = "Slay the Spire 2 / Godot 4.5.1",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        },
        SourceOfTruthIsRuntimeModelDb = true
    };

    foreach (var character in characters)
    {
        project.Overrides.Add(new EntityOverrideEnvelope
        {
            EntityKind = ModStudioEntityKind.Character,
            EntityId = character.CharacterId,
            BehaviorSource = BehaviorSource.Native,
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["starting_deck_ids"] = string.Join(", ", Enumerable.Repeat(CustomCardId, 8)),
                ["starting_potion_ids"] = CustomPotionId,
                ["starting_relic_ids"] = string.Join(", ", character.StartingRelicIds.Append(CustomRelicId))
            },
            Notes = "For Stage 09 autoslay proof, every character starts with new custom card, potion, and relic content."
        });
    }

    var cardGraph = CreateCardGraph();
    var potionGraph = CreatePotionGraph();
    var relicGraph = CreateRelicGraph();
    project.Graphs[cardGraph.GraphId] = cardGraph;
    project.Graphs[potionGraph.GraphId] = potionGraph;
    project.Graphs[relicGraph.GraphId] = relicGraph;

    project.Overrides.Add(new EntityOverrideEnvelope
    {
        EntityKind = ModStudioEntityKind.Card,
        EntityId = CustomCardId,
        BehaviorSource = BehaviorSource.Graph,
        GraphId = cardGraph.GraphId,
        Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["title"] = "Stage 09 Blade",
            ["description"] = "Logs STAGE09_CARD_OK and deals 14 damage.",
            ["type"] = "Attack",
            ["rarity"] = "Common",
            ["pool_id"] = sampleCardPoolId,
            ["portrait_path"] = sampleCardPortraitPath,
            ["target_type"] = "AnyEnemy",
            ["energy_cost"] = "1",
            ["energy_cost_x"] = false.ToString(),
            ["canonical_star_cost"] = "-1",
            ["star_cost_x"] = false.ToString(),
            ["can_be_generated_in_combat"] = true.ToString()
        },
        Notes = "Real-game proof for brand-new custom card registration."
    });

    project.Overrides.Add(new EntityOverrideEnvelope
    {
        EntityKind = ModStudioEntityKind.Potion,
        EntityId = CustomPotionId,
        BehaviorSource = BehaviorSource.Graph,
        GraphId = potionGraph.GraphId,
        Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["title"] = "Stage 09 Guard Draught",
            ["description"] = "Logs STAGE09_POTION_OK and grants 9 Block.",
            ["rarity"] = "Common",
            ["usage"] = "CombatOnly",
            ["target_type"] = "Self",
            ["pool_id"] = samplePotionPoolId,
            ["image_path"] = samplePotionImagePath,
            ["can_be_generated_in_combat"] = true.ToString()
        },
        Notes = "Real-game proof for brand-new custom potion registration."
    });

    project.Overrides.Add(new EntityOverrideEnvelope
    {
        EntityKind = ModStudioEntityKind.Relic,
        EntityId = CustomRelicId,
        BehaviorSource = BehaviorSource.Graph,
        GraphId = relicGraph.GraphId,
        Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["title"] = "Stage 09 Memory Relic",
            ["description"] = "Logs STAGE09_RELIC_OK after cards are played.",
            ["rarity"] = "Starter",
            ["pool_id"] = sampleRelicPoolId,
            ["icon_path"] = sampleRelicIconPath
        },
        Notes = "Real-game proof for brand-new custom relic registration."
    });

    return project;
}

static BehaviorGraphDefinition CreateCardGraph()
{
    const string graphId = "graph.card.stage09.custom_card";
    var entryId = $"{graphId}.entry";
    var logId = $"{graphId}.log";
    var damageId = $"{graphId}.damage";
    var exitId = $"{graphId}.exit";

    return new BehaviorGraphDefinition
    {
        GraphId = graphId,
        Name = "Stage 09 Custom Card Graph",
        Description = "Proof graph for a brand-new custom card.",
        EntityKind = ModStudioEntityKind.Card,
        EntryNodeId = entryId,
        Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["trigger.card.on_play"] = entryId,
            ["trigger.default"] = entryId
        },
        Nodes =
        [
            new BehaviorGraphNodeDefinition { NodeId = entryId, NodeType = "flow.entry", DisplayName = "Entry", Properties = new Dictionary<string, string>(StringComparer.Ordinal) },
            new BehaviorGraphNodeDefinition { NodeId = logId, NodeType = "debug.log", DisplayName = "Log", Properties = new Dictionary<string, string>(StringComparer.Ordinal) { ["message"] = "STAGE09_CARD_OK" } },
            new BehaviorGraphNodeDefinition { NodeId = damageId, NodeType = "combat.damage", DisplayName = "Damage", Properties = new Dictionary<string, string>(StringComparer.Ordinal) { ["amount"] = "14", ["target"] = "current_target", ["props"] = "Move" } },
            new BehaviorGraphNodeDefinition { NodeId = exitId, NodeType = "flow.exit", DisplayName = "Exit", Properties = new Dictionary<string, string>(StringComparer.Ordinal) }
        ],
        Connections =
        [
            new BehaviorGraphConnectionDefinition { FromNodeId = entryId, FromPortId = "next", ToNodeId = logId, ToPortId = "in" },
            new BehaviorGraphConnectionDefinition { FromNodeId = logId, FromPortId = "out", ToNodeId = damageId, ToPortId = "in" },
            new BehaviorGraphConnectionDefinition { FromNodeId = damageId, FromPortId = "out", ToNodeId = exitId, ToPortId = "in" }
        ]
    };
}

static BehaviorGraphDefinition CreatePotionGraph()
{
    const string graphId = "graph.potion.stage09.custom_potion";
    var entryId = $"{graphId}.entry";
    var logId = $"{graphId}.log";
    var blockId = $"{graphId}.block";
    var exitId = $"{graphId}.exit";

    return new BehaviorGraphDefinition
    {
        GraphId = graphId,
        Name = "Stage 09 Custom Potion Graph",
        Description = "Proof graph for a brand-new custom potion.",
        EntityKind = ModStudioEntityKind.Potion,
        EntryNodeId = entryId,
        Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["trigger.potion.on_use"] = entryId,
            ["trigger.default"] = entryId
        },
        Nodes =
        [
            new BehaviorGraphNodeDefinition { NodeId = entryId, NodeType = "flow.entry", DisplayName = "Entry", Properties = new Dictionary<string, string>(StringComparer.Ordinal) },
            new BehaviorGraphNodeDefinition { NodeId = logId, NodeType = "debug.log", DisplayName = "Log", Properties = new Dictionary<string, string>(StringComparer.Ordinal) { ["message"] = "STAGE09_POTION_OK" } },
            new BehaviorGraphNodeDefinition { NodeId = blockId, NodeType = "combat.gain_block", DisplayName = "Gain Block", Properties = new Dictionary<string, string>(StringComparer.Ordinal) { ["amount"] = "9", ["target"] = "self", ["props"] = "Unpowered" } },
            new BehaviorGraphNodeDefinition { NodeId = exitId, NodeType = "flow.exit", DisplayName = "Exit", Properties = new Dictionary<string, string>(StringComparer.Ordinal) }
        ],
        Connections =
        [
            new BehaviorGraphConnectionDefinition { FromNodeId = entryId, FromPortId = "next", ToNodeId = logId, ToPortId = "in" },
            new BehaviorGraphConnectionDefinition { FromNodeId = logId, FromPortId = "out", ToNodeId = blockId, ToPortId = "in" },
            new BehaviorGraphConnectionDefinition { FromNodeId = blockId, FromPortId = "out", ToNodeId = exitId, ToPortId = "in" }
        ]
    };
}

static BehaviorGraphDefinition CreateRelicGraph()
{
    const string graphId = "graph.relic.stage09.custom_relic";
    var entryId = $"{graphId}.entry";
    var logId = $"{graphId}.log";
    var exitId = $"{graphId}.exit";

    return new BehaviorGraphDefinition
    {
        GraphId = graphId,
        Name = "Stage 09 Custom Relic Graph",
        Description = "Proof graph for a brand-new custom relic.",
        EntityKind = ModStudioEntityKind.Relic,
        EntryNodeId = entryId,
        Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["trigger.relic.after_card_played"] = entryId,
            ["trigger.default"] = entryId
        },
        Nodes =
        [
            new BehaviorGraphNodeDefinition { NodeId = entryId, NodeType = "flow.entry", DisplayName = "Entry", Properties = new Dictionary<string, string>(StringComparer.Ordinal) },
            new BehaviorGraphNodeDefinition { NodeId = logId, NodeType = "debug.log", DisplayName = "Log", Properties = new Dictionary<string, string>(StringComparer.Ordinal) { ["message"] = "STAGE09_RELIC_OK" } },
            new BehaviorGraphNodeDefinition { NodeId = exitId, NodeType = "flow.exit", DisplayName = "Exit", Properties = new Dictionary<string, string>(StringComparer.Ordinal) }
        ],
        Connections =
        [
            new BehaviorGraphConnectionDefinition { FromNodeId = entryId, FromPortId = "next", ToNodeId = logId, ToPortId = "in" },
            new BehaviorGraphConnectionDefinition { FromNodeId = logId, FromPortId = "out", ToNodeId = exitId, ToPortId = "in" }
        ]
    };
}

sealed record CharacterSetup(string CharacterId, IReadOnlyList<string> StartingRelicIds);
