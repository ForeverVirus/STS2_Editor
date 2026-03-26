using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using STS2_Editor.Scripts.Editor.Core.Models;
using STS2_Editor.Scripts.Editor.Core.Services;
using STS2_Editor.Scripts.Editor.Core.Utilities;
using STS2_Editor.Scripts.Editor.Graph;
using STS2_Editor.Scripts.Editor.Packaging;
using STS2_Editor.Scripts.Editor.Runtime;
using static STS2_Editor.Scripts.Editor.UI.ModStudioUiFactory;

namespace STS2_Editor.Scripts.Editor.UI;

public sealed partial class NModStudioProjectWindow : NSubmenu
{
    private const string BackButtonScenePath = "res://scenes/ui/back_button.tscn";
    private const string AutoDescriptionCacheKeyPrefix = "__modstudio_auto_description__";
    private const string GraphAutoDescriptionCacheKey = "__modstudio_graph_auto_description__";
    private const string NodeAutoDescriptionCacheKeyPrefix = "__modstudio_node_auto_description__:";

    private readonly EditorProjectStore _projectStore = ModStudioBootstrap.ProjectStore;
    private readonly ModelMetadataService _metadataService = ModStudioBootstrap.ModelMetadataService;
    private readonly ProjectAssetBindingService _assetBindingService = ModStudioBootstrap.ProjectAssetBindingService;
    private readonly BehaviorGraphRegistry _graphRegistry = ModStudioBootstrap.GraphRegistry;
    private readonly PackageArchiveService _packageArchiveService = ModStudioBootstrap.PackageArchiveService;
    private readonly GraphDescriptionGenerator _graphDescriptionGenerator = new();
    private readonly NativeBehaviorAutoGraphService _nativeAutoGraphService = new();
    private DynamicPreviewContext _graphPreviewContext = new();

    private readonly Dictionary<ModStudioEntityKind, List<EntityBrowserItem>> _browserItemsCache = new();
    private readonly Dictionary<string, EntityEditorViewCache> _entityViewCache = new(StringComparer.Ordinal);

    private bool _uiBuilt;
    private bool _dirty;
    private bool _suppressDirty;
    private bool _basicDraftDirty;
    private bool _graphDraftDirty;
    private bool _assetTabLoadedForCurrentItem;
    private bool _graphTabLoadedForCurrentItem;
    private bool _graphCanvasSignalsWired;
    private BehaviorGraphDefinition? _cachedCompiledEventGraph;
    private EventGraphValidationResult? _cachedCompiledEventResult;
    private string _currentProjectPath = string.Empty;
    private EditorProject? _project;
    private ModStudioEntityKind _currentKind = ModStudioEntityKind.Card;
    private string? _currentEntityId;
    private EntityBrowserItem? _currentItem;
    private string _currentResolvedAssetPath = string.Empty;
    private string _selectedRuntimeAssetPath = string.Empty;
    private AssetRef? _selectedImportedAsset;
    private EntityEditorViewCache? _currentViewCache;
    private Action? _pendingAfterSave;
    private Action? _pendingAfterDiscard;
    private IDisposable? _authoringIsolationLease;

    private ModStudioProjectMenuBar? _menuBar;
    private ModStudioEntityBrowserPanel? _browserPanel;
    private ModStudioCenterEditor? _centerEditor;
    private ModStudioProjectDetailPanel? _detailPanel;
    private ModStudioUnsavedDialog? _unsavedDialog;
    private ColorRect? _background;
    private VBoxContainer? _rootContainer;
    private Control? _projectGateOverlay;
    private Label? _projectGateTitleLabel;
    private RichTextLabel? _projectGateBodyLabel;
    private Button? _projectGateNewButton;
    private Button? _projectGateOpenButton;
    private Button? _projectGateCloseButton;
    private FileDialog? _newProjectDialog;
    private FileDialog? _openProjectDialog;
    private FileDialog? _exportDialog;
    private FileDialog? _assetImportDialog;
    private ModStudioGraphImportPickerDialog? _graphImportDialog;

    protected override Control? InitialFocusedControl => null;

    public static IEnumerable<string> AssetPaths => new[] { BackButtonScenePath };

    public static NModStudioProjectWindow Create() => new();

    public override void _Ready()
    {
        ModStudioBootstrap.Initialize();
        _authoringIsolationLease ??= ModStudioAuthoringIsolation.EnterProjectMode();
        ModStudioBootstrap.EnsureRuntimeInitialized();

        BuildUi();
        base.ConnectSignals();
        base.HideBackButtonImmediately();
        GetViewport().Connect(Viewport.SignalName.SizeChanged, Callable.From(RefreshFullscreenLayout));
        Callable.From(RefreshFullscreenLayout).CallDeferred();

        ModStudioLocalization.LanguageChanged += RefreshLocalizedText;
        RefreshLocalizedText();
        RefreshProjectState();
        RefreshProjectGate();
    }

    private void AddBackButton()
    {
        var scene = ResourceLoader.Load<PackedScene>(BackButtonScenePath);
        if (scene == null)
        {
            return;
        }

        var backButton = scene.Instantiate<NBackButton>(PackedScene.GenEditState.Disabled);
        backButton.Name = "BackButton";
        AddChild(backButton);
    }

    public override void OnSubmenuOpened()
    {
        _authoringIsolationLease ??= ModStudioAuthoringIsolation.EnterProjectMode();
        base.HideBackButtonImmediately();
        RefreshFullscreenLayout();
        RefreshProjectState();
        RefreshProjectGate();
        if (_project != null)
        {
            RefreshBrowserItems(selectFirstIfPossible: _currentItem == null);
        }
    }

    protected override void OnSubmenuShown()
    {
        base.OnSubmenuShown();
        base.HideBackButtonImmediately();
        Callable.From(RefreshFullscreenLayout).CallDeferred();
    }

    public override void OnSubmenuClosed()
    {
        _authoringIsolationLease?.Dispose();
        _authoringIsolationLease = null;
        base.OnSubmenuClosed();
    }

    private void BuildUi()
    {
        if (_uiBuilt)
        {
            return;
        }

        _uiBuilt = true;
        SetAnchorsPreset(LayoutPreset.FullRect);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        GrowHorizontal = GrowDirection.Both;
        GrowVertical = GrowDirection.Both;
        MouseFilter = MouseFilterEnum.Stop;

        AddBackButton();

        _background = new ColorRect
        {
            AnchorRight = 1f,
            AnchorBottom = 1f,
            Color = new Color(0.03f, 0.04f, 0.06f, 1f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(_background);

        _rootContainer = new VBoxContainer
        {
            AnchorRight = 1f,
            AnchorBottom = 1f,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        _rootContainer.AddThemeConstantOverride("separation", 0);
        AddChild(_rootContainer);

        _menuBar = new ModStudioProjectMenuBar();
        _menuBar.NewProjectRequested += ShowNewProjectDialog;
        _menuBar.OpenProjectRequested += ShowOpenProjectDialog;
        _menuBar.SaveProjectRequested += SaveProject;
        _menuBar.ExportPackageRequested += ExportProject;
        _menuBar.RevertRequested += RevertBasic;
        _menuBar.SwitchModeRequested += RequestSwitchMode;
        _menuBar.ExitRequested += RequestExit;
        _menuBar.LanguageChanged += code => ModStudioLocalization.SetLanguage(code);
        _rootContainer.AddChild(_menuBar);

        var body = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        body.AddThemeConstantOverride("separation", 10);
        _rootContainer.AddChild(body);

        _browserPanel = new ModStudioEntityBrowserPanel
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsStretchRatio = 2f
        };
        _browserPanel.KindChanged += OnKindChanged;
        _browserPanel.ItemSelected += OnItemSelected;
        _browserPanel.CreateEntryRequested += CreateEntryRequested;
        body.AddChild(_browserPanel);

        _centerEditor = new ModStudioCenterEditor
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsStretchRatio = 6f
        };
        _centerEditor.EnsureBuilt();
        _centerEditor.BasicEditor.SaveRequested += SaveBasic;
        _centerEditor.BasicEditor.RevertRequested += RevertBasic;
        _centerEditor.BasicEditor.FieldChanged += MarkBasicDirty;
        _centerEditor.AssetEditor.SaveRequested += SaveAssetBinding;
        _centerEditor.AssetEditor.RevertRequested += RevertAssetBinding;
        _centerEditor.GraphEditor.ImportRequested += ImportGraphTemplate;
        _centerEditor.GraphEditor.SaveRequested += SaveGraph;
        _centerEditor.GraphEditor.ValidateRequested += ValidateGraph;
        _centerEditor.Tabs.TabChanged += OnCenterTabChanged;
        body.AddChild(_centerEditor);

        _detailPanel = new ModStudioProjectDetailPanel
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsStretchRatio = 2f
        };
        _detailPanel.EnsureBuilt();
        _detailPanel.RuntimeAssetList.Connect("item_selected", Callable.From<long>(OnRuntimeAssetSelected));
        _detailPanel.ImportedAssetList.Connect("item_selected", Callable.From<long>(OnImportedAssetSelected));
        _detailPanel.GraphEnabledCheck.Toggled += OnGraphEnabledToggled;
        _detailPanel.GraphIdEdit.TextChanged += _ => MarkGraphDirty();
        _detailPanel.GraphNameEdit.TextChanged += _ => MarkGraphDirty();
        _detailPanel.GraphDescriptionEdit.TextChanged += () => MarkGraphDirty();
        _detailPanel.PreviewContextChanged += OnPreviewContextChanged;
        _detailPanel.NodePropertyChanged += OnSelectedNodePropertyChanged;
        _detailPanel.SelectedNodeDisplayNameChanged += OnSelectedNodeDisplayNameChanged;
        _detailPanel.SelectedNodeDescriptionChanged += OnSelectedNodeDescriptionChanged;
        _detailPanel.EventOptionAddRequested += OnEventOptionAddRequested;
        body.AddChild(_detailPanel);
        _detailPanel.SetTab(_centerEditor.Tabs.CurrentTab);

        _unsavedDialog = new ModStudioUnsavedDialog();
        _unsavedDialog.SaveAndExit += HandleUnsavedSave;
        _unsavedDialog.DiscardAndExit += HandleUnsavedDiscard;
        _unsavedDialog.Cancel += HideUnsavedDialog;
        AddChild(_unsavedDialog);

        _newProjectDialog = BuildNewProjectDialog();
        _openProjectDialog = BuildOpenProjectDialog();
        _exportDialog = BuildPackageDialog();
        _assetImportDialog = BuildAssetDialog();
        _graphImportDialog = new ModStudioGraphImportPickerDialog();
        _graphImportDialog.CandidateConfirmed += ImportGraphFromCandidate;
        AddChild(_newProjectDialog);
        AddChild(_openProjectDialog);
        AddChild(_exportDialog);
        AddChild(_assetImportDialog);
        AddChild(_graphImportDialog);

        _projectGateOverlay = BuildProjectGateOverlay();
        AddChild(_projectGateOverlay);

        RefreshFullscreenLayout();
    }

    private void RefreshFullscreenLayout()
    {
        if (!IsInsideTree())
        {
            return;
        }

        var parentSize = GetParent() is Control parentControl ? parentControl.Size : Vector2.Zero;
        var viewportSize = GetViewportRect().Size;
        var targetSize = parentSize.X > 0f && parentSize.Y > 0f ? parentSize : viewportSize;
        if (targetSize.X <= 0f || targetSize.Y <= 0f)
        {
            return;
        }

        AnchorLeft = 0f;
        AnchorTop = 0f;
        AnchorRight = 1f;
        AnchorBottom = 1f;
        OffsetLeft = 0f;
        OffsetTop = 0f;
        OffsetRight = 0f;
        OffsetBottom = 0f;
        Position = Vector2.Zero;
        Size = targetSize;
        CustomMinimumSize = targetSize;
        GrowHorizontal = GrowDirection.Both;
        GrowVertical = GrowDirection.Both;

        ApplyFullRectToChild(_background, targetSize);
        ApplyFullRectToChild(_rootContainer, targetSize);
        ApplyFullRectToChild(_projectGateOverlay, targetSize);
    }

    private static void ApplyFullRectToChild(Control? control, Vector2 targetSize)
    {
        if (control == null)
        {
            return;
        }

        control.AnchorLeft = 0f;
        control.AnchorTop = 0f;
        control.AnchorRight = 1f;
        control.AnchorBottom = 1f;
        control.OffsetLeft = 0f;
        control.OffsetTop = 0f;
        control.OffsetRight = 0f;
        control.OffsetBottom = 0f;
        control.Position = Vector2.Zero;
        control.Size = targetSize;
        control.GrowHorizontal = GrowDirection.Both;
        control.GrowVertical = GrowDirection.Both;
    }

    private FileDialog BuildProjectDialog(FileDialog.FileModeEnum mode, Action<string> onSelected)
    {
        var dialog = new FileDialog
        {
            Access = FileDialog.AccessEnum.Filesystem,
            FileMode = mode,
            UseNativeDialog = false,
            Title = mode == FileDialog.FileModeEnum.SaveFile
                ? Dual("新建项目", "New Project")
                : Dual("打开项目", "Open Project")
        };
        dialog.AddFilter("project.json ; project.json");
        dialog.FileSelected += path => onSelected(path);
        return dialog;
    }

    private FileDialog BuildNewProjectDialog()
    {
        var dialog = new FileDialog
        {
            Access = FileDialog.AccessEnum.Filesystem,
            FileMode = FileDialog.FileModeEnum.OpenDir,
            UseNativeDialog = false,
            Title = Dual("选择项目目录", "Choose Project Directory")
        };
        dialog.DirSelected += OnNewProjectChosen;
        return dialog;
    }

    private FileDialog BuildOpenProjectDialog()
    {
        var dialog = new FileDialog
        {
            Access = FileDialog.AccessEnum.Filesystem,
            FileMode = FileDialog.FileModeEnum.OpenAny,
            UseNativeDialog = false,
            Title = Dual("打开项目", "Open Project")
        };
        dialog.AddFilter("project.json ; project.json");
        dialog.DirSelected += OnOpenProjectChosen;
        dialog.FileSelected += OnOpenProjectChosen;
        return dialog;
    }

    private FileDialog BuildPackageDialog()
    {
        var dialog = new FileDialog
        {
            Access = FileDialog.AccessEnum.Filesystem,
            FileMode = FileDialog.FileModeEnum.SaveFile,
            UseNativeDialog = false,
            Title = Dual("导出包", "Export Package")
        };
        dialog.AddFilter("*.sts2pack ; STS2 Package");
        dialog.FileSelected += ExportProjectToPath;
        return dialog;
    }

    private FileDialog BuildAssetDialog()
    {
        var dialog = new FileDialog
        {
            Access = FileDialog.AccessEnum.Filesystem,
            FileMode = FileDialog.FileModeEnum.OpenFile,
            UseNativeDialog = false,
            Title = Dual("选择外部素材", "Select External Asset")
        };
        dialog.AddFilter("*.png ; PNG");
        dialog.AddFilter("*.jpg ; JPG");
        dialog.AddFilter("*.jpeg ; JPEG");
        dialog.AddFilter("*.webp ; WEBP");
        dialog.FileSelected += ImportExternalAsset;
        return dialog;
    }

    private void RefreshLocalizedText()
    {
        _menuBar?.RefreshTexts();
        _browserPanel?.RefreshTexts();
        _centerEditor?.RefreshTexts();
        _detailPanel?.RefreshTexts();
        RefreshProjectGateTexts();
        RefreshProjectState();
    }

    private void RefreshProjectGate()
    {
        var hasProject = _project != null;
        if (_rootContainer != null)
        {
            _rootContainer.Visible = hasProject;
        }

        if (_projectGateOverlay != null)
        {
            _projectGateOverlay.Visible = !hasProject;
        }

        RefreshProjectGateTexts();
        if (hasProject)
        {
            RefreshTabAvailability();
        }
    }

    private void RefreshProjectGateTexts()
    {
        if (_projectGateTitleLabel != null)
        {
            _projectGateTitleLabel.Text = Dual("选择项目开始编辑", "Choose a project to start editing");
        }

        if (_projectGateBodyLabel != null)
        {
            _projectGateBodyLabel.Text = Dual(
                "Project Mode 需要先打开一个项目。请选择“新建项目”或“打开项目”，完成后才会进入完整的全屏编辑界面。",
                "Project Mode requires an active project. Choose New Project or Open Project before entering the full-screen editor.");
        }

        if (_projectGateNewButton != null)
        {
            _projectGateNewButton.Text = Dual("新建项目", "New Project");
        }

        if (_projectGateOpenButton != null)
        {
            _projectGateOpenButton.Text = Dual("打开项目", "Open Project");
        }

        if (_projectGateCloseButton != null)
        {
            _projectGateCloseButton.Text = Dual("返回模式选择", "Back To Mode Chooser");
        }
    }

    private Control BuildProjectGateOverlay()
    {
        var overlay = new Control
        {
            AnchorRight = 1f,
            AnchorBottom = 1f,
            MouseFilter = MouseFilterEnum.Stop
        };

        overlay.AddChild(new ColorRect
        {
            AnchorRight = 1f,
            AnchorBottom = 1f,
            Color = new Color(0.03f, 0.04f, 0.06f, 0.96f),
            MouseFilter = MouseFilterEnum.Ignore
        });

        var center = new CenterContainer
        {
            AnchorRight = 1f,
            AnchorBottom = 1f,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        overlay.AddChild(center);

        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(640f, 0f),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
            SizeFlagsVertical = SizeFlags.ShrinkCenter
        };
        center.AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 24);
        margin.AddThemeConstantOverride("margin_top", 24);
        margin.AddThemeConstantOverride("margin_right", 24);
        margin.AddThemeConstantOverride("margin_bottom", 24);
        panel.AddChild(margin);

        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", 14);
        margin.AddChild(root);

        _projectGateTitleLabel = MakeLabel(string.Empty, true);
        _projectGateTitleLabel.HorizontalAlignment = HorizontalAlignment.Center;
        root.AddChild(_projectGateTitleLabel);

        _projectGateBodyLabel = MakeDetails(string.Empty, scrollActive: false, fitContent: true, minHeight: 92f);
        root.AddChild(_projectGateBodyLabel);

        _projectGateNewButton = MakeButton(string.Empty, ShowNewProjectDialogCore);
        _projectGateNewButton.CustomMinimumSize = new Vector2(0f, 42f);
        root.AddChild(_projectGateNewButton);

        _projectGateOpenButton = MakeButton(string.Empty, ShowOpenProjectDialogCore);
        _projectGateOpenButton.CustomMinimumSize = new Vector2(0f, 42f);
        root.AddChild(_projectGateOpenButton);

        _projectGateCloseButton = MakeButton(string.Empty, ReturnToChooser);
        _projectGateCloseButton.CustomMinimumSize = new Vector2(0f, 38f);
        root.AddChild(_projectGateCloseButton);

        return overlay;
    }

    private void ShowNewProjectDialog()
    {
        if (_dirty)
        {
            ShowUnsavedDialog(ShowNewProjectDialogCore, ShowNewProjectDialogCore);
            return;
        }

        ShowNewProjectDialogCore();
    }

    private void ShowNewProjectDialogCore()
    {
        if (_newProjectDialog == null)
        {
            return;
        }

        var preferredDirectory = ResolvePreferredProjectDirectory();
        if (!string.IsNullOrWhiteSpace(preferredDirectory) && Directory.Exists(preferredDirectory))
        {
            _newProjectDialog.CurrentDir = preferredDirectory;
        }

        _newProjectDialog.PopupCentered();
    }

    private void ShowOpenProjectDialog()
    {
        if (_dirty)
        {
            ShowUnsavedDialog(ShowOpenProjectDialogCore, ShowOpenProjectDialogCore);
            return;
        }

        ShowOpenProjectDialogCore();
    }

    private void ShowOpenProjectDialogCore()
    {
        if (_openProjectDialog == null)
        {
            return;
        }

        var preferredDirectory = ResolvePreferredProjectDirectory();
        if (!string.IsNullOrWhiteSpace(preferredDirectory) && Directory.Exists(preferredDirectory))
        {
            _openProjectDialog.CurrentDir = preferredDirectory;
        }

        _openProjectDialog.PopupCentered();
    }

    private void RefreshProjectState()
    {
        _menuBar?.SetProjectState(_project?.Manifest.Name ?? Dual("未打开项目", "No project open"), _dirty);
    }

    private void OnKindChanged(ModStudioEntityKind kind)
    {
        if (_basicDraftDirty || _graphDraftDirty)
        {
            CommitPendingDrafts();
        }

        _currentKind = kind;
        _currentEntityId = null;
        _currentItem = null;
        _currentViewCache = null;
        _assetTabLoadedForCurrentItem = false;
        _graphTabLoadedForCurrentItem = false;
        RefreshTabAvailability();
        RefreshBrowserItems(selectFirstIfPossible: true);
    }

    private void OnItemSelected(EntityBrowserItem item)
    {
        if (_currentItem != null &&
            !string.Equals(_currentItem.EntityId, item.EntityId, StringComparison.Ordinal) &&
            (_basicDraftDirty || _graphDraftDirty))
        {
            CommitPendingDrafts();
        }

        if (item.Kind != _currentKind)
        {
            _currentKind = item.Kind;
        }

        _currentItem = item;
        _currentEntityId = item.EntityId;
        RefreshTabAvailability();
        LoadEntity(item);
    }

    private void CreateEntryRequested()
    {
        if (_project == null)
        {
            return;
        }

        if (_currentKind == ModStudioEntityKind.Character || _currentKind == ModStudioEntityKind.Monster)
        {
            return;
        }

        var entityId = _metadataService.GenerateProjectEntityId(_project, _currentKind);
        var metadata = _metadataService.CreateDefaultMetadata(_currentKind, entityId);
        var envelope = new EntityOverrideEnvelope
        {
            EntityKind = _currentKind,
            EntityId = entityId,
            BehaviorSource = BehaviorSource.Native,
            Metadata = new Dictionary<string, string>(metadata, StringComparer.Ordinal)
        };
        _project.Overrides.Add(envelope);
        MarkDirty();
        _browserItemsCache.Remove(_currentKind);
        RefreshBrowserItems(selectFirstIfPossible: false);
        SelectEntity(entityId);
    }

    private void LoadEntity(EntityBrowserItem item)
    {
        _suppressDirty = true;
        try
        {
            _basicDraftDirty = false;
            _graphDraftDirty = false;
            _assetTabLoadedForCurrentItem = false;
            _graphTabLoadedForCurrentItem = false;
            _currentViewCache = GetOrCreateEntityCache(item);
            _graphPreviewContext = BuildPreviewContext(item, _currentViewCache.MergedMetadata);
            RefreshBasicView(item, _currentViewCache);
            RefreshCurrentTabView(forceLoad: true);
            return;
        }
        finally
        {
            _suppressDirty = false;
        }
    }

    private void RefreshAssetDetails(EntityBrowserItem item, EntityOverrideEnvelope? envelope)
    {
        if (_detailPanel == null || _centerEditor == null)
        {
            return;
        }

        var resolvedPath = GetRuntimeAssetPath(item.Kind, item.EntityId, envelope);
        _currentResolvedAssetPath = resolvedPath ?? string.Empty;
        _selectedRuntimeAssetPath = _currentResolvedAssetPath;
        _selectedImportedAsset = ResolveImportedAssetBinding(item.Kind, envelope, _currentResolvedAssetPath);

        var currentTexture = LoadTexture(_currentResolvedAssetPath);
        _centerEditor.AssetEditor.SetPreviews(
            currentTexture,
            _currentResolvedAssetPath,
            currentTexture,
            _currentResolvedAssetPath,
            BuildAssetDetails(item, _currentResolvedAssetPath, _selectedRuntimeAssetPath));
        _detailPanel.SetRuntimeAssets(_assetBindingService.GetRuntimeAssetCandidates(item.Kind));
        _detailPanel.SetImportedAssets(GetImportedAssetsForKind(item.Kind));
    }

    private void ShowAssetFallback(EntityBrowserItem item)
    {
        _currentResolvedAssetPath = string.Empty;
        _selectedRuntimeAssetPath = string.Empty;
        _selectedImportedAsset = null;

        _centerEditor?.AssetEditor.SetPreviews(
            null,
            string.Empty,
            null,
            string.Empty,
            string.Join(System.Environment.NewLine, new[]
            {
                $"Current: {string.Empty}",
                $"Candidate: {string.Empty}",
                $"Runtime candidates: 0",
                $"Imported assets: {GetImportedAssetsForKind(item.Kind).Count}",
                Dual("当前条目的运行时素材读取失败，已降级为安全模式。", "Runtime asset lookup failed for the current entry; falling back to safe mode.")
            }));

        _detailPanel?.SetRuntimeAssets(Array.Empty<string>());
        _detailPanel?.SetImportedAssets(GetImportedAssetsForKind(item.Kind));
    }

    private void ShowAssetFallback(EntityBrowserItem item, EntityEditorViewCache cache)
    {
        _currentResolvedAssetPath = string.Empty;
        _selectedRuntimeAssetPath = string.Empty;
        _selectedImportedAsset = null;

        _centerEditor?.EnsureAssetsBuilt();
        _centerEditor?.AssetEditor.SetPreviews(
            null,
            string.Empty,
            null,
            string.Empty,
            string.Join(System.Environment.NewLine, new[]
            {
                $"Current: {string.Empty}",
                $"Candidate: {string.Empty}",
                $"Runtime candidates: {cache.RuntimeAssetCandidates.Count}",
                $"Imported assets: {cache.ImportedAssets.Count}",
                Dual("当前条目运行时素材读取失败，已降级为安全模式。", "Runtime asset lookup failed for the current entry; falling back to safe mode.")
            }));

        _detailPanel?.SetRuntimeAssets(cache.RuntimeAssetCandidates);
        _detailPanel?.SetImportedAssets(cache.ImportedAssets);
        _assetTabLoadedForCurrentItem = true;
    }

    private void RefreshGraphDetails(EntityBrowserItem item, EntityOverrideEnvelope? envelope)
    {
        if (_detailPanel == null || _centerEditor == null)
        {
            return;
        }

        var graph = GetGraph(item, envelope);
        var sourceModel = ResolveSourceModel(item.Kind, item.EntityId);
        if (graph != null)
        {
            RefreshDerivedGraphText(graph, updateBasicPreview: false);
            _centerEditor.BindGraph(graph, _graphRegistry, sourceModel, _graphPreviewContext);
            _detailPanel.SetGraphDetails(graph.GraphId, graph.Name, graph.Description, envelope?.BehaviorSource == BehaviorSource.Graph);
            _detailPanel.SetGraphInfo(BuildGraphOverviewText(graph, envelope));
            UpdateSelectedNodeDetails(graph, null);
            return;
        }

        if (!item.IsProjectOnly &&
            _nativeAutoGraphService.TryCreateGraph(item.Kind, item.EntityId, out var autoGraphResult) &&
            autoGraphResult != null)
        {
            RefreshDerivedGraphText(autoGraphResult.Graph, updateBasicPreview: false);
            _centerEditor.BindGraph(autoGraphResult.Graph, _graphRegistry, sourceModel, _graphPreviewContext);
            _detailPanel.SetGraphDetails(autoGraphResult.Graph.GraphId, autoGraphResult.Graph.Name, autoGraphResult.Graph.Description, false);
            _detailPanel.SetGraphInfo(BuildGraphOverviewText(autoGraphResult.Graph, null));
            UpdateSelectedNodeDetails(autoGraphResult.Graph, autoGraphResult.Graph.EntryNodeId);
            return;
        }

        _centerEditor.ClearGraph();
        _detailPanel.SetGraphDetails(string.Empty, item.Title, string.Empty, false);
            _detailPanel.SetGraphInfo(Dual("当前条目尚未创建 graph。", "No graph has been created for the current entry."));
        _detailPanel.SetSelectedNode(null);
        _detailPanel.SetSelectedNodeProperties(new Dictionary<string, string>());
    }

    private void ClearEditorCaches()
    {
        _entityViewCache.Clear();
        _browserItemsCache.Clear();
        _currentViewCache = null;
        _graphCanvasSignalsWired = false;
    }

    private string BuildEntityCacheKey(ModStudioEntityKind kind, string entityId)
    {
        return $"{kind}:{entityId}";
    }

    private EntityEditorViewCache GetOrCreateEntityCache(EntityBrowserItem item)
    {
        var cacheKey = BuildEntityCacheKey(item.Kind, item.EntityId);
        if (_entityViewCache.TryGetValue(cacheKey, out var existing))
        {
            return existing;
        }

        var originalMetadata = GetOriginalMetadata(item);
        var envelope = GetEnvelope(item.Kind, item.EntityId);
        var mergedMetadata = MergeMetadata(originalMetadata, envelope);

        var cache = new EntityEditorViewCache
        {
            OriginalMetadata = new Dictionary<string, string>(originalMetadata, StringComparer.Ordinal),
            MergedMetadata = mergedMetadata
        };

        _entityViewCache[cacheKey] = cache;
        return cache;
    }

    private BehaviorGraphDefinition? GetOrCreateAutoGraphCache(EntityBrowserItem item, EntityEditorViewCache cache)
    {
        if (cache.AutoGraph != null)
        {
            return cache.AutoGraph;
        }

        if (item.IsProjectOnly)
        {
            return null;
        }

        if (!_nativeAutoGraphService.TryCreateGraph(item.Kind, item.EntityId, out var autoGraphResult) ||
            autoGraphResult?.Graph == null)
        {
            return null;
        }

        cache.AutoGraph = CloneGraph(autoGraphResult.Graph);
        return cache.AutoGraph;
    }

    private void RefreshBasicView(EntityBrowserItem item, EntityEditorViewCache cache)
    {
        if (_centerEditor == null || _detailPanel == null)
        {
            return;
        }

        var title = ResolveEntityDisplayTitle(item, cache.MergedMetadata);
        _centerEditor.BasicEditor.BindMetadata(title, item.Kind, BuildBasicEditorMetadata(item.Kind, cache.MergedMetadata));
        _detailPanel.SetBasicText(BuildBasicDetailsText(item, BuildBasicEditorMetadata(item.Kind, cache.OriginalMetadata)));
    }

    private void RefreshCurrentTabView(bool forceLoad)
    {
        if (_currentItem == null || _centerEditor == null)
        {
            return;
        }

        var cache = _currentViewCache ?? GetOrCreateEntityCache(_currentItem);
        var tabIndex = _centerEditor.Tabs.CurrentTab;
        if (tabIndex == 0)
        {
            RefreshBasicView(_currentItem, cache);
            if (_graphTabLoadedForCurrentItem)
            {
                RefreshCurrentGraphInfo();
            }
            return;
        }

        if (tabIndex == 1)
        {
            if (forceLoad || !_assetTabLoadedForCurrentItem)
            {
                RefreshAssetView(_currentItem, cache);
            }
            return;
        }

        if (tabIndex == 2)
        {
            if (forceLoad || !_graphTabLoadedForCurrentItem)
            {
                RefreshGraphView(_currentItem, cache);
            }
        }
    }

    private EntityEditorViewCache? RefreshCurrentEntityCache(bool clearAutoGraph = false)
    {
        if (_currentItem == null)
        {
            return null;
        }

        var cache = GetOrCreateEntityCache(_currentItem);
        cache.OriginalMetadata = new Dictionary<string, string>(GetOriginalMetadata(_currentItem), StringComparer.Ordinal);
        var envelope = GetEnvelope(_currentKind, _currentItem.EntityId);
        cache.MergedMetadata = MergeMetadata(cache.OriginalMetadata, envelope);
        if (_assetTabLoadedForCurrentItem && !cache.AssetsLoaded)
        {
            cache.RuntimeAssetCandidates = _assetBindingService.GetRuntimeAssetCandidates(_currentKind).ToList();
            cache.ImportedAssets = GetImportedAssetsForKind(_currentKind);
            cache.AssetsLoaded = true;
        }
        if (clearAutoGraph)
        {
            cache.AutoGraph = null;
        }

        _currentViewCache = cache;
        return cache;
    }

    private void RefreshAssetView(EntityBrowserItem item, EntityEditorViewCache cache)
    {
        if (!SupportsAssets(item.Kind))
        {
            ShowAssetFallback(item, cache);
            return;
        }

        if (!cache.AssetsLoaded)
        {
            cache.RuntimeAssetCandidates = _assetBindingService.GetRuntimeAssetCandidates(item.Kind).ToList();
            cache.ImportedAssets = GetImportedAssetsForKind(item.Kind);
            cache.AssetsLoaded = true;
        }

        var envelope = GetEnvelope(item.Kind, item.EntityId);
        var resolvedPath = GetRuntimeAssetPath(item.Kind, item.EntityId, envelope);
        _currentResolvedAssetPath = resolvedPath ?? string.Empty;

        if (_selectedImportedAsset == null)
        {
            _selectedRuntimeAssetPath = string.IsNullOrWhiteSpace(_selectedRuntimeAssetPath)
                ? _currentResolvedAssetPath
                : _selectedRuntimeAssetPath;
        }
        else
        {
            _selectedRuntimeAssetPath = _selectedImportedAsset.ManagedPath;
        }

        _selectedImportedAsset = ResolveImportedAssetBinding(item.Kind, envelope, _selectedRuntimeAssetPath) ?? _selectedImportedAsset;
        var candidatePath = _selectedImportedAsset?.ManagedPath ?? _selectedRuntimeAssetPath;
        var currentTexture = LoadTexture(_currentResolvedAssetPath);
        var candidateTexture = LoadTexture(candidatePath);

        _centerEditor?.EnsureAssetsBuilt();
        _centerEditor?.AssetEditor.SetPreviews(
            currentTexture,
            _currentResolvedAssetPath,
            candidateTexture,
            candidatePath,
            BuildAssetDetails(item, _currentResolvedAssetPath, candidatePath));

        _detailPanel?.SetRuntimeAssets(cache.RuntimeAssetCandidates);
        _detailPanel?.SetImportedAssets(cache.ImportedAssets);
        _assetTabLoadedForCurrentItem = true;
    }

    private void RefreshGraphView(EntityBrowserItem item, EntityEditorViewCache cache)
    {
        if (_detailPanel == null || _centerEditor == null)
        {
            return;
        }

        if (!SupportsGraph(item.Kind))
        {
            _centerEditor.ClearGraph();
            _detailPanel.SetGraphDetails(string.Empty, ResolveEntityDisplayTitle(item, cache.MergedMetadata), string.Empty, false);
            _detailPanel.SetGraphInfo(Dual("当前分类不支持 Graph 编辑。", "Graph editing is not supported for the current category."));
            _detailPanel.SetPreviewContext(_graphPreviewContext);
            _detailPanel.SetSelectedNode(null);
            _detailPanel.SetSelectedNodeProperties(new Dictionary<string, string>());
            _graphTabLoadedForCurrentItem = true;
            return;
        }

        _centerEditor.EnsureGraphBuilt();
        EnsureGraphCanvasSignalsWired();
        var envelope = GetEnvelope(item.Kind, item.EntityId);
        var sourceModel = ResolveSourceModel(item.Kind, item.EntityId);
        var graph = GetGraph(item, envelope) ?? GetOrCreateAutoGraphCache(item, cache);
        if (graph == null)
        {
            var scaffoldGraphId = $"draft_{item.Kind.ToString().ToLowerInvariant()}_{Guid.NewGuid():N}";
            var scaffold = BehaviorGraphTemplateFactory.CreateDefaultScaffold(
                scaffoldGraphId,
                item.Kind,
                ResolveEntityDisplayTitle(item, cache.MergedMetadata),
                item.Summary);
            RefreshDerivedGraphText(scaffold, updateBasicPreview: false);
            _centerEditor.BindGraph(scaffold, _graphRegistry, sourceModel, _graphPreviewContext);
            _detailPanel.SetGraphDetails(scaffold.GraphId, scaffold.Name, scaffold.Description, false);
            _detailPanel.SetGraphInfo(Dual(
                "当前条目的原版效果暂未自动转成 graph，已为你创建可手工编辑的空白 scaffold。",
                "Native behavior could not be converted automatically. A blank scaffold graph has been created for manual editing."));
            _detailPanel.SetPreviewContext(_graphPreviewContext);
            UpdateSelectedNodeDetails(scaffold, scaffold.EntryNodeId);
            _graphTabLoadedForCurrentItem = true;
            return;
        }

        var graphToBind = graph == cache.AutoGraph ? CloneGraph(graph) : graph;

        RefreshDerivedGraphText(graphToBind, updateBasicPreview: false);
        _centerEditor.BindGraph(graphToBind, _graphRegistry, sourceModel, _graphPreviewContext);
        _detailPanel.SetGraphDetails(graphToBind.GraphId, graphToBind.Name, graphToBind.Description, envelope?.BehaviorSource == BehaviorSource.Graph);
        _detailPanel.SetGraphInfo(BuildGraphOverviewText(graphToBind, envelope));
        _detailPanel.SetPreviewContext(_graphPreviewContext);
        UpdateSelectedNodeDetails(graphToBind, graphToBind.EntryNodeId);
        _graphTabLoadedForCurrentItem = true;
    }

    private void OnCenterTabChanged(long index)
    {
        _detailPanel?.SetTab((int)index);
        RefreshCurrentTabView(forceLoad: true);
    }

    private void EnsureGraphCanvasSignalsWired()
    {
        if (_graphCanvasSignalsWired || _centerEditor?.GraphEditor?.CanvasView == null)
        {
            return;
        }

        _centerEditor.GraphEditor.CanvasView.GraphChanged += OnGraphChanged;
        _centerEditor.GraphEditor.CanvasView.SelectedNodeChanged += OnGraphNodeChanged;
        _graphCanvasSignalsWired = true;
    }

    private static string ResolveEntityDisplayTitle(EntityBrowserItem item, IReadOnlyDictionary<string, string> metadata)
    {
        return metadata.TryGetValue("title", out var value) && !string.IsNullOrWhiteSpace(value) ? value : item.Title;
    }

    private void UpdateCachedBrowserItem(EntityBrowserItem item, IReadOnlyDictionary<string, string> metadata)
    {
        if (!_browserItemsCache.TryGetValue(item.Kind, out var items))
        {
            return;
        }

        var index = items.FindIndex(entry => string.Equals(entry.EntityId, item.EntityId, StringComparison.Ordinal));
        if (index < 0)
        {
            return;
        }

        items[index] = BuildBrowserItemSnapshot(item, metadata, items[index]);
        _browserPanel?.BindItems(items);
        _browserPanel?.SetSelection(item.Kind, item.EntityId);
    }

    private EntityBrowserItem BuildBrowserItemSnapshot(EntityBrowserItem item, IReadOnlyDictionary<string, string> metadata, EntityBrowserItem fallback)
    {
        var title = ResolveEntityDisplayTitle(item, metadata);
        var summary = item.Kind switch
        {
            ModStudioEntityKind.Character => $"HP {MetadataOrFallback(metadata, "starting_hp", "0")} | {Dual("金币", "Gold")} {MetadataOrFallback(metadata, "starting_gold", "0")} | {Dual("能量", "Energy")} {MetadataOrFallback(metadata, "max_energy", "0")}",
            ModStudioEntityKind.Card => $"{ModStudioFieldDisplayNames.FormatPropertyValue("type", MetadataOrFallback(metadata, "type", "Attack"))} | {ModStudioFieldDisplayNames.FormatPropertyValue("rarity", MetadataOrFallback(metadata, "rarity", "Common"))} | {Dual("卡池", "Pool")} {ModStudioFieldDisplayNames.FormatPropertyValue("pool_id", MetadataOrFallback(metadata, "pool_id", "-"))}",
            ModStudioEntityKind.Relic => $"{ModStudioFieldDisplayNames.FormatPropertyValue("rarity", MetadataOrFallback(metadata, "rarity", "Common"))} | {Dual("卡池", "Pool")} {ModStudioFieldDisplayNames.FormatPropertyValue("pool_id", MetadataOrFallback(metadata, "pool_id", "-"))}",
            ModStudioEntityKind.Potion => $"{ModStudioFieldDisplayNames.FormatPropertyValue("rarity", MetadataOrFallback(metadata, "rarity", "Common"))} | {ModStudioFieldDisplayNames.FormatPropertyValue("usage", MetadataOrFallback(metadata, "usage", "CombatOnly"))} | {ModStudioFieldDisplayNames.FormatPropertyValue("target_type", MetadataOrFallback(metadata, "target_type", "Self"))}",
            ModStudioEntityKind.Event => $"{ModStudioFieldDisplayNames.FormatPropertyValue("layout_type", MetadataOrFallback(metadata, "layout_type", "Default"))} | {Dual("共享", "Shared")} {ModStudioFieldDisplayNames.FormatPropertyValue("is_shared", MetadataOrFallback(metadata, "is_shared", "True"))}",
            ModStudioEntityKind.Enchantment => $"{Dual("显示数值", "Show Amount")} {ModStudioFieldDisplayNames.FormatPropertyValue("show_amount", MetadataOrFallback(metadata, "show_amount", "False"))} | {Dual("图标", "Icon")} {MetadataOrFallback(metadata, "icon_path", "-")}",
            _ => fallback.Summary
        };

        return new EntityBrowserItem
        {
            Kind = item.Kind,
            EntityId = item.EntityId,
            IsProjectOnly = item.IsProjectOnly,
            Title = title,
            Summary = summary,
            DetailText = fallback.DetailText
        };
    }

    private BehaviorGraphDefinition? GetGraph(EntityBrowserItem item, EntityOverrideEnvelope? envelope)
    {
        if (_project == null)
        {
            return null;
        }

        if (envelope?.GraphId != null &&
            _project.Graphs.TryGetValue(envelope.GraphId, out var graphFromEnvelope))
        {
            return graphFromEnvelope;
        }

        var stagedGraphId = $"{item.Kind}:{item.EntityId}";
        if (_project.Graphs.TryGetValue(stagedGraphId, out var stagedGraph))
        {
            return stagedGraph;
        }

        return null;
    }

    private void OnRuntimeAssetSelected(long index)
    {
        if (_detailPanel == null || _centerEditor == null || _currentItem == null || index < 0)
        {
            return;
        }

        if (index >= _detailPanel.RuntimeAssetList.ItemCount)
        {
            return;
        }

        var metadata = _detailPanel.RuntimeAssetList.GetItemMetadata((int)index);
        var selectedPath = metadata.VariantType == Variant.Type.Nil ? string.Empty : metadata.AsString();
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return;
        }

        _selectedImportedAsset = null;
        _selectedRuntimeAssetPath = selectedPath;
        var currentTexture = LoadTexture(_currentResolvedAssetPath);
        var candidateTexture = LoadTexture(selectedPath);
        _centerEditor.AssetEditor.SetPreviews(
            currentTexture,
            _currentResolvedAssetPath,
            candidateTexture,
            selectedPath,
            BuildAssetDetails(_currentItem, _currentResolvedAssetPath, selectedPath));
    }

    private void OnImportedAssetSelected(long index)
    {
        if (_detailPanel == null || _centerEditor == null || _currentItem == null || index < 0)
        {
            return;
        }

        var metadata = _detailPanel.ImportedAssetList.GetItemMetadata((int)index);
        var assetId = metadata.VariantType == Variant.Type.Nil ? string.Empty : metadata.AsString();
        if (string.Equals(assetId, "__add_external__", StringComparison.Ordinal))
        {
            ShowAssetImportDialog();
            return;
        }

        var asset = GetImportedAssetsForKind(_currentKind)
            .FirstOrDefault(candidate => string.Equals(candidate.Id, assetId, StringComparison.Ordinal));
        if (asset == null)
        {
            return;
        }

        _selectedImportedAsset = asset;
        _selectedRuntimeAssetPath = asset.ManagedPath;
        var currentTexture = LoadTexture(_currentResolvedAssetPath);
        var candidateTexture = LoadTexture(asset.ManagedPath);
        _centerEditor.AssetEditor.SetPreviews(
            currentTexture,
            _currentResolvedAssetPath,
            candidateTexture,
            asset.ManagedPath,
            BuildAssetDetails(_currentItem, _currentResolvedAssetPath, asset.ManagedPath));
    }

    private void ShowAssetImportDialog()
    {
        if (_assetImportDialog == null)
        {
            return;
        }

        _assetImportDialog.PopupCentered();
    }

    private void ImportExternalAsset(string sourcePath)
    {
        if (_project == null || _currentItem == null || string.IsNullOrWhiteSpace(sourcePath))
        {
            return;
        }

        if (!_assetBindingService.TryGetDescriptor(_currentKind, out var descriptor))
        {
            return;
        }

        var projectAssetsRoot = ModStudioPaths.GetProjectAssetsDirectoryFromRoot(_currentProjectPath);
        Directory.CreateDirectory(projectAssetsRoot);
        var roleDirectory = Path.Combine(projectAssetsRoot, _currentKind.ToString().ToLowerInvariant(), SanitizeFileName(_currentItem.EntityId));
        Directory.CreateDirectory(roleDirectory);

        var destinationPath = Path.Combine(roleDirectory, Path.GetFileName(sourcePath));
        File.Copy(sourcePath, destinationPath, overwrite: true);

        var asset = new AssetRef
        {
            SourceType = "external",
            LogicalRole = descriptor.LogicalRole,
            SourcePath = Path.GetFullPath(sourcePath),
            ManagedPath = destinationPath,
            PackagePath = string.Empty,
            FileName = Path.GetFileName(destinationPath)
        };

        _project.ProjectAssets.Add(asset);
        _selectedImportedAsset = asset;
        _selectedRuntimeAssetPath = asset.ManagedPath;
        MarkDirty();
        RefreshCurrentEntityCache();
        _detailPanel?.SetImportedAssets(_currentViewCache?.ImportedAssets ?? GetImportedAssetsForKind(_currentKind));
        if (_centerEditor != null)
        {
            var currentTexture = LoadTexture(_currentResolvedAssetPath);
            var candidateTexture = LoadTexture(asset.ManagedPath);
            _centerEditor.AssetEditor.SetPreviews(
                currentTexture,
                _currentResolvedAssetPath,
                candidateTexture,
                asset.ManagedPath,
                BuildAssetDetails(_currentItem, _currentResolvedAssetPath, asset.ManagedPath));
        }
    }

    private void SaveAssetBinding()
    {
        if (_project == null || _currentItem == null)
        {
            return;
        }

        if (_selectedImportedAsset != null && _assetBindingService.TryGetDescriptor(_currentKind, out _))
        {
            _assetBindingService.BindProjectAsset(_project, _currentKind, _currentItem.EntityId, _selectedImportedAsset.Id);
            MarkDirty();
            RefreshCurrentEntityCache();
            if (_currentViewCache != null)
            {
                UpdateCachedBrowserItem(_currentItem, _currentViewCache.MergedMetadata);
            }
            RefreshCurrentTabView(forceLoad: true);
            return;
        }

        if (!string.IsNullOrWhiteSpace(_selectedRuntimeAssetPath))
        {
            _assetBindingService.BindRuntimeAsset(_project, _currentKind, _currentItem.EntityId, _selectedRuntimeAssetPath);
            MarkDirty();
            RefreshCurrentEntityCache();
            if (_currentViewCache != null)
            {
                UpdateCachedBrowserItem(_currentItem, _currentViewCache.MergedMetadata);
            }
            RefreshCurrentTabView(forceLoad: true);
        }
    }

    private void CommitPendingDrafts()
    {
        if (_graphDraftDirty)
        {
            SaveGraph();
        }

        if (_basicDraftDirty)
        {
            SaveBasic();
        }
    }
}


