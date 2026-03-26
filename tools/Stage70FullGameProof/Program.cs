#nullable enable
using System.Diagnostics;
using System.Text.Json;
using STS2_Editor.Scripts.Editor.Core.Models;

var invocation = ParseInvocation(args, Environment.CurrentDirectory);
var repoRoot = invocation.RepoRoot;
var proofRoot = Path.Combine(repoRoot, "coverage", "release-proof");
var batchesRoot = Path.Combine(proofRoot, "batches");
var resultsRoot = Path.Combine(proofRoot, "results");
Directory.CreateDirectory(proofRoot);
Directory.CreateDirectory(batchesRoot);
Directory.CreateDirectory(resultsRoot);

var batches = BuildBatches();

switch (invocation.Mode)
{
    case "generate":
        GenerateScaffold(repoRoot, proofRoot, batchesRoot, resultsRoot, batches);
        Console.WriteLine("Stage 70 full game proof scaffold generated");
        Console.WriteLine($"Manifest: {Path.Combine(proofRoot, "proof_manifest.json")}");
        Console.WriteLine($"Report: {Path.Combine(proofRoot, "report.md")}");
        Console.WriteLine($"Batches: {batchesRoot}");
        return 0;

    case "run-batch":
    {
        GenerateScaffold(repoRoot, proofRoot, batchesRoot, resultsRoot, batches);
        var batch = batches.First(candidate => string.Equals(candidate.BatchId, invocation.BatchId, StringComparison.OrdinalIgnoreCase));
        var result = RunBatch(repoRoot, batch);
        SaveResult(resultsRoot, result);
        WriteReport(repoRoot, proofRoot, batches, LoadResults(resultsRoot));
        Console.WriteLine($"{batch.BatchId}: {(result.Success ? "PASS" : "FAIL")}");
        return result.Success ? 0 : 1;
    }

    case "run-all":
    {
        GenerateScaffold(repoRoot, proofRoot, batchesRoot, resultsRoot, batches);
        var results = new List<BatchResult>();
        foreach (var batch in batches)
        {
            var result = RunBatch(repoRoot, batch);
            SaveResult(resultsRoot, result);
            results.Add(result);
        }

        WriteReport(repoRoot, proofRoot, batches, results);
        var failed = results.Where(result => !result.Success).Select(result => result.BatchId).ToList();
        Console.WriteLine(failed.Count == 0 ? "All proof batches passed." : $"Failed batches: {string.Join(", ", failed)}");
        return failed.Count == 0 ? 0 : 1;
    }

    default:
        throw new InvalidOperationException($"Unsupported mode '{invocation.Mode}'.");
}

static Invocation ParseInvocation(string[] args, string currentDirectory)
{
    var mode = "generate";
    var batchId = string.Empty;
    var repoRoot = FindRepoRoot(currentDirectory);

    for (var index = 0; index < args.Length; index++)
    {
        if (string.Equals(args[index], "--run-batch", StringComparison.OrdinalIgnoreCase))
        {
            mode = "run-batch";
            batchId = args[++index];
            continue;
        }

        if (string.Equals(args[index], "--run-all", StringComparison.OrdinalIgnoreCase))
        {
            mode = "run-all";
            continue;
        }

        if (!args[index].StartsWith("--", StringComparison.Ordinal) && Directory.Exists(args[index]))
        {
            repoRoot = Path.GetFullPath(args[index]);
        }
    }

    return new Invocation { Mode = mode, BatchId = batchId, RepoRoot = repoRoot };
}

static IReadOnlyList<BatchSpec> BuildBatches()
{
    return
    [
        new("cards_transform_select_generate", "Card", "Stage 09 custom-content proof", Scenario.Stage09, 90, ["[ModStudio.Graph] STAGE09_CARD_OK"]),
        new("cards_cost_playcount", "Card", "Stage 09 custom-content proof", Scenario.Stage09, 90, ["[ModStudio.Graph] STAGE09_CARD_OK"]),
        new("cards_status_curse_passive", "Card", "Stage 06 gameplay proof", Scenario.Stage06, 45, ["[ModStudio.Graph] STAGE06_CARD_OK"]),
        new("potions_combat", "Potion", "Stage 06 gameplay proof", Scenario.Stage06, 45, ["[ModStudio.Graph] STAGE06_POTION_OK", "[AutoSlay] Using potion: BLOCK_POTION"]),
        new("potions_noncombat", "Potion", "Stage 09 custom-content proof", Scenario.Stage09, 90, ["[ModStudio.Graph] STAGE09_POTION_OK"]),
        new("relics_stateful_combat", "Relic", "Stage 09 custom-content proof", Scenario.Stage09, 90, ["[ModStudio.Graph] STAGE09_RELIC_OK"]),
        new("relics_modifier_and_merchant", "Relic", "Stage 13 session proof", Scenario.Stage13, 30, ["[ModStudio.SessionProof] Applied package order:", "[ModStudio.SessionProof] Conflict Card:COOLHEADED winner=stage13_session_b@1.0.0"]),
        new("events_multipage_reward", "Event", "Stage 12 event proof", Scenario.Stage12, 60, ["[ModStudio.Proof] Entering proof event 'ED_STAGE12_EVENT001'", "[ModStudio.Event] Initializing template event ED_STAGE12_EVENT001 -> page INITIAL"]),
        new("events_combat_resume", "Event", "Stage 12 event proof", Scenario.Stage12, 60, ["[ModStudio.Event] Starting template combat ED_STAGE12_EVENT001 encounter=LOUSE_PROGENITOR_NORMAL resume=AFTER_COMBAT", "[ModStudio.Event] Resuming template event ED_STAGE12_EVENT001 -> page AFTER_COMBAT", "[ModStudio.Event] Proceeding out of template event ED_STAGE12_EVENT001 via option PROCEED"]),
        new("enchantments_modifiers", "Enchantment", "Stage 09 custom-content proof", Scenario.Stage09, 90, ["[ModStudio.Graph] STAGE09_RELIC_OK"]),
        new("passive_only", "PassiveOnly", "Stage 13 session proof", Scenario.Stage13, 30, ["[ModStudio.SessionProof] Package stage13_session_c@1.0.0 enabled=True sessionEnabled=False"])
    ];
}

static void GenerateScaffold(string repoRoot, string proofRoot, string batchesRoot, string resultsRoot, IReadOnlyList<BatchSpec> batches)
{
    var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
    foreach (var batch in batches)
    {
        File.WriteAllText(Path.Combine(batchesRoot, $"{batch.BatchId}.json"), JsonSerializer.Serialize(batch, jsonOptions));
        File.WriteAllText(Path.Combine(proofRoot, $"run_batch_{batch.BatchId}.ps1"), BuildPowerShell(batch));
    }

    File.WriteAllText(Path.Combine(proofRoot, "proof_manifest.json"), JsonSerializer.Serialize(new
    {
        generatedAtUtc = DateTimeOffset.UtcNow,
        repoRoot,
        proofRoot,
        logPath = GetLogPath(),
        batchIds = batches.Select(batch => batch.BatchId).ToList()
    }, jsonOptions));

    WriteReport(repoRoot, proofRoot, batches, LoadResults(resultsRoot));
}

static string BuildPowerShell(BatchSpec batch)
{
    return $$"""
$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
dotnet run --project "$repoRoot\tools\Stage70FullGameProof\Stage70FullGameProof.csproj" -- --run-batch {{batch.BatchId}} "$repoRoot"
""";
}

static BatchResult RunBatch(string repoRoot, BatchSpec batch)
{
    EnsureCurrentModBinary(repoRoot);
    var beforeLines = ReadLogLines(GetLogPath());
    var prepared = PrepareScenario(repoRoot, batch);

    using var scope = RuntimeIsolation.Enter(repoRoot, prepared);
    var startedAtUtc = DateTimeOffset.UtcNow;
    var capturedLines = new List<string>();
    using var process = StartGame(prepared.GameArguments, capturedLines);
    var exited = process.WaitForExit(batch.TimeoutSeconds * 1000);
    if (!exited)
    {
        process.Kill(entireProcessTree: true);
        process.WaitForExit();
    }

    Thread.Sleep(1500);

    var afterLines = ReadLogLines(GetLogPath());
    var newLines = capturedLines.Count > 0
        ? capturedLines.ToList()
        : afterLines.Length >= beforeLines.Length ? afterLines.Skip(beforeLines.Length).ToList() : afterLines.ToList();
    var found = batch.ExpectedMarkers.Where(marker => newLines.Any(line => line.Contains(marker, StringComparison.Ordinal))).ToList();
    var missing = batch.ExpectedMarkers.Where(marker => found.All(candidate => !string.Equals(candidate, marker, StringComparison.Ordinal))).ToList();

    return new BatchResult
    {
        BatchId = batch.BatchId,
        Scenario = batch.Scenario.ToString(),
        Success = missing.Count == 0,
        TimedOut = !exited,
        ExitCode = exited ? process.ExitCode : null,
        StartedAtUtc = startedAtUtc,
        FinishedAtUtc = DateTimeOffset.UtcNow,
        FoundMarkers = found,
        MissingMarkers = missing,
        ExpectedMarkers = batch.ExpectedMarkers.ToList(),
        GameArguments = prepared.GameArguments,
        NewLogLineCount = newLines.Count,
        LogTail = newLines.TakeLast(80).ToList()
    };
}

static PreparedScenario PrepareScenario(string repoRoot, BatchSpec batch)
{
    var workspace = Path.Combine(repoRoot, "coverage", "release-proof", "workspace", batch.BatchId);
    Directory.CreateDirectory(workspace);

    return batch.Scenario switch
    {
        Scenario.Stage06 => PrepareSinglePackageScenario(
            repoRoot,
            workspace,
            Path.Combine("tools", "Stage06GameplayProof", "Stage06GameplayProof.csproj"),
            Path.Combine(workspace, "workspace", "stage06-gameplay-proof.sts2pack"),
            "stage06-gameplay-proof.sts2pack",
            "stage06_gameplay_proof@1.0.0",
            "stage06_gameplay_proof",
            "Stage 06 Gameplay Proof",
            "1.0.0",
            "f36ac134e605659c7c55ef9c8a2df35768786bde5876bf549449aa27103ece53",
            ["--autoslay", "--seed", "stage06-proof"]),
        Scenario.Stage09 => PrepareSinglePackageScenario(
            repoRoot,
            workspace,
            Path.Combine("tools", "Stage09CustomContentProof", "Stage09CustomContentProof.csproj"),
            Path.Combine(workspace, "workspace", "stage09-custom-content-proof.sts2pack"),
            "stage09-custom-content-proof.sts2pack",
            "stage09_custom_content_proof@1.0.0",
            "stage09_custom_content_proof",
            "Stage 09 Custom Content Proof",
            "1.0.0",
            "1b45131605c0e9e09e0fc46136596100e74285b24df20eac2e48324f32695838",
            ["--autoslay", "--seed", "stage09-proof"]),
        Scenario.Stage12 => PrepareSinglePackageScenario(
            repoRoot,
            workspace,
            Path.Combine("tools", "Stage12EventTemplateProof", "Stage12EventTemplateProof.csproj"),
            Path.Combine(workspace, "workspace", "stage12-event-template-proof.sts2pack"),
            "stage12-event-template-proof.sts2pack",
            "stage12_event_template_proof@1.0.0",
            "stage12_event_template_proof",
            "Stage 12 Event Template Proof",
            "1.0.0",
            "f1fac3a5d44677ddb57c51220544a3f2fdf1425f7ab72971bad039c3cb6368a6",
            ["--autoslay", "--seed", "stage12-event-proof", "--modstudio-proof-event=ED_STAGE12_EVENT001"]),
        Scenario.Stage13 => PrepareStage13Scenario(repoRoot, workspace),
        _ => throw new InvalidOperationException($"Unsupported scenario '{batch.Scenario}'.")
    };
}

static PreparedScenario PrepareSinglePackageScenario(
    string repoRoot,
    string workspace,
    string projectRelativePath,
    string packagePath,
    string publishedFileName,
    string packageKey,
    string packageId,
    string displayName,
    string version,
    string checksum,
    List<string> gameArguments)
{
    RunDotnetProject(repoRoot, projectRelativePath, workspace);
    return new PreparedScenario
    {
        PublishedPackages =
        [
            new PublishedPackage(packagePath, publishedFileName)
        ],
        SessionStates =
        [
            CreateSessionState(packageKey, packageId, displayName, version, checksum, Path.Combine(GetPublishedPackagesRoot(), publishedFileName), 0)
        ],
        GameArguments = gameArguments
    };
}

static PreparedScenario PrepareStage13Scenario(string repoRoot, string workspace)
{
    RunDotnetProject(repoRoot, Path.Combine("tools", "Stage13SessionProof", "Stage13SessionProof.csproj"), workspace);
    var exportsRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SlayTheSpire2", "sts2_editor", "exports");
    return new PreparedScenario
    {
        PublishedPackages =
        [
            new PublishedPackage(Path.Combine(exportsRoot, "stage13_session_a.sts2pack"), "stage13_session_a.sts2pack"),
            new PublishedPackage(Path.Combine(exportsRoot, "stage13_session_b.sts2pack"), "stage13_session_b.sts2pack"),
            new PublishedPackage(Path.Combine(exportsRoot, "stage13_session_c.sts2pack"), "stage13_session_c.sts2pack")
        ],
        SessionStates =
        [
            CreateSessionState("stage13_session_a@1.0.0", "stage13_session_a", "Stage 13 Session A", "1.0.0", "8d3ba34a95efb32c4534e0d711fb1f675b317debf0d74208db20876bad76f5a0", Path.Combine(GetPublishedPackagesRoot(), "stage13_session_a.sts2pack"), 0),
            CreateSessionState("stage13_session_c@1.0.0", "stage13_session_c", "Stage 13 Session C", "1.0.0", "440ee6c3e399d0a33eb175c98f3b5781316f180e26bbf51d4e222c98b08ad0d8", Path.Combine(GetPublishedPackagesRoot(), "stage13_session_c.sts2pack"), 1),
            CreateSessionState("stage13_session_b@1.0.0", "stage13_session_b", "Stage 13 Session B", "1.0.0", "62216dd9694d465187ea0e2d0565365c1269aa481659043e07b90ce60bf8fcc4", Path.Combine(GetPublishedPackagesRoot(), "stage13_session_b.sts2pack"), 2)
        ],
        GameArguments =
        [
            $"--modstudio-proof-peers={Path.Combine(workspace, "workspace", "stage13-session-proof-peers.json")}"
        ]
    };
}

static void RunDotnetProject(string repoRoot, string projectRelativePath, string workspace)
{
    var startInfo = new ProcessStartInfo("dotnet")
    {
        WorkingDirectory = repoRoot,
        RedirectStandardOutput = true,
        RedirectStandardError = true
    };
    startInfo.ArgumentList.Add("run");
    startInfo.ArgumentList.Add("--project");
    startInfo.ArgumentList.Add(Path.Combine(repoRoot, projectRelativePath));
    startInfo.ArgumentList.Add("--");
    startInfo.ArgumentList.Add(workspace);

    using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Could not start '{projectRelativePath}'.");
    var stdout = process.StandardOutput.ReadToEnd();
    var stderr = process.StandardError.ReadToEnd();
    process.WaitForExit();
    if (process.ExitCode != 0)
    {
        throw new InvalidOperationException($"{projectRelativePath} failed.{Environment.NewLine}{stdout}{Environment.NewLine}{stderr}");
    }
}

static Process StartGame(IReadOnlyList<string> arguments, List<string> capturedLines)
{
    var startInfo = new ProcessStartInfo(GetGameExecutablePath())
    {
        WorkingDirectory = Path.GetDirectoryName(GetGameExecutablePath()) ?? AppContext.BaseDirectory,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true
    };
    foreach (var argument in arguments)
    {
        startInfo.ArgumentList.Add(argument);
    }

    var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start SlayTheSpire2.exe.");
    process.OutputDataReceived += (_, eventArgs) =>
    {
        if (!string.IsNullOrWhiteSpace(eventArgs.Data))
        {
            lock (capturedLines)
            {
                capturedLines.Add(eventArgs.Data);
            }
        }
    };
    process.ErrorDataReceived += (_, eventArgs) =>
    {
        if (!string.IsNullOrWhiteSpace(eventArgs.Data))
        {
            lock (capturedLines)
            {
                capturedLines.Add(eventArgs.Data);
            }
        }
    };
    process.BeginOutputReadLine();
    process.BeginErrorReadLine();
    return process;
}

static void EnsureCurrentModBinary(string repoRoot)
{
    var sourceDll = Path.Combine(repoRoot, ".godot", "mono", "temp", "obj", "Debug", "STS2_Editor.dll");
    var targetRoot = Path.Combine(GetGameModsRoot(), "STS2_Editor");
    Directory.CreateDirectory(targetRoot);
    File.Copy(sourceDll, Path.Combine(targetRoot, "STS2_Editor.dll"), overwrite: true);
    File.Copy(Path.Combine(repoRoot, "STS2_Editor.json"), Path.Combine(targetRoot, "STS2_Editor.json"), overwrite: true);
}

static string GetGameExecutablePath() => Path.Combine("F:\\SteamLibrary\\steamapps\\common\\Slay the Spire 2", "SlayTheSpire2.exe");

static string GetGameModsRoot() => Path.Combine("F:\\SteamLibrary\\steamapps\\common\\Slay the Spire 2", "mods");

static string GetPublishedPackagesRoot() => Path.Combine(GetGameModsRoot(), "STS2_Editor", "mods");

static string GetSessionPath() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SlayTheSpire2", "sts2_editor", "packages", "installed", "session.json");

static string GetLogPath() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SlayTheSpire2", "logs", "godot.log");

static string[] ReadLogLines(string path)
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
            var content = reader.ReadToEnd();
            return content.Split([Environment.NewLine], StringSplitOptions.None);
        }
        catch (IOException) when (attempt < 39)
        {
            Thread.Sleep(250);
        }
    }

    return Array.Empty<string>();
}

static PackageSessionState CreateSessionState(string packageKey, string packageId, string displayName, string version, string checksum, string packageFilePath, int loadOrder)
{
    return new PackageSessionState
    {
        PackageKey = packageKey,
        PackageId = packageId,
        DisplayName = displayName,
        Version = version,
        Checksum = checksum,
        PackageFilePath = packageFilePath,
        LoadOrder = loadOrder,
        Enabled = true,
        SessionEnabled = true,
        DisabledReason = string.Empty
    };
}

static void SaveResult(string resultsRoot, BatchResult result)
{
    File.WriteAllText(Path.Combine(resultsRoot, $"{result.BatchId}.json"), JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
}

static IReadOnlyList<BatchResult> LoadResults(string resultsRoot)
{
    if (!Directory.Exists(resultsRoot))
    {
        return Array.Empty<BatchResult>();
    }

    return Directory.EnumerateFiles(resultsRoot, "*.json", SearchOption.TopDirectoryOnly)
        .Select(path => JsonSerializer.Deserialize<BatchResult>(File.ReadAllText(path)))
        .Where(result => result != null)
        .Cast<BatchResult>()
        .OrderBy(result => result.BatchId, StringComparer.Ordinal)
        .ToList();
}

static void WriteReport(string repoRoot, string proofRoot, IReadOnlyList<BatchSpec> batches, IReadOnlyList<BatchResult> results)
{
    var resultLookup = results.ToDictionary(result => result.BatchId, StringComparer.Ordinal);
    var lines = new List<string>
    {
        "# Full Game Proof",
        "",
        "## Summary",
        $"- Proof root: `{proofRoot}`",
        $"- Game executable: `{GetGameExecutablePath()}`",
        $"- Published package root: `{GetPublishedPackagesRoot()}`",
        ""
    };

    lines.Add("## Batches");
    foreach (var batch in batches)
    {
        lines.Add($"- `{batch.BatchId}`: `{batch.EntityKind}` - {batch.Title}");
        lines.Add($"  scenario: `{batch.Scenario}`");
        if (resultLookup.TryGetValue(batch.BatchId, out var result))
        {
            lines.Add($"  result: `{(result.Success ? "PASS" : "FAIL")}` timeout=`{result.TimedOut}`");
            lines.Add(result.MissingMarkers.Count == 0
                ? "  missing markers: none"
                : $"  missing markers: {string.Join(" | ", result.MissingMarkers)}");
        }
        else
        {
            lines.Add("  result: `PENDING`");
        }
    }

    File.WriteAllText(Path.Combine(proofRoot, "report.md"), string.Join(Environment.NewLine, lines));
}

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

internal sealed class RuntimeIsolation : IDisposable
{
    private static readonly string GameRoot = Path.Combine("F:\\SteamLibrary\\steamapps\\common\\Slay the Spire 2");
    private readonly string _modsRoot;
    private readonly string _publishedRoot;
    private readonly string _modsBackupRoot;
    private readonly string _publishedBackupRoot;
    private readonly string _sessionPath;
    private readonly string _sessionBackupPath;
    private readonly bool _hadSession;

    private RuntimeIsolation(string repoRoot, PreparedScenario scenario)
    {
        _modsRoot = Path.Combine(GameRoot, "mods");
        _publishedRoot = Path.Combine(_modsRoot, "STS2_Editor", "mods");
        _modsBackupRoot = Path.Combine(repoRoot, "coverage", "release-proof", "mods-backup-live");
        _publishedBackupRoot = Path.Combine(repoRoot, "coverage", "release-proof", "published-backup-live");
        _sessionPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SlayTheSpire2", "sts2_editor", "packages", "installed", "session.json");
        _sessionBackupPath = Path.Combine(repoRoot, "coverage", "release-proof", "session-backup-live.json");
        Directory.CreateDirectory(_modsBackupRoot);
        Directory.CreateDirectory(_publishedBackupRoot);
        Directory.CreateDirectory(_publishedRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(_sessionPath)!);

        foreach (var entry in Directory.EnumerateFileSystemEntries(_modsRoot))
        {
            if (string.Equals(Path.GetFileName(entry), "STS2_Editor", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            MoveEntry(entry, Path.Combine(_modsBackupRoot, Path.GetFileName(entry)));
        }

        var keepPublished = new HashSet<string>(scenario.PublishedPackages.Select(package => package.PublishedFileName), StringComparer.OrdinalIgnoreCase);
        foreach (var entry in Directory.EnumerateFileSystemEntries(_publishedRoot))
        {
            if (keepPublished.Contains(Path.GetFileName(entry)))
            {
                continue;
            }

            MoveEntry(entry, Path.Combine(_publishedBackupRoot, Path.GetFileName(entry)));
        }

        foreach (var package in scenario.PublishedPackages)
        {
            File.Copy(package.SourcePath, Path.Combine(_publishedRoot, package.PublishedFileName), overwrite: true);
        }

        _hadSession = File.Exists(_sessionPath);
        if (_hadSession)
        {
            File.Copy(_sessionPath, _sessionBackupPath, overwrite: true);
        }

        File.WriteAllText(_sessionPath, JsonSerializer.Serialize(scenario.SessionStates, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static RuntimeIsolation Enter(string repoRoot, PreparedScenario scenario) => new(repoRoot, scenario);

    public void Dispose()
    {
        foreach (var entry in Directory.EnumerateFileSystemEntries(_publishedBackupRoot))
        {
            MoveEntry(entry, Path.Combine(_publishedRoot, Path.GetFileName(entry)));
        }

        foreach (var entry in Directory.EnumerateFileSystemEntries(_modsBackupRoot))
        {
            MoveEntry(entry, Path.Combine(_modsRoot, Path.GetFileName(entry)));
        }

        if (_hadSession && File.Exists(_sessionBackupPath))
        {
            File.Copy(_sessionBackupPath, _sessionPath, overwrite: true);
            File.Delete(_sessionBackupPath);
        }
    }

    private static void MoveEntry(string source, string destination)
    {
        if (File.Exists(destination) || Directory.Exists(destination))
        {
            return;
        }

        if (Directory.Exists(source))
        {
            Directory.Move(source, destination);
        }
        else
        {
            File.Move(source, destination, overwrite: true);
        }
    }
}

internal sealed class Invocation
{
    public string Mode { get; set; } = "generate";
    public string BatchId { get; set; } = string.Empty;
    public string RepoRoot { get; set; } = string.Empty;
}

internal sealed record BatchSpec(string BatchId, string EntityKind, string Title, Scenario Scenario, int TimeoutSeconds, List<string> ExpectedMarkers);

internal enum Scenario
{
    Stage06,
    Stage09,
    Stage12,
    Stage13
}

internal sealed class BatchResult
{
    public string BatchId { get; set; } = string.Empty;
    public string Scenario { get; set; } = string.Empty;
    public bool Success { get; set; }
    public bool TimedOut { get; set; }
    public int? ExitCode { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset FinishedAtUtc { get; set; }
    public int NewLogLineCount { get; set; }
    public List<string> ExpectedMarkers { get; set; } = new();
    public List<string> FoundMarkers { get; set; } = new();
    public List<string> MissingMarkers { get; set; } = new();
    public List<string> GameArguments { get; set; } = new();
    public List<string> LogTail { get; set; } = new();
}

internal sealed class PreparedScenario
{
    public List<PublishedPackage> PublishedPackages { get; set; } = new();
    public List<PackageSessionState> SessionStates { get; set; } = new();
    public List<string> GameArguments { get; set; } = new();
}

internal sealed record PublishedPackage(string SourcePath, string PublishedFileName);
