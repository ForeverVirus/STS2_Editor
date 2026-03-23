using System.Text.Json;
using STS2_Editor.Scripts.Editor;
using STS2_Editor.Scripts.Editor.Core.Models;
using STS2_Editor.Scripts.Editor.Core.Utilities;
using STS2_Editor.Scripts.Editor.Graph;
using STS2_Editor.Scripts.Editor.Packaging;
using STS2_Editor.Scripts.Editor.Runtime;

var root = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])
    ? Path.GetFullPath(args[0])
    : Path.Combine(Path.GetTempPath(), $"sts2-editor-stage21-22-smoke-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}");

Directory.CreateDirectory(root);
ModStudioPaths.EnsureAllDirectories();

var report = new List<string>();
var failures = new List<string>();

Run("real directory project workflow", () => TestRealDirectoryProjectWorkflow(root, report));
Run("published package discovery and hot reload", () => TestPublishedPackageDiscovery(report));

Console.WriteLine("Stage 21-22 smoke test summary");
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

void TestRealDirectoryProjectWorkflow(string workspaceRoot, List<string> output)
{
    var store = new EditorProjectStore();
    var projectRoot = Path.Combine(workspaceRoot, "real-project");
    var project = store.CreateProject(projectRoot, "Stage21 Project", "Codex", "real directory smoke", overwriteExistingProject: true);

    var graph = BehaviorGraphTemplateFactory.CreateDefaultScaffold("stage21_graph", ModStudioEntityKind.Card, "Stage21 Graph", "Smoke graph");
    project.Graphs[graph.GraphId] = graph;
    project.Overrides.Add(new EntityOverrideEnvelope
    {
        EntityKind = ModStudioEntityKind.Card,
        EntityId = "stage21_card",
        BehaviorSource = BehaviorSource.Graph,
        GraphId = graph.GraphId,
        Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Name"] = "Stage21 Card",
            ["Description"] = "Smoke test card"
        }
    });

    store.Save(project, projectRoot);

    var projectFile = Path.Combine(projectRoot, "project.json");
    var assetsDirectory = Path.Combine(projectRoot, "assets");
    if (!File.Exists(projectFile))
    {
        throw new InvalidOperationException("project.json was not created in the selected real directory.");
    }

    if (!Directory.Exists(assetsDirectory))
    {
        throw new InvalidOperationException("assets directory was not created in the selected real directory.");
    }

    if (!store.TryLoad(projectRoot, out var loadedByRoot) || loadedByRoot is null)
    {
        throw new InvalidOperationException("Project could not be re-opened from the selected directory.");
    }

    if (!store.TryLoad(projectFile, out var loadedByFile) || loadedByFile is null)
    {
        throw new InvalidOperationException("Project could not be re-opened from project.json.");
    }

    var settingsPath = ModStudioPaths.SettingsFilePath;
    if (!File.Exists(settingsPath))
    {
        throw new InvalidOperationException("Settings file was not created after opening a real directory project.");
    }

    var settings = JsonSerializer.Deserialize<ModStudioSettings>(File.ReadAllText(settingsPath), ModStudioJson.Options)
        ?? throw new InvalidOperationException("Settings file could not be parsed.");
    if (!string.Equals(Path.GetFullPath(projectRoot), Path.GetFullPath(settings.LastProjectPath), StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("LastProjectPath was not updated to the real directory project root.");
    }

    output.Add($"  Project root: {projectRoot}");
    output.Add($"  Project file: {projectFile}");
    output.Add($"  Recent project count: {settings.RecentProjectPaths.Count}");
}

void TestPublishedPackageDiscovery(List<string> output)
{
    var publishedRoot = ModStudioPaths.PublishedPackagesRootPath;
    var smokeRoot = Path.Combine(publishedRoot, "_codex_stage22_smoke");
    var archiveService = new PackageArchiveService();
    var backend = new RuntimePackageBackend(archiveService);

    Directory.CreateDirectory(smokeRoot);

    var projectA = CreatePackageProject("stage22-smoke-a", "Stage22 Smoke A");
    var projectB = CreatePackageProject("stage22-smoke-b", "Stage22 Smoke B");

    var packagePathA = Path.Combine(smokeRoot, "stage22-smoke-a.sts2pack");
    var packagePathB = Path.Combine(smokeRoot, "stage22-smoke-b.sts2pack");

    try
    {
        archiveService.Export(projectA, new PackageExportOptions
        {
            PackageId = "stage22-smoke-a",
            DisplayName = "Stage22 Smoke A",
            Version = "1.0.0",
            Author = "Codex",
            Description = "Stage22 package smoke test A"
        }, packagePathA);

        backend.Initialize();
        if (!backend.InstalledPackages.Any(package => string.Equals(package.PackageFilePath, Path.GetFullPath(packagePathA), StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Runtime package backend did not discover the first published .sts2pack.");
        }

        archiveService.Export(projectB, new PackageExportOptions
        {
            PackageId = "stage22-smoke-b",
            DisplayName = "Stage22 Smoke B",
            Version = "1.0.0",
            Author = "Codex",
            Description = "Stage22 package smoke test B"
        }, packagePathB);

        backend.RebuildFromInstalledPackages();
        if (!backend.InstalledPackages.Any(package => string.Equals(package.PackageFilePath, Path.GetFullPath(packagePathB), StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Runtime package backend did not discover the second published .sts2pack after hot reload.");
        }

        File.Delete(packagePathB);
        backend.RebuildFromInstalledPackages();
        if (backend.InstalledPackages.Any(package => string.Equals(package.PackageFilePath, Path.GetFullPath(packagePathB), StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Runtime package backend kept a deleted published package after hot reload.");
        }

        output.Add($"  Published root: {publishedRoot}");
        output.Add($"  Discovered package count after reload: {backend.InstalledPackages.Count}");
        output.Add($"  Smoke package A: {packagePathA}");
    }
    finally
    {
        TryDeleteFile(packagePathA);
        TryDeleteFile(packagePathB);
        TryDeleteDirectory(smokeRoot);
    }
}

static EditorProject CreatePackageProject(string projectId, string name)
{
    var graph = BehaviorGraphTemplateFactory.CreateDefaultScaffold($"{projectId}.graph", ModStudioEntityKind.Card, name, $"{name} graph");
    return new EditorProject
    {
        Manifest = new EditorProjectManifest
        {
            ProjectId = projectId,
            Name = name,
            Author = "Codex",
            Description = $"{name} description",
            TargetGameVersion = "smoke",
            EditorVersion = "2.0-smoke",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        },
        Overrides =
        {
            new EntityOverrideEnvelope
            {
                EntityKind = ModStudioEntityKind.Card,
                EntityId = $"{projectId}_card",
                BehaviorSource = BehaviorSource.Graph,
                GraphId = graph.GraphId,
                Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Name"] = name,
                    ["Description"] = $"{name} card"
                }
            }
        },
        Graphs = new Dictionary<string, BehaviorGraphDefinition>(StringComparer.Ordinal)
        {
            [graph.GraphId] = graph
        },
        SourceOfTruthIsRuntimeModelDb = true
    };
}

static void TryDeleteFile(string filePath)
{
    try
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }
    catch
    {
        // Cleanup only.
    }
}

static void TryDeleteDirectory(string directoryPath)
{
    try
    {
        if (Directory.Exists(directoryPath))
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }
    catch
    {
        // Cleanup only.
    }
}
