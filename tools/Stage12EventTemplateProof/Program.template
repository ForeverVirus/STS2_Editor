using System.Text.Json;
using MegaCrit.Sts2.Core.Helpers;
using STS2_Editor.Scripts.Editor.Core.Models;
using STS2_Editor.Scripts.Editor.Core.Utilities;
using STS2_Editor.Scripts.Editor.Packaging;

const string ProofProjectId = "stage12_event_template_proof";
const string ProofVersion = "1.0.0";
const string CustomEventEditorId = "ed_stage12__event_001";
const string CustomEventRuntimeId = "ED_STAGE12_EVENT001";
const string ProofEncounterId = "LOUSE_PROGENITOR_NORMAL";

var root = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])
    ? Path.GetFullPath(args[0])
    : Path.Combine(Path.GetTempPath(), $"sts2-editor-stage12-proof-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}");

Directory.CreateDirectory(root);
var workspace = Path.Combine(root, "workspace");
Directory.CreateDirectory(workspace);

var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
var gameUserRoot = Path.Combine(appData, "SlayTheSpire2");
var editorRoot = Path.Combine(gameUserRoot, "sts2_editor");
var projectsRoot = Path.Combine(editorRoot, "projects");
var exportsRoot = Path.Combine(editorRoot, "exports");
var installedRoot = Path.Combine(editorRoot, "packages", "installed");
var reportPath = Path.Combine(workspace, "stage12-event-template-proof-report.txt");
var packagePath = Path.Combine(workspace, "stage12-event-template-proof.sts2pack");
var exportedCopyPath = Path.Combine(exportsRoot, "stage12-event-template-proof.sts2pack");
var projectDirectory = Path.Combine(projectsRoot, ProofProjectId);
var projectFilePath = Path.Combine(projectDirectory, "project.json");

Directory.CreateDirectory(gameUserRoot);
Directory.CreateDirectory(editorRoot);
Directory.CreateDirectory(projectsRoot);
Directory.CreateDirectory(exportsRoot);
Directory.CreateDirectory(installedRoot);

var report = new List<string>
{
    "Stage 12 Event Template Proof Package Generator",
    $"Workspace: {workspace}",
    $"Game user root: {gameUserRoot}",
    $"Editor root: {editorRoot}"
};

var project = CreateProofProject();
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

var exportedPath = archiveService.Export(project, exportOptions, packagePath);
File.Copy(exportedPath, exportedCopyPath, overwrite: true);
report.Add($"Exported package: {exportedPath}");
report.Add($"Copied package to editor exports: {exportedCopyPath}");

if (!archiveService.TryImport(exportedPath, out var manifest, out var importedProject) || manifest is null || importedProject is null)
{
    throw new InvalidOperationException("Failed to import the freshly exported Stage 12 event proof package.");
}

InstallPackage(archiveService, exportedPath, manifest, importedProject, installedRoot, report);
WriteReport(reportPath, report);

Console.WriteLine("Stage 12 event-template proof package prepared.");
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
    report.Add("Recommended proof launch:");
    report.Add($"  - SlayTheSpire2.exe --autoslay --seed stage12-event-proof --modstudio-proof-event={CustomEventRuntimeId}");
    report.Add("Expected godot.log evidence:");
    report.Add($"  - [ModStudio.Proof] Entering proof event '{CustomEventRuntimeId}'");
    report.Add($"  - [ModStudio.Event] Initializing template event {CustomEventRuntimeId} -> page INITIAL");
    report.Add($"  - [ModStudio.Event] Starting template combat {CustomEventRuntimeId} encounter={ProofEncounterId} resume=AFTER_COMBAT");
    report.Add($"  - [ModStudio.Event] Resuming template event {CustomEventRuntimeId} -> page AFTER_COMBAT");
    report.Add($"  - [ModStudio.Event] Proceeding out of template event {CustomEventRuntimeId} via option PROCEED");
}

static void WriteReport(string reportPath, IEnumerable<string> lines)
{
    File.WriteAllLines(reportPath, lines);
}

static EditorProject CreateProofProject()
{
    var proofPortraitPath = ImageHelper.GetImagePath("events/slippery_bridge.png");

    return new EditorProject
    {
        Manifest = new EditorProjectManifest
        {
            ProjectId = ProofProjectId,
            Name = "Stage 12 Event Template Proof",
            Author = "Codex",
            Description = "Controlled package for real-game validation of brand-new template events.",
            EditorVersion = "stage12",
            TargetGameVersion = "Slay the Spire 2 / Godot 4.5.1",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        },
        Overrides =
        [
            new EntityOverrideEnvelope
            {
                EntityKind = ModStudioEntityKind.Event,
                EntityId = CustomEventEditorId,
                BehaviorSource = BehaviorSource.Native,
                Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["title"] = "Stage 12 Proof Event",
                    ["layout_type"] = "Default",
                    ["is_shared"] = true.ToString(),
                    ["portrait_path"] = proofPortraitPath,
                    ["event_start_page_id"] = "INITIAL",
                    ["event_page.INITIAL.description"] = "Stage 12 initial page. Choose the only option to enter the proof combat.",
                    ["event_page.INITIAL.option_order"] = "PROVE_COMBAT",
                    ["event_option.INITIAL.PROVE_COMBAT.title"] = "Start proof combat",
                    ["event_option.INITIAL.PROVE_COMBAT.description"] = "Launch the template-controlled combat encounter.",
                    ["event_option.INITIAL.PROVE_COMBAT.encounter_id"] = ProofEncounterId,
                    ["event_option.INITIAL.PROVE_COMBAT.resume_page_id"] = "AFTER_COMBAT",
                    ["event_page.AFTER_COMBAT.description"] = "Stage 12 proof complete. Proceed back to the map.",
                    ["event_page.AFTER_COMBAT.option_order"] = "PROCEED",
                    ["event_option.AFTER_COMBAT.PROCEED.title"] = "Proceed",
                    ["event_option.AFTER_COMBAT.PROCEED.description"] = "Return to the map and let autoslay continue.",
                    ["event_option.AFTER_COMBAT.PROCEED.is_proceed"] = true.ToString(),
                    ["event_option.AFTER_COMBAT.PROCEED.save_choice_to_history"] = false.ToString()
                },
                Notes = "Brand-new dynamic event used for real-game template validation."
            }
        ],
        SourceOfTruthIsRuntimeModelDb = true
    };
}
