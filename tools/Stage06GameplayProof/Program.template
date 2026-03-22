using System.Text.Json;
using STS2_Editor.Scripts.Editor.Core.Models;
using STS2_Editor.Scripts.Editor.Core.Utilities;
using STS2_Editor.Scripts.Editor.Graph;
using STS2_Editor.Scripts.Editor.Packaging;
using STS2_Editor.Scripts.Editor.Runtime;

const string GameplayProofProjectId = "stage06_gameplay_proof";
const string GameplayProofVersion = "1.0.0";
const string GameplayProofCardId = "NEUTRALIZE";
const string GameplayProofPotionId = "BLOCK_POTION";

var root = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])
    ? Path.GetFullPath(args[0])
    : Path.Combine(Path.GetTempPath(), $"sts2-editor-stage06-proof-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}");

Directory.CreateDirectory(root);
var workspace = Path.Combine(root, "workspace");
Directory.CreateDirectory(workspace);

var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
var gameUserRoot = Path.Combine(appData, "SlayTheSpire2");
var editorRoot = Path.Combine(gameUserRoot, "sts2_editor");
var projectsRoot = Path.Combine(editorRoot, "projects");
var exportsRoot = Path.Combine(editorRoot, "exports");
var installedRoot = Path.Combine(editorRoot, "packages", "installed");
var reportPath = Path.Combine(workspace, "stage06-gameplay-proof-report.txt");
var packagePath = Path.Combine(workspace, "stage06-gameplay-proof.sts2pack");
var exportedCopyPath = Path.Combine(exportsRoot, "stage06-gameplay-proof.sts2pack");
var projectDirectory = Path.Combine(projectsRoot, GameplayProofProjectId);
var projectFilePath = Path.Combine(projectDirectory, "project.json");

Directory.CreateDirectory(gameUserRoot);
Directory.CreateDirectory(editorRoot);
Directory.CreateDirectory(projectsRoot);
Directory.CreateDirectory(exportsRoot);
Directory.CreateDirectory(installedRoot);

var report = new List<string>
{
    "Stage 06 Gameplay Proof Package Generator",
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
    throw new InvalidOperationException("Failed to import the freshly exported gameplay-proof package.");
}

InstallPackage(archiveService, exportedPath, manifest, importedProject, installedRoot, report);
WriteReport(reportPath, report);

Console.WriteLine("Stage 06 gameplay proof package prepared.");
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
    report.Add("  - AutoSlay log contains 'Using potion: BLOCK_POTION'");
    report.Add("  - AutoSlay log contains 'Playing NEUTRALIZE'");
    report.Add("  - Godot log contains '[ModStudio.Graph] STAGE06_POTION_OK'");
    report.Add("  - Godot log contains '[ModStudio.Graph] STAGE06_CARD_OK'");
}

static void WriteReport(string reportPath, IEnumerable<string> lines)
{
    File.WriteAllLines(reportPath, lines);
}

static EditorProject CreateGameplayProofProject()
{
    var characterIds = new[]
    {
        "IRONCLAD",
        "SILENT",
        "REGENT",
        "NECROBINDER",
        "DEFECT"
    };

    var project = new EditorProject
    {
        Manifest = new EditorProjectManifest
        {
            ProjectId = GameplayProofProjectId,
            Name = "Stage 06 Gameplay Proof",
            Author = "Codex",
            Description = "Controlled package for real-game autoslay verification of Mod Studio graph overrides.",
            EditorVersion = "stage06",
            TargetGameVersion = "Slay the Spire 2 / Godot 4.5.1",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        },
        SourceOfTruthIsRuntimeModelDb = true
    };

    foreach (var characterId in characterIds)
    {
        project.Overrides.Add(new EntityOverrideEnvelope
        {
            EntityKind = ModStudioEntityKind.Character,
            EntityId = characterId,
            BehaviorSource = BehaviorSource.Native,
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["starting_deck_ids"] = string.Join(", ", Enumerable.Repeat(GameplayProofCardId, 8)),
                ["starting_potion_ids"] = GameplayProofPotionId
            },
            Notes = "For Stage 06 autoslay proof, every character starts with the same graph-driven card and potion."
        });
    }

    var cardGraph = CreateCardGraph();
    var potionGraph = CreatePotionGraph();
    project.Graphs[cardGraph.GraphId] = cardGraph;
    project.Graphs[potionGraph.GraphId] = potionGraph;

    project.Overrides.Add(new EntityOverrideEnvelope
    {
        EntityKind = ModStudioEntityKind.Card,
        EntityId = GameplayProofCardId,
        BehaviorSource = BehaviorSource.Graph,
        GraphId = cardGraph.GraphId,
        Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["title"] = "Stage 06 Neutralize",
            ["description"] = "Logs STAGE06_CARD_OK and deals 12 damage."
        },
        Notes = "Real-game graph verification card."
    });

    project.Overrides.Add(new EntityOverrideEnvelope
    {
        EntityKind = ModStudioEntityKind.Potion,
        EntityId = GameplayProofPotionId,
        BehaviorSource = BehaviorSource.Graph,
        GraphId = potionGraph.GraphId,
        Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["title"] = "Stage 06 Block Potion",
            ["description"] = "Logs STAGE06_POTION_OK and grants 7 Block."
        },
        Notes = "Real-game graph verification potion."
    });

    return project;
}

static BehaviorGraphDefinition CreateCardGraph()
{
    const string graphId = "graph.card.stage06.neutralize";
    var entryId = $"{graphId}.entry";
    var logId = $"{graphId}.log";
    var damageId = $"{graphId}.damage";
    var exitId = $"{graphId}.exit";

    return new BehaviorGraphDefinition
    {
        GraphId = graphId,
        Name = "Stage 06 Neutralize Graph",
        Description = "Proof graph for a guaranteed autoslay-played card.",
        EntityKind = ModStudioEntityKind.Card,
        EntryNodeId = entryId,
        Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["trigger.card.on_play"] = entryId,
            ["trigger.default"] = entryId
        },
        Nodes =
        [
            new BehaviorGraphNodeDefinition
            {
                NodeId = entryId,
                NodeType = "flow.entry",
                DisplayName = "Entry",
                Properties = new Dictionary<string, string>(StringComparer.Ordinal)
            },
            new BehaviorGraphNodeDefinition
            {
                NodeId = logId,
                NodeType = "debug.log",
                DisplayName = "Log",
                Properties = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["message"] = "STAGE06_CARD_OK"
                }
            },
            new BehaviorGraphNodeDefinition
            {
                NodeId = damageId,
                NodeType = "combat.damage",
                DisplayName = "Damage",
                Properties = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["amount"] = "12",
                    ["target"] = "current_target",
                    ["props"] = "Move"
                }
            },
            new BehaviorGraphNodeDefinition
            {
                NodeId = exitId,
                NodeType = "flow.exit",
                DisplayName = "Exit",
                Properties = new Dictionary<string, string>(StringComparer.Ordinal)
            }
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
    const string graphId = "graph.potion.stage06.block_potion";
    var entryId = $"{graphId}.entry";
    var logId = $"{graphId}.log";
    var blockId = $"{graphId}.block";
    var exitId = $"{graphId}.exit";

    return new BehaviorGraphDefinition
    {
        GraphId = graphId,
        Name = "Stage 06 Block Potion Graph",
        Description = "Proof graph for a guaranteed autoslay-used potion.",
        EntityKind = ModStudioEntityKind.Potion,
        EntryNodeId = entryId,
        Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["trigger.potion.on_use"] = entryId,
            ["trigger.default"] = entryId
        },
        Nodes =
        [
            new BehaviorGraphNodeDefinition
            {
                NodeId = entryId,
                NodeType = "flow.entry",
                DisplayName = "Entry",
                Properties = new Dictionary<string, string>(StringComparer.Ordinal)
            },
            new BehaviorGraphNodeDefinition
            {
                NodeId = logId,
                NodeType = "debug.log",
                DisplayName = "Log",
                Properties = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["message"] = "STAGE06_POTION_OK"
                }
            },
            new BehaviorGraphNodeDefinition
            {
                NodeId = blockId,
                NodeType = "combat.gain_block",
                DisplayName = "Gain Block",
                Properties = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["amount"] = "7",
                    ["target"] = "self",
                    ["props"] = "Unpowered"
                }
            },
            new BehaviorGraphNodeDefinition
            {
                NodeId = exitId,
                NodeType = "flow.exit",
                DisplayName = "Exit",
                Properties = new Dictionary<string, string>(StringComparer.Ordinal)
            }
        ],
        Connections =
        [
            new BehaviorGraphConnectionDefinition { FromNodeId = entryId, FromPortId = "next", ToNodeId = logId, ToPortId = "in" },
            new BehaviorGraphConnectionDefinition { FromNodeId = logId, FromPortId = "out", ToNodeId = blockId, ToPortId = "in" },
            new BehaviorGraphConnectionDefinition { FromNodeId = blockId, FromPortId = "out", ToNodeId = exitId, ToPortId = "in" }
        ]
    };
}
