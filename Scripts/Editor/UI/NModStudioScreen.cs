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
using STS2_Editor.Scripts.Editor.Runtime;

namespace STS2_Editor.Scripts.Editor.UI;

public sealed partial class NModStudioScreen : NSubmenu
{
    private const string BackButtonScenePath = "res://scenes/ui/back_button.tscn";
    private const string LocalizationKeyMeta = "modstudio_loc_key";
    private const string EntityKindMeta = "modstudio_entity_kind";
    private bool _uiBuilt;
    private Control? _defaultFocus;
    private Button? _projectTabButton;
    private Button? _packageTabButton;
    private Button? _languageZhButton;
    private Button? _languageEnButton;
    private VBoxContainer? _projectPage;
    private VBoxContainer? _packagePage;
    private VBoxContainer? _projectList;
    private VBoxContainer? _packageList;
    private HBoxContainer? _categoryRow;
    private GridContainer? _browserGrid;
    private RichTextLabel? _browserDetails;
    private RichTextLabel? _assetDetails;
    private RichTextLabel? _projectDetails;
    private RichTextLabel? _packageDetails;
    private RichTextLabel? _packageConflictDetails;
    private Label? _stateLabel;
    private CheckButton? _graphToggle;
    private LineEdit? _graphIdEdit;
    private LineEdit? _assetPathEdit;
    private LineEdit? _assetCatalogFilterEdit;
    private TextEdit? _graphJsonEdit;
    private TextEdit? _metadataEdit;
    private TextEdit? _notesEdit;
    private VBoxContainer? _graphTemplateList;
    private VBoxContainer? _assetCatalogList;
    private RichTextLabel? _graphValidationDetails;
    private RichTextLabel? _graphCatalogDetails;
    private TextureRect? _assetPreview;
    private FileDialog? _assetImportDialog;
    private FileDialog? _packageImportDialog;
    private ModStudioEntityKind _selectedKind = ModStudioEntityKind.Character;
    private EntityBrowserItem? _selectedBrowserItem;
    private PackageSessionState? _selectedPackage;
    private EditorProject? _currentProject;
    private IReadOnlyList<EditorProjectManifest> _projects = Array.Empty<EditorProjectManifest>();
    private IReadOnlyList<PackageSessionState> _packages = Array.Empty<PackageSessionState>();
    private readonly List<ModStudioEntityKind> _browserKinds = Enum
        .GetValues<ModStudioEntityKind>()
        .Where(kind => kind != ModStudioEntityKind.Monster)
        .ToList();
    private string _lastAction = "";

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
        title.AddChild(MakeLocalizedLabel("mod_studio.title", expand: true));
        _stateLabel = new Label { HorizontalAlignment = HorizontalAlignment.Right, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Text = L("state.ready") };
        title.AddChild(_stateLabel);
        title.AddChild(MakeLocalizedLabel("mod_studio.language"));
        _languageZhButton = MakeButton(ModStudioLocalization.T("language.zh"), () => SetUiLanguage(ModStudioLocalization.ChineseLanguageCode), true);
        _languageEnButton = MakeButton(ModStudioLocalization.T("language.en"), () => SetUiLanguage(ModStudioLocalization.EnglishLanguageCode), true);
        _languageZhButton.CustomMinimumSize = new Vector2(84f, 34f);
        _languageEnButton.CustomMinimumSize = new Vector2(84f, 34f);
        SetLocalizationKey(_languageZhButton, "language.zh");
        SetLocalizationKey(_languageEnButton, "language.en");
        title.AddChild(_languageZhButton);
        title.AddChild(_languageEnButton);
        v.AddChild(title);

        var tabs = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        tabs.AddThemeConstantOverride("separation", 8);
        _projectTabButton = MakeLocalizedButton("tab.project_mode", () => SetMode(true), true);
        _packageTabButton = MakeLocalizedButton("tab.package_mode", () => SetMode(false), true);
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

        v.AddChild(MakeLocalizedDetails("mod_studio.source_of_truth_note", scrollActive: false, fitContent: true));
        _packageImportDialog = new FileDialog { Name = "PackageImportDialog", Title = L("placeholder.import_package_title"), Access = FileDialog.AccessEnum.Filesystem, FileMode = FileDialog.FileModeEnum.OpenFile, UseNativeDialog = true };
        SetLocalizationKey(_packageImportDialog, "placeholder.import_package_title");
        _packageImportDialog.Connect(FileDialog.SignalName.FileSelected, Callable.From<string>(OnPackageImportSelected));
        AddChild(_packageImportDialog);
        _assetImportDialog = new FileDialog
        {
            Name = "AssetImportDialog",
            Title = L("placeholder.import_asset_title"),
            Access = FileDialog.AccessEnum.Filesystem,
            FileMode = FileDialog.FileModeEnum.OpenFile,
            UseNativeDialog = true
        };
        _assetImportDialog.Filters = new[] { "*.png,*.jpg,*.jpeg,*.webp ; Supported Images" };
        SetLocalizationKey(_assetImportDialog, "placeholder.import_asset_title");
        _assetImportDialog.Connect(FileDialog.SignalName.FileSelected, Callable.From<string>(OnAssetImportSelected));
        AddChild(_assetImportDialog);
        AddBackButton();
        ConnectGraphEditorSignals();
        ConnectAssetEditorSignals();
        RefreshLanguageButtons();
        UpdateAssetInputPlaceholders();
    }

    private VBoxContainer BuildProjectPage()
    {
        var page = new VBoxContainer { Name = "ProjectPage", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill, Visible = true };
        var split = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        split.AddThemeConstantOverride("separation", 18);
        page.AddChild(split);

        var left = new VBoxContainer { CustomMinimumSize = new Vector2(360f, 0f), SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        left.AddThemeConstantOverride("separation", 8);
        left.AddChild(MakeLocalizedLabel("label.projects"));
        left.AddChild(MakeLocalizedActionRow(("button.new", CreateNewProject), ("button.duplicate", DuplicateCurrentProject), ("button.delete", DeleteCurrentProject), ("button.export_install", ExportCurrentProject)));
        left.AddChild(MakeScrollList("ProjectList", out _projectList));
        left.AddChild(MakeLocalizedLabel("label.current_project"));
        _projectDetails = MakeLocalizedDetails("placeholder.projects_intro");
        left.AddChild(_projectDetails);
        split.AddChild(left);

        var right = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        right.AddThemeConstantOverride("separation", 8);
        right.AddChild(MakeLocalizedLabel("label.modeldb_browser"));
        right.AddChild(MakeCategoryRow(out _categoryRow));
        right.AddChild(MakeLocalizedActionRow(("button.new_entry", CreateNewEntryForSelectedKind)));
        _browserGrid = new GridContainer { Columns = 2, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        var browserScroll = new ScrollContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        browserScroll.AddChild(_browserGrid);
        right.AddChild(browserScroll);
        right.AddChild(MakeLocalizedLabel("label.override_editor"));
        _browserDetails = MakeLocalizedDetails("placeholder.browser_intro");
        right.AddChild(_browserDetails);
        right.AddChild(MakeLocalizedLabel("label.asset_binding"));
        right.AddChild(BuildAssetEditorPanel());
        right.AddChild(MakeLocalizedActionRow(
            ("button.capture_runtime", CaptureSelectedRuntimeMetadata),
            ("button.apply_event_template_scaffold", ApplyEventTemplateScaffold),
            ("button.save_override", SaveCurrentOverride),
            ("button.save_graph", SaveCurrentGraph),
            ("button.remove_override", RemoveCurrentOverride)));
        _graphToggle = new CheckButton { Text = L("button.use_graph_behavior"), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        SetLocalizationKey(_graphToggle, "button.use_graph_behavior");
        right.AddChild(_graphToggle);
        right.AddChild(MakeLocalizedLabel("label.graph_id"));
        _graphIdEdit = new LineEdit { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        right.AddChild(_graphIdEdit);
        right.AddChild(MakeLocalizedLabel("label.quick_presets"));
        _graphTemplateList = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _graphTemplateList.AddThemeConstantOverride("separation", 6);
        var templateScroll = new ScrollContainer { CustomMinimumSize = new Vector2(0f, 150f), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.Fill };
        templateScroll.AddChild(_graphTemplateList);
        right.AddChild(templateScroll);
        right.AddChild(MakeLocalizedLabel("label.graph_json"));
        _graphJsonEdit = new TextEdit { CustomMinimumSize = new Vector2(0f, 220f), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        right.AddChild(_graphJsonEdit);
        right.AddChild(MakeLocalizedLabel("label.graph_validation"));
        _graphValidationDetails = MakeLocalizedDetails("placeholder.graph_validation_intro");
        right.AddChild(_graphValidationDetails);
        right.AddChild(MakeLocalizedLabel("label.node_catalog"));
        _graphCatalogDetails = MakeLocalizedDetails("placeholder.node_catalog_intro");
        right.AddChild(_graphCatalogDetails);
        right.AddChild(MakeLocalizedLabel("label.metadata_json"));
        _metadataEdit = new TextEdit { CustomMinimumSize = new Vector2(0f, 210f), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        right.AddChild(_metadataEdit);
        right.AddChild(MakeLocalizedLabel("label.notes"));
        _notesEdit = new TextEdit { CustomMinimumSize = new Vector2(0f, 120f), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.Fill };
        right.AddChild(_notesEdit);
        split.AddChild(right);
        return page;
    }

    private VBoxContainer BuildAssetEditorPanel()
    {
        var panel = new VBoxContainer { Name = "AssetEditorPanel", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        panel.AddThemeConstantOverride("separation", 6);

        _assetDetails = MakeLocalizedDetails("placeholder.asset_binding_intro", scrollActive: false, fitContent: true);
        _assetDetails.CustomMinimumSize = new Vector2(0f, 92f);
        panel.AddChild(_assetDetails);

        var previewPanel = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _assetPreview = new TextureRect
        {
            CustomMinimumSize = new Vector2(0f, 180f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize
        };
        previewPanel.AddChild(_assetPreview);
        panel.AddChild(previewPanel);

        _assetPathEdit = new LineEdit { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        panel.AddChild(_assetPathEdit);
        panel.AddChild(MakeLocalizedActionRow(
            ("button.use_runtime_asset", BindSelectedRuntimeAsset),
            ("button.apply_asset_path", ApplyManualAssetPath),
            ("button.import_external_asset", ShowAssetImportDialog),
            ("button.clear_asset_override", ClearSelectedAssetBinding)));

        _assetCatalogFilterEdit = new LineEdit { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        panel.AddChild(_assetCatalogFilterEdit);
        panel.AddChild(MakeLocalizedLabel("label.asset_catalog"));
        panel.AddChild(MakeScrollList("AssetCatalogList", out _assetCatalogList));
        return panel;
    }

    private VBoxContainer BuildPackagePage()
    {
        var page = new VBoxContainer { Name = "PackagePage", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill, Visible = false };
        page.AddThemeConstantOverride("separation", 8);
        page.AddChild(MakeLocalizedActionRow(("button.import", ShowPackageImportDialog), ("button.refresh", RefreshPackages), ("button.enable_disable", ToggleSelectedPackage), ("button.move_up", () => MoveSelectedPackage(-1)), ("button.move_down", () => MoveSelectedPackage(1))));
        page.AddChild(MakeLocalizedLabel("label.installed_packages"));
        page.AddChild(MakeScrollList("PackageList", out _packageList));
        page.AddChild(MakeLocalizedLabel("label.package_details"));
        _packageDetails = MakeLocalizedDetails("placeholder.package_intro");
        page.AddChild(_packageDetails);
        page.AddChild(MakeLocalizedLabel("label.package_conflicts"));
        _packageConflictDetails = MakeLocalizedDetails("placeholder.package_conflicts_intro");
        page.AddChild(_packageConflictDetails);
        return page;
    }

    private Button MakeButton(string text, Action onPressed, bool toggle = false)
    {
        var b = new Button { Text = text, ToggleMode = toggle, FocusMode = Control.FocusModeEnum.All, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0f, 34f) };
        b.Connect(Button.SignalName.Pressed, Callable.From(onPressed));
        return b;
    }

    private Button MakeLocalizedButton(string key, Action onPressed, bool toggle = false)
    {
        var button = MakeButton(L(key), onPressed, toggle);
        SetLocalizationKey(button, key);
        return button;
    }

    private Label MakeLocalizedLabel(string key, bool expand = false)
    {
        var label = new Label { Text = L(key), SizeFlagsHorizontal = expand ? Control.SizeFlags.ExpandFill : Control.SizeFlags.Fill };
        SetLocalizationKey(label, key);
        return label;
    }

    private RichTextLabel MakeLocalizedDetails(string key, bool scrollActive = true, bool fitContent = false)
    {
        var details = new RichTextLabel
        {
            BbcodeEnabled = false,
            ScrollActive = scrollActive,
            FitContent = fitContent,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0f, 130f),
            Text = L(key)
        };
        SetLocalizationKey(details, key);
        return details;
    }

    private HBoxContainer MakeActionRow(params (string Text, Action Callback)[] actions)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 6);
        foreach (var action in actions) row.AddChild(MakeButton(action.Text, action.Callback));
        return row;
    }

    private HBoxContainer MakeLocalizedActionRow(params (string Key, Action Callback)[] actions)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 6);
        foreach (var action in actions)
        {
            row.AddChild(MakeLocalizedButton(action.Key, action.Callback));
        }

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
            var button = MakeButton(ModStudioLocalization.GetEntityKindDisplayName(kind), () => SelectBrowserKind(captured), true);
            button.SetMeta(EntityKindMeta, kind.ToString());
            row.AddChild(button);
        }
        return scroll;
    }

    private string L(string key) => ModStudioLocalization.T(key);

    private string LF(string key, params object?[] args) => ModStudioLocalization.F(key, args);

    private string BoolText(bool value) => ModStudioLocalization.T(value ? "bool.true" : "bool.false");

    private static void SetLocalizationKey(GodotObject node, string key)
    {
        node.SetMeta(LocalizationKeyMeta, key);
    }

    private void SetUiLanguage(string languageCode)
    {
        if (!ModStudioLocalization.SetLanguage(languageCode))
        {
            RefreshLanguageButtons();
            return;
        }

        RefreshLocalizedUi();
        SetAction(LF("status.language_switched", ModStudioLocalization.T(ModStudioLocalization.IsChinese ? "language.zh" : "language.en")));
    }

    private void RefreshLocalizedUi()
    {
        ApplyLocalizationRecursive(this);
        RefreshLanguageButtons();
        RefreshCategoryButtonTexts();
        UpdateAssetInputPlaceholders();
        RefreshSelectedBrowserDetailText();
        PopulateProjectList();
        PopulatePackageList();
        UpdateProjectDetails();
        UpdatePackageDetails();
        RefreshAssetEditor();
        RefreshGraphEditorPanels();
        UpdateStateText();
        if (_packageImportDialog != null)
        {
            _packageImportDialog.Title = L("placeholder.import_package_title");
        }

        if (_assetImportDialog != null)
        {
            _assetImportDialog.Title = L("placeholder.import_asset_title");
        }

        ModStudioUiPatches.RefreshMenuButtonTexts();
    }

    private void ApplyLocalizationRecursive(Node node)
    {
        if (node.HasMeta(LocalizationKeyMeta))
        {
            var key = node.GetMeta(LocalizationKeyMeta).AsString();
            switch (node)
            {
                case Button button:
                    button.Text = L(key);
                    break;
                case Label label:
                    label.Text = L(key);
                    break;
                case RichTextLabel richTextLabel:
                    richTextLabel.Text = L(key);
                    break;
                case FileDialog fileDialog:
                    fileDialog.Title = L(key);
                    break;
            }
        }

        foreach (var child in node.GetChildren())
        {
            ApplyLocalizationRecursive(child);
        }
    }

    private void RefreshLanguageButtons()
    {
        if (_languageZhButton != null)
        {
            _languageZhButton.ButtonPressed = ModStudioLocalization.IsChinese;
        }

        if (_languageEnButton != null)
        {
            _languageEnButton.ButtonPressed = !ModStudioLocalization.IsChinese;
        }
    }

    private void RefreshCategoryButtonTexts()
    {
        foreach (var button in _categoryRow?.GetChildren().OfType<Button>() ?? Enumerable.Empty<Button>())
        {
            if (!button.HasMeta(EntityKindMeta))
            {
                continue;
            }

            if (!Enum.TryParse<ModStudioEntityKind>(button.GetMeta(EntityKindMeta).AsString(), out var kind))
            {
                continue;
            }

            button.Text = ModStudioLocalization.GetEntityKindDisplayName(kind);
            button.ButtonPressed = kind == _selectedKind;
        }
    }

    private void RefreshSelectedBrowserDetailText()
    {
        if (_selectedBrowserItem == null)
        {
            if (_browserDetails != null)
            {
                _browserDetails.Text = L("placeholder.select_runtime_object");
            }

            RefreshAssetEditor();
            return;
        }

        var updated = ModStudioBootstrap.ModelMetadataService
            .GetItems(_selectedKind)
            .FirstOrDefault(item => item.EntityId == _selectedBrowserItem.EntityId);
        if (updated != null)
        {
            _selectedBrowserItem = updated;
        }

        if (_browserDetails != null)
        {
            _browserDetails.Text = _selectedBrowserItem.DetailText;
        }

        RefreshAssetEditor();
    }

    private void UpdateAssetInputPlaceholders()
    {
        if (_assetPathEdit != null)
        {
            _assetPathEdit.PlaceholderText = L("placeholder.asset_path");
        }

        if (_assetCatalogFilterEdit != null)
        {
            _assetCatalogFilterEdit.PlaceholderText = L("placeholder.asset_catalog_filter");
        }
    }

    private void ConnectAssetEditorSignalsLegacy()
    {
        if (_assetPathEdit != null)
        {
            _assetPathEdit.Connect(LineEdit.SignalName.TextChanged, Callable.From<string>(OnAssetPathTextChanged));
        }

        if (_assetCatalogFilterEdit != null)
        {
            _assetCatalogFilterEdit.Connect(LineEdit.SignalName.TextChanged, Callable.From<string>(OnAssetCatalogFilterChanged));
        }
    }

    private void OnAssetPathTextChanged(string _)
    {
        RefreshAssetPreview();
    }

    private void OnAssetCatalogFilterChangedLegacy(string _)
    {
        RefreshAssetEditor();
    }

    private void RefreshAssetEditorLegacy()
    {
        if (_assetDetails == null || _assetPathEdit == null || _assetCatalogList == null || _assetPreview == null)
        {
            return;
        }

        if (_currentProject == null || _selectedBrowserItem == null)
        {
            _assetDetails.Text = L("placeholder.select_runtime_object");
            _assetPathEdit.Text = string.Empty;
            _assetPreview.Texture = null;
            ClearChildren(_assetCatalogList);
            return;
        }

        if (!ModStudioBootstrap.ProjectAssetBindingService.TryGetDescriptor(_selectedBrowserItem.Kind, out var descriptor))
        {
            _assetDetails.Text = L("placeholder.asset_binding_unsupported");
            _assetPathEdit.Text = string.Empty;
            _assetPreview.Texture = null;
            ClearChildren(_assetCatalogList);
            return;
        }

        var envelope = _currentProject.Overrides.FirstOrDefault(item => item.EntityKind == _selectedBrowserItem.Kind && item.EntityId == _selectedBrowserItem.EntityId);
        var metadata = envelope?.Metadata ?? ModStudioBootstrap.ModelMetadataService.GetEditableMetadata(_selectedBrowserItem.Kind, _selectedBrowserItem.EntityId);
        var resolvedPath = ModStudioBootstrap.ProjectAssetBindingService.ResolveDisplayPath(_currentProject, envelope, _selectedBrowserItem.Kind, metadata);
        if (string.IsNullOrWhiteSpace(resolvedPath))
        {
            resolvedPath = ModStudioBootstrap.ProjectAssetBindingService.GetRuntimeAssetPath(_selectedBrowserItem.Kind, _selectedBrowserItem.EntityId) ?? string.Empty;
        }

        _assetPathEdit.Text = resolvedPath;
        _assetDetails.Text = string.Join(
            System.Environment.NewLine,
            LF("detail.asset_binding_kind", ModStudioLocalization.GetEntityKindDisplayName(_selectedBrowserItem.Kind)),
            LF("detail.asset_binding_role", ModStudioLocalization.T(descriptor.DisplayNameKey)),
            LF("detail.asset_binding_path", string.IsNullOrWhiteSpace(resolvedPath) ? L("state.none") : resolvedPath),
            LF("detail.asset_binding_mode", envelope?.BehaviorSource == BehaviorSource.Graph ? L("button.use_graph_behavior") : L("entity.native")));

        ClearChildren(_assetCatalogList);
        var filter = _assetCatalogFilterEdit?.Text?.Trim();
        var candidates = ModStudioBootstrap.ProjectAssetBindingService.GetRuntimeAssetCandidates(_selectedBrowserItem.Kind);
        foreach (var candidate in candidates.Where(candidate => string.IsNullOrWhiteSpace(filter) ||
                                                               candidate.Contains(filter, StringComparison.OrdinalIgnoreCase)))
        {
            var captured = candidate;
            var button = MakeListEntry(captured, L("button.use_runtime_asset"));
            button.Connect(Button.SignalName.Pressed, Callable.From(() =>
            {
                if (_assetPathEdit != null)
                {
                    _assetPathEdit.Text = captured;
                }
                RefreshAssetPreview();
            }));
            _assetCatalogList.AddChild(button);
        }

        if (_assetCatalogList.GetChildCount() == 0)
        {
            _assetCatalogList.AddChild(MakeListEntry(L("placeholder.no_entries_title"), L("placeholder.asset_catalog_empty")));
        }

        RefreshAssetPreview();
    }

    private void RefreshAssetPreview()
    {
        if (_assetPreview == null || _assetPathEdit == null)
        {
            return;
        }

        _assetPreview.Texture = LoadPreviewTexture(_assetPathEdit.Text);
    }

    private Texture2D? LoadPreviewTexture(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var normalized = ModStudioAssetReference.NormalizeReferencePath(path);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        try
        {
            if (normalized.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
            {
                return ResourceLoader.Load<Texture2D>(normalized, null, ResourceLoader.CacheMode.Reuse);
            }

            if (normalized.StartsWith("user://", StringComparison.OrdinalIgnoreCase))
            {
                return ResourceLoader.Load<Texture2D>(ProjectSettings.GlobalizePath(normalized), null, ResourceLoader.CacheMode.Reuse);
            }

            if (Path.IsPathRooted(normalized) && File.Exists(normalized))
            {
                var image = Image.LoadFromFile(normalized);
                return ImageTexture.CreateFromImage(image);
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private void BindSelectedRuntimeAssetLegacy()
    {
        if (_currentProject == null || _selectedBrowserItem == null || _assetPathEdit == null)
        {
            SetAction(L("status.select_object_before_override"));
            return;
        }

        try
        {
            var result = ModStudioBootstrap.ProjectAssetBindingService.BindRuntimeAsset(
                _currentProject,
                _selectedBrowserItem.Kind,
                _selectedBrowserItem.EntityId,
                _assetPathEdit.Text);
            ModStudioBootstrap.ProjectStore.Save(_currentProject);
            _projects = SafeGetProjects();
            UpdateProjectDetails();
            SyncOverrideEditor();
            RefreshAssetEditor();
            SetAction(LF("status.asset_bound_runtime", ModStudioLocalization.GetEntityKindDisplayName(_selectedBrowserItem.Kind), _selectedBrowserItem.EntityId, result.ResolvedPath));
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to bind runtime asset: {ex.Message}");
            SetAction(LF("status.asset_bind_failed", ex.Message));
        }
    }

    private void ApplyManualAssetPathLegacy()
    {
        BindSelectedRuntimeAsset();
    }

    private void ShowAssetImportDialogLegacy()
    {
        if (_assetImportDialog == null)
        {
            SetAction(L("status.asset_dialog_unavailable"));
            return;
        }

        _assetImportDialog.PopupCenteredRatio(0.75f);
    }

    private void OnAssetImportSelectedLegacy(string sourcePath)
    {
        if (_currentProject == null || _selectedBrowserItem == null)
        {
            SetAction(L("status.select_object_before_override"));
            return;
        }

        try
        {
            var result = ModStudioBootstrap.ProjectAssetBindingService.ImportExternalAsset(
                _currentProject,
                _selectedBrowserItem.Kind,
                _selectedBrowserItem.EntityId,
                sourcePath);
            ModStudioBootstrap.ProjectStore.Save(_currentProject);
            _projects = SafeGetProjects();
            UpdateProjectDetails();
            SyncOverrideEditor();
            RefreshAssetEditor();
            SetAction(LF("status.asset_imported", ModStudioLocalization.GetEntityKindDisplayName(_selectedBrowserItem.Kind), _selectedBrowserItem.EntityId, result.ResolvedPath));
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to import asset: {ex.Message}");
            SetAction(LF("status.asset_import_failed", ex.Message));
        }
    }

    private void ClearSelectedAssetBindingLegacy()
    {
        if (_currentProject == null || _selectedBrowserItem == null)
        {
            SetAction(L("status.select_object_before_override"));
            return;
        }

        try
        {
            ModStudioBootstrap.ProjectAssetBindingService.ClearAssetBinding(
                _currentProject,
                _selectedBrowserItem.Kind,
                _selectedBrowserItem.EntityId);
            ModStudioBootstrap.ProjectStore.Save(_currentProject);
            _projects = SafeGetProjects();
            UpdateProjectDetails();
            SyncOverrideEditor();
            RefreshAssetEditor();
            SetAction(LF("status.asset_cleared", ModStudioLocalization.GetEntityKindDisplayName(_selectedBrowserItem.Kind), _selectedBrowserItem.EntityId));
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to clear asset binding: {ex.Message}");
            SetAction(LF("status.asset_clear_failed", ex.Message));
        }
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
        catch (Exception ex) { Log.Warn($"Failed to load Mod Studio projects: {ex.Message}"); SetAction(LF("status.project_load_failed", ex.Message)); return Array.Empty<EditorProjectManifest>(); }
    }

    private IReadOnlyList<PackageSessionState> SafeGetPackages()
    {
        try { return ModStudioBootstrap.RuntimeRegistry.SessionStates.OrderBy(x => x.LoadOrder).ThenBy(x => x.PackageKey, StringComparer.Ordinal).ToList(); }
        catch (Exception ex) { Log.Warn($"Failed to load Mod Studio package sessions: {ex.Message}"); SetAction(LF("status.package_load_failed", ex.Message)); return Array.Empty<PackageSessionState>(); }
    }

    private void PopulateProjectList()
    {
        ClearChildren(_projectList);
        if (_projectList == null) return;
        if (_projects.Count == 0) { _projectList.AddChild(MakeListEntry(L("placeholder.no_projects"), L("placeholder.no_projects_hint"))); return; }
        foreach (var project in _projects)
        {
            var title = _currentProject?.Manifest.ProjectId == project.ProjectId ? $"[{L("list.active")}] {project.Name}" : project.Name;
            var button = MakeListEntry(title, $"{project.Author} | {project.TargetGameVersion}");
            button.Connect(Button.SignalName.Pressed, Callable.From(() => OpenProject(project.ProjectId)));
            _projectList.AddChild(button);
        }
    }

    private void PopulatePackageList()
    {
        ClearChildren(_packageList);
        if (_packageList == null) return;
        if (_packages.Count == 0) { _packageList.AddChild(MakeListEntry(L("placeholder.no_packages"), L("placeholder.no_packages_hint"))); return; }
        foreach (var package in _packages)
        {
            var subtitle = $"#{package.LoadOrder} | {L("list.enabled")} {BoolText(package.Enabled)} | {L("list.session_enabled")} {BoolText(package.SessionEnabled)}";
            var button = MakeListEntry(package.DisplayName, subtitle);
            button.Connect(Button.SignalName.Pressed, Callable.From(() => { _selectedPackage = package; UpdatePackageDetails(); UpdateStateText(); }));
            _packageList.AddChild(button);
        }
    }

    private Button MakeListEntry(string title, string subtitle) => new() { Text = $"{title}\n{subtitle}", CustomMinimumSize = new Vector2(0f, 56f), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };

    private void SelectBrowserKind(ModStudioEntityKind kind, bool preserveSelectedItem = true)
    {
        _selectedKind = kind;
        var items = ModStudioBootstrap.ModelMetadataService.GetItems(kind, _currentProject);
        if (!preserveSelectedItem || _selectedBrowserItem?.Kind != kind) _selectedBrowserItem = null;
        foreach (var button in _categoryRow?.GetChildren().OfType<Button>() ?? Enumerable.Empty<Button>())
        {
            button.ButtonPressed = button.HasMeta(EntityKindMeta) &&
                string.Equals(button.GetMeta(EntityKindMeta).AsString(), kind.ToString(), StringComparison.Ordinal);
        }
        ClearChildren(_browserGrid);
        if (_browserGrid == null) return;
        if (items.Count == 0)
        {
            var kindName = ModStudioLocalization.GetEntityKindDisplayName(kind);
            _browserGrid.AddChild(MakeListEntry(L("placeholder.no_entries_title"), LF("placeholder.no_entries_kind", kindName)));
            if (_browserDetails != null) _browserDetails.Text = LF("placeholder.no_entries_kind", kindName);
            SyncOverrideEditor();
            RefreshGraphEditorPanels();
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
        RefreshGraphEditorPanels();
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
        if (!ModStudioBootstrap.ProjectStore.TryLoad(projectId, out var project) || project == null) { SetAction(LF("status.project_open_failed", projectId)); return; }
        _currentProject = project;
        PopulateProjectList();
        UpdateProjectDetails();
        SyncOverrideEditor();
        SetAction(LF("status.project_opened", project.Manifest.Name));
    }

    private void CreateNewProject()
    {
        try
        {
            _currentProject = ModStudioBootstrap.ProjectStore.CreateProject(LF("project.default_name", DateTimeOffset.Now.ToString("yyyy-MM-dd HH-mm-ss")));
            _projects = SafeGetProjects();
            PopulateProjectList();
            UpdateProjectDetails();
            SyncOverrideEditor();
            SetAction(LF("status.project_created", _currentProject.Manifest.Name));
        }
        catch (Exception ex) { Log.Warn($"Failed to create project: {ex.Message}"); SetAction(LF("status.create_project_failed", ex.Message)); }
    }

    private void DuplicateCurrentProject()
    {
        if (_currentProject == null) { SetAction(L("status.select_project_before_duplicate")); return; }
        try
        {
            _currentProject = ModStudioBootstrap.ProjectStore.DuplicateProject(_currentProject.Manifest.ProjectId);
            _projects = SafeGetProjects();
            PopulateProjectList();
            UpdateProjectDetails();
            SetAction(LF("status.project_duplicated", _currentProject.Manifest.Name));
        }
        catch (Exception ex) { Log.Warn($"Failed to duplicate project: {ex.Message}"); SetAction(LF("status.duplicate_project_failed", ex.Message)); }
    }

    private void DeleteCurrentProject()
    {
        if (_currentProject == null) { SetAction(L("status.select_project_before_delete")); return; }
        var deletedName = _currentProject.Manifest.Name;
        if (!ModStudioBootstrap.ProjectStore.DeleteProject(_currentProject.Manifest.ProjectId)) { SetAction(LF("status.project_delete_failed", deletedName)); return; }
        _currentProject = null;
        _projects = SafeGetProjects();
        if (_projects.Count > 0) ModStudioBootstrap.ProjectStore.TryLoad(_projects[0].ProjectId, out _currentProject);
        PopulateProjectList();
        UpdateProjectDetails();
        SyncOverrideEditor();
        SetAction(LF("status.project_deleted", deletedName));
    }

    private void ExportCurrentProject()
    {
        if (_currentProject == null) { SetAction(L("status.select_project_before_export")); return; }
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
            SetAction(LF("status.exported_and_installed", _currentProject.Manifest.Name, path));
        }
        catch (Exception ex) { Log.Warn($"Failed to export project: {ex.Message}"); SetAction(LF("status.export_failed", ex.Message)); }
    }

    private void CreateNewEntryForSelectedKind()
    {
        if (_currentProject == null)
        {
            SetAction(L("status.select_project_before_new_entry"));
            return;
        }

        if (_selectedKind is ModStudioEntityKind.Character or ModStudioEntityKind.Monster)
        {
            SetAction(LF("status.new_entry_unsupported", ModStudioLocalization.GetEntityKindDisplayName(_selectedKind)));
            return;
        }

        try
        {
            var entityId = ModStudioBootstrap.ModelMetadataService.GenerateProjectEntityId(_currentProject, _selectedKind);
            var metadata = new Dictionary<string, string>(ModStudioBootstrap.ModelMetadataService.CreateDefaultMetadata(_selectedKind, entityId), StringComparer.Ordinal);
            var behaviorSource = _selectedKind == ModStudioEntityKind.Event ? BehaviorSource.Native : BehaviorSource.Graph;
            var graphId = behaviorSource == BehaviorSource.Graph ? BuildDefaultGraphId(_selectedKind, entityId) : null;

            var envelope = new EntityOverrideEnvelope
            {
                EntityKind = _selectedKind,
                EntityId = entityId,
                BehaviorSource = behaviorSource,
                GraphId = graphId,
                Metadata = metadata,
                Notes = L("default.entry_notes")
            };

            _currentProject.Overrides.Add(envelope);
            if (behaviorSource == BehaviorSource.Graph && graphId != null)
            {
                EnsureGraphExists(_currentProject, graphId, _selectedKind, entityId);
            }

            ModStudioBootstrap.ProjectStore.Save(_currentProject);
            _projects = SafeGetProjects();
            PopulateProjectList();
            UpdateProjectDetails();
            _selectedBrowserItem = new EntityBrowserItem { Kind = _selectedKind, EntityId = entityId, IsProjectOnly = true };
            SelectBrowserKind(_selectedKind, true);
            SetAction(LF("status.created_entry", ModStudioLocalization.GetEntityKindDisplayName(_selectedKind), entityId));
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to create new entry: {ex.Message}");
            SetAction(LF("status.create_entry_failed", ex.Message));
        }
    }

    private void CaptureSelectedRuntimeMetadata()
    {
        if (_selectedBrowserItem == null || _metadataEdit == null) { SetAction(L("status.capture_runtime_missing_selection")); return; }
        if (_selectedBrowserItem.IsProjectOnly || !ModStudioBootstrap.ModelMetadataService.HasRuntimeEntity(_selectedBrowserItem.Kind, _selectedBrowserItem.EntityId))
        {
            SetAction(L("status.capture_runtime_unavailable_project_entry"));
            return;
        }

        _metadataEdit.Text = JsonSerializer.Serialize(ModStudioBootstrap.ModelMetadataService.GetEditableMetadata(_selectedBrowserItem.Kind, _selectedBrowserItem.EntityId), ModStudioJson.Options);
        SetAction(LF("status.capture_runtime_done", ModStudioLocalization.GetEntityKindDisplayName(_selectedBrowserItem.Kind), _selectedBrowserItem.EntityId));
    }

    private void SaveCurrentOverride()
    {
        if (_currentProject == null) { SetAction(L("status.select_project_before_override")); return; }
        if (_selectedBrowserItem == null) { SetAction(L("status.select_object_before_override")); return; }
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
            if (useGraph && envelope.GraphId != null) EnsureGraphExists(_currentProject, envelope.GraphId, _selectedBrowserItem.Kind, _selectedBrowserItem.EntityId);
            PersistGraphJsonIfPresent(updateActionOnSuccess: false);
            ModStudioBootstrap.ProjectStore.Save(_currentProject);
            _projects = SafeGetProjects();
            PopulateProjectList();
            UpdateProjectDetails();
            SyncOverrideEditor();
            SetAction(LF("status.saved_override", ModStudioLocalization.GetEntityKindDisplayName(_selectedBrowserItem.Kind), _selectedBrowserItem.EntityId));
        }
        catch (Exception ex) { Log.Warn($"Failed to save override: {ex.Message}"); SetAction(LF("status.save_override_failed", ex.Message)); }
    }

    private void SaveCurrentGraph()
    {
        if (_currentProject == null)
        {
            SetAction(L("status.select_project_before_save_graph"));
            return;
        }

        if (_selectedBrowserItem == null)
        {
            SetAction(L("status.select_object_before_save_graph"));
            return;
        }

        try
        {
            PersistGraphJsonIfPresent(updateActionOnSuccess: true);
            ModStudioBootstrap.ProjectStore.Save(_currentProject);
            UpdateProjectDetails();
            SyncOverrideEditor();
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to save graph: {ex.Message}");
            SetAction(LF("status.save_graph_failed", ex.Message));
        }
    }

    private void ApplyEventTemplateScaffold()
    {
        if (_currentProject == null)
        {
            SetAction(L("status.select_project_before_event_template"));
            return;
        }

        if (_selectedBrowserItem == null || _selectedBrowserItem.Kind != ModStudioEntityKind.Event)
        {
            SetAction(L("status.event_template_only"));
            return;
        }

        try
        {
            var metadata = GetEffectiveMetadataForSelectedItem();
            metadata["event_start_page_id"] = "INITIAL";
            metadata["event_page.INITIAL.description"] = metadata.TryGetValue("initial_description", out var initialDescription) && !string.IsNullOrWhiteSpace(initialDescription)
                ? initialDescription
                : L("event_template.default_initial_description");
            metadata["event_page.INITIAL.option_order"] = "CONTINUE";
            metadata["event_option.INITIAL.CONTINUE.title"] = L("event_template.default_option_continue_title");
            metadata["event_option.INITIAL.CONTINUE.description"] = L("event_template.default_option_continue_description");
            metadata["event_option.INITIAL.CONTINUE.next_page_id"] = "DONE";
            metadata["event_page.DONE.description"] = L("event_template.default_done_description");

            if (_metadataEdit != null)
            {
                _metadataEdit.Text = JsonSerializer.Serialize(metadata, ModStudioJson.Options);
            }

            SetAction(LF("status.event_template_scaffold_applied", _selectedBrowserItem.EntityId));
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to apply event template scaffold: {ex.Message}");
            SetAction(LF("status.event_template_scaffold_failed", ex.Message));
        }
    }

    private void RemoveCurrentOverride()
    {
        if (_currentProject == null) { SetAction(L("status.select_project_before_remove_override")); return; }
        if (_selectedBrowserItem == null) { SetAction(L("status.select_object_before_remove_override")); return; }
        var removed = _currentProject.Overrides.RemoveAll(x => x.EntityKind == _selectedBrowserItem.Kind && x.EntityId == _selectedBrowserItem.EntityId);
        if (removed == 0) { SetAction(LF("status.no_override_exists", ModStudioLocalization.GetEntityKindDisplayName(_selectedBrowserItem.Kind), _selectedBrowserItem.EntityId)); return; }
        ModStudioBootstrap.ProjectStore.Save(_currentProject);
        UpdateProjectDetails();
        SyncOverrideEditor();
        SetAction(LF("status.removed_override", ModStudioLocalization.GetEntityKindDisplayName(_selectedBrowserItem.Kind), _selectedBrowserItem.EntityId));
    }

    private void ShowPackageImportDialog()
    {
        if (_packageImportDialog == null) { SetAction(L("status.package_dialog_unavailable")); return; }
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
            SetAction(LF("status.package_imported", install.PackageState.DisplayName));
        }
        catch (Exception ex) { Log.Warn($"Failed to import package: {ex.Message}"); SetAction(LF("status.package_import_failed", ex.Message)); }
    }

    private void RefreshPackages()
    {
        ModStudioBootstrap.RuntimeRegistry.Refresh();
        _packages = SafeGetPackages();
        _selectedPackage = _packages.FirstOrDefault(x => x.PackageKey == _selectedPackage?.PackageKey) ?? _packages.FirstOrDefault();
        PopulatePackageList();
        UpdatePackageDetails();
        SetAction(L("status.package_catalog_refreshed"));
    }

    private void ToggleSelectedPackage()
    {
        if (_selectedPackage == null) { SetAction(L("status.select_package_before_toggle")); return; }
        ModStudioBootstrap.RuntimeRegistry.EnablePackage(_selectedPackage.PackageKey, !_selectedPackage.Enabled);
        _packages = SafeGetPackages();
        _selectedPackage = _packages.FirstOrDefault(x => x.PackageKey == _selectedPackage.PackageKey);
        PopulatePackageList();
        UpdatePackageDetails();
        SetAction(LF("status.toggled_package", _selectedPackage?.DisplayName ?? L("misc.unknown")));
    }

    private void MoveSelectedPackage(int direction)
    {
        if (_selectedPackage == null) { SetAction(L("status.select_package_before_reorder")); return; }
        if (!ModStudioBootstrap.RuntimeRegistry.MovePackage(_selectedPackage.PackageKey, direction)) { SetAction(L("status.package_order_unchanged")); return; }
        _packages = SafeGetPackages();
        _selectedPackage = _packages.FirstOrDefault(x => x.PackageKey == _selectedPackage.PackageKey);
        PopulatePackageList();
        UpdatePackageDetails();
        SetAction(LF("status.moved_package", _selectedPackage?.DisplayName ?? L("misc.unknown")));
    }

    private void UpdateProjectDetails()
    {
        if (_projectDetails == null) return;
        if (_currentProject == null) { _projectDetails.Text = L("placeholder.no_project_selected"); return; }
        _projectDetails.Text = string.Join(System.Environment.NewLine,
            LF("detail.project_id", _currentProject.Manifest.ProjectId),
            LF("detail.name", _currentProject.Manifest.Name),
            LF("detail.author", _currentProject.Manifest.Author),
            LF("detail.description", _currentProject.Manifest.Description),
            LF("detail.target_game", _currentProject.Manifest.TargetGameVersion),
            LF("detail.editor_version", _currentProject.Manifest.EditorVersion),
            LF("detail.overrides", _currentProject.Overrides.Count),
            LF("detail.graphs", _currentProject.Graphs.Count),
            LF("detail.assets", _currentProject.ProjectAssets.Count),
            LF("detail.updated", _currentProject.Manifest.UpdatedAtUtc));
    }

    private void UpdatePackageDetails()
    {
        if (_packageDetails == null) return;
        if (_selectedPackage == null)
        {
            _packageDetails.Text = L("placeholder.no_package_selected");
            if (_packageConflictDetails != null)
            {
                _packageConflictDetails.Text = L("placeholder.no_package_selected");
            }
            return;
        }
        var runtimePackage = ModStudioBootstrap.RuntimeRegistry.InstalledPackages.FirstOrDefault(x => x.PackageKey == _selectedPackage.PackageKey);
        _packageDetails.Text = string.Join(System.Environment.NewLine,
            LF("detail.package_key", _selectedPackage.PackageKey),
            LF("detail.package_id", _selectedPackage.PackageId),
            LF("detail.display_name", _selectedPackage.DisplayName),
            LF("detail.version", _selectedPackage.Version),
            LF("detail.checksum", _selectedPackage.Checksum),
            LF("detail.load_order", _selectedPackage.LoadOrder),
            LF("detail.enabled", BoolText(_selectedPackage.Enabled)),
            LF("detail.session_enabled", BoolText(_selectedPackage.SessionEnabled)),
            LF("detail.disabled_reason", _selectedPackage.DisabledReason),
            LF("detail.package_file", _selectedPackage.PackageFilePath),
            LF("detail.override_count", runtimePackage?.Project.Overrides.Count ?? 0),
            LF("detail.graph_count", runtimePackage?.Project.Graphs.Count ?? 0),
            LF("detail.asset_count", runtimePackage?.Project.ProjectAssets.Count ?? 0));
        if (_packageConflictDetails != null)
        {
            _packageConflictDetails.Text = BuildPackageConflictDetails(_selectedPackage.PackageKey);
        }
    }

    private string BuildPackageConflictDetails(string packageKey)
    {
        var conflicts = ModStudioBootstrap.RuntimeRegistry.LastResolution.Conflicts
            .Where(conflict => conflict.Participants.Any(participant => string.Equals(participant.PackageKey, packageKey, StringComparison.Ordinal)))
            .OrderBy(conflict => conflict.EntityKind)
            .ThenBy(conflict => conflict.EntityId, StringComparer.Ordinal)
            .ToList();

        if (conflicts.Count == 0)
        {
            return L("placeholder.no_package_conflicts");
        }

        var lines = new List<string>
        {
            LF("detail.package_conflict_count", conflicts.Count)
        };

        foreach (var conflict in conflicts.Take(20))
        {
            var winner = conflict.Participants
                .FirstOrDefault(participant => string.Equals(participant.PackageKey, conflict.WinningPackageKey, StringComparison.Ordinal));
            var participantText = string.Join(" -> ", conflict.Participants
                .OrderBy(participant => participant.LoadOrder)
                .ThenBy(participant => participant.PackageKey, StringComparer.Ordinal)
                .Select(participant => $"{participant.DisplayName}#{participant.LoadOrder}"));
            lines.Add(LF(
                "detail.package_conflict_entry",
                ModStudioLocalization.GetEntityKindDisplayName(conflict.EntityKind),
                conflict.EntityId,
                winner?.DisplayName ?? conflict.WinningPackageKey,
                participantText));
        }

        if (conflicts.Count > 20)
        {
            lines.Add(LF("placeholder.package_conflicts_more", conflicts.Count - 20));
        }

        return string.Join(System.Environment.NewLine, lines);
    }

    private void SyncOverrideEditor()
    {
        if (_browserDetails == null || _metadataEdit == null || _notesEdit == null || _graphToggle == null || _graphIdEdit == null || _graphJsonEdit == null) return;
        if (_selectedBrowserItem == null)
        {
            _browserDetails.Text = L("placeholder.select_runtime_object");
            _graphToggle.ButtonPressed = false;
            _graphIdEdit.Text = string.Empty;
            _graphJsonEdit.Text = string.Empty;
            _metadataEdit.Text = string.Empty;
            _notesEdit.Text = string.Empty;
            RefreshAssetEditor();
            RefreshGraphEditorPanels();
            return;
        }

        _browserDetails.Text = _selectedBrowserItem.DetailText;
        var envelope = _currentProject?.Overrides.FirstOrDefault(x => x.EntityKind == _selectedBrowserItem.Kind && x.EntityId == _selectedBrowserItem.EntityId);
        if (envelope == null)
        {
            _graphToggle.ButtonPressed = false;
            _graphIdEdit.Text = string.Empty;
            _graphJsonEdit.Text = string.Empty;
            _metadataEdit.Text = JsonSerializer.Serialize(ModStudioBootstrap.ModelMetadataService.GetEditableMetadata(_selectedBrowserItem.Kind, _selectedBrowserItem.EntityId), ModStudioJson.Options);
            _notesEdit.Text = string.Empty;
            RefreshAssetEditor();
            return;
        }

        _graphToggle.ButtonPressed = envelope.BehaviorSource == BehaviorSource.Graph;
        _graphIdEdit.Text = envelope.GraphId ?? string.Empty;
        _graphJsonEdit.Text = ResolveGraphJsonText(_currentProject, envelope, _selectedBrowserItem.Kind);
        _metadataEdit.Text = JsonSerializer.Serialize(envelope.Metadata, ModStudioJson.Options);
        _notesEdit.Text = envelope.Notes ?? string.Empty;
        RefreshAssetEditor();
        RefreshGraphEditorPanels();
    }

    private void UpdateStateText()
    {
        if (_stateLabel == null) return;
        var mode = _projectPage?.Visible == true ? L("tab.project_mode") : L("tab.package_mode");
        var selected = _selectedBrowserItem != null ? $"{ModStudioLocalization.GetEntityKindDisplayName(_selectedBrowserItem.Kind)}:{_selectedBrowserItem.EntityId}" : _selectedPackage?.PackageKey ?? L("state.none");
        var project = _currentProject?.Manifest.Name ?? L("state.none");
        _stateLabel.Text = LF("state.mode", mode, project, _packages.Count, selected, _lastAction);
    }

    private void SetAction(string message)
    {
        _lastAction = message;
        UpdateStateText();
    }

    private void ConnectAssetEditorSignals()
    {
        if (_assetCatalogFilterEdit != null)
        {
            _assetCatalogFilterEdit.Connect(LineEdit.SignalName.TextChanged, Callable.From<string>(OnAssetCatalogFilterChanged));
        }

        if (_metadataEdit != null)
        {
            _metadataEdit.Connect(TextEdit.SignalName.TextChanged, Callable.From<string>(OnMetadataJsonTextChanged));
        }
    }

    private void OnAssetCatalogFilterChanged(string _)
    {
        RefreshAssetCatalogList();
    }

    private void OnMetadataJsonTextChanged(string _)
    {
        RefreshAssetEditor();
    }

    private void ConnectGraphEditorSignals()
    {
        if (_graphJsonEdit != null)
        {
            _graphJsonEdit.Connect(TextEdit.SignalName.TextChanged, Callable.From<string>(OnGraphJsonTextChanged));
        }

        if (_graphIdEdit != null)
        {
            _graphIdEdit.Connect(LineEdit.SignalName.TextChanged, Callable.From<string>(OnGraphIdTextChanged));
        }
    }

    private void RefreshAssetEditor()
    {
        if (_assetDetails == null || _assetPathEdit == null || _assetPreview == null)
        {
            return;
        }

        if (_selectedBrowserItem == null)
        {
            _assetDetails.Text = L("placeholder.asset_binding_select");
            _assetPathEdit.Text = string.Empty;
            _assetPreview.Texture = null;
            RefreshAssetCatalogList();
            return;
        }

        if (!ModStudioBootstrap.ProjectAssetBindingService.TryGetDescriptor(_selectedBrowserItem.Kind, out var descriptor))
        {
            _assetDetails.Text = LF("placeholder.asset_binding_unsupported", ModStudioLocalization.GetEntityKindDisplayName(_selectedBrowserItem.Kind));
            _assetPathEdit.Text = string.Empty;
            _assetPreview.Texture = null;
            RefreshAssetCatalogList();
            return;
        }

        var metadata = GetEffectiveMetadataForSelectedItem();
        var currentValue = metadata.TryGetValue(descriptor.MetadataKey, out var rawValue)
            ? rawValue
            : string.Empty;
        var runtimePath = ModStudioBootstrap.ProjectAssetBindingService.GetRuntimeAssetPath(_selectedBrowserItem.Kind, _selectedBrowserItem.EntityId) ?? string.Empty;
        var envelope = _currentProject?.Overrides.FirstOrDefault(item => item.EntityKind == _selectedBrowserItem.Kind && item.EntityId == _selectedBrowserItem.EntityId);
        var resolvedPath = ModStudioBootstrap.ProjectAssetBindingService.ResolveDisplayPath(_currentProject, envelope, _selectedBrowserItem.Kind, metadata);
        var boundAssetCount = envelope?.Assets.Count(asset => string.Equals(asset.LogicalRole, descriptor.LogicalRole, StringComparison.Ordinal)) ?? 0;

        _assetDetails.Text = string.Join(
            System.Environment.NewLine,
            LF("asset.detail.role", L(descriptor.DisplayNameKey)),
            LF("asset.detail.metadata_key", descriptor.MetadataKey),
            LF("asset.detail.runtime_default", runtimePath),
            LF("asset.detail.current_binding", string.IsNullOrWhiteSpace(currentValue) ? L("misc.none") : currentValue),
            LF("asset.detail.resolved_path", string.IsNullOrWhiteSpace(resolvedPath) ? L("misc.none") : resolvedPath),
            LF("asset.detail.tracked_assets", boundAssetCount));
        _assetPathEdit.Text = currentValue;
        _assetPreview.Texture = LoadPreviewTexture(resolvedPath ?? currentValue);
        RefreshAssetCatalogList();
    }

    private void RefreshAssetCatalogList()
    {
        ClearChildren(_assetCatalogList);
        if (_assetCatalogList == null)
        {
            return;
        }

        if (_selectedBrowserItem == null)
        {
            _assetCatalogList.AddChild(MakeListEntry(L("placeholder.no_selection"), L("placeholder.asset_binding_select")));
            return;
        }

        if (!ModStudioBootstrap.ProjectAssetBindingService.TryGetDescriptor(_selectedBrowserItem.Kind, out var descriptor))
        {
            _assetCatalogList.AddChild(MakeListEntry(L("placeholder.no_entries_title"), LF("placeholder.asset_binding_unsupported", ModStudioLocalization.GetEntityKindDisplayName(_selectedBrowserItem.Kind))));
            return;
        }

        if (!descriptor.SupportsRuntimeCatalog)
        {
            _assetCatalogList.AddChild(MakeListEntry(L("placeholder.asset_catalog_unavailable"), L("placeholder.asset_catalog_external_only")));
            return;
        }

        var filter = (_assetCatalogFilterEdit?.Text ?? string.Empty).Trim();
        var candidates = ModStudioBootstrap.ProjectAssetBindingService
            .GetRuntimeAssetCandidates(_selectedBrowserItem.Kind)
            .Where(path => string.IsNullOrWhiteSpace(filter) || path.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (candidates.Count == 0)
        {
            _assetCatalogList.AddChild(MakeListEntry(L("placeholder.no_entries_title"), L("placeholder.asset_catalog_no_match")));
            return;
        }

        const int maxEntries = 200;
        foreach (var path in candidates.Take(maxEntries))
        {
            var captured = path;
            var button = MakeListEntry(path, L("asset.catalog_click_to_apply"));
            button.CustomMinimumSize = new Vector2(0f, 64f);
            button.Connect(Button.SignalName.Pressed, Callable.From(() => BindCatalogAssetPath(captured)));
            _assetCatalogList.AddChild(button);
        }

        if (candidates.Count > maxEntries)
        {
            _assetCatalogList.AddChild(MakeListEntry(L("placeholder.asset_catalog_truncated_title"), LF("placeholder.asset_catalog_truncated", candidates.Count - maxEntries)));
        }
    }

    private void BindSelectedRuntimeAsset()
    {
        if (!TryGetSelectedAssetContext(out var descriptor, out _))
        {
            return;
        }

        var runtimePath = ModStudioBootstrap.ProjectAssetBindingService.GetRuntimeAssetPath(_selectedBrowserItem!.Kind, _selectedBrowserItem.EntityId);
        if (string.IsNullOrWhiteSpace(runtimePath))
        {
            SetAction(L("status.asset_runtime_unavailable"));
            return;
        }

        BindAssetPath(runtimePath, descriptor, treatRootedAsExternalImport: false, successMessage: LF("status.bound_runtime_asset", _selectedBrowserItem.EntityId));
    }

    private void ApplyManualAssetPath()
    {
        if (!TryGetSelectedAssetContext(out var descriptor, out _))
        {
            return;
        }

        var rawPath = (_assetPathEdit?.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            SetAction(L("status.asset_path_required"));
            return;
        }

        BindAssetPath(rawPath, descriptor, treatRootedAsExternalImport: true, successMessage: LF("status.bound_manual_asset", _selectedBrowserItem!.EntityId));
    }

    private void ShowAssetImportDialog()
    {
        if (_assetImportDialog == null)
        {
            SetAction(L("status.asset_dialog_unavailable"));
            return;
        }

        if (!TryGetSelectedAssetContext(out _, out _))
        {
            return;
        }

        _assetImportDialog.PopupCenteredRatio(0.75f);
    }

    private void OnAssetImportSelected(string sourcePath)
    {
        if (!TryGetSelectedAssetContext(out _, out _))
        {
            return;
        }

        try
        {
            var result = ModStudioBootstrap.ProjectAssetBindingService.ImportExternalAsset(_currentProject!, _selectedBrowserItem!.Kind, _selectedBrowserItem.EntityId, sourcePath);
            PersistProjectAfterAssetChange(result);
            SetAction(LF("status.imported_external_asset", Path.GetFileName(sourcePath), _selectedBrowserItem.EntityId));
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to import external asset: {ex.Message}");
            SetAction(LF("status.import_external_asset_failed", ex.Message));
        }
    }

    private void ClearSelectedAssetBinding()
    {
        if (!TryGetSelectedAssetContext(out _, out _))
        {
            return;
        }

        try
        {
            var result = ModStudioBootstrap.ProjectAssetBindingService.ClearAssetBinding(_currentProject!, _selectedBrowserItem!.Kind, _selectedBrowserItem.EntityId);
            PersistProjectAfterAssetChange(result);
            SetAction(LF("status.cleared_asset_binding", _selectedBrowserItem.EntityId));
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to clear asset binding: {ex.Message}");
            SetAction(LF("status.clear_asset_binding_failed", ex.Message));
        }
    }

    private void BindCatalogAssetPath(string runtimePath)
    {
        if (_assetPathEdit != null)
        {
            _assetPathEdit.Text = runtimePath;
        }

        ApplyManualAssetPath();
    }

    private void BindAssetPath(string rawPath, AssetBindingDescriptor descriptor, bool treatRootedAsExternalImport, string successMessage)
    {
        try
        {
            AssetBindingResult result;
            if (treatRootedAsExternalImport && Path.IsPathRooted(rawPath) && File.Exists(rawPath))
            {
                result = ModStudioBootstrap.ProjectAssetBindingService.ImportExternalAsset(_currentProject!, _selectedBrowserItem!.Kind, _selectedBrowserItem.EntityId, rawPath);
            }
            else
            {
                result = ModStudioBootstrap.ProjectAssetBindingService.BindRuntimeAsset(_currentProject!, _selectedBrowserItem!.Kind, _selectedBrowserItem.EntityId, rawPath);
            }

            PersistProjectAfterAssetChange(result);
            SetAction(successMessage);
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to bind asset path '{rawPath}': {ex.Message}");
            SetAction(LF("status.bind_asset_failed", ex.Message));
        }
    }

    private bool TryGetSelectedAssetContext(out AssetBindingDescriptor descriptor, out EntityOverrideEnvelope? envelope)
    {
        descriptor = new AssetBindingDescriptor();
        envelope = null;

        if (_currentProject == null)
        {
            SetAction(L("status.select_project_before_asset"));
            return false;
        }

        if (_selectedBrowserItem == null)
        {
            SetAction(L("status.select_object_before_asset"));
            return false;
        }

        if (!ModStudioBootstrap.ProjectAssetBindingService.TryGetDescriptor(_selectedBrowserItem.Kind, out descriptor))
        {
            SetAction(LF("status.asset_binding_unsupported", ModStudioLocalization.GetEntityKindDisplayName(_selectedBrowserItem.Kind)));
            return false;
        }

        envelope = _currentProject.Overrides.FirstOrDefault(item => item.EntityKind == _selectedBrowserItem.Kind && item.EntityId == _selectedBrowserItem.EntityId);
        return true;
    }

    private Dictionary<string, string> GetEffectiveMetadataForSelectedItem()
    {
        if (_selectedBrowserItem == null)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        if (!string.IsNullOrWhiteSpace(_metadataEdit?.Text))
        {
            try
            {
                return ParseMetadata(_metadataEdit.Text);
            }
            catch
            {
                // Fall back to runtime metadata below.
            }
        }

        return new Dictionary<string, string>(
            ModStudioBootstrap.ModelMetadataService.GetEditableMetadata(_selectedBrowserItem.Kind, _selectedBrowserItem.EntityId),
            StringComparer.Ordinal);
    }

    private void PersistProjectAfterAssetChange(AssetBindingResult result)
    {
        if (_currentProject == null || _selectedBrowserItem == null)
        {
            return;
        }

        ModStudioBootstrap.ProjectStore.Save(_currentProject);
        _projects = SafeGetProjects();
        PopulateProjectList();
        UpdateProjectDetails();
        SyncOverrideEditor();

        if (_assetPathEdit != null)
        {
            _assetPathEdit.Text = result.MetadataValue;
        }
    }

    private static Texture2D? LoadPreviewTextureLegacy(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return RuntimeAssetLoader.LoadTexture(path);
        }
        catch
        {
            return null;
        }
    }

    private void OnGraphJsonTextChanged(string _)
    {
        RefreshGraphValidationPanel();
    }

    private void OnGraphIdTextChanged(string _)
    {
        RefreshGraphValidationPanel();
        UpdateStateText();
    }

    private void RefreshGraphEditorPanels()
    {
        RefreshGraphTemplatePanel();
        RefreshGraphCatalogPanel();
        RefreshGraphValidationPanel();
    }

    private void RefreshGraphTemplatePanel()
    {
        if (_graphTemplateList == null)
        {
            return;
        }

        ClearChildren(_graphTemplateList);
        if (_selectedBrowserItem == null)
        {
            _graphTemplateList.AddChild(MakeListEntry(L("placeholder.no_selection"), L("placeholder.graph_presets_select")));
            return;
        }

        _graphTemplateList.AddChild(MakeListEntry(L("placeholder.default_scaffold"), L("placeholder.default_scaffold_hint")));
        var scaffoldButton = MakeLocalizedButton("button.apply_default_scaffold", () => ApplyGraphTemplate(null));
        scaffoldButton.CustomMinimumSize = new Vector2(0f, 60f);
        _graphTemplateList.AddChild(scaffoldButton);

        var presets = ModStudioGraphUiHelpers.GetPresets(_selectedBrowserItem.Kind);
        if (presets.Count == 0)
        {
            _graphTemplateList.AddChild(MakeListEntry(L("placeholder.no_specialized_presets"), L("placeholder.no_specialized_presets_hint")));
            return;
        }

        foreach (var preset in presets)
        {
            var captured = preset;
            var button = MakeButton(captured.Name, () => ApplyGraphTemplate(captured));
            button.Text = $"{captured.Name}\n{captured.Description}";
            button.CustomMinimumSize = new Vector2(0f, 72f);
            _graphTemplateList.AddChild(button);
        }
    }

    private void RefreshGraphCatalogPanel()
    {
        if (_graphCatalogDetails == null)
        {
            return;
        }

        if (_selectedBrowserItem == null)
        {
            _graphCatalogDetails.Text = L("placeholder.node_catalog_select");
            return;
        }

        _graphCatalogDetails.Text = ModStudioGraphUiHelpers.BuildNodeCatalogText(ModStudioBootstrap.GraphRegistry, _selectedBrowserItem.Kind);
    }

    private void RefreshGraphValidationPanel()
    {
        if (_graphValidationDetails == null)
        {
            return;
        }

        if (_selectedBrowserItem == null)
        {
            _graphValidationDetails.Text = L("placeholder.graph_validation_select");
            return;
        }

        if (_graphToggle?.ButtonPressed != true)
        {
            _graphValidationDetails.Text = L("placeholder.graph_disabled");
            return;
        }

        if (!TryParseGraphFromEditor(out var graph, out var validation, out var parseError))
        {
            _graphValidationDetails.Text = ModStudioGraphUiHelpers.BuildValidationText(graph, validation, parseError);
            return;
        }

        _graphValidationDetails.Text = ModStudioGraphUiHelpers.BuildValidationText(graph, validation);
    }

    private void ApplyGraphTemplate(GraphTemplatePreset? preset)
    {
        if (_currentProject == null)
        {
            SetAction(L("status.select_project_before_template"));
            return;
        }

        if (_selectedBrowserItem == null)
        {
            SetAction(L("status.select_object_before_template"));
            return;
        }

        try
        {
            var graphId = (_graphIdEdit?.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(graphId))
            {
                graphId = BuildDefaultGraphId(_selectedBrowserItem.Kind, _selectedBrowserItem.EntityId);
                if (_graphIdEdit != null)
                {
                    _graphIdEdit.Text = graphId;
                }
            }

            var graph = preset == null
                ? ModStudioGraphUiHelpers.CreateScaffoldGraph(_selectedBrowserItem.Kind, graphId, _selectedBrowserItem.EntityId)
                : ModStudioGraphUiHelpers.CreatePresetGraph(_selectedBrowserItem.Kind, graphId, _selectedBrowserItem.EntityId, preset);

            if (_graphToggle != null)
            {
                _graphToggle.ButtonPressed = true;
            }
            if (_graphJsonEdit != null)
            {
                _graphJsonEdit.Text = JsonSerializer.Serialize(graph, ModStudioJson.Options);
            }

            var envelope = _currentProject.Overrides.FirstOrDefault(x => x.EntityKind == _selectedBrowserItem.Kind && x.EntityId == _selectedBrowserItem.EntityId);
            if (envelope == null)
            {
                envelope = new EntityOverrideEnvelope
                {
                    EntityKind = _selectedBrowserItem.Kind,
                    EntityId = _selectedBrowserItem.EntityId,
                    Metadata = new Dictionary<string, string>(ModStudioBootstrap.ModelMetadataService.GetEditableMetadata(_selectedBrowserItem.Kind, _selectedBrowserItem.EntityId), StringComparer.Ordinal)
                };
                _currentProject.Overrides.Add(envelope);
            }
            else if (envelope.Metadata.Count == 0)
            {
                envelope.Metadata = new Dictionary<string, string>(ModStudioBootstrap.ModelMetadataService.GetEditableMetadata(_selectedBrowserItem.Kind, _selectedBrowserItem.EntityId), StringComparer.Ordinal);
            }

            envelope.BehaviorSource = BehaviorSource.Graph;
            envelope.GraphId = graphId;
            _currentProject.Graphs[graphId] = graph;
            ModStudioBootstrap.ProjectStore.Save(_currentProject);
            _projects = SafeGetProjects();
            PopulateProjectList();
            UpdateProjectDetails();
            RefreshGraphEditorPanels();
            SetAction(preset == null
                ? LF("status.applied_scaffold", ModStudioLocalization.GetEntityKindDisplayName(_selectedBrowserItem.Kind), _selectedBrowserItem.EntityId)
                : LF("status.applied_template", preset.Name, ModStudioLocalization.GetEntityKindDisplayName(_selectedBrowserItem.Kind), _selectedBrowserItem.EntityId));
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to apply graph template: {ex.Message}");
            SetAction(LF("status.apply_template_failed", ex.Message));
        }
    }

    private bool TryParseGraphFromEditor(out BehaviorGraphDefinition? graph, out BehaviorGraphValidationResult? validation, out string? parseError)
    {
        graph = null;
        validation = null;
        parseError = null;

        if (_graphToggle?.ButtonPressed != true)
        {
            return false;
        }

        var rawJson = _graphJsonEdit?.Text?.Trim();
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            parseError = L("graph.validation.empty");
            return false;
        }

        try
        {
            graph = JsonSerializer.Deserialize<BehaviorGraphDefinition>(rawJson, ModStudioJson.Options);
        }
        catch (Exception ex)
        {
            parseError = ex.Message;
            return false;
        }

        if (graph == null)
        {
            parseError = L("graph.validation.deserialize_failed");
            return false;
        }

        graph.EntityKind ??= _selectedBrowserItem?.Kind;
        validation = ModStudioBootstrap.GraphRegistry.Validate(graph);
        return true;
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

    private void PersistGraphJsonIfPresent(bool updateActionOnSuccess)
    {
        if (_currentProject == null || _selectedBrowserItem == null)
        {
            return;
        }

        var useGraph = _graphToggle?.ButtonPressed == true;
        if (!useGraph)
        {
            return;
        }

        var graphId = (_graphIdEdit?.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(graphId))
        {
            graphId = BuildDefaultGraphId(_selectedBrowserItem.Kind, _selectedBrowserItem.EntityId);
            if (_graphIdEdit != null)
            {
                _graphIdEdit.Text = graphId;
            }
        }

        EnsureGraphExists(_currentProject, graphId, _selectedBrowserItem.Kind, _selectedBrowserItem.EntityId);
        if (string.IsNullOrWhiteSpace(_graphJsonEdit?.Text))
        {
            _graphJsonEdit!.Text = _currentProject.Graphs.TryGetValue(graphId, out var existingGraph)
                ? JsonSerializer.Serialize(existingGraph, ModStudioJson.Options)
                : string.Empty;
        }

        if (!TryParseGraphFromEditor(out var graph, out var validation, out var parseError))
        {
            throw new InvalidOperationException(parseError ?? L("graph.validation.validate_failed"));
        }

        if (validation != null && !validation.IsValid)
        {
            throw new InvalidOperationException(string.Join(" | ", validation.Errors));
        }

        if (graph == null)
        {
            throw new InvalidOperationException(L("graph.validation.deserialize_failed"));
        }
        graph.GraphId = graphId;
        graph.EntityKind ??= _selectedBrowserItem.Kind;

        _currentProject.Graphs[graphId] = graph;
        _graphJsonEdit.Text = JsonSerializer.Serialize(graph, ModStudioJson.Options);
        if (updateActionOnSuccess)
        {
            SetAction(LF("status.saved_graph", graphId));
        }
    }

    private static string ResolveGraphJsonText(EditorProject? project, EntityOverrideEnvelope? envelope, ModStudioEntityKind kind)
    {
        if (project == null || envelope?.GraphId == null)
        {
            return string.Empty;
        }

        if (!project.Graphs.TryGetValue(envelope.GraphId, out var graph))
        {
            return string.Empty;
        }

        graph.EntityKind ??= kind;
        return JsonSerializer.Serialize(graph, ModStudioJson.Options);
    }

    private static void EnsureGraphExists(EditorProject project, string graphId, ModStudioEntityKind kind, string entityId)
    {
        if (project.Graphs.ContainsKey(graphId))
        {
            return;
        }

        project.Graphs[graphId] = ModStudioGraphUiHelpers.CreateScaffoldGraph(kind, graphId, entityId);
    }

    private static void ClearChildren(Node? node)
    {
        if (node == null) return;
        foreach (var child in node.GetChildren()) child.QueueFree();
    }
}
