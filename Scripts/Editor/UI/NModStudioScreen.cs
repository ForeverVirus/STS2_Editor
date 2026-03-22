using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using STS2_Editor.Scripts.Editor.Core.Models;
using STS2_Editor.Scripts.Editor.Core.Utilities;
using STS2_Editor.Scripts.Editor.Graph;
using STS2_Editor.Scripts.Editor.Packaging;

namespace STS2_Editor.Scripts.Editor.UI;

public sealed partial class NModStudioScreen : NSubmenu
{
    private const string BackButtonScenePath = "res://scenes/ui/back_button.tscn";
    private bool _uiBuilt;
    private Control? _defaultFocus;
    private Button? _projectTabButton;
    private Button? _packageTabButton;
    private VBoxContainer? _projectPage;
    private VBoxContainer? _packagePage;
    private VBoxContainer? _projectList;
    private VBoxContainer? _packageList;
    private HBoxContainer? _categoryRow;
    private GridContainer? _browserGrid;
    private RichTextLabel? _browserDetails;
    private RichTextLabel? _projectDetails;
    private RichTextLabel? _packageDetails;
    private Label? _stateLabel;
    private CheckButton? _graphToggle;
    private LineEdit? _graphIdEdit;
    private TextEdit? _metadataEdit;
    private TextEdit? _notesEdit;
    private FileDialog? _packageImportDialog;
    private ModStudioEntityKind _selectedKind = ModStudioEntityKind.Character;
    private EntityBrowserItem? _selectedBrowserItem;
    private PackageSessionState? _selectedPackage;
    private EditorProject? _currentProject;
    private IReadOnlyList<EditorProjectManifest> _projects = Array.Empty<EditorProjectManifest>();
    private IReadOnlyList<PackageSessionState> _packages = Array.Empty<PackageSessionState>();
    private readonly List<ModStudioEntityKind> _browserKinds = Enum.GetValues<ModStudioEntityKind>().ToList();
    private string _lastAction = "Ready";

    protected override Control? InitialFocusedControl => _defaultFocus ?? _projectTabButton;
    public static IEnumerable<string> AssetPaths => new[] { BackButtonScenePath };
    public static NModStudioScreen Create() => new();

    public override void _Ready()
    {
        EnsureUiBuilt();
        base.ConnectSignals();
        RefreshData();
        SetMode(true);
    }

    public override void OnSubmenuOpened()
    {
        RefreshData();
        SetMode(_projectPage?.Visible ?? true);
        _defaultFocus ??= _projectTabButton;
    }

    protected override void OnSubmenuShown()
    {
        base.OnSubmenuShown();
        _defaultFocus ??= _projectTabButton;
    }

    private void EnsureUiBuilt()
    {
        if (_uiBuilt) return;
        _uiBuilt = true;
        SetAnchorsPreset(Control.LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        BuildUiTree();
    }

    private void BuildUiTree()
    {
        var root = new MarginContainer { Name = "ModStudioRoot", AnchorRight = 1f, AnchorBottom = 1f, OffsetLeft = 70f, OffsetTop = 70f, OffsetRight = -70f, OffsetBottom = -70f };
        AddChild(root);
        var v = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        v.AddThemeConstantOverride("separation", 12);
        root.AddChild(v);

        var title = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        title.AddChild(new Label { Text = "Mod Studio", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        _stateLabel = new Label { HorizontalAlignment = HorizontalAlignment.Right, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Text = "Loading..." };
        title.AddChild(_stateLabel);
        v.AddChild(title);

        var tabs = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        tabs.AddThemeConstantOverride("separation", 8);
        _projectTabButton = MakeButton("Project Mode", () => SetMode(true), true);
        _packageTabButton = MakeButton("Package Mode", () => SetMode(false), true);
        tabs.AddChild(_projectTabButton);
        tabs.AddChild(_packageTabButton);
        v.AddChild(tabs);

        var pages = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        pages.AddThemeConstantOverride("separation", 18);
        _projectPage = BuildProjectPage();
        _packagePage = BuildPackagePage();
        pages.AddChild(_projectPage);
        pages.AddChild(_packagePage);
        var panel = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        var margin = new MarginContainer { AnchorRight = 1f, AnchorBottom = 1f };
        panel.AddChild(margin);
        margin.AddChild(pages);
        v.AddChild(panel);

        v.AddChild(new RichTextLabel { BbcodeEnabled = false, ScrollActive = false, AutowrapMode = TextServer.AutowrapMode.WordSmart, Text = "Runtime truth source is ModelDb and live models. sts2_guides is auxiliary only." });
        _packageImportDialog = new FileDialog { Name = "PackageImportDialog", Title = "Import STS2 Package", Access = FileDialog.AccessEnum.Filesystem, FileMode = FileDialog.FileModeEnum.OpenFile, UseNativeDialog = true };
        _packageImportDialog.Connect(FileDialog.SignalName.FileSelected, Callable.From<string>(OnPackageImportSelected));
        AddChild(_packageImportDialog);
        AddBackButton();
    }

    private VBoxContainer BuildProjectPage()
    {
        var page = new VBoxContainer { Name = "ProjectPage", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill, Visible = true };
        var split = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        split.AddThemeConstantOverride("separation", 18);
        page.AddChild(split);

        var left = new VBoxContainer { CustomMinimumSize = new Vector2(360f, 0f), SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        left.AddThemeConstantOverride("separation", 8);
        left.AddChild(MakeLabel("Projects"));
        left.AddChild(MakeActionRow(("New", CreateNewProject), ("Duplicate", DuplicateCurrentProject), ("Delete", DeleteCurrentProject), ("Export+Install", ExportCurrentProject)));
        left.AddChild(MakeScrollList("ProjectList", out _projectList));
        left.AddChild(MakeLabel("Current Project"));
        _projectDetails = MakeDetails("Select or create a project to begin editing overrides.");
        left.AddChild(_projectDetails);
        split.AddChild(left);

        var right = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        right.AddThemeConstantOverride("separation", 8);
        right.AddChild(MakeLabel("ModelDb Browser"));
        right.AddChild(MakeCategoryRow(out _categoryRow));
        _browserGrid = new GridContainer { Columns = 2, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        var browserScroll = new ScrollContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        browserScroll.AddChild(_browserGrid);
        right.AddChild(browserScroll);
        right.AddChild(MakeLabel("Override Editor"));
        _browserDetails = MakeDetails("Select a runtime object to inspect and edit its override snapshot.");
        right.AddChild(_browserDetails);
        right.AddChild(MakeActionRow(("Capture Runtime", CaptureSelectedRuntimeMetadata), ("Save Override", SaveCurrentOverride), ("Remove Override", RemoveCurrentOverride)));
        _graphToggle = new CheckButton { Text = "Use Graph Behavior", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        right.AddChild(_graphToggle);
        right.AddChild(MakeLabel("Graph Id"));
        _graphIdEdit = new LineEdit { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        right.AddChild(_graphIdEdit);
        right.AddChild(MakeLabel("Metadata JSON"));
        _metadataEdit = new TextEdit { CustomMinimumSize = new Vector2(0f, 210f), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        right.AddChild(_metadataEdit);
        right.AddChild(MakeLabel("Notes"));
        _notesEdit = new TextEdit { CustomMinimumSize = new Vector2(0f, 120f), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.Fill };
        right.AddChild(_notesEdit);
        split.AddChild(right);
        return page;
    }

    private VBoxContainer BuildPackagePage()
    {
        var page = new VBoxContainer { Name = "PackagePage", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill, Visible = false };
        page.AddThemeConstantOverride("separation", 8);
        page.AddChild(MakeActionRow(("Import", ShowPackageImportDialog), ("Refresh", RefreshPackages), ("Enable/Disable", ToggleSelectedPackage), ("Move Up", () => MoveSelectedPackage(-1)), ("Move Down", () => MoveSelectedPackage(1))));
        page.AddChild(MakeLabel("Installed Packages"));
        page.AddChild(MakeScrollList("PackageList", out _packageList));
        page.AddChild(MakeLabel("Package Details"));
        _packageDetails = MakeDetails("Import a .sts2pack and manage enable state or load order here.");
        page.AddChild(_packageDetails);
        return page;
    }

    private Button MakeButton(string text, Action onPressed, bool toggle = false)
    {
        var b = new Button { Text = text, ToggleMode = toggle, FocusMode = Control.FocusModeEnum.All, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0f, 34f) };
        b.Connect(Button.SignalName.Pressed, Callable.From(onPressed));
        return b;
    }

    private Label MakeLabel(string text) => new() { Text = text, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
    private RichTextLabel MakeDetails(string text) => new() { BbcodeEnabled = false, ScrollActive = true, FitContent = false, AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0f, 130f), Text = text };

    private HBoxContainer MakeActionRow(params (string Text, Action Callback)[] actions)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 6);
        foreach (var action in actions) row.AddChild(MakeButton(action.Text, action.Callback));
        return row;
    }

    private ScrollContainer MakeScrollList(string name, out VBoxContainer list)
    {
        var scroll = new ScrollContainer { Name = $"{name}Scroll", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        list = new VBoxContainer { Name = name, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(list);
        return scroll;
    }

    private ScrollContainer MakeCategoryRow(out HBoxContainer row)
    {
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0f, 70f), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(row);
        foreach (var kind in _browserKinds)
        {
            var captured = kind;
            row.AddChild(MakeButton(kind.ToString(), () => SelectBrowserKind(captured), true));
        }
        return scroll;
    }

    private void AddBackButton()
    {
        var scene = ResourceLoader.Load<PackedScene>(BackButtonScenePath);
        if (scene == null) { Log.Warn("Mod Studio back button scene could not be loaded."); return; }
        var backButton = scene.Instantiate<NBackButton>(PackedScene.GenEditState.Disabled);
        backButton.Name = "BackButton";
        AddChild(backButton);
    }

    private void RefreshData()
    {
        var projectId = _currentProject?.Manifest.ProjectId;
        var packageKey = _selectedPackage?.PackageKey;
        _projects = SafeGetProjects();
        ModStudioBootstrap.RuntimeRegistry.Refresh();
        _packages = SafeGetPackages();
        if (!string.IsNullOrWhiteSpace(projectId) && ModStudioBootstrap.ProjectStore.TryLoad(projectId, out var loaded) && loaded != null) _currentProject = loaded;
        else if (_currentProject == null && _projects.Count > 0) ModStudioBootstrap.ProjectStore.TryLoad(_projects[0].ProjectId, out _currentProject);
        else if (_projects.Count == 0) _currentProject = null;
        _selectedPackage = !string.IsNullOrWhiteSpace(packageKey) ? _packages.FirstOrDefault(p => p.PackageKey == packageKey) : _packages.FirstOrDefault();
        PopulateProjectList();
        PopulatePackageList();
        SelectBrowserKind(_selectedKind, true);
        UpdateProjectDetails();
        UpdatePackageDetails();
        SyncOverrideEditor();
        UpdateStateText();
    }

    private IReadOnlyList<EditorProjectManifest> SafeGetProjects()
    {
        try { return ModStudioBootstrap.ProjectStore.EnumerateProjectManifests(); }
        catch (Exception ex) { Log.Warn($"Failed to load Mod Studio projects: {ex.Message}"); SetAction($"Project load failed: {ex.Message}"); return Array.Empty<EditorProjectManifest>(); }
    }

    private IReadOnlyList<PackageSessionState> SafeGetPackages()
    {
        try { return ModStudioBootstrap.RuntimeRegistry.SessionStates.OrderBy(x => x.LoadOrder).ThenBy(x => x.PackageKey, StringComparer.Ordinal).ToList(); }
        catch (Exception ex) { Log.Warn($"Failed to load Mod Studio package sessions: {ex.Message}"); SetAction($"Package load failed: {ex.Message}"); return Array.Empty<PackageSessionState>(); }
    }

    private void PopulateProjectList()
    {
        ClearChildren(_projectList);
        if (_projectList == null) return;
        if (_projects.Count == 0) { _projectList.AddChild(MakeListEntry("No projects found", "Create a project to start building a package.")); return; }
        foreach (var project in _projects)
        {
            var title = _currentProject?.Manifest.ProjectId == project.ProjectId ? $"[Active] {project.Name}" : project.Name;
            var button = MakeListEntry(title, $"{project.Author} | {project.TargetGameVersion}");
            button.Connect(Button.SignalName.Pressed, Callable.From(() => OpenProject(project.ProjectId)));
            _projectList.AddChild(button);
        }
    }

    private void PopulatePackageList()
    {
        ClearChildren(_packageList);
        if (_packageList == null) return;
        if (_packages.Count == 0) { _packageList.AddChild(MakeListEntry("No packages installed", "Import or export a .sts2pack to populate package mode.")); return; }
        foreach (var package in _packages)
        {
            var button = MakeListEntry(package.DisplayName, $"#{package.LoadOrder} | {(package.Enabled ? "Enabled" : "Disabled")} | {(package.SessionEnabled ? "Session On" : "Session Off")}");
            button.Connect(Button.SignalName.Pressed, Callable.From(() => { _selectedPackage = package; UpdatePackageDetails(); UpdateStateText(); }));
            _packageList.AddChild(button);
        }
    }

    private Button MakeListEntry(string title, string subtitle) => new() { Text = $"{title}\n{subtitle}", CustomMinimumSize = new Vector2(0f, 56f), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };

    private void SelectBrowserKind(ModStudioEntityKind kind, bool preserveSelectedItem = true)
    {
        _selectedKind = kind;
        var items = ModStudioBootstrap.ModelMetadataService.GetItems(kind);
        if (!preserveSelectedItem || _selectedBrowserItem?.Kind != kind) _selectedBrowserItem = null;
        foreach (var button in _categoryRow?.GetChildren().OfType<Button>() ?? Enumerable.Empty<Button>()) button.ButtonPressed = button.Text == kind.ToString();
        ClearChildren(_browserGrid);
        if (_browserGrid == null) return;
        if (items.Count == 0)
        {
            _browserGrid.AddChild(MakeListEntry("No entries", $"No {kind} entries were found."));
            if (_browserDetails != null) _browserDetails.Text = $"No {kind} entries were found.";
            SyncOverrideEditor();
            UpdateStateText();
            return;
        }

        _selectedBrowserItem = _selectedBrowserItem == null ? items[0] : items.FirstOrDefault(x => x.EntityId == _selectedBrowserItem.EntityId) ?? items[0];
        foreach (var item in items)
        {
            var captured = item;
            var button = MakeListEntry(item.Title, item.Summary);
            button.CustomMinimumSize = new Vector2(0f, 88f);
            button.Connect(Button.SignalName.Pressed, Callable.From(() => { _selectedBrowserItem = captured; if (_browserDetails != null) _browserDetails.Text = captured.DetailText; SyncOverrideEditor(); UpdateStateText(); }));
            _browserGrid.AddChild(button);
        }

        if (_browserDetails != null) _browserDetails.Text = _selectedBrowserItem.DetailText;
        SyncOverrideEditor();
        UpdateStateText();
    }

    private void SetMode(bool isProjectMode)
    {
        if (_projectPage == null || _packagePage == null) return;
        _projectPage.Visible = isProjectMode;
        _packagePage.Visible = !isProjectMode;
        if (_projectTabButton != null) _projectTabButton.ButtonPressed = isProjectMode;
        if (_packageTabButton != null) _packageTabButton.ButtonPressed = !isProjectMode;
        _defaultFocus = isProjectMode ? _projectTabButton : _packageTabButton;
        UpdateStateText();
    }

    private void OpenProject(string projectId)
    {
        if (!ModStudioBootstrap.ProjectStore.TryLoad(projectId, out var project) || project == null) { SetAction($"Project '{projectId}' could not be opened."); return; }
        _currentProject = project;
        PopulateProjectList();
        UpdateProjectDetails();
        SyncOverrideEditor();
        SetAction($"Opened project '{project.Manifest.Name}'.");
    }

    private void CreateNewProject()
    {
        try
        {
            _currentProject = ModStudioBootstrap.ProjectStore.CreateProject($"New Project {DateTimeOffset.Now:yyyy-MM-dd HH-mm-ss}");
            _projects = SafeGetProjects();
            PopulateProjectList();
            UpdateProjectDetails();
            SyncOverrideEditor();
            SetAction($"Created project '{_currentProject.Manifest.Name}'.");
        }
        catch (Exception ex) { Log.Warn($"Failed to create project: {ex.Message}"); SetAction($"Create project failed: {ex.Message}"); }
    }

    private void DuplicateCurrentProject()
    {
        if (_currentProject == null) { SetAction("Select a project before duplicating it."); return; }
        try
        {
            _currentProject = ModStudioBootstrap.ProjectStore.DuplicateProject(_currentProject.Manifest.ProjectId);
            _projects = SafeGetProjects();
            PopulateProjectList();
            UpdateProjectDetails();
            SetAction($"Duplicated project as '{_currentProject.Manifest.Name}'.");
        }
        catch (Exception ex) { Log.Warn($"Failed to duplicate project: {ex.Message}"); SetAction($"Duplicate failed: {ex.Message}"); }
    }

    private void DeleteCurrentProject()
    {
        if (_currentProject == null) { SetAction("Select a project before deleting it."); return; }
        var deletedName = _currentProject.Manifest.Name;
        if (!ModStudioBootstrap.ProjectStore.DeleteProject(_currentProject.Manifest.ProjectId)) { SetAction($"Project '{deletedName}' could not be deleted."); return; }
        _currentProject = null;
        _projects = SafeGetProjects();
        if (_projects.Count > 0) ModStudioBootstrap.ProjectStore.TryLoad(_projects[0].ProjectId, out _currentProject);
        PopulateProjectList();
        UpdateProjectDetails();
        SyncOverrideEditor();
        SetAction($"Deleted project '{deletedName}'.");
    }

    private void ExportCurrentProject()
    {
        if (_currentProject == null) { SetAction("Select a project before exporting it."); return; }
        try
        {
            var path = ModStudioBootstrap.PackageStore.ExportProject(_currentProject, new PackageExportOptions
            {
                DisplayName = _currentProject.Manifest.Name,
                Author = _currentProject.Manifest.Author,
                Description = _currentProject.Manifest.Description,
                EditorVersion = _currentProject.Manifest.EditorVersion,
                TargetGameVersion = _currentProject.Manifest.TargetGameVersion
            });
            var install = ModStudioBootstrap.RuntimeRegistry.ImportPackage(path);
            _packages = SafeGetPackages();
            _selectedPackage = _packages.FirstOrDefault(x => x.PackageKey == install.PackageState.PackageKey);
            PopulatePackageList();
            UpdatePackageDetails();
            SetAction($"Exported and installed '{_currentProject.Manifest.Name}' to {path}.");
        }
        catch (Exception ex) { Log.Warn($"Failed to export project: {ex.Message}"); SetAction($"Export failed: {ex.Message}"); }
    }

    private void CaptureSelectedRuntimeMetadata()
    {
        if (_selectedBrowserItem == null || _metadataEdit == null) { SetAction("Select a runtime object before capturing metadata."); return; }
        _metadataEdit.Text = JsonSerializer.Serialize(ModStudioBootstrap.ModelMetadataService.GetEditableMetadata(_selectedBrowserItem.Kind, _selectedBrowserItem.EntityId), ModStudioJson.Options);
        SetAction($"Captured runtime fields for {_selectedBrowserItem.Kind}:{_selectedBrowserItem.EntityId}.");
    }

    private void SaveCurrentOverride()
    {
        if (_currentProject == null) { SetAction("Select or create a project before saving overrides."); return; }
        if (_selectedBrowserItem == null) { SetAction("Select a runtime object before saving an override."); return; }
        try
        {
            var metadata = ParseMetadata(_metadataEdit?.Text ?? string.Empty);
            var graphId = (_graphIdEdit?.Text ?? string.Empty).Trim();
            var useGraph = _graphToggle?.ButtonPressed == true;
            if (useGraph && string.IsNullOrWhiteSpace(graphId))
            {
                graphId = BuildDefaultGraphId(_selectedBrowserItem.Kind, _selectedBrowserItem.EntityId);
                if (_graphIdEdit != null) _graphIdEdit.Text = graphId;
            }

            var envelope = _currentProject.Overrides.FirstOrDefault(x => x.EntityKind == _selectedBrowserItem.Kind && x.EntityId == _selectedBrowserItem.EntityId);
            if (envelope == null)
            {
                envelope = new EntityOverrideEnvelope { EntityKind = _selectedBrowserItem.Kind, EntityId = _selectedBrowserItem.EntityId };
                _currentProject.Overrides.Add(envelope);
            }

            envelope.BehaviorSource = useGraph ? BehaviorSource.Graph : BehaviorSource.Native;
            envelope.GraphId = string.IsNullOrWhiteSpace(graphId) ? null : graphId;
            envelope.Metadata = metadata;
            envelope.Notes = string.IsNullOrWhiteSpace(_notesEdit?.Text) ? null : _notesEdit?.Text.Trim();
            if (useGraph && envelope.GraphId != null) EnsureGraphExists(_currentProject, envelope.GraphId, _selectedBrowserItem.Kind);
            ModStudioBootstrap.ProjectStore.Save(_currentProject);
            _projects = SafeGetProjects();
            PopulateProjectList();
            UpdateProjectDetails();
            SetAction($"Saved override for {_selectedBrowserItem.Kind}:{_selectedBrowserItem.EntityId}.");
        }
        catch (Exception ex) { Log.Warn($"Failed to save override: {ex.Message}"); SetAction($"Save override failed: {ex.Message}"); }
    }

    private void RemoveCurrentOverride()
    {
        if (_currentProject == null) { SetAction("Select a project before removing overrides."); return; }
        if (_selectedBrowserItem == null) { SetAction("Select a runtime object before removing its override."); return; }
        var removed = _currentProject.Overrides.RemoveAll(x => x.EntityKind == _selectedBrowserItem.Kind && x.EntityId == _selectedBrowserItem.EntityId);
        if (removed == 0) { SetAction($"No override existed for {_selectedBrowserItem.Kind}:{_selectedBrowserItem.EntityId}."); return; }
        ModStudioBootstrap.ProjectStore.Save(_currentProject);
        UpdateProjectDetails();
        SyncOverrideEditor();
        SetAction($"Removed override for {_selectedBrowserItem.Kind}:{_selectedBrowserItem.EntityId}.");
    }

    private void ShowPackageImportDialog()
    {
        if (_packageImportDialog == null) { SetAction("Package import dialog is not available."); return; }
        _packageImportDialog.PopupCenteredRatio(0.75f);
    }

    private void OnPackageImportSelected(string packageFilePath)
    {
        try
        {
            var install = ModStudioBootstrap.RuntimeRegistry.ImportPackage(packageFilePath);
            _packages = SafeGetPackages();
            _selectedPackage = _packages.FirstOrDefault(x => x.PackageKey == install.PackageState.PackageKey);
            PopulatePackageList();
            UpdatePackageDetails();
            SetAction($"Imported package '{install.PackageState.DisplayName}'.");
        }
        catch (Exception ex) { Log.Warn($"Failed to import package: {ex.Message}"); SetAction($"Import failed: {ex.Message}"); }
    }

    private void RefreshPackages()
    {
        ModStudioBootstrap.RuntimeRegistry.Refresh();
        _packages = SafeGetPackages();
        _selectedPackage = _packages.FirstOrDefault(x => x.PackageKey == _selectedPackage?.PackageKey) ?? _packages.FirstOrDefault();
        PopulatePackageList();
        UpdatePackageDetails();
        SetAction("Package catalog refreshed.");
    }

    private void ToggleSelectedPackage()
    {
        if (_selectedPackage == null) { SetAction("Select a package before changing its state."); return; }
        ModStudioBootstrap.RuntimeRegistry.EnablePackage(_selectedPackage.PackageKey, !_selectedPackage.Enabled);
        _packages = SafeGetPackages();
        _selectedPackage = _packages.FirstOrDefault(x => x.PackageKey == _selectedPackage.PackageKey);
        PopulatePackageList();
        UpdatePackageDetails();
        SetAction($"Toggled package '{_selectedPackage?.DisplayName ?? "unknown"}'.");
    }

    private void MoveSelectedPackage(int direction)
    {
        if (_selectedPackage == null) { SetAction("Select a package before reordering it."); return; }
        if (!ModStudioBootstrap.RuntimeRegistry.MovePackage(_selectedPackage.PackageKey, direction)) { SetAction("Package load order did not change."); return; }
        _packages = SafeGetPackages();
        _selectedPackage = _packages.FirstOrDefault(x => x.PackageKey == _selectedPackage.PackageKey);
        PopulatePackageList();
        UpdatePackageDetails();
        SetAction($"Moved package '{_selectedPackage?.DisplayName ?? "unknown"}'.");
    }

    private void UpdateProjectDetails()
    {
        if (_projectDetails == null) return;
        if (_currentProject == null) { _projectDetails.Text = "No project selected."; return; }
        _projectDetails.Text = string.Join(System.Environment.NewLine,
            $"Project Id: {_currentProject.Manifest.ProjectId}",
            $"Name: {_currentProject.Manifest.Name}",
            $"Author: {_currentProject.Manifest.Author}",
            $"Description: {_currentProject.Manifest.Description}",
            $"Target Game: {_currentProject.Manifest.TargetGameVersion}",
            $"Editor Version: {_currentProject.Manifest.EditorVersion}",
            $"Overrides: {_currentProject.Overrides.Count}",
            $"Graphs: {_currentProject.Graphs.Count}",
            $"Assets: {_currentProject.ProjectAssets.Count}",
            $"Updated: {_currentProject.Manifest.UpdatedAtUtc:u}");
    }

    private void UpdatePackageDetails()
    {
        if (_packageDetails == null) return;
        if (_selectedPackage == null) { _packageDetails.Text = "No package selected."; return; }
        var runtimePackage = ModStudioBootstrap.RuntimeRegistry.InstalledPackages.FirstOrDefault(x => x.PackageKey == _selectedPackage.PackageKey);
        _packageDetails.Text = string.Join(System.Environment.NewLine,
            $"Package Key: {_selectedPackage.PackageKey}",
            $"Package Id: {_selectedPackage.PackageId}",
            $"Display Name: {_selectedPackage.DisplayName}",
            $"Version: {_selectedPackage.Version}",
            $"Checksum: {_selectedPackage.Checksum}",
            $"Load Order: {_selectedPackage.LoadOrder}",
            $"Enabled: {_selectedPackage.Enabled}",
            $"Session Enabled: {_selectedPackage.SessionEnabled}",
            $"Disabled Reason: {_selectedPackage.DisabledReason}",
            $"Package File: {_selectedPackage.PackageFilePath}",
            $"Override Count: {runtimePackage?.Project.Overrides.Count ?? 0}",
            $"Graph Count: {runtimePackage?.Project.Graphs.Count ?? 0}",
            $"Asset Count: {runtimePackage?.Project.ProjectAssets.Count ?? 0}");
    }

    private void SyncOverrideEditor()
    {
        if (_browserDetails == null || _metadataEdit == null || _notesEdit == null || _graphToggle == null || _graphIdEdit == null) return;
        if (_selectedBrowserItem == null)
        {
            _browserDetails.Text = "Select a runtime object to inspect it.";
            _graphToggle.ButtonPressed = false;
            _graphIdEdit.Text = string.Empty;
            _metadataEdit.Text = string.Empty;
            _notesEdit.Text = string.Empty;
            return;
        }

        _browserDetails.Text = _selectedBrowserItem.DetailText;
        var envelope = _currentProject?.Overrides.FirstOrDefault(x => x.EntityKind == _selectedBrowserItem.Kind && x.EntityId == _selectedBrowserItem.EntityId);
        if (envelope == null)
        {
            _graphToggle.ButtonPressed = false;
            _graphIdEdit.Text = string.Empty;
            _metadataEdit.Text = JsonSerializer.Serialize(ModStudioBootstrap.ModelMetadataService.GetEditableMetadata(_selectedBrowserItem.Kind, _selectedBrowserItem.EntityId), ModStudioJson.Options);
            _notesEdit.Text = string.Empty;
            return;
        }

        _graphToggle.ButtonPressed = envelope.BehaviorSource == BehaviorSource.Graph;
        _graphIdEdit.Text = envelope.GraphId ?? string.Empty;
        _metadataEdit.Text = JsonSerializer.Serialize(envelope.Metadata, ModStudioJson.Options);
        _notesEdit.Text = envelope.Notes ?? string.Empty;
    }

    private void UpdateStateText()
    {
        if (_stateLabel == null) return;
        var mode = _projectPage?.Visible == true ? "Project Mode" : "Package Mode";
        var selected = _selectedBrowserItem != null ? $"{_selectedBrowserItem.Kind}:{_selectedBrowserItem.EntityId}" : _selectedPackage?.PackageKey ?? "none";
        var project = _currentProject?.Manifest.Name ?? "none";
        _stateLabel.Text = $"{mode} | Project {project} | Packages {_packages.Count} | Selected {selected} | {_lastAction}";
    }

    private void SetAction(string message)
    {
        _lastAction = message;
        UpdateStateText();
    }

    private static Dictionary<string, string> ParseMetadata(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, string>(StringComparer.Ordinal);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json, ModStudioJson.Options) ?? new Dictionary<string, string>(StringComparer.Ordinal);
    }

    private static string BuildDefaultGraphId(ModStudioEntityKind kind, string entityId)
    {
        var normalizedId = entityId.Replace(':', '_').Replace('/', '_').Replace('\\', '_');
        return $"graph.{kind.ToString().ToLowerInvariant()}.{normalizedId}";
    }

    private static void EnsureGraphExists(EditorProject project, string graphId, ModStudioEntityKind kind)
    {
        if (project.Graphs.ContainsKey(graphId)) return;
        var entryNodeId = $"entry_{kind.ToString().ToLowerInvariant()}";
        var exitNodeId = $"exit_{kind.ToString().ToLowerInvariant()}";
        project.Graphs[graphId] = new BehaviorGraphDefinition
        {
            GraphId = graphId,
            Name = $"{kind} Override Graph",
            Description = "Phase 1 default graph scaffold generated by Mod Studio.",
            EntityKind = kind,
            EntryNodeId = entryNodeId,
            Nodes =
            [
                new BehaviorGraphNodeDefinition { NodeId = entryNodeId, NodeType = "flow.entry", DisplayName = "Entry" },
                new BehaviorGraphNodeDefinition { NodeId = exitNodeId, NodeType = "flow.exit", DisplayName = "Exit" }
            ],
            Connections =
            [
                new BehaviorGraphConnectionDefinition { FromNodeId = entryNodeId, FromPortId = "next", ToNodeId = exitNodeId, ToPortId = "in" }
            ]
        };
    }

    private static void ClearChildren(Node? node)
    {
        if (node == null) return;
        foreach (var child in node.GetChildren()) child.QueueFree();
    }
}
