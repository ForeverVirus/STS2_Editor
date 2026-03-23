using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using STS2_Editor.Scripts.Editor.Core.Models;
using STS2_Editor.Scripts.Editor.Core.Services;
using STS2_Editor.Scripts.Editor.Core.Utilities;
using STS2_Editor.Scripts.Editor.Graph;
using STS2_Editor.Scripts.Editor.Packaging;
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

    private bool _uiBuilt;
    private bool _dirty;
    private bool _suppressDirty;
    private bool _basicDraftDirty;
    private bool _graphDraftDirty;
    private string _currentProjectPath = string.Empty;
    private EditorProject? _project;
    private ModStudioEntityKind _currentKind = ModStudioEntityKind.Card;
    private string? _currentEntityId;
    private EntityBrowserItem? _currentItem;
    private string _currentResolvedAssetPath = string.Empty;
    private string _selectedRuntimeAssetPath = string.Empty;
    private AssetRef? _selectedImportedAsset;
    private Action? _pendingAfterSave;
    private Action? _pendingAfterDiscard;

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
        _centerEditor.GraphEditor.CanvasView.GraphChanged += OnGraphChanged;
        _centerEditor.GraphEditor.CanvasView.SelectedNodeChanged += OnGraphNodeChanged;
        _centerEditor.Tabs.TabChanged += index => _detailPanel?.SetTab((int)index);
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
        _detailPanel.NodePropertyChanged += OnSelectedNodePropertyChanged;
        _detailPanel.SelectedNodeDisplayNameChanged += OnSelectedNodeDisplayNameChanged;
        _detailPanel.SelectedNodeDescriptionChanged += OnSelectedNodeDescriptionChanged;
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
                "Project Mode 需要先打开一个项目工程。请选择“新建项目”或“打开项目”，完成后才会进入完整的全屏编辑界面。",
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
            if (_browserPanel != null)
            {
                _browserPanel.SetSelection(item.Kind, item.EntityId);
            }

            var originalMetadata = GetOriginalMetadata(item);
            var envelope = GetEnvelope(item.Kind, item.EntityId);
            var mergedMetadata = MergeMetadata(originalMetadata, envelope);
            _centerEditor?.BasicEditor.BindMetadata(item.Title, BuildBasicEditorMetadata(item.Kind, mergedMetadata));
            _detailPanel?.SetBasicText(BuildBasicDetailsText(item, BuildBasicEditorMetadata(item.Kind, originalMetadata)));

            if (SupportsAssets(item.Kind))
            {
                try
                {
                    RefreshAssetDetails(item, envelope);
                }
                catch
                {
                    ShowAssetFallback(item);
                }
            }
            else
            {
                ShowAssetFallback(item);
            }

            if (SupportsGraph(item.Kind))
            {
                RefreshGraphDetails(item, envelope);
            }
            else
            {
                _centerEditor?.ClearGraph();
                _detailPanel?.SetGraphDetails(string.Empty, item.Title, string.Empty, false);
                _detailPanel?.SetGraphInfo(Dual("当前分类不支持 Graph 编辑。", "Graph editing is not supported for the current category."));
                _detailPanel?.SetSelectedNode(null);
                _detailPanel?.SetSelectedNodeProperties(new Dictionary<string, string>());
            }
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

    private void RefreshGraphDetails(EntityBrowserItem item, EntityOverrideEnvelope? envelope)
    {
        if (_detailPanel == null || _centerEditor == null)
        {
            return;
        }

        var graph = GetGraph(item, envelope);
        if (graph != null)
        {
            RefreshDerivedGraphText(graph, updateBasicPreview: false);
            _centerEditor.BindGraph(graph, _graphRegistry);
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
            _centerEditor.BindGraph(autoGraphResult.Graph, _graphRegistry);
            _detailPanel.SetGraphDetails(autoGraphResult.Graph.GraphId, autoGraphResult.Graph.Name, autoGraphResult.Graph.Description, false);
            _detailPanel.SetGraphInfo(BuildGraphOverviewText(autoGraphResult.Graph, null));
            UpdateSelectedNodeDetails(autoGraphResult.Graph, autoGraphResult.Graph.EntryNodeId);
            return;
        }

        _centerEditor.ClearGraph();
        _detailPanel.SetGraphDetails(string.Empty, item.Title, string.Empty, false);
        _detailPanel.SetGraphInfo(Dual("尚未为当前条目创建 graph。", "No graph has been created for the current entry."));
        _detailPanel.SetSelectedNode(null);
        _detailPanel.SetSelectedNodeProperties(new Dictionary<string, string>());
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
        _detailPanel?.SetImportedAssets(GetImportedAssetsForKind(_currentKind));
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
            LoadEntity(_currentItem);
            return;
        }

        if (!string.IsNullOrWhiteSpace(_selectedRuntimeAssetPath))
        {
            _assetBindingService.BindRuntimeAsset(_project, _currentKind, _currentItem.EntityId, _selectedRuntimeAssetPath);
            MarkDirty();
            LoadEntity(_currentItem);
        }
    }

    private void RevertAssetBinding()
    {
        if (_project == null || _currentItem == null)
        {
            return;
        }

        _assetBindingService.ClearAssetBinding(_project, _currentKind, _currentItem.EntityId);
        _selectedImportedAsset = null;
        _selectedRuntimeAssetPath = string.Empty;
        _currentResolvedAssetPath = string.Empty;
        var envelope = GetEnvelope(_currentKind, _currentItem.EntityId);
        if (envelope != null)
        {
            TryRemoveEmptyEnvelope(_currentItem, envelope);
        }
        MarkDirty();
        LoadEntity(_currentItem);
    }

    private void SaveBasic()
    {
        if (_project == null || _currentItem == null || _centerEditor == null)
        {
            return;
        }

        var values = _centerEditor.BasicEditor.GetFieldValues();
        var envelope = GetOrCreateEnvelope(_currentKind, _currentItem.EntityId);
        foreach (var key in GetBasicFieldKeys(_currentKind))
        {
            if (values.TryGetValue(key, out var value))
            {
                envelope.Metadata[key] = value;
            }
            else
            {
                envelope.Metadata.Remove(key);
            }
        }

        _basicDraftDirty = false;
        MarkDirty();
        RefreshBrowserItems(selectFirstIfPossible: false);
        SelectEntity(_currentItem.EntityId);
    }

    private void RevertBasic()
    {
        if (_project == null || _currentItem == null)
        {
            return;
        }

        var envelope = GetEnvelope(_currentKind, _currentItem.EntityId);
        if (envelope == null)
        {
            LoadEntity(_currentItem);
            return;
        }

        if (_currentItem.IsProjectOnly)
        {
            var defaults = _metadataService.CreateDefaultMetadata(_currentKind, _currentItem.EntityId);
            foreach (var key in GetBasicFieldKeys(_currentKind))
            {
                if (defaults.TryGetValue(key, out var value))
                {
                    envelope.Metadata[key] = value;
                }
                else
                {
                    envelope.Metadata.Remove(key);
                }
            }
        }
        else
        {
            foreach (var key in GetBasicFieldKeys(_currentKind))
            {
                envelope.Metadata.Remove(key);
            }
        }

        TryRemoveEmptyEnvelope(_currentItem, envelope);
        _basicDraftDirty = false;
        MarkDirty();
        RefreshBrowserItems(selectFirstIfPossible: false);
        SelectEntity(_currentItem.EntityId);
    }

    private void ImportGraphTemplate()
    {
        if (_currentItem == null || _project == null || _graphImportDialog == null)
        {
            return;
        }

        var candidates = _metadataService.GetItems(_currentKind, _project)
            .Where(item => !string.Equals(item.EntityId, _currentItem.EntityId, StringComparison.Ordinal))
            .Select(item => new ModStudioGraphImportCandidate
            {
                EntityId = item.EntityId,
                Title = item.Title,
                Summary = item.Summary,
                IsProjectOnly = item.IsProjectOnly,
                IsCurrentEntity = string.Equals(item.EntityId, _currentItem.EntityId, StringComparison.Ordinal),
                SourceItem = item
            })
            .ToList();

        if (candidates.Count == 0)
        {
            CreateDefaultGraphForCurrentItem();
            return;
        }

        _graphImportDialog.SetCandidates(candidates);
        _graphImportDialog.ShowDialog();
    }

    private void ImportGraphFromCandidate(ModStudioGraphImportCandidate candidate)
    {
        if (_project == null || _currentItem == null)
        {
            return;
        }

        var sourceItem = candidate.SourceItem;
        if (sourceItem == null)
        {
            return;
        }

        if (!TryCreateImportGraph(sourceItem, out var importedGraph, out var graphInfo) || importedGraph == null)
        {
            CreateDefaultGraphForCurrentItem();
            return;
        }

        importedGraph.GraphId = Guid.NewGuid().ToString("N");
        importedGraph.Name = _currentItem.Title;
        importedGraph.EntityKind = _currentKind;
        BindDraftGraph(importedGraph, graphInfo, true);
    }

    private void CreateDefaultGraphForCurrentItem()
    {
        if (_currentItem == null)
        {
            return;
        }

        var graphId = Guid.NewGuid().ToString("N");
        var graph = BehaviorGraphTemplateFactory.CreateDefaultScaffold(graphId, _currentKind, _currentItem.Title, _currentItem.Summary);
        BindDraftGraph(graph, BuildGraphOverviewText(graph, null), true);
    }

    private void BindDraftGraph(BehaviorGraphDefinition graph, string graphInfo, bool enableGraphBehavior)
    {
        _ = graphInfo;
        RefreshDerivedGraphText(graph, updateBasicPreview: true);
        _centerEditor?.BindGraph(graph, _graphRegistry);
        if (_detailPanel != null)
        {
            _detailPanel.SetGraphDetails(graph.GraphId, graph.Name, graph.Description, enableGraphBehavior);
            _detailPanel.SetGraphInfo(BuildGraphOverviewText(graph, GetEnvelope(_currentKind, _currentEntityId)));
        }
        _graphDraftDirty = true;
        MarkDirty();
        UpdateSelectedNodeDetails(graph, graph.EntryNodeId);
    }

    private bool TryCreateImportGraph(EntityBrowserItem sourceItem, out BehaviorGraphDefinition? graph, out string graphInfo)
    {
        graph = null;
        graphInfo = string.Empty;

        var sourceEnvelope = GetEnvelope(sourceItem.Kind, sourceItem.EntityId);
        var existingGraph = GetGraph(sourceItem, sourceEnvelope);
        if (existingGraph != null)
        {
            graph = CloneGraph(existingGraph);
            graphInfo = BuildGraphOverviewText(graph, sourceEnvelope);
            return true;
        }

        if (!sourceItem.IsProjectOnly &&
            _nativeAutoGraphService.TryCreateGraph(sourceItem.Kind, sourceItem.EntityId, out var autoGraphResult) &&
            autoGraphResult != null)
        {
            graph = CloneGraph(autoGraphResult.Graph);
            graphInfo = autoGraphResult.Summary;
            return true;
        }

        return false;
    }

    private void SaveGraph()
    {
        if (_project == null || _currentItem == null || _centerEditor == null || _detailPanel == null)
        {
            return;
        }

        var detailPanel = _detailPanel;
        var graph = _centerEditor.GraphEditor.CanvasView.BoundGraph;
        if (graph == null)
        {
            CreateDefaultGraphForCurrentItem();
            graph = _centerEditor.GraphEditor.CanvasView.BoundGraph;
            if (graph == null)
            {
                return;
            }
        }

        _centerEditor.GraphEditor.CanvasView.ExportLayout();
        var previousGraphId = GetEnvelope(_currentKind, _currentItem.EntityId)?.GraphId;
        graph.Description = detailPanel.GraphDescriptionEdit.Text ?? graph.Description;
        graph.Name = detailPanel.GraphNameEdit.Text ?? graph.Name;
        graph.GraphId = detailPanel.GraphIdEdit.Text ?? graph.GraphId;
        graph.EntityKind = _currentKind;

        var envelope = GetOrCreateEnvelope(_currentKind, _currentItem.EntityId);
        envelope.BehaviorSource = detailPanel.GraphEnabledCheck.ButtonPressed ? BehaviorSource.Graph : BehaviorSource.Native;
        envelope.GraphId = graph.GraphId;
        if (!string.IsNullOrWhiteSpace(previousGraphId) &&
            !string.Equals(previousGraphId, graph.GraphId, StringComparison.Ordinal))
        {
            _project.Graphs.Remove(previousGraphId);
        }
        _project.Graphs[graph.GraphId] = CloneGraph(graph);
        ApplyGeneratedDescription(envelope, graph);
        _graphDraftDirty = false;
        MarkDirty();
        RefreshGraphDetails(_currentItem, envelope);
    }

    private void ValidateGraph()
    {
        if (_centerEditor?.GraphEditor.CanvasView.BoundGraph == null || _detailPanel == null)
        {
            return;
        }

        var validation = _graphRegistry.Validate(_centerEditor.GraphEditor.CanvasView.BoundGraph);
        _detailPanel.SetGraphInfo(string.Join(System.Environment.NewLine, validation.Errors.DefaultIfEmpty(Dual("Graph 校验通过。", "Graph validation passed."))));
    }

    private void OnGraphEnabledToggled(bool toggled)
    {
        if (_suppressDirty)
        {
            return;
        }

        if (toggled && _centerEditor?.GraphEditor.CanvasView.BoundGraph == null)
        {
            CreateDefaultGraphForCurrentItem();
            return;
        }

        MarkGraphDirty();
    }

    private void OnGraphNodeChanged(string? nodeId)
    {
        if (_centerEditor?.GraphEditor.CanvasView.BoundGraph == null)
        {
            return;
        }

        var graph = _centerEditor.GraphEditor.CanvasView.BoundGraph;
        var node = string.IsNullOrWhiteSpace(nodeId)
            ? null
            : graph.Nodes.FirstOrDefault(item => string.Equals(item.NodeId, nodeId, StringComparison.Ordinal));
        UpdateSelectedNodeDetails(graph, node?.NodeId);
    }

    private void UpdateSelectedNodeDetails(BehaviorGraphDefinition graph, string? selectedNodeId)
    {
        if (_detailPanel == null)
        {
            return;
        }

        var selectedNode = string.IsNullOrWhiteSpace(selectedNodeId)
            ? null
            : graph.Nodes.FirstOrDefault(node => string.Equals(node.NodeId, selectedNodeId, StringComparison.Ordinal));

        WithSuppressedDirty(() =>
        {
            _detailPanel.SetSelectedNode(selectedNode);
            _detailPanel.SetSelectedNodeProperties(selectedNode?.Properties ?? new Dictionary<string, string>());
        });
    }

    private void OnNewProjectChosen(string path)
    {
        var normalized = ModStudioPaths.NormalizeProjectRootPath(path);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        _project = _projectStore.CreateProject(normalized, Path.GetFileName(normalized), overwriteExistingProject: true);
        _currentProjectPath = normalized;
        _currentKind = ModStudioEntityKind.Card;
        _currentEntityId = null;
        _currentItem = null;
        _dirty = false;
        _basicDraftDirty = false;
        _graphDraftDirty = false;
        ModStudioSettingsStore.RecordRecentProject(normalized);
        RefreshProjectGate();
        RefreshBrowserItems(selectFirstIfPossible: true);
        RefreshProjectState();
    }

    private void OnOpenProjectChosen(string path)
    {
        if (_projectStore.TryLoad(path, out var project) && project != null)
        {
            _project = project;
            _currentProjectPath = _projectStore.GetProjectDirectory(path);
            _currentEntityId = null;
            _currentItem = null;
            _currentKind = ModStudioEntityKind.Card;
            _dirty = false;
            _basicDraftDirty = false;
            _graphDraftDirty = false;
            ModStudioSettingsStore.RecordRecentProject(_currentProjectPath);
            RefreshProjectGate();
            RefreshBrowserItems(selectFirstIfPossible: true);
            RefreshProjectState();
        }
    }

    private void SaveProject()
    {
        if (_project == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_currentProjectPath))
        {
            return;
        }

        CommitPendingDrafts();
        _projectStore.Save(_project, _currentProjectPath);
        ModStudioSettingsStore.RecordRecentProject(_currentProjectPath);
        _dirty = false;
        _basicDraftDirty = false;
        _graphDraftDirty = false;
        RefreshProjectState();
    }

    private void ExportProject()
    {
        if (_project == null || string.IsNullOrWhiteSpace(_currentProjectPath))
        {
            return;
        }

        if (_exportDialog != null)
        {
            Directory.CreateDirectory(ModStudioPaths.PublishedPackagesRootPath);
            _exportDialog.CurrentDir = ModStudioPaths.PublishedPackagesRootPath;
            _exportDialog.CurrentFile = $"{SanitizeFileName(_project.Manifest.Name)}.sts2pack";
            _exportDialog.PopupCentered();
        }
    }

    private void ExportProjectToPath(string filePath)
    {
        if (_project == null)
        {
            return;
        }

        CommitPendingDrafts();

        var options = new PackageExportOptions
        {
            PackageId = _project.Manifest.ProjectId,
            DisplayName = _project.Manifest.Name,
            Version = "1.0.0",
            Author = _project.Manifest.Author,
            Description = _project.Manifest.Description
        };
        _packageArchiveService.Export(_project, options, filePath);

        var publishedRoot = Path.GetFullPath(ModStudioPaths.PublishedPackagesRootPath);
        var exportedPath = Path.GetFullPath(filePath);
        if (exportedPath.StartsWith(publishedRoot, StringComparison.OrdinalIgnoreCase))
        {
            ModStudioBootstrap.RuntimeRegistry.Refresh();
        }
    }

    private void RequestSwitchMode()
    {
        if (_dirty)
        {
            ShowUnsavedDialog(ReturnToChooser, ReturnToChooser);
            return;
        }

        ReturnToChooser();
    }

    private void RequestExit()
    {
        if (_dirty)
        {
            ShowUnsavedDialog(ExitToMainMenu, ExitToMainMenu);
            return;
        }

        ExitToMainMenu();
    }

    private void ShowUnsavedDialog(Action afterSave, Action afterDiscard)
    {
        _pendingAfterSave = afterSave;
        _pendingAfterDiscard = afterDiscard;
        _unsavedDialog?.Show();
    }

    private void HandleUnsavedSave()
    {
        _unsavedDialog?.Hide();
        var action = _pendingAfterSave;
        _pendingAfterSave = null;
        _pendingAfterDiscard = null;
        SaveProject();
        action?.Invoke();
    }

    private void HandleUnsavedDiscard()
    {
        _unsavedDialog?.Hide();
        var action = _pendingAfterDiscard;
        _pendingAfterSave = null;
        _pendingAfterDiscard = null;
        action?.Invoke();
    }

    private void HideUnsavedDialog()
    {
        _unsavedDialog?.Hide();
        _pendingAfterSave = null;
        _pendingAfterDiscard = null;
    }

    private void ReturnToChooser()
    {
        _stack.Pop();
    }

    private void ExitToMainMenu()
    {
        while (_stack.Peek() != null)
        {
            _stack.Pop();
        }
    }

    private void RefreshBrowserItems(bool selectFirstIfPossible)
    {
        var items = _metadataService.GetItems(_currentKind, _project).ToList();
        _browserPanel?.BindItems(items);
        _browserPanel?.SetSelection(_currentKind, _currentEntityId);

        if (selectFirstIfPossible && items.Count > 0 && string.IsNullOrWhiteSpace(_currentEntityId))
        {
            SelectEntity(items[0].EntityId);
        }
    }

    private void SelectEntity(string entityId)
    {
        var item = _metadataService.GetItems(_currentKind, _project).FirstOrDefault(entry => string.Equals(entry.EntityId, entityId, StringComparison.Ordinal));
        if (item == null)
        {
            return;
        }

        _currentItem = item;
        _currentEntityId = item.EntityId;
        LoadEntity(item);
    }

    private EntityOverrideEnvelope? GetEnvelope(ModStudioEntityKind kind, string? entityId)
    {
        if (_project == null || string.IsNullOrWhiteSpace(entityId))
        {
            return null;
        }

        return _project.Overrides.FirstOrDefault(item => item.EntityKind == kind && string.Equals(item.EntityId, entityId, StringComparison.Ordinal));
    }

    private EntityOverrideEnvelope GetOrCreateEnvelope(ModStudioEntityKind kind, string entityId)
    {
        if (_project == null)
        {
            throw new InvalidOperationException("Project is not loaded.");
        }

        var envelope = GetEnvelope(kind, entityId);
        if (envelope != null)
        {
            return envelope;
        }

        envelope = new EntityOverrideEnvelope
        {
            EntityKind = kind,
            EntityId = entityId
        };
        _project.Overrides.Add(envelope);
        return envelope;
    }

    private string BuildBasicDetailsText(EntityBrowserItem item, IReadOnlyDictionary<string, string> metadata)
    {
        var lines = new List<string>
        {
            $"{Dual("类型", "Kind")}: {ModStudioLocalization.GetEntityKindDisplayName(item.Kind)}",
            $"{Dual("ID", "Id")}: {item.EntityId}",
            $"{Dual("名称", "Title")}: {item.Title}",
            $"{Dual("摘要", "Summary")}: {item.Summary}"
        };

        foreach (var pair in metadata.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            lines.Add($"{ModStudioFieldDisplayNames.Get(pair.Key)}: {FormatBasicDetailValue(pair.Key, pair.Value)}");
        }

        return string.Join(System.Environment.NewLine, lines);
    }

    private string FormatBasicDetailValue(string key, string? value)
    {
        return key switch
        {
            "starting_deck_ids" => FormatCardIdList(value),
            "starting_relic_ids" => FormatRelicIdList(value),
            "starting_potion_ids" => FormatPotionIdList(value),
            _ => ModStudioFieldDisplayNames.FormatValue(value)
        };
    }

    private static string FormatCardIdList(string? value)
    {
        return FormatIdList(value, id =>
        {
            var card = ModelDb.AllCards.FirstOrDefault(candidate => string.Equals(candidate.Id.Entry, id, StringComparison.Ordinal));
            return card == null ? id : $"{card.Title} [{id}]";
        });
    }

    private static string FormatRelicIdList(string? value)
    {
        return FormatIdList(value, id =>
        {
            var relic = ModelDb.AllRelics.FirstOrDefault(candidate => string.Equals(candidate.Id.Entry, id, StringComparison.Ordinal));
            return relic == null ? id : $"{SafeLocalized(relic.Title)} [{id}]";
        });
    }

    private static string FormatPotionIdList(string? value)
    {
        return FormatIdList(value, id =>
        {
            var potion = ModelDb.AllPotions.FirstOrDefault(candidate => string.Equals(candidate.Id.Entry, id, StringComparison.Ordinal));
            return potion == null ? id : $"{SafeLocalized(potion.Title)} [{id}]";
        });
    }

    private static string FormatIdList(string? value, Func<string, string> formatter)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var values = value
            .Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(entry => entry.Trim())
            .Where(entry => !string.IsNullOrWhiteSpace(entry))
            .Select(formatter)
            .ToList();

        return values.Count == 0 ? string.Empty : string.Join(", ", values);
    }

    private static string SafeLocalized(MegaCrit.Sts2.Core.Localization.LocString? locString)
    {
        if (locString == null)
        {
            return string.Empty;
        }

        try
        {
            return locString.GetRawText();
        }
        catch
        {
            return string.Empty;
        }
    }

    private string BuildAssetDetails(EntityBrowserItem item, string currentPath, string candidatePath)
    {
        return string.Join(System.Environment.NewLine, new[]
        {
            $"{Dual("当前路径", "Current Path")}: {currentPath ?? string.Empty}",
            $"{Dual("候选路径", "Candidate Path")}: {candidatePath ?? string.Empty}",
            $"{Dual("游戏内素材数量", "Game Asset Count")}: {_assetBindingService.GetRuntimeAssetCandidates(item.Kind).Count}",
            $"{Dual("已导入素材数量", "Imported Asset Count")}: {GetImportedAssetsForKind(item.Kind).Count}"
        });
    }

    private string? GetRuntimeAssetPath(ModStudioEntityKind kind, string entityId, EntityOverrideEnvelope? envelope)
    {
        if (envelope != null)
        {
            var binding = _assetBindingService.TryGetDescriptor(kind, out var descriptor) ? descriptor.MetadataKey : string.Empty;
            if (!string.IsNullOrWhiteSpace(binding) && envelope.Metadata.TryGetValue(binding, out var path) && !string.IsNullOrWhiteSpace(path))
            {
                return path;
            }
        }

        return _assetBindingService.GetRuntimeAssetPath(kind, entityId);
    }

    private List<AssetRef> GetImportedAssetsForKind(ModStudioEntityKind kind)
    {
        if (_project == null || !_assetBindingService.TryGetDescriptor(kind, out var descriptor))
        {
            return new List<AssetRef>();
        }

        return _project.ProjectAssets
            .Where(asset => string.Equals(asset.LogicalRole, descriptor.LogicalRole, StringComparison.Ordinal))
            .OrderBy(asset => asset.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static Texture2D? LoadTexture(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            if (path.StartsWith("res://", StringComparison.OrdinalIgnoreCase) || path.StartsWith("user://", StringComparison.OrdinalIgnoreCase))
            {
                return ResourceLoader.Load<Texture2D>(path);
            }

            var image = new Image();
            if (image.Load(path) == Error.Ok)
            {
                return ImageTexture.CreateFromImage(image);
            }
        }
        catch
        {
            // Ignore preview load failures.
        }

        return null;
    }

    private void RefreshTabAvailability()
    {
        var supportsAssets = SupportsAssets(_currentKind);
        var supportsGraph = SupportsGraph(_currentKind);
        _centerEditor?.SetFeatureAvailability(supportsAssets, supportsGraph);
        _detailPanel?.SetFeatureAvailability(supportsAssets, supportsGraph);
    }

    private bool SupportsAssets(ModStudioEntityKind kind)
    {
        return _assetBindingService.TryGetDescriptor(kind, out var descriptor) &&
               (descriptor.SupportsRuntimeCatalog || descriptor.SupportsExternalImport);
    }

    private static bool SupportsGraph(ModStudioEntityKind kind)
    {
        return kind is ModStudioEntityKind.Card or
               ModStudioEntityKind.Relic or
               ModStudioEntityKind.Potion or
               ModStudioEntityKind.Event or
               ModStudioEntityKind.Enchantment;
    }

    private void TryRestoreLastProject()
    {
        if (_project != null || !string.IsNullOrWhiteSpace(_currentProjectPath))
        {
            return;
        }

        var settings = ModStudioSettingsStore.Load();
        if (string.IsNullOrWhiteSpace(settings.LastProjectPath))
        {
            return;
        }

        if (_projectStore.TryLoad(settings.LastProjectPath, out var restoredProject) && restoredProject != null)
        {
            _project = restoredProject;
            _currentProjectPath = _projectStore.GetProjectDirectory(settings.LastProjectPath);
        }
    }

    private string ResolvePreferredProjectDirectory()
    {
        if (!string.IsNullOrWhiteSpace(_currentProjectPath))
        {
            return _currentProjectPath;
        }

        var settings = ModStudioSettingsStore.Load();
        if (!string.IsNullOrWhiteSpace(settings.LastProjectPath))
        {
            return settings.LastProjectPath;
        }

        return ModStudioPaths.LegacyProjectsPath;
    }

    private static string SanitizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "asset";
        }

        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalid, '_');
        }

        return value;
    }

    private void MarkDirty()
    {
        if (_suppressDirty)
        {
            return;
        }

        _dirty = true;
        RefreshProjectState();
    }

    private void MarkBasicDirty()
    {
        if (_suppressDirty)
        {
            return;
        }

        _basicDraftDirty = true;
        MarkDirty();
    }

    private void MarkGraphDirty()
    {
        if (_suppressDirty)
        {
            return;
        }

        _graphDraftDirty = true;
        MarkDirty();
    }

    private void OnGraphChanged(BehaviorGraphDefinition graph)
    {
        MarkGraphDirty();
        RefreshDerivedGraphText(graph, updateBasicPreview: true);
        _detailPanel?.SetGraphInfo(BuildGraphOverviewText(graph, GetEnvelope(_currentKind, _currentEntityId)));
        UpdateSelectedNodeDetails(graph, _centerEditor?.GraphEditor.CanvasView.SelectedNodeId);
    }

    private void OnSelectedNodePropertyChanged(string propertyKey, string propertyValue)
    {
        var node = GetSelectedGraphNode();
        if (node == null)
        {
            return;
        }

        node.Properties[propertyKey] = propertyValue;
        RefreshDerivedNodeText(node);
        _centerEditor?.GraphEditor.CanvasView.UpdateNodePresentation(node);
        RefreshCurrentGraphInfo();
        MarkGraphDirty();
    }

    private void OnSelectedNodeDisplayNameChanged(string displayName)
    {
        var node = GetSelectedGraphNode();
        if (node == null)
        {
            return;
        }

        node.DisplayName = displayName;
        _centerEditor?.GraphEditor.CanvasView.UpdateNodePresentation(node);
        RefreshCurrentGraphInfo();
        MarkGraphDirty();
    }

    private void OnSelectedNodeDescriptionChanged(string description)
    {
        var node = GetSelectedGraphNode();
        if (node == null)
        {
            return;
        }

        node.Description = description;
        var graph = _centerEditor?.GraphEditor.CanvasView.BoundGraph;
        if (graph != null)
        {
            SetNodeAutoDescriptionCache(node.NodeId, string.Empty, graph);
        }
        _centerEditor?.GraphEditor.CanvasView.UpdateNodePresentation(node);
        RefreshCurrentGraphInfo();
        MarkGraphDirty();
    }

    private void RefreshCurrentGraphInfo()
    {
        var graph = _centerEditor?.GraphEditor.CanvasView.BoundGraph;
        if (graph == null || _detailPanel == null)
        {
            return;
        }

        RefreshDerivedGraphText(graph, updateBasicPreview: true);
        _detailPanel.SetGraphInfo(BuildGraphOverviewText(graph, GetEnvelope(_currentKind, _currentEntityId)));
        UpdateSelectedNodeDetails(graph, _centerEditor?.GraphEditor.CanvasView.SelectedNodeId);
    }

    private BehaviorGraphNodeDefinition? GetSelectedGraphNode()
    {
        var graph = _centerEditor?.GraphEditor.CanvasView.BoundGraph;
        var selectedNodeId = _centerEditor?.GraphEditor.CanvasView.SelectedNodeId;
        if (graph == null || string.IsNullOrWhiteSpace(selectedNodeId))
        {
            return null;
        }

        return graph.Nodes.FirstOrDefault(node => string.Equals(node.NodeId, selectedNodeId, StringComparison.Ordinal));
    }

    private IReadOnlyDictionary<string, string> GetOriginalMetadata(EntityBrowserItem item)
    {
        if (item.IsProjectOnly)
        {
            return new Dictionary<string, string>(_metadataService.CreateDefaultMetadata(item.Kind, item.EntityId), StringComparer.OrdinalIgnoreCase);
        }

        return new Dictionary<string, string>(_metadataService.GetEditableMetadata(item.Kind, item.EntityId), StringComparer.OrdinalIgnoreCase);
    }

    private Dictionary<string, string> MergeMetadata(IReadOnlyDictionary<string, string> originalMetadata, EntityOverrideEnvelope? envelope)
    {
        var merged = new Dictionary<string, string>(originalMetadata, StringComparer.OrdinalIgnoreCase);
        if (envelope == null)
        {
            return merged;
        }

        foreach (var pair in envelope.Metadata)
        {
            merged[pair.Key] = pair.Value;
        }

        return merged;
    }

    private Dictionary<string, string> BuildBasicEditorMetadata(ModStudioEntityKind kind, IReadOnlyDictionary<string, string> metadata)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in GetBasicFieldKeys(kind))
        {
            if (metadata.TryGetValue(key, out var value))
            {
                result[key] = value;
            }
        }

        return result;
    }

    private static IReadOnlyList<string> GetBasicFieldKeys(ModStudioEntityKind kind)
    {
        return kind switch
        {
            ModStudioEntityKind.Character => new[] { "title", "starting_hp", "starting_gold", "max_energy", "base_orb_slot_count", "starting_deck_ids", "starting_relic_ids", "starting_potion_ids" },
            ModStudioEntityKind.Card => new[] { "title", "description", "pool_id", "type", "rarity", "target_type", "energy_cost", "energy_cost_x", "canonical_star_cost", "star_cost_x", "can_be_generated_in_combat" },
            ModStudioEntityKind.Relic => new[] { "title", "description", "rarity", "pool_id" },
            ModStudioEntityKind.Potion => new[] { "title", "description", "rarity", "usage", "target_type", "pool_id", "can_be_generated_in_combat" },
            ModStudioEntityKind.Event => new[] { "title", "initial_description", "layout_type", "is_shared" },
            ModStudioEntityKind.Enchantment => new[] { "title", "description", "show_amount", "has_extra_card_text", "extra_card_text" },
            _ => Array.Empty<string>()
        };
    }

    private AssetRef? ResolveImportedAssetBinding(ModStudioEntityKind kind, EntityOverrideEnvelope? envelope, string resolvedPath)
    {
        if (_project == null || envelope == null || string.IsNullOrWhiteSpace(resolvedPath) || !_assetBindingService.TryGetDescriptor(kind, out var descriptor))
        {
            return null;
        }

        return envelope.Assets.FirstOrDefault(asset =>
                   string.Equals(asset.LogicalRole, descriptor.LogicalRole, StringComparison.Ordinal) &&
                   string.Equals(asset.ManagedPath, resolvedPath, StringComparison.OrdinalIgnoreCase)) ??
               _project.ProjectAssets.FirstOrDefault(asset =>
                   string.Equals(asset.LogicalRole, descriptor.LogicalRole, StringComparison.Ordinal) &&
                   string.Equals(asset.ManagedPath, resolvedPath, StringComparison.OrdinalIgnoreCase));
    }

    private bool TryRemoveEmptyEnvelope(EntityBrowserItem item, EntityOverrideEnvelope envelope)
    {
        if (_project == null || item.IsProjectOnly)
        {
            return false;
        }

        if (envelope.Metadata.Count > 0 || envelope.Assets.Count > 0 || !string.IsNullOrWhiteSpace(envelope.GraphId) || envelope.BehaviorSource == BehaviorSource.Graph)
        {
            return false;
        }

        return _project.Overrides.Remove(envelope);
    }

    private BehaviorGraphDefinition CloneGraph(BehaviorGraphDefinition graph)
    {
        return new BehaviorGraphDefinition
        {
            GraphId = graph.GraphId,
            Name = graph.Name,
            Description = graph.Description,
            Version = graph.Version,
            EntityKind = graph.EntityKind,
            EntryNodeId = graph.EntryNodeId,
            Metadata = new Dictionary<string, string>(graph.Metadata, StringComparer.Ordinal),
            Nodes = graph.Nodes.Select(node => new BehaviorGraphNodeDefinition
            {
                NodeId = node.NodeId,
                NodeType = node.NodeType,
                DisplayName = node.DisplayName,
                Description = node.Description,
                Properties = new Dictionary<string, string>(node.Properties, StringComparer.Ordinal)
            }).ToList(),
            Connections = graph.Connections.Select(connection => new BehaviorGraphConnectionDefinition
            {
                FromNodeId = connection.FromNodeId,
                FromPortId = connection.FromPortId,
                ToNodeId = connection.ToNodeId,
                ToPortId = connection.ToPortId
            }).ToList()
        };
    }

    private void ApplyGeneratedDescription(EntityOverrideEnvelope envelope, BehaviorGraphDefinition graph)
    {
        var metadataKey = GetDescriptionMetadataKey(_currentKind);
        if (string.IsNullOrWhiteSpace(metadataKey))
        {
            return;
        }

        var generatedDescription = _graphDescriptionGenerator.Generate(graph).Description;
        if (string.IsNullOrWhiteSpace(generatedDescription))
        {
            return;
        }

        var cacheKey = BuildAutoDescriptionCacheKey(metadataKey);
        envelope.Metadata.TryGetValue(metadataKey, out var existingDescription);
        envelope.Metadata.TryGetValue(cacheKey, out var cachedAutoDescription);
        if (!string.IsNullOrWhiteSpace(existingDescription) &&
            !string.Equals(existingDescription, cachedAutoDescription, StringComparison.Ordinal))
        {
            return;
        }

        envelope.Metadata[metadataKey] = generatedDescription;
        envelope.Metadata[cacheKey] = generatedDescription;
    }

    private void RefreshDerivedGraphText(BehaviorGraphDefinition graph, bool updateBasicPreview)
    {
        var generatedDescription = _graphDescriptionGenerator.Generate(graph).Description;
        graph.Metadata.TryGetValue(GraphAutoDescriptionCacheKey, out var previousAutoDescription);

        foreach (var node in graph.Nodes)
        {
            RefreshDerivedNodeText(graph, node);
        }

        if (!string.IsNullOrWhiteSpace(generatedDescription))
        {
            if (string.IsNullOrWhiteSpace(graph.Description) ||
                string.Equals(graph.Description, previousAutoDescription, StringComparison.Ordinal))
            {
                graph.Description = generatedDescription;
            }

            graph.Metadata[GraphAutoDescriptionCacheKey] = generatedDescription;
            if (updateBasicPreview)
            {
                TrySyncBasicDescriptionPreview(generatedDescription, previousAutoDescription);
            }
        }

        if (_detailPanel != null)
        {
            WithSuppressedDirty(() => _detailPanel.GraphDescriptionEdit.Text = graph.Description ?? string.Empty);
        }
    }

    private void TrySyncBasicDescriptionPreview(string generatedDescription, string? previousAutoDescription)
    {
        if (_centerEditor?.BasicEditor == null || _currentItem == null)
        {
            return;
        }

        var metadataKey = GetDescriptionMetadataKey(_currentKind);
        if (string.IsNullOrWhiteSpace(metadataKey) || string.IsNullOrWhiteSpace(generatedDescription))
        {
            return;
        }

        if (!_centerEditor.BasicEditor.TryGetFieldValue(metadataKey, out var currentFieldValue))
        {
            return;
        }

        var originalMetadata = GetOriginalMetadata(_currentItem);
        originalMetadata.TryGetValue(metadataKey, out var originalValue);

        var envelope = GetEnvelope(_currentKind, _currentItem.EntityId);
        string? envelopeValue = null;
        string? cachedAutoDescription = null;
        envelope?.Metadata.TryGetValue(metadataKey, out envelopeValue);
        envelope?.Metadata.TryGetValue(BuildAutoDescriptionCacheKey(metadataKey), out cachedAutoDescription);

        var canReplace =
            string.IsNullOrWhiteSpace(currentFieldValue) ||
            string.Equals(currentFieldValue, previousAutoDescription, StringComparison.Ordinal) ||
            string.Equals(currentFieldValue, cachedAutoDescription, StringComparison.Ordinal) ||
            string.Equals(currentFieldValue, envelopeValue, StringComparison.Ordinal) ||
            string.Equals(currentFieldValue, originalValue, StringComparison.Ordinal);

        if (!canReplace)
        {
            return;
        }

        _centerEditor.BasicEditor.TrySetFieldValue(metadataKey, generatedDescription, raiseChanged: false);
    }

    private void RefreshDerivedNodeText(BehaviorGraphNodeDefinition node)
    {
        var graph = _centerEditor?.GraphEditor.CanvasView.BoundGraph;
        if (graph == null)
        {
            return;
        }

        RefreshDerivedNodeText(graph, node);
    }

    private void RefreshDerivedNodeText(BehaviorGraphDefinition graph, BehaviorGraphNodeDefinition node)
    {
        var suggestedDescription = ModStudioGraphCanvasView.GetSuggestedNodeDescription(node);
        if (string.IsNullOrWhiteSpace(suggestedDescription))
        {
            return;
        }

        var cacheKey = BuildNodeAutoDescriptionCacheKey(node.NodeId);
        graph.Metadata.TryGetValue(cacheKey, out var previousAutoDescription);
        if (string.IsNullOrWhiteSpace(node.Description) ||
            string.Equals(node.Description, previousAutoDescription, StringComparison.Ordinal))
        {
            node.Description = suggestedDescription;
        }

        SetNodeAutoDescriptionCache(node.NodeId, suggestedDescription, graph);
    }

    private string BuildGraphOverviewText(BehaviorGraphDefinition graph, EntityOverrideEnvelope? envelope)
    {
        var descriptionResult = _graphDescriptionGenerator.Generate(graph);
        return string.Join(System.Environment.NewLine, new[]
        {
            $"{Dual("Graph ID", "Graph Id")}: {graph.GraphId}",
            $"{Dual("模式", "Mode")}: {(envelope?.BehaviorSource == BehaviorSource.Graph ? Dual("Graph", "Graph") : Dual("原版", "Native"))}",
            $"{Dual("节点数", "Nodes")}: {graph.Nodes.Count}",
            $"{Dual("连线数", "Connections")}: {graph.Connections.Count}",
            $"{Dual("自动描述", "Auto Description")}: {(string.IsNullOrWhiteSpace(descriptionResult.Description) ? Dual("不可用", "Unavailable") : descriptionResult.Description)}"
        });
    }

    private string BuildGraphInfoText(BehaviorGraphDefinition graph, EntityOverrideEnvelope? envelope)
    {
        var descriptionResult = _graphDescriptionGenerator.Generate(graph);
        return string.Join(System.Environment.NewLine, new[]
        {
            $"{Dual("Graph ID", "Graph Id")}: {graph.GraphId}",
            $"{Dual("模式", "Mode")}: {(envelope?.BehaviorSource == BehaviorSource.Graph ? Dual("Graph", "Graph") : Dual("原版", "Native"))}",
            $"{Dual("节点数", "Nodes")}: {graph.Nodes.Count}",
            $"{Dual("连线数", "Connections")}: {graph.Connections.Count}",
            $"Auto Description: {(string.IsNullOrWhiteSpace(descriptionResult.Description) ? Dual("不可用", "Unavailable") : descriptionResult.Description)}"
        });
    }

    private static string GetDescriptionMetadataKey(ModStudioEntityKind kind)
    {
        return kind switch
        {
            ModStudioEntityKind.Card => "description",
            ModStudioEntityKind.Relic => "description",
            ModStudioEntityKind.Potion => "description",
            ModStudioEntityKind.Event => "initial_description",
            ModStudioEntityKind.Enchantment => "description",
            _ => string.Empty
        };
    }

    private static string BuildAutoDescriptionCacheKey(string metadataKey)
    {
        return $"{AutoDescriptionCacheKeyPrefix}{metadataKey}";
    }

    private static string BuildNodeAutoDescriptionCacheKey(string nodeId)
    {
        return $"{NodeAutoDescriptionCacheKeyPrefix}{nodeId}";
    }

    private static void SetNodeAutoDescriptionCache(string nodeId, string description, BehaviorGraphDefinition graph)
    {
        var cacheKey = BuildNodeAutoDescriptionCacheKey(nodeId);
        if (string.IsNullOrWhiteSpace(description))
        {
            graph.Metadata.Remove(cacheKey);
            return;
        }

        graph.Metadata[cacheKey] = description;
    }

    private void WithSuppressedDirty(Action action)
    {
        var previous = _suppressDirty;
        _suppressDirty = true;
        try
        {
            action();
        }
        finally
        {
            _suppressDirty = previous;
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
