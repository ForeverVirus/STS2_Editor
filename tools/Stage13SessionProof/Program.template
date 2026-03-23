using System.Text.Json;
using STS2_Editor.Scripts.Editor.Core.Models;
using STS2_Editor.Scripts.Editor.Core.Utilities;
using STS2_Editor.Scripts.Editor.Packaging;

const string Version = "1.0.0";
const string CardId = "COOLHEADED";
const string PackageAId = "stage13_session_a";
const string PackageBId = "stage13_session_b";
const string PackageCId = "stage13_session_c";

var root = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])
    ? Path.GetFullPath(args[0])
    : Path.Combine(Path.GetTempPath(), $"sts2-editor-stage13-proof-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}");

Directory.CreateDirectory(root);
var workspace = Path.Combine(root, "workspace");
Directory.CreateDirectory(workspace);

var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
var gameUserRoot = Path.Combine(appData, "SlayTheSpire2");
var editorRoot = Path.Combine(gameUserRoot, "sts2_editor");
var projectsRoot = Path.Combine(editorRoot, "projects");
var exportsRoot = Path.Combine(editorRoot, "exports");
var installedRoot = Path.Combine(editorRoot, "packages", "installed");
var sessionPath = Path.Combine(installedRoot, "session.json");
var proofJsonPath = Path.Combine(workspace, "stage13-session-proof-peers.json");
var reportPath = Path.Combine(workspace, "stage13-session-proof-report.txt");

Directory.CreateDirectory(gameUserRoot);
Directory.CreateDirectory(editorRoot);
Directory.CreateDirectory(projectsRoot);
Directory.CreateDirectory(exportsRoot);
Directory.CreateDirectory(installedRoot);

var report = new List<string>
{
    "Stage 13 Session Proof Package Generator",
    $"Workspace: {workspace}",
    $"Game user root: {gameUserRoot}",
    $"Editor root: {editorRoot}"
};

var archiveService = new PackageArchiveService();
var packages = new[]
{
    CreateProofPackage(PackageAId, "A", "Stage13 A wins only if B absent."),
    CreateProofPackage(PackageBId, "B", "Stage13 B should win because it loads last among active packages."),
    CreateProofPackage(PackageCId, "C", "Stage13 C should be disabled by peer-missing negotiation.")
};

foreach (var package in packages)
{
    ExportAndInstall(package.Project, package.PackageId, package.DisplayName, archiveService, projectsRoot, exportsRoot, installedRoot, report);
}

var installedPackages = DiscoverInstalledPackages(installedRoot)
    .ToDictionary(package => package.PackageKey, StringComparer.Ordinal);

var sessionStates = File.Exists(sessionPath)
    ? JsonSerializer.Deserialize<List<PackageSessionState>>(File.ReadAllText(sessionPath), ModStudioJson.Options) ?? new List<PackageSessionState>()
    : new List<PackageSessionState>();

var stateLookup = sessionStates.ToDictionary(state => state.PackageKey, StringComparer.Ordinal);
foreach (var package in installedPackages.Values)
{
    if (!stateLookup.TryGetValue(package.PackageKey, out var state))
    {
        state = new PackageSessionState
        {
            PackageKey = package.PackageKey,
            PackageId = package.PackageId,
            DisplayName = package.DisplayName,
            Version = package.Version,
            Checksum = package.Checksum,
            PackageFilePath = package.PackageFilePath,
            Enabled = true,
            SessionEnabled = true,
            DisabledReason = string.Empty
        };
        sessionStates.Add(state);
        stateLookup[package.PackageKey] = state;
    }

    state.PackageId = package.PackageId;
    state.DisplayName = package.DisplayName;
    state.Version = package.Version;
    state.Checksum = package.Checksum;
    state.PackageFilePath = package.PackageFilePath;
}

var stage13Keys = packages
    .Select(package => $"{package.PackageId}@{Version}")
    .ToArray();
var nonStage13 = sessionStates
    .Where(state => !stage13Keys.Contains(state.PackageKey, StringComparer.Ordinal))
    .OrderBy(state => state.LoadOrder)
    .ThenBy(state => state.PackageKey, StringComparer.Ordinal)
    .ToList();

var stage13Ordered = new[]
{
    stateLookup[$"{PackageAId}@{Version}"],
    stateLookup[$"{PackageCId}@{Version}"],
    stateLookup[$"{PackageBId}@{Version}"]
};

foreach (var state in stage13Ordered)
{
    state.Enabled = true;
    state.SessionEnabled = true;
    state.DisabledReason = string.Empty;
}

var reordered = nonStage13.Concat(stage13Ordered).ToList();
for (var index = 0; index < reordered.Count; index++)
{
    reordered[index].LoadOrder = index;
}

ModStudioJson.Save(sessionPath, reordered);
report.Add($"Normalized session file: {sessionPath}");

var peerPackages = reordered
    .Where(state => state.Enabled)
    .Where(state => !string.Equals(state.PackageKey, $"{PackageCId}@{Version}", StringComparison.Ordinal))
    .Select(state => new RemotePeerPackageState
    {
        PackageKey = state.PackageKey,
        Checksum = state.Checksum
    })
    .ToList();

var proofRequest = new
{
    peerSnapshots = new[]
    {
        new
        {
            peerId = "peer-ab",
            packages = peerPackages
        }
    },
    focusPackageKeys = new[]
    {
        $"{PackageAId}@{Version}",
        $"{PackageBId}@{Version}",
        $"{PackageCId}@{Version}"
    },
    focusEntities = new[]
    {
        new
        {
            entityKind = "Card",
            entityId = CardId
        }
    }
};

ModStudioJson.Save(proofJsonPath, proofRequest);
report.Add($"Proof request JSON: {proofJsonPath}");
report.Add($"Focus card: {CardId}");
report.Add("Expected live-game log evidence:");
report.Add($"  - [ModStudio.SessionProof] Package {PackageAId}@{Version} enabled=True sessionEnabled=True");
report.Add($"  - [ModStudio.SessionProof] Package {PackageCId}@{Version} enabled=True sessionEnabled=False ... missing on peer");
report.Add($"  - [ModStudio.SessionProof] Package {PackageBId}@{Version} enabled=True sessionEnabled=True");
report.Add($"  - [ModStudio.SessionProof] Applied package order: ... {PackageAId}@{Version} -> {PackageBId}@{Version}");
report.Add($"  - [ModStudio.SessionProof] Conflict Card:{CardId} winner={PackageBId}@{Version}");
report.Add("Recommended proof launch:");
report.Add($"  - SlayTheSpire2.exe --modstudio-proof-peers=\"{proofJsonPath}\"");
report.Add("Automation note:");
report.Add("  - Start the game with the above argument, wait until main menu appears and SessionProof logs are written, then close the game.");

File.WriteAllLines(reportPath, report);
Console.WriteLine("Stage 13 session proof prepared.");
foreach (var line in report)
{
    Console.WriteLine(line);
}

return 0;

static (string PackageId, string DisplayName, EditorProject Project) CreateProofPackage(string packageId, string variant, string notes)
{
    return (packageId, $"Stage 13 Session {variant}", new EditorProject
    {
        Manifest = new EditorProjectManifest
        {
            ProjectId = packageId,
            Name = $"Stage 13 Session {variant}",
            Author = "Codex",
            Description = notes,
            EditorVersion = "stage13",
            TargetGameVersion = "Slay the Spire 2 / Godot 4.5.1",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        },
        Overrides =
        [
            new EntityOverrideEnvelope
            {
                EntityKind = ModStudioEntityKind.Card,
                EntityId = CardId,
                BehaviorSource = BehaviorSource.Native,
                Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["title"] = $"Stage13 {variant} Coolheaded",
                    ["description"] = $"Session proof variant {variant} for {CardId}."
                },
                Notes = notes
            }
        ],
        SourceOfTruthIsRuntimeModelDb = true
    });
}

static void ExportAndInstall(
    EditorProject project,
    string packageId,
    string displayName,
    PackageArchiveService archiveService,
    string projectsRoot,
    string exportsRoot,
    string installedRoot,
    IList<string> report)
{
    var projectDirectory = Path.Combine(projectsRoot, packageId);
    var projectFilePath = Path.Combine(projectDirectory, "project.json");
    var workspaceDirectory = Path.Combine(Path.GetTempPath(), "sts2-editor-stage13-export");
    Directory.CreateDirectory(workspaceDirectory);
    Directory.CreateDirectory(projectDirectory);
    ModStudioJson.Save(projectFilePath, project);
    report.Add($"Saved project: {projectFilePath}");

    var packageFilePath = Path.Combine(workspaceDirectory, packageId + ".sts2pack");
    var exportOptions = new PackageExportOptions
    {
        PackageId = packageId,
        DisplayName = displayName,
        Author = project.Manifest.Author,
        Description = project.Manifest.Description,
        Version = Version,
        EditorVersion = project.Manifest.EditorVersion,
        TargetGameVersion = project.Manifest.TargetGameVersion
    };

    var exportedPath = archiveService.Export(project, exportOptions, packageFilePath);
    var exportedCopyPath = Path.Combine(exportsRoot, packageId + ".sts2pack");
    File.Copy(exportedPath, exportedCopyPath, overwrite: true);

    if (!archiveService.TryImport(exportedPath, out var manifest, out var importedProject) || manifest is null || importedProject is null)
    {
        throw new InvalidOperationException("Failed to re-import exported proof package " + packageId);
    }

    var packageDirectory = Path.Combine(installedRoot, manifest.PackageKey);
    if (Directory.Exists(packageDirectory))
    {
        Directory.Delete(packageDirectory, recursive: true);
    }

    Directory.CreateDirectory(packageDirectory);
    var normalizedProject = archiveService.NormalizeImportedProject(manifest, importedProject, installedRoot);
    archiveService.ExtractManagedAssets(exportedPath, manifest, normalizedProject, installedRoot);
    ModStudioJson.Save(Path.Combine(packageDirectory, "manifest.json"), manifest);
    ModStudioJson.Save(Path.Combine(packageDirectory, "project.json"), normalizedProject);

    report.Add($"Installed package: {packageDirectory}");
}

static IReadOnlyList<InstalledManifestRecord> DiscoverInstalledPackages(string installedRoot)
{
    var records = new List<InstalledManifestRecord>();
    if (!Directory.Exists(installedRoot))
    {
        return records;
    }

    foreach (var directory in Directory.EnumerateDirectories(installedRoot, "*", SearchOption.TopDirectoryOnly))
    {
        var manifestPath = Path.Combine(directory, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            continue;
        }

        var manifest = ModStudioJson.LoadOrDefault(manifestPath, () => new EditorPackageManifest());
        if (string.IsNullOrWhiteSpace(manifest.PackageKey))
        {
            continue;
        }

        records.Add(new InstalledManifestRecord
        {
            PackageKey = manifest.PackageKey,
            PackageId = manifest.PackageId,
            DisplayName = manifest.DisplayName,
            Version = manifest.Version,
            Checksum = manifest.Checksum,
            PackageFilePath = directory
        });
    }

    return records;
}

sealed class InstalledManifestRecord
{
    public string PackageKey { get; init; } = string.Empty;

    public string PackageId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string Version { get; init; } = string.Empty;

    public string Checksum { get; init; } = string.Empty;

    public string PackageFilePath { get; init; } = string.Empty;
}
