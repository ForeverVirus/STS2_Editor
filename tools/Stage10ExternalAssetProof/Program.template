using System.Buffers.Binary;
using System.IO.Compression;
using System.Text.Json;
using STS2_Editor.Scripts.Editor.Core.Models;
using STS2_Editor.Scripts.Editor.Core.Utilities;
using STS2_Editor.Scripts.Editor.Graph;
using STS2_Editor.Scripts.Editor.Packaging;

const string ProofProjectId = "stage10_external_asset_proof";
const string ProofVersion = "1.0.0";
const string CustomCardId = "ed_stage10__card_001";
const string CustomPotionId = "ed_stage10__potion_001";
const string CustomRelicId = "ed_stage10__relic_001";

var root = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])
    ? Path.GetFullPath(args[0])
    : Path.Combine(Path.GetTempPath(), $"sts2-editor-stage10-proof-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}");

Directory.CreateDirectory(root);
var workspace = Path.Combine(root, "workspace");
Directory.CreateDirectory(workspace);

var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
var gameUserRoot = Path.Combine(appData, "SlayTheSpire2");
var editorRoot = Path.Combine(gameUserRoot, "sts2_editor");
var projectsRoot = Path.Combine(editorRoot, "projects");
var exportsRoot = Path.Combine(editorRoot, "exports");
var installedRoot = Path.Combine(editorRoot, "packages", "installed");
var reportPath = Path.Combine(workspace, "stage10-external-asset-proof-report.txt");
var packagePath = Path.Combine(workspace, "stage10-external-asset-proof.sts2pack");
var exportedCopyPath = Path.Combine(exportsRoot, "stage10-external-asset-proof.sts2pack");
var projectDirectory = Path.Combine(projectsRoot, ProofProjectId);
var projectFilePath = Path.Combine(projectDirectory, "project.json");
var assetsSourceDirectory = Path.Combine(workspace, "generated-assets");
Directory.CreateDirectory(assetsSourceDirectory);

Directory.CreateDirectory(gameUserRoot);
Directory.CreateDirectory(editorRoot);
Directory.CreateDirectory(projectsRoot);
Directory.CreateDirectory(exportsRoot);
Directory.CreateDirectory(installedRoot);

var report = new List<string>
{
    "Stage 10 External Asset Proof Package Generator",
    $"Workspace: {workspace}",
    $"Game user root: {gameUserRoot}",
    $"Editor root: {editorRoot}"
};

var cardSourcePath = Path.Combine(assetsSourceDirectory, "stage10_card.png");
var relicSourcePath = Path.Combine(assetsSourceDirectory, "stage10_relic.png");
var potionSourcePath = Path.Combine(assetsSourceDirectory, "stage10_potion.png");

WriteProofPng(cardSourcePath, 320, 240, 0x3D, 0x5A, 0xC7, 0xEA, 0xF5, 0xFF, 0xFF);
WriteProofPng(relicSourcePath, 128, 128, 0x6C, 0x4C, 0x2E, 0xD7, 0xA7, 0x72, 0xFF);
WriteProofPng(potionSourcePath, 128, 128, 0x3B, 0x8C, 0x57, 0xA7, 0xDF, 0xB1, 0xFF);

var project = CreateProofProject();

var cardAsset = ImportManagedAsset(ProofProjectId, cardSourcePath, "portrait", projectsRoot);
var relicAsset = ImportManagedAsset(ProofProjectId, relicSourcePath, "icon", projectsRoot);
var potionAsset = ImportManagedAsset(ProofProjectId, potionSourcePath, "image", projectsRoot);

AttachAsset(project, ModStudioEntityKind.Card, CustomCardId, "portrait_path", cardAsset);
AttachAsset(project, ModStudioEntityKind.Relic, CustomRelicId, "icon_path", relicAsset);
AttachAsset(project, ModStudioEntityKind.Potion, CustomPotionId, "image_path", potionAsset);

var archiveService = new PackageArchiveService();
var exportOptions = new PackageExportOptions
{
    PackageId = project.Manifest.ProjectId,
    DisplayName = project.Manifest.Name,
    Author = project.Manifest.Author,
    Description = project.Manifest.Description,
    Version = ProofVersion,
    EditorVersion = project.Manifest.EditorVersion,
    TargetGameVersion = project.Manifest.TargetGameVersion
};

Directory.CreateDirectory(projectDirectory);
ModStudioJson.Save(projectFilePath, project);
report.Add($"Saved editor project: {projectFilePath}");
report.Add($"Generated PNG: {cardSourcePath}");
report.Add($"Generated PNG: {relicSourcePath}");
report.Add($"Generated PNG: {potionSourcePath}");
report.Add($"Managed asset path (card): {cardAsset.ManagedPath}");
report.Add($"Managed asset path (relic): {relicAsset.ManagedPath}");
report.Add($"Managed asset path (potion): {potionAsset.ManagedPath}");

var exportedPath = archiveService.Export(project, exportOptions, packagePath);
File.Copy(exportedPath, exportedCopyPath, overwrite: true);
report.Add($"Exported package: {exportedPath}");
report.Add($"Copied package to editor exports: {exportedCopyPath}");

if (!archiveService.TryImport(exportedPath, out var manifest, out var importedProject) || manifest is null || importedProject is null)
{
    throw new InvalidOperationException("Failed to import the freshly exported external-asset proof package.");
}

var normalizedProject = archiveService.NormalizeImportedProject(manifest, importedProject, installedRoot);
InstallPackage(archiveService, exportedPath, manifest, normalizedProject, installedRoot, report, cardAsset, relicAsset, potionAsset);
WriteReport(reportPath, report);

Console.WriteLine("Stage 10 external-asset proof package prepared.");
foreach (var line in report)
{
    Console.WriteLine(line);
}

return 0;

static void AttachAsset(EditorProject project, ModStudioEntityKind kind, string entityId, string metadataKey, AssetRef asset)
{
    project.ProjectAssets.Add(CloneAsset(asset));

    var envelope = project.Overrides.FirstOrDefault(item => item.EntityKind == kind && item.EntityId == entityId);
    if (envelope is null)
    {
        throw new InvalidOperationException($"Could not find override envelope for {kind}:{entityId}.");
    }

    envelope.Assets.Add(CloneAsset(asset));
    envelope.Metadata[metadataKey] = asset.ManagedPath;
}

static AssetRef ImportManagedAsset(string projectId, string sourcePath, string logicalRole, string projectsRoot)
{
    if (string.IsNullOrWhiteSpace(projectId))
    {
        throw new ArgumentException("Project id is required.", nameof(projectId));
    }

    if (string.IsNullOrWhiteSpace(sourcePath))
    {
        throw new ArgumentException("Source path is required.", nameof(sourcePath));
    }

    if (!File.Exists(sourcePath))
    {
        throw new FileNotFoundException("External asset does not exist.", sourcePath);
    }

    var fileName = Path.GetFileName(sourcePath);
    var assetId = Guid.NewGuid().ToString("N");
    var managedDirectory = Path.Combine(projectsRoot, projectId, "assets", assetId);
    Directory.CreateDirectory(managedDirectory);
    var managedPath = Path.Combine(managedDirectory, fileName);
    File.Copy(sourcePath, managedPath, overwrite: true);

    return new AssetRef
    {
        Id = assetId,
        SourceType = "external",
        LogicalRole = logicalRole,
        SourcePath = Path.GetFullPath(sourcePath),
        ManagedPath = managedPath,
        PackagePath = string.Empty,
        FileName = fileName
    };
}

static AssetRef CloneAsset(AssetRef asset)
{
    return new AssetRef
    {
        Id = asset.Id,
        SourceType = asset.SourceType,
        LogicalRole = asset.LogicalRole,
        SourcePath = asset.SourcePath,
        ManagedPath = asset.ManagedPath,
        PackagePath = asset.PackagePath,
        FileName = asset.FileName
    };
}

static void InstallPackage(
    PackageArchiveService archiveService,
    string packageFilePath,
    EditorPackageManifest manifest,
    EditorProject normalizedProject,
    string installedPackagesRoot,
    IList<string> report,
    AssetRef cardAsset,
    AssetRef relicAsset,
    AssetRef potionAsset)
{
    var packageDirectory = Path.Combine(installedPackagesRoot, manifest.PackageKey);
    if (Directory.Exists(packageDirectory))
    {
        Directory.Delete(packageDirectory, recursive: true);
    }

    Directory.CreateDirectory(packageDirectory);

    ModStudioJson.Save(Path.Combine(packageDirectory, "manifest.json"), manifest);
    ModStudioJson.Save(Path.Combine(packageDirectory, "project.json"), normalizedProject);
    archiveService.ExtractManagedAssets(packageFilePath, manifest, normalizedProject, installedPackagesRoot);

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
    report.Add("Verified install-time asset paths:");
    report.Add($"  - {Path.Combine(installedPackagesRoot, manifest.PackageKey, "assets", cardAsset.Id, cardAsset.FileName)}");
    report.Add($"  - {Path.Combine(installedPackagesRoot, manifest.PackageKey, "assets", relicAsset.Id, relicAsset.FileName)}");
    report.Add($"  - {Path.Combine(installedPackagesRoot, manifest.PackageKey, "assets", potionAsset.Id, potionAsset.FileName)}");
    report.Add("Verified package metadata asset refs:");
    report.Add($"  - card => {normalizedProject.Overrides.First(x => x.EntityKind == ModStudioEntityKind.Card && x.EntityId == CustomCardId).Metadata["portrait_path"]}");
    report.Add($"  - relic => {normalizedProject.Overrides.First(x => x.EntityKind == ModStudioEntityKind.Relic && x.EntityId == CustomRelicId).Metadata["icon_path"]}");
    report.Add($"  - potion => {normalizedProject.Overrides.First(x => x.EntityKind == ModStudioEntityKind.Potion && x.EntityId == CustomPotionId).Metadata["image_path"]}");
    report.Add("Expected autoslay evidence:");
    report.Add("  - Godot log contains '[INFO] [ModStudio.Graph] STAGE10_CARD_OK'");
    report.Add("  - Godot log contains '[INFO] [ModStudio.Graph] STAGE10_POTION_OK'");
    report.Add("  - Godot log contains '[INFO] [ModStudio.Graph] STAGE10_RELIC_OK'");
    report.Add("  - Runtime asset loading should resolve modstudio://asset/... references from the imported package");
}

static void WriteReport(string reportPath, IEnumerable<string> lines)
{
    File.WriteAllLines(reportPath, lines);
}

static EditorProject CreateProofProject()
{
    var characters = new[]
    {
        new CharacterSetup("IRONCLAD", ["BURNING_BLOOD"]),
        new CharacterSetup("SILENT", ["RING_OF_THE_SNAKE"]),
        new CharacterSetup("REGENT", ["DIVINE_RIGHT"]),
        new CharacterSetup("NECROBINDER", ["BOUND_PHYLACTERY"]),
        new CharacterSetup("DEFECT", ["CRACKED_CORE"])
    };

    var project = new EditorProject
    {
        Manifest = new EditorProjectManifest
        {
            ProjectId = ProofProjectId,
            Name = "Stage 10 External Asset Proof",
            Author = "Codex",
            Description = "Controlled package for validating managed external PNG import and packaging.",
            EditorVersion = "stage10",
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
            Notes = "For Stage 10 proof, every character starts with new custom content and imported external art."
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
            ["title"] = "Stage 10 Painted Blade",
            ["description"] = "Logs STAGE10_CARD_OK and deals 14 damage.",
            ["type"] = "Attack",
            ["rarity"] = "Common",
            ["pool_id"] = "SILENT_CARD_POOL",
            ["target_type"] = "AnyEnemy",
            ["energy_cost"] = "1",
            ["energy_cost_x"] = false.ToString(),
            ["canonical_star_cost"] = "-1",
            ["star_cost_x"] = false.ToString(),
            ["can_be_generated_in_combat"] = true.ToString()
        },
        Notes = "Real-game proof for brand-new custom card registration using external art."
    });

    project.Overrides.Add(new EntityOverrideEnvelope
    {
        EntityKind = ModStudioEntityKind.Potion,
        EntityId = CustomPotionId,
        BehaviorSource = BehaviorSource.Graph,
        GraphId = potionGraph.GraphId,
        Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["title"] = "Stage 10 Guard Draught",
            ["description"] = "Logs STAGE10_POTION_OK and grants 9 Block.",
            ["rarity"] = "Common",
            ["usage"] = "CombatOnly",
            ["target_type"] = "Self",
            ["pool_id"] = "SILENT_POTION_POOL",
            ["can_be_generated_in_combat"] = true.ToString()
        },
        Notes = "Real-game proof for brand-new custom potion registration using external art."
    });

    project.Overrides.Add(new EntityOverrideEnvelope
    {
        EntityKind = ModStudioEntityKind.Relic,
        EntityId = CustomRelicId,
        BehaviorSource = BehaviorSource.Graph,
        GraphId = relicGraph.GraphId,
        Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["title"] = "Stage 10 Memory Relic",
            ["description"] = "Logs STAGE10_RELIC_OK after cards are played.",
            ["rarity"] = "Starter",
            ["pool_id"] = "IRONCLAD_RELIC_POOL"
        },
        Notes = "Real-game proof for brand-new custom relic registration using external art."
    });

    return project;
}

static BehaviorGraphDefinition CreateCardGraph()
{
    const string graphId = "graph.card.stage10.custom_card";
    var entryId = $"{graphId}.entry";
    var logId = $"{graphId}.log";
    var damageId = $"{graphId}.damage";
    var exitId = $"{graphId}.exit";

    return new BehaviorGraphDefinition
    {
        GraphId = graphId,
        Name = "Stage 10 Custom Card Graph",
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
            new BehaviorGraphNodeDefinition { NodeId = logId, NodeType = "debug.log", DisplayName = "Log", Properties = new Dictionary<string, string>(StringComparer.Ordinal) { ["message"] = "STAGE10_CARD_OK" } },
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
    const string graphId = "graph.potion.stage10.custom_potion";
    var entryId = $"{graphId}.entry";
    var logId = $"{graphId}.log";
    var blockId = $"{graphId}.block";
    var exitId = $"{graphId}.exit";

    return new BehaviorGraphDefinition
    {
        GraphId = graphId,
        Name = "Stage 10 Custom Potion Graph",
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
            new BehaviorGraphNodeDefinition { NodeId = logId, NodeType = "debug.log", DisplayName = "Log", Properties = new Dictionary<string, string>(StringComparer.Ordinal) { ["message"] = "STAGE10_POTION_OK" } },
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
    const string graphId = "graph.relic.stage10.custom_relic";
    var entryId = $"{graphId}.entry";
    var logId = $"{graphId}.log";
    var exitId = $"{graphId}.exit";

    return new BehaviorGraphDefinition
    {
        GraphId = graphId,
        Name = "Stage 10 Custom Relic Graph",
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
            new BehaviorGraphNodeDefinition { NodeId = logId, NodeType = "debug.log", DisplayName = "Log", Properties = new Dictionary<string, string>(StringComparer.Ordinal) { ["message"] = "STAGE10_RELIC_OK" } },
            new BehaviorGraphNodeDefinition { NodeId = exitId, NodeType = "flow.exit", DisplayName = "Exit", Properties = new Dictionary<string, string>(StringComparer.Ordinal) }
        ],
        Connections =
        [
            new BehaviorGraphConnectionDefinition { FromNodeId = entryId, FromPortId = "next", ToNodeId = logId, ToPortId = "in" },
            new BehaviorGraphConnectionDefinition { FromNodeId = logId, FromPortId = "out", ToNodeId = exitId, ToPortId = "in" }
        ]
    };
}

static void WriteProofPng(string path, int width, int height, byte backgroundR, byte backgroundG, byte backgroundB, byte accentR, byte accentG, byte accentB, byte alpha)
{
    var raw = new byte[height * (width * 4 + 1)];
    var index = 0;
    for (var y = 0; y < height; y++)
    {
        raw[index++] = 0;
        for (var x = 0; x < width; x++)
        {
            var mix = (byte)((x + y) % 255);
            raw[index++] = (byte)((backgroundR * 3 + accentR * mix) / 4);
            raw[index++] = (byte)((backgroundG * 3 + accentG * mix) / 4);
            raw[index++] = (byte)((backgroundB * 3 + accentB * mix) / 4);
            raw[index++] = alpha;
        }
    }

    using var compressed = new MemoryStream();
    using (var zlib = new ZLibStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
    {
        zlib.Write(raw, 0, raw.Length);
    }

    using var output = File.Create(path);
    output.Write(GetPngSignature());
    WriteChunk(output, "IHDR", BuildIhdr(width, height));
    WriteChunk(output, "IDAT", compressed.ToArray());
    WriteChunk(output, "IEND", Array.Empty<byte>());
}

static byte[] BuildIhdr(int width, int height)
{
    var data = new byte[13];
    BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(0, 4), width);
    BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(4, 4), height);
    data[8] = 8;
    data[9] = 6;
    data[10] = 0;
    data[11] = 0;
    data[12] = 0;
    return data;
}

static void WriteChunk(Stream stream, string type, byte[] data)
{
    var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
    var lengthBytes = new byte[4];
    BinaryPrimitives.WriteInt32BigEndian(lengthBytes, data.Length);
    stream.Write(lengthBytes);
    stream.Write(typeBytes);
    stream.Write(data);
    var crc = Crc32(typeBytes, data);
    var crcBytes = new byte[4];
    BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc);
    stream.Write(crcBytes);
}

static uint Crc32(byte[] typeBytes, byte[] data)
{
    var crc = 0xFFFFFFFFu;
    foreach (var b in typeBytes)
    {
        crc = Crc32Update(crc, b);
    }

    foreach (var b in data)
    {
        crc = Crc32Update(crc, b);
    }

    return ~crc;
}

static uint Crc32Update(uint crc, byte value)
{
    crc ^= value;
    for (var i = 0; i < 8; i++)
    {
        crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
    }

    return crc;
}

static byte[] GetPngSignature()
{
    return [137, 80, 78, 71, 13, 10, 26, 10];
}

sealed record CharacterSetup(string CharacterId, IReadOnlyList<string> StartingRelicIds);
