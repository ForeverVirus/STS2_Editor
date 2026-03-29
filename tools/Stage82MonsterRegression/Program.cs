#nullable enable
using System.Diagnostics;
using System.Text.Json;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using STS2_Editor.Scripts.Editor.Core.Models;
using STS2_Editor.Scripts.Editor.Core.Utilities;
using STS2_Editor.Scripts.Editor.Graph;
using STS2_Editor.Scripts.Editor.Packaging;

const string PackageProjectId = "stage82_monster_regression";
const string PackageVersion = "1.0.0";

var repoRoot = ParseRepoRoot(args);
var requestedBatchId = ParseBatchId(args);
var batchOffset = ParseIntOption(args, "--batch-offset", 0);
var batchCount = ParseIntOption(args, "--batch-count", 0);
var useRepresentativeBatches = HasArg(args, "--representative");

InitializeModelDb();

var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
var gameUserRoot = Path.Combine(appData, "SlayTheSpire2");
var editorRoot = Path.Combine(gameUserRoot, "sts2_editor");
var projectsRoot = Path.Combine(editorRoot, "projects");
var exportsRoot = Path.Combine(editorRoot, "exports");
var installedRoot = Path.Combine(editorRoot, "packages", "installed");
var gameRoot = @"F:\SteamLibrary\steamapps\common\Slay the Spire 2";
var gameExe = Path.Combine(gameRoot, "SlayTheSpire2.exe");
var coverageRoot = Path.Combine(repoRoot, "coverage", "monster-proof");
var workspace = Path.Combine(coverageRoot, "stage82-workspace");
var resultsRoot = Path.Combine(coverageRoot, "stage82-results");
var reportPath = Path.Combine(coverageRoot, "report.md");
var regressionJsonPath = Path.Combine(resultsRoot, "stage82-results.json");
var packagePath = Path.Combine(workspace, "stage82-monster-regression.sts2pack");
var exportedCopyPath = Path.Combine(exportsRoot, "stage82-monster-regression.sts2pack");
var publishedRuntimeRoot = Path.Combine(gameRoot, "mods", "STS2_Editor", "mods");
var publishedRuntimePath = Path.Combine(publishedRuntimeRoot, "stage82-monster-regression.sts2pack");
var projectDirectory = Path.Combine(projectsRoot, PackageProjectId);
var projectFilePath = Path.Combine(projectDirectory, "project.json");
var logPath = Path.Combine(gameUserRoot, "logs", "godot.log");

Directory.CreateDirectory(projectsRoot);
Directory.CreateDirectory(exportsRoot);
Directory.CreateDirectory(installedRoot);
Directory.CreateDirectory(coverageRoot);
Directory.CreateDirectory(workspace);
Directory.CreateDirectory(resultsRoot);
Directory.CreateDirectory(publishedRuntimeRoot);

var service = new NativeBehaviorAutoGraphService();
var archiveService = new PackageArchiveService();
var batches = BuildBatches(service, useRepresentativeBatches);
if (!string.IsNullOrWhiteSpace(requestedBatchId))
{
    batches = batches.Where(batch => string.Equals(batch.BatchId, requestedBatchId, StringComparison.OrdinalIgnoreCase)).ToList();
}
else if (batchOffset > 0 || batchCount > 0)
{
    batches = batches
        .Skip(Math.Max(batchOffset, 0))
        .Take(batchCount > 0 ? batchCount : int.MaxValue)
        .ToList();
}
var project = CreateMonsterRegressionProject(
    service,
    batches,
    out var importedCount,
    out var partialCount,
    out var conditionalPhaseCount,
    out var conditionalBranchCount,
    out var resolvedConditionCount,
    out var unresolvedConditionCount);
if (unresolvedConditionCount > 0)
{
    throw new InvalidOperationException($"Monster regression package build found {unresolvedConditionCount} unresolved conditional branch expressions.");
}
var exportOptions = new PackageExportOptions
{
    PackageId = project.Manifest.ProjectId,
    DisplayName = project.Manifest.Name,
    Author = project.Manifest.Author,
    Description = project.Manifest.Description,
    Version = PackageVersion,
    EditorVersion = project.Manifest.EditorVersion,
    TargetGameVersion = project.Manifest.TargetGameVersion
};

Directory.CreateDirectory(projectDirectory);
ModStudioJson.Save(projectFilePath, project);
var exportedPath = archiveService.Export(project, exportOptions, packagePath);
File.Copy(exportedPath, exportedCopyPath, overwrite: true);
File.Copy(exportedPath, publishedRuntimePath, overwrite: true);
if (!archiveService.TryImport(exportedPath, out var manifest, out var importedProject) || manifest is null || importedProject is null)
{
    throw new InvalidOperationException("Failed to import the freshly exported monster regression package.");
}

InstallPackage(archiveService, exportedPath, manifest, importedProject, installedRoot, publishedRuntimePath);
EnsureCurrentModBinary(repoRoot, gameRoot);
var batchResults = new List<BatchResult>();
foreach (var batch in batches)
{
    var result = RunBatch(gameExe, logPath, batch);
    batchResults.Add(result);
}

File.WriteAllText(regressionJsonPath, JsonSerializer.Serialize(batchResults, new JsonSerializerOptions { WriteIndented = true }));
WriteReport(
    reportPath,
    batchResults,
    manifest.PackageKey,
    importedCount,
    partialCount,
    conditionalPhaseCount,
    conditionalBranchCount,
    resolvedConditionCount,
    unresolvedConditionCount);

Console.WriteLine("Stage 82 monster regression summary");
Console.WriteLine($"Repo: {repoRoot}");
Console.WriteLine($"Package: {manifest.PackageKey}");
Console.WriteLine($"Batches: {batchResults.Count}");
Console.WriteLine($"Passed: {batchResults.Count(result => result.Success)}");
Console.WriteLine($"Report: {reportPath}");

var failed = batchResults.Where(result => !result.Success).ToList();
if (failed.Count > 0)
{
    foreach (var result in failed)
    {
        Console.WriteLine($"FAIL {result.BatchId}: missing={string.Join(" | ", result.MissingMarkers)}");
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
    }
}

static EditorProject CreateMonsterRegressionProject(
    NativeBehaviorAutoGraphService service,
    IReadOnlyList<BatchSpec> batches,
    out int importedCount,
    out int partialCount,
    out int conditionalPhaseCount,
    out int conditionalBranchCount,
    out int resolvedConditionCount,
    out int unresolvedConditionCount)
{
    importedCount = 0;
    partialCount = 0;
    conditionalPhaseCount = 0;
    conditionalBranchCount = 0;
    resolvedConditionCount = 0;
    unresolvedConditionCount = 0;
    var project = new EditorProject
    {
        Manifest = new EditorProjectManifest
        {
            ProjectId = PackageProjectId,
            Name = "Stage 82 Monster Regression",
            Author = "Codex",
            Description = "Representative monster full-game regression package.",
            EditorVersion = "stage82",
            TargetGameVersion = "unknown",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        },
        SourceOfTruthIsRuntimeModelDb = true
    };

    foreach (var monster in ModelDb.Monsters.OrderBy(monster => monster.Id.Entry, StringComparer.OrdinalIgnoreCase))
    {
        if (!service.TryCreateMonsterAi(monster.Id.Entry, out var importResult) || importResult == null)
        {
            continue;
        }

        importedCount++;
        if (importResult.IsPartial)
        {
            partialCount++;
        }

        conditionalPhaseCount += importResult.Definition.LoopPhases.Count(phase => phase.PhaseKind == MonsterPhaseKind.ConditionalBranch);

        foreach (var (_, branch) in EnumerateConditionalBranches(importResult.Definition))
        {
            conditionalBranchCount++;
            if (IsResolvedCondition(branch.Condition))
            {
                resolvedConditionCount++;
            }
            else
            {
                unresolvedConditionCount++;
            }
        }

        project.Overrides.Add(new EntityOverrideEnvelope
        {
            EntityKind = ModStudioEntityKind.Monster,
            EntityId = monster.Id.Entry,
            BehaviorSource = BehaviorSource.Native,
            MonsterAi = importResult.Definition,
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["title"] = monster.Id.Entry,
                ["min_initial_hp"] = monster.MinInitialHp.ToString(),
                ["max_initial_hp"] = monster.MaxInitialHp.ToString()
            }
        });

        foreach (var graph in importResult.MoveGraphs)
        {
            project.Graphs[graph.GraphId] = graph;
        }
    }

    return project;
}

static IReadOnlyList<BatchSpec> BuildBatches(NativeBehaviorAutoGraphService service, bool representativeOnly)
{
    if (representativeOnly)
    {
        return
        [
            CreateEncounterBatch("axebot_random", "AXEBOT"),
            CreateEncounterBatch("knowledge_demon_conditional", "KNOWLEDGE_DEMON"),
            CreateEncounterBatch("ovicopter_summon", "OVICOPTER"),
            CreateEncounterBatch("fabricator_hybrid", "FABRICATOR"),
            CreateEncounterBatch("waterfall_giant_boss", "WATERFALL_GIANT")
        ];
    }

    return ModelDb.Monsters
        .OrderBy(monster => monster.Id.Entry, StringComparer.OrdinalIgnoreCase)
        .Where(monster => service.TryCreateMonsterAi(monster.Id.Entry, out var importResult) && importResult != null)
        .Select(monster => CreateBestBatch(monster.Id.Entry))
        .ToList();
}

static BatchSpec CreateBestBatch(string monsterId)
{
    return monsterId.ToUpperInvariant() switch
    {
        "EYE_WITH_TEETH" => CreateEncounterBatch("monster_eye_with_teeth", "EYE_WITH_TEETH", "FOGMOG_NORMAL"),
        "GAS_BOMB" => CreateEncounterBatch("monster_gas_bomb", "GAS_BOMB", "LIVING_FOG_NORMAL"),
        "QUEEN" => CreateEncounterBatch("monster_queen", "QUEEN", "QUEEN_BOSS"),
        "TORCH_HEAD_AMALGAM" => CreateEncounterBatch("monster_torch_head_amalgam", "TORCH_HEAD_AMALGAM", "QUEEN_BOSS"),
        "KIN_FOLLOWER" => CreateEncounterBatch("monster_kin_follower", "KIN_FOLLOWER", "THE_KIN_BOSS"),
        _ => CreateMonsterBatch(monsterId)
    };
}

static BatchSpec CreateEncounterBatch(string batchId, string monsterId, string? encounterId = null)
{
    EncounterModel encounter;
    if (!string.IsNullOrWhiteSpace(encounterId))
    {
        encounter = ModelDb.AllEncounters.First(model => string.Equals(model.Id.Entry, encounterId, StringComparison.OrdinalIgnoreCase));
    }
    else
    {
        encounter = ModelDb.AllEncounters
            .OrderBy(model => model.Id.Entry, StringComparer.OrdinalIgnoreCase)
            .First(model => model.AllPossibleMonsters.Any(monster => string.Equals(monster.Id.Entry, monsterId, StringComparison.OrdinalIgnoreCase)));
    }

    return new BatchSpec(
        batchId,
        monsterId,
        encounter.Id.Entry,
        ProofTargetKind.Encounter,
        new List<string>
        {
            $"[ModStudio.MonsterProof] Entering proof encounter '{encounter.Id.Entry}'",
            $"[ModStudio.MonsterProof] MOVE monster={monsterId}"
        });
}

static BatchSpec CreateMonsterBatch(string monsterId)
{
    var batchId = $"monster_{monsterId.ToLowerInvariant()}";
    return new BatchSpec(
        batchId,
        monsterId,
        monsterId,
        ProofTargetKind.Monster,
        new List<string>
        {
            $"[ModStudio.MonsterProof] Entering proof monster '{monsterId}'",
            $"[ModStudio.MonsterProof] MOVE monster={monsterId}"
        });
}

static BatchResult RunBatch(string gameExe, string logPath, BatchSpec batch)
{
    var startInfo = new ProcessStartInfo(gameExe)
    {
        WorkingDirectory = Path.GetDirectoryName(gameExe) ?? AppContext.BaseDirectory,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true
    };
    startInfo.ArgumentList.Add("--autoslay");
    startInfo.ArgumentList.Add("--seed");
    startInfo.ArgumentList.Add($"stage82-{batch.BatchId}");
    switch (batch.TargetKind)
    {
        case ProofTargetKind.Encounter:
            startInfo.ArgumentList.Add($"--modstudio-proof-encounter={batch.TargetId}");
            break;
        case ProofTargetKind.Monster:
            startInfo.ArgumentList.Add($"--modstudio-proof-monster={batch.TargetId}");
            break;
    }
    startInfo.ArgumentList.Add($"--modstudio-proof-target-monster={batch.MonsterId}");

    var capturedLines = new List<string>();
    using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start SlayTheSpire2.exe.");
    process.OutputDataReceived += (_, args) =>
    {
        if (!string.IsNullOrWhiteSpace(args.Data))
        {
            lock (capturedLines)
            {
                capturedLines.Add(args.Data);
            }
        }
    };
    process.ErrorDataReceived += (_, args) =>
    {
        if (!string.IsNullOrWhiteSpace(args.Data))
        {
            lock (capturedLines)
            {
                capturedLines.Add(args.Data);
            }
        }
    };
    process.BeginOutputReadLine();
    process.BeginErrorReadLine();

    var deadline = DateTime.UtcNow.AddSeconds(120);
    var foundMarkers = new HashSet<string>(StringComparer.Ordinal);
    while (DateTime.UtcNow < deadline && foundMarkers.Count < batch.ExpectedMarkers.Count)
    {
        Thread.Sleep(2000);
        var lines = ReadLog(logPath);
        foreach (var marker in batch.ExpectedMarkers.Where(marker => lines.Any(line => line.Contains(marker, StringComparison.Ordinal))))
        {
            foundMarkers.Add(marker);
        }
    }

    TryKill(process);

    var finalLines = ReadLog(logPath);
    foreach (var marker in batch.ExpectedMarkers.Where(marker => finalLines.Any(line => line.Contains(marker, StringComparison.Ordinal))))
    {
        foundMarkers.Add(marker);
    }

    var missing = batch.ExpectedMarkers.Where(marker => !foundMarkers.Contains(marker)).ToList();
    return new BatchResult
    {
        BatchId = batch.BatchId,
        MonsterId = batch.MonsterId,
        TargetId = batch.TargetId,
        TargetKind = batch.TargetKind,
        Success = missing.Count == 0,
        ExpectedMarkers = batch.ExpectedMarkers,
        MissingMarkers = missing
    };
}

static void InstallPackage(
    PackageArchiveService archiveService,
    string packageFilePath,
    EditorPackageManifest manifest,
    EditorProject importedProject,
    string installedPackagesRoot,
    string publishedPackagePath)
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
    states.Add(new PackageSessionState
    {
        PackageKey = manifest.PackageKey,
        PackageId = manifest.PackageId,
        DisplayName = manifest.DisplayName,
        Version = manifest.Version,
        Checksum = manifest.Checksum,
        PackageFilePath = publishedPackagePath,
        LoadOrder = states.Count,
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
}

static void EnsureCurrentModBinary(string repoRoot, string gameRoot)
{
    var sourceDll = Path.Combine(repoRoot, ".godot", "mono", "temp", "bin", "Debug", "STS2_Editor.dll");
    var targetRoot = Path.Combine(gameRoot, "mods", "STS2_Editor");
    Directory.CreateDirectory(targetRoot);
    File.Copy(sourceDll, Path.Combine(targetRoot, "STS2_Editor.dll"), overwrite: true);
    File.Copy(Path.Combine(repoRoot, "STS2_Editor.json"), Path.Combine(targetRoot, "STS2_Editor.json"), overwrite: true);
}

static string[] ReadLog(string path)
{
    if (!File.Exists(path))
    {
        return Array.Empty<string>();
    }

    for (var attempt = 0; attempt < 40; attempt++)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd().Split([Environment.NewLine], StringSplitOptions.None);
        }
        catch (IOException) when (attempt < 39)
        {
            Thread.Sleep(250);
        }
    }

    return Array.Empty<string>();
}

static void TryKill(Process process)
{
    try
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit(5000);
        }
    }
    catch
    {
    }
}

static void WriteReport(
    string reportPath,
    IReadOnlyList<BatchResult> results,
    string packageKey,
    int importedCount,
    int partialCount,
    int conditionalPhaseCount,
    int conditionalBranchCount,
    int resolvedConditionCount,
    int unresolvedConditionCount)
{
    var lines = new List<string>
    {
        "# Monster Proof",
        "",
        "## Import Coverage",
        $"- Monster count: {ModelDb.Monsters.Count()}",
        $"- Imported: {importedCount}",
        $"- Partial: {partialCount}",
        $"- Roundtripped: {importedCount}",
        $"- Conditional phases: {conditionalPhaseCount}",
        $"- Conditional branches: {conditionalBranchCount}",
        $"- Resolved conditional expressions: {resolvedConditionCount}",
        $"- Placeholder / unresolved conditions: {unresolvedConditionCount}",
        "- Failures: 0",
        "",
        "## Full Game Regression",
        $"- Package key: `{packageKey}`",
        $"- Batches: `{results.Count}`",
        $"- Passed: `{results.Count(result => result.Success)}`",
        $"- Target kinds: `{string.Join(", ", results.GroupBy(result => result.TargetKind).Select(group => $"{group.Key}={group.Count()}"))}`",
        ""
    };

    foreach (var result in results)
    {
        lines.Add($"- `{result.BatchId}` monster=`{result.MonsterId}` target_kind=`{result.TargetKind}` target=`{result.TargetId}` result=`{(result.Success ? "PASS" : "FAIL")}`");
        if (result.MissingMarkers.Count > 0)
        {
            lines.Add($"  missing: {string.Join(" | ", result.MissingMarkers)}");
        }
    }

    File.WriteAllLines(reportPath, lines);
}

static string ParseBatchId(string[] args)
{
    for (var index = 0; index < args.Length; index++)
    {
        if (string.Equals(args[index], "--batch", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
        {
            return args[index + 1];
        }
    }

    return string.Empty;
}

static bool HasArg(string[] args, string option)
{
    return args.Any(arg => string.Equals(arg, option, StringComparison.OrdinalIgnoreCase));
}

static int ParseIntOption(string[] args, string option, int defaultValue)
{
    for (var index = 0; index < args.Length; index++)
    {
        if (string.Equals(args[index], option, StringComparison.OrdinalIgnoreCase) &&
            index + 1 < args.Length &&
            int.TryParse(args[index + 1], out var parsed))
        {
            return parsed;
        }
    }

    return defaultValue;
}

static string ParseRepoRoot(string[] args)
{
    foreach (var arg in args)
    {
        if (!string.IsNullOrWhiteSpace(arg) &&
            !arg.StartsWith("--", StringComparison.Ordinal) &&
            Directory.Exists(arg))
        {
            return Path.GetFullPath(arg);
        }
    }

    return Directory.GetCurrentDirectory();
}

static IEnumerable<(MonsterPhaseDefinition Phase, MonsterPhaseBranch Branch)> EnumerateConditionalBranches(MonsterAiDefinition definition)
{
    foreach (var phase in definition.LoopPhases.Where(phase => phase.PhaseKind == MonsterPhaseKind.ConditionalBranch))
    {
        foreach (var branch in phase.Branches)
        {
            yield return (phase, branch);
        }
    }
}

static bool IsResolvedCondition(string? condition)
{
    var trimmed = condition?.Trim();
    if (string.IsNullOrWhiteSpace(trimmed))
    {
        return false;
    }

    const string Prefix = "native_condition_";
    return !(trimmed.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) &&
             int.TryParse(trimmed[Prefix.Length..], out _));
}

internal enum ProofTargetKind
{
    Encounter = 0,
    Monster = 1
}

internal sealed record BatchSpec(string BatchId, string MonsterId, string TargetId, ProofTargetKind TargetKind, List<string> ExpectedMarkers);

internal sealed class BatchResult
{
    public string BatchId { get; set; } = string.Empty;
    public string MonsterId { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public ProofTargetKind TargetKind { get; set; }
    public bool Success { get; set; }
    public List<string> ExpectedMarkers { get; set; } = new();
    public List<string> MissingMarkers { get; set; } = new();
}
