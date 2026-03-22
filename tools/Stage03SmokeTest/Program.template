using System.Text.Json;
using STS2_Editor.Scripts.Editor.Core.Models;
using STS2_Editor.Scripts.Editor.Core.Utilities;
using STS2_Editor.Scripts.Editor.Graph;
using STS2_Editor.Scripts.Editor.Packaging;
using STS2_Editor.Scripts.Editor.Runtime;

var root = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])
    ? Path.GetFullPath(args[0])
    : Path.Combine(Path.GetTempPath(), $"sts2-editor-stage03-smoke-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}");

Directory.CreateDirectory(root);
var workspace = Path.Combine(root, "workspace");
Directory.CreateDirectory(workspace);

var report = new List<string>();
var failures = new List<string>();

Run("graph registry and validation", () => TestGraphValidation(report));
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
