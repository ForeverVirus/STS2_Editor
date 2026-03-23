using Godot;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using STS2_Editor.Scripts.Editor;
using STS2_Editor.Scripts.Editor.Core.Models;
using STS2_Editor.Scripts.Editor.Core.Utilities;
using STS2_Editor.Scripts.Editor.Runtime;

namespace STS2_Editor.Scripts.Editor.UI;

public sealed partial class NModStudioPackageWindow : NSubmenu
{
    private const string BackButtonScenePath = "res://scenes/ui/back_button.tscn";

    private bool _uiBuilt;
    private bool _refreshQueued;
    private string _searchQuery = string.Empty;
    private string? _selectedPackageKey;

    private ModStudioPackageMenuBar? _menuBar;
    private LineEdit? _searchBox;
    private VBoxContainer? _rowsRoot;
    private Label? _emptyLabel;
    private Label? _summaryLabel;
    private Label? _detailsTitleLabel;
    private RichTextLabel? _detailsBodyLabel;
    private RichTextLabel? _conflictBodyLabel;

    private readonly Dictionary<string, PackageSessionState> _sessionStates = new(StringComparer.Ordinal);
    private readonly Dictionary<string, RuntimeInstalledPackage> _installedPackages = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PackageEntryRow> _rows = new(StringComparer.Ordinal);

    protected override Control? InitialFocusedControl => _searchBox;

    public static IEnumerable<string> AssetPaths => new[] { BackButtonScenePath };

    public static NModStudioPackageWindow Create() => new();

    public override void _Ready()
    {
        ModStudioBootstrap.Initialize();
        ModStudioBootstrap.EnsureRuntimeInitialized();

        BuildUi();
        base.ConnectSignals();
        base.HideBackButtonImmediately();

        ModStudioLocalization.LanguageChanged += RefreshLocalizedText;
        ModStudioBootstrap.RuntimeRegistry.ResolutionChanged += OnResolutionChanged;

        RefreshFromRuntimeRegistry();
    }

    public override void OnSubmenuOpened()
    {
        base.HideBackButtonImmediately();
        RefreshFromRuntimeRegistry();
    }

    protected override void OnSubmenuShown()
    {
        base.OnSubmenuShown();
        base.HideBackButtonImmediately();
    }

    public override void OnSubmenuClosed()
    {
        base.OnSubmenuClosed();
        _refreshQueued = false;
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
        MouseFilter = MouseFilterEnum.Stop;

        AddBackButton();

        AddChild(new ColorRect
        {
            AnchorRight = 1f,
            AnchorBottom = 1f,
            Color = new Color(0.02f, 0.03f, 0.05f, 0.72f),
            MouseFilter = MouseFilterEnum.Ignore
        });

        var center = new CenterContainer
        {
            AnchorRight = 1f,
            AnchorBottom = 1f,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        AddChild(center);

        var panel = new PanelContainer
        {
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
            SizeFlagsVertical = SizeFlags.ShrinkCenter
        };
        center.AddChild(panel);

        var viewportSize = GetViewportRect().Size;
        panel.CustomMinimumSize = new Vector2(Mathf.Max(980f, viewportSize.X * 0.66f), Mathf.Max(700f, viewportSize.Y * 0.8f));

        var outer = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        outer.AddThemeConstantOverride("separation", 0);
        panel.AddChild(outer);

        _menuBar = new ModStudioPackageMenuBar();
        _menuBar.HotReloadRequested += HotReloadPackages;
        _menuBar.CloseRequested += RequestCloseWindow;
        outer.AddChild(_menuBar);

        var body = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        body.AddThemeConstantOverride("separation", 8);
        outer.AddChild(body);

        var leftPane = BuildLeftPane();
        leftPane.SizeFlagsStretchRatio = 4f;
        body.AddChild(leftPane);

        var rightPane = BuildRightPane();
        rightPane.SizeFlagsStretchRatio = 6f;
        body.AddChild(rightPane);
    }

    private Control BuildLeftPane()
    {
        var panel = new PanelContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_top", 12);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_bottom", 12);
        panel.AddChild(margin);

        var content = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        content.AddThemeConstantOverride("separation", 8);
        margin.AddChild(content);

        content.AddChild(ModStudioPackageUi.MakeLabel("\u5df2\u53d1\u5e03\u7684\u6a21\u7ec4\u5305", "Published Packages", true));

        _searchBox = ModStudioPackageUi.MakeSearchBox("\u641c\u7d22 mod \u540d\u79f0/\u4f5c\u8005/\u63cf\u8ff0", "Search name/author/description");
        _searchBox.TextChanged += OnSearchChanged;
        content.AddChild(_searchBox);

        _summaryLabel = ModStudioPackageUi.MakeLabel(string.Empty, string.Empty, true);
        content.AddChild(_summaryLabel);

        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        content.AddChild(scroll);

        var listRoot = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        listRoot.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(listRoot);

        _emptyLabel = ModStudioPackageUi.MakeLabel("\u6682\u65e0\u53d1\u5e03\u5305", "No published packages found", true);
        listRoot.AddChild(_emptyLabel);

        _rowsRoot = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        _rowsRoot.AddThemeConstantOverride("separation", 6);
        listRoot.AddChild(_rowsRoot);

        return panel;
    }

    private Control BuildRightPane()
    {
        var panel = new PanelContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_top", 12);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_bottom", 12);
        panel.AddChild(margin);

        var content = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        content.AddThemeConstantOverride("separation", 8);
        margin.AddChild(content);

        _detailsTitleLabel = ModStudioPackageUi.MakeLabel("\u8bf7\u5148\u9009\u62e9\u4e00\u4e2a\u6a21\u7ec4\u5305", "Select a package", true);
        content.AddChild(_detailsTitleLabel);

        _detailsBodyLabel = ModStudioPackageUi.MakeRichText(string.Empty, string.Empty, false, 150f);
        content.AddChild(_detailsBodyLabel);

        content.AddChild(ModStudioPackageUi.MakeLabel("\u51b2\u7a81\u8be6\u60c5", "Conflict Details", true));
        _conflictBodyLabel = ModStudioPackageUi.MakeRichText(string.Empty, string.Empty, false, 220f);
        content.AddChild(_conflictBodyLabel);

        return panel;
    }

    private void AddBackButton()
    {
        var scene = ResourceLoader.Load<PackedScene>(BackButtonScenePath);
        if (scene == null)
        {
            Log.Warn("Package mode back button scene could not be loaded.");
            return;
        }

        var backButton = scene.Instantiate<NBackButton>(PackedScene.GenEditState.Disabled);
        backButton.Name = "BackButton";
        AddChild(backButton);
    }

    private void OnSearchChanged(string text)
    {
        _searchQuery = text?.Trim() ?? string.Empty;
        RefreshPackageList();
    }

    private void OnResolutionChanged(RuntimeOverrideResolutionResult _)
    {
        if (_refreshQueued)
        {
            return;
        }

        _refreshQueued = true;
        CallDeferred(nameof(RefreshFromRuntimeRegistry));
    }

    private void RefreshFromRuntimeRegistry()
    {
        _refreshQueued = false;

        _sessionStates.Clear();
        _installedPackages.Clear();

        foreach (var package in ModStudioBootstrap.RuntimeRegistry.InstalledPackages)
        {
            _installedPackages[package.PackageKey] = package;
        }

        foreach (var state in ModStudioBootstrap.RuntimeRegistry.SessionStates.OrderBy(state => state.LoadOrder).ThenBy(state => state.PackageKey, StringComparer.Ordinal))
        {
            _sessionStates[state.PackageKey] = CloneState(state);
        }

        if (string.IsNullOrWhiteSpace(_selectedPackageKey) || !_sessionStates.ContainsKey(_selectedPackageKey))
        {
            _selectedPackageKey = _sessionStates.Values.OrderBy(state => state.LoadOrder).FirstOrDefault()?.PackageKey;
        }

        RefreshLocalizedText();
        RefreshPackageList();
        RefreshDetails();
        UpdateMenuBarState();
    }

    private void RefreshLocalizedText()
    {
        _menuBar?.RefreshLocalizedText();

        if (_emptyLabel != null)
        {
            _emptyLabel.Text = ModStudioPackageUi.T("\u6682\u65e0\u53d1\u5e03\u5305", "No published packages found");
        }

        if (_detailsTitleLabel != null && string.IsNullOrWhiteSpace(_selectedPackageKey))
        {
            _detailsTitleLabel.Text = ModStudioPackageUi.T("\u8bf7\u5148\u9009\u62e9\u4e00\u4e2a\u6a21\u7ec4\u5305", "Select a package");
        }
    }

    private void RefreshPackageList()
    {
        if (_rowsRoot == null)
        {
            return;
        }

        ModStudioPackageUi.ClearChildren(_rowsRoot);
        _rows.Clear();

        var ordered = _sessionStates.Values
            .OrderBy(state => state.LoadOrder)
            .ThenBy(state => state.PackageKey, StringComparer.Ordinal)
            .ToList();

        var filtered = ordered.Where(MatchesSearch).ToList();
        if (filtered.Count > 0 && !filtered.Any(state => string.Equals(state.PackageKey, _selectedPackageKey, StringComparison.Ordinal)))
        {
            _selectedPackageKey = filtered[0].PackageKey;
        }

        if (_summaryLabel != null)
        {
            _summaryLabel.Text = ModStudioPackageUi.T($"\u663e\u793a {filtered.Count}/{ordered.Count}", $"Showing {filtered.Count}/{ordered.Count}");
        }

        if (_emptyLabel != null)
        {
            _emptyLabel.Visible = filtered.Count == 0;
        }

        foreach (var state in filtered)
        {
            _installedPackages.TryGetValue(state.PackageKey, out var installed);
            var row = new PackageEntryRow(state, installed, string.Equals(state.PackageKey, _selectedPackageKey, StringComparison.Ordinal), OnRowSelected, OnRowEnabledChanged, OnRowReordered);
            _rows[state.PackageKey] = row;
            _rowsRoot.AddChild(row);
        }

        RefreshDetails();
        UpdateMenuBarState();
    }

    private bool MatchesSearch(PackageSessionState state)
    {
        if (string.IsNullOrWhiteSpace(_searchQuery))
        {
            return true;
        }

        if (_installedPackages.TryGetValue(state.PackageKey, out var package))
        {
            var manifest = package.Manifest;
            return Contains(manifest.DisplayName, _searchQuery) ||
                   Contains(manifest.PackageId, _searchQuery) ||
                   Contains(manifest.Version, _searchQuery) ||
                   Contains(manifest.Author, _searchQuery) ||
                   Contains(manifest.Description, _searchQuery) ||
                   Contains(state.PackageKey, _searchQuery);
        }

        return Contains(state.PackageKey, _searchQuery);
    }

    private static bool Contains(string? value, string query)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private void OnRowSelected(string packageKey)
    {
        _selectedPackageKey = packageKey;
        foreach (var row in _rows.Values)
        {
            row.SetSelected(string.Equals(row.PackageKey, packageKey, StringComparison.Ordinal));
        }

        RefreshDetails();
    }

    private void OnRowEnabledChanged(string packageKey, bool enabled)
    {
        ModStudioBootstrap.RuntimeRegistry.EnablePackage(packageKey, enabled);
    }

    private void OnRowReordered(string sourceKey, string targetKey)
    {
        if (string.Equals(sourceKey, targetKey, StringComparison.Ordinal))
        {
            return;
        }

        var ordered = _sessionStates.Values
            .OrderBy(state => state.LoadOrder)
            .ThenBy(state => state.PackageKey, StringComparer.Ordinal)
            .Select(CloneState)
            .ToList();

        var sourceIndex = ordered.FindIndex(state => string.Equals(state.PackageKey, sourceKey, StringComparison.Ordinal));
        var targetIndex = ordered.FindIndex(state => string.Equals(state.PackageKey, targetKey, StringComparison.Ordinal));
        if (sourceIndex < 0 || targetIndex < 0)
        {
            return;
        }

        var source = ordered[sourceIndex];
        ordered.RemoveAt(sourceIndex);
        if (sourceIndex < targetIndex)
        {
            targetIndex--;
        }

        ordered.Insert(targetIndex, source);
        for (var i = 0; i < ordered.Count; i++)
        {
            ordered[i].LoadOrder = i;
        }

        ModStudioBootstrap.RuntimeRegistry.SetSessionStates(ordered);
        _selectedPackageKey = sourceKey;
    }

    private void HotReloadPackages()
    {
        ModStudioBootstrap.RuntimeRegistry.Refresh();
        RefreshFromRuntimeRegistry();
    }

    private void RequestCloseWindow()
    {
        _stack.Pop();
    }

    private void RefreshDetails()
    {
        if (_detailsTitleLabel == null || _detailsBodyLabel == null || _conflictBodyLabel == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_selectedPackageKey) ||
            !_sessionStates.TryGetValue(_selectedPackageKey, out var state))
        {
            _detailsTitleLabel.Text = ModStudioPackageUi.T("\u8bf7\u5148\u9009\u62e9\u4e00\u4e2a\u6a21\u7ec4\u5305", "Select a package");
            _detailsBodyLabel.Text = ModStudioPackageUi.T("\u5de6\u4fa7\u5217\u8868\u4f1a\u663e\u793a\u5f53\u524d\u771f\u5b9e\u76ee\u5f55\u4e0b\u7684 .sts2pack \u5305\u3002", "The left list shows .sts2pack packages discovered from the real game directory.");
            _conflictBodyLabel.Text = string.Empty;
            return;
        }

        _installedPackages.TryGetValue(state.PackageKey, out var package);
        var manifest = package?.Manifest ?? new EditorPackageManifest();
        _detailsTitleLabel.Text = string.IsNullOrWhiteSpace(manifest.DisplayName) ? state.PackageKey : manifest.DisplayName;
        _detailsBodyLabel.Text =
            $"{ModStudioPackageUi.T("\u5305\u952e", "Package Key")}: {state.PackageKey}\n" +
            $"{ModStudioPackageUi.T("\u5305 ID", "Package Id")}: {manifest.PackageId}\n" +
            $"{ModStudioPackageUi.T("\u7248\u672c", "Version")}: {manifest.Version}\n" +
            $"{ModStudioPackageUi.T("\u4f5c\u8005", "Author")}: {manifest.Author}\n" +
            $"{ModStudioPackageUi.T("\u5df2\u542f\u7528", "Enabled")}: {state.Enabled}\n" +
            $"{ModStudioPackageUi.T("\u4f1a\u8bdd\u542f\u7528", "Session Enabled")}: {state.SessionEnabled}\n" +
            $"{ModStudioPackageUi.T("\u52a0\u8f7d\u987a\u5e8f", "Load Order")}: {state.LoadOrder}\n" +
            $"{ModStudioPackageUi.T("\u6821\u9a8c\u503c", "Checksum")}: {state.Checksum}\n" +
            $"{ModStudioPackageUi.T("\u8def\u5f84", "Path")}: {state.PackageFilePath}\n" +
            $"{ModStudioPackageUi.T("\u63cf\u8ff0", "Description")}:\n{manifest.Description}";

        _conflictBodyLabel.Text = BuildConflictText(state.PackageKey);
    }

    private string BuildConflictText(string packageKey)
    {
        var conflicts = ModStudioBootstrap.RuntimeRegistry.LastResolution.Conflicts
            .Where(conflict => conflict.Participants.Any(participant => string.Equals(participant.PackageKey, packageKey, StringComparison.Ordinal)))
            .OrderBy(conflict => conflict.EntityKind.ToString(), StringComparer.Ordinal)
            .ThenBy(conflict => conflict.EntityId, StringComparer.Ordinal)
            .ToList();

        if (conflicts.Count == 0)
        {
            return ModStudioPackageUi.T("\u5f53\u524d\u6ca1\u6709\u68c0\u6d4b\u5230\u5bf9\u8c61\u7ea7\u51b2\u7a81\u3002", "No object-level conflicts were detected for this package.");
        }

        var builder = new System.Text.StringBuilder();
        foreach (var conflict in conflicts.Take(24))
        {
            builder.AppendLine($"{conflict.EntityKind}:{conflict.EntityId} -> {conflict.WinningPackageKey}");
        }

        if (conflicts.Count > 24)
        {
            builder.AppendLine(ModStudioPackageUi.T($"\u8fd8\u6709 {conflicts.Count - 24} \u9879\u672a\u663e\u793a\u3002", $"{conflicts.Count - 24} more conflicts are hidden."));
        }

        return builder.ToString();
    }

    private void UpdateMenuBarState()
    {
        if (_menuBar == null)
        {
            return;
        }

        _menuBar.SetPublishedRootPath(ModStudioPaths.PublishedPackagesRootPath);
        var enabledCount = _sessionStates.Values.Count(state => state.Enabled);
        _menuBar.SetStateText(ModStudioPackageUi.T($"\u5df2\u542f\u7528 {enabledCount}/{_sessionStates.Count}", $"Enabled {enabledCount}/{_sessionStates.Count}"));
    }

    private static PackageSessionState CloneState(PackageSessionState state)
    {
        return new PackageSessionState
        {
            PackageKey = state.PackageKey,
            PackageId = state.PackageId,
            DisplayName = state.DisplayName,
            Version = state.Version,
            Checksum = state.Checksum,
            PackageFilePath = state.PackageFilePath,
            LoadOrder = state.LoadOrder,
            Enabled = state.Enabled,
            SessionEnabled = state.SessionEnabled,
            DisabledReason = state.DisabledReason
        };
    }

    private sealed partial class PackageEntryRow : PanelContainer
    {
        private readonly Action<string> _selectCallback;
        private readonly Action<string, bool> _enabledChangedCallback;
        private readonly Action<string, string> _reorderCallback;
        private readonly Label _titleLabel;
        private readonly Label _summaryLabel;
        private readonly ColorRect _background;

        public string PackageKey { get; }

        public PackageEntryRow(
            PackageSessionState state,
            RuntimeInstalledPackage? installed,
            bool selected,
            Action<string> selectCallback,
            Action<string, bool> enabledChangedCallback,
            Action<string, string> reorderCallback)
        {
            PackageKey = state.PackageKey;
            _selectCallback = selectCallback;
            _enabledChangedCallback = enabledChangedCallback;
            _reorderCallback = reorderCallback;

            MouseFilter = MouseFilterEnum.Stop;
            CustomMinimumSize = new Vector2(0f, 58f);

            _background = new ColorRect
            {
                AnchorRight = 1f,
                AnchorBottom = 1f,
                MouseFilter = MouseFilterEnum.Ignore
            };
            AddChild(_background);

            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", 10);
            margin.AddThemeConstantOverride("margin_top", 8);
            margin.AddThemeConstantOverride("margin_right", 10);
            margin.AddThemeConstantOverride("margin_bottom", 8);
            AddChild(margin);

            var row = new HBoxContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill
            };
            row.AddThemeConstantOverride("separation", 8);
            margin.AddChild(row);

            var toggleButton = new CheckButton
            {
                ButtonPressed = state.Enabled,
                CustomMinimumSize = new Vector2(42f, 28f)
            };
            toggleButton.Toggled += pressed => _enabledChangedCallback(PackageKey, pressed);
            row.AddChild(toggleButton);

            var textStack = new VBoxContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill
            };
            textStack.AddThemeConstantOverride("separation", 2);
            row.AddChild(textStack);

            var displayName = string.IsNullOrWhiteSpace(installed?.Manifest.DisplayName) ? state.PackageKey : installed!.Manifest.DisplayName;
            _titleLabel = new Label
            {
                Text = displayName,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            textStack.AddChild(_titleLabel);

            _summaryLabel = new Label
            {
                Text = $"{state.PackageKey} | #{state.LoadOrder + 1} | {(state.Enabled ? ModStudioPackageUi.T("\u5df2\u542f\u7528", "Enabled") : ModStudioPackageUi.T("\u5df2\u7981\u7528", "Disabled"))}",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            textStack.AddChild(_summaryLabel);

            row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

            row.AddChild(new Label
            {
                Text = "\u2192",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                CustomMinimumSize = new Vector2(24f, 0f)
            });

            GuiInput += OnGuiInput;
            SetSelected(selected);
        }

        public void SetSelected(bool selected)
        {
            _background.Color = selected
                ? new Color(0.22f, 0.32f, 0.42f, 0.85f)
                : new Color(0.12f, 0.14f, 0.16f, 0.95f);
        }

        public override Variant _GetDragData(Vector2 atPosition)
        {
            var preview = new Label
            {
                Text = _titleLabel.Text,
                CustomMinimumSize = new Vector2(220f, 24f)
            };
            SetDragPreview(preview);
            return PackageKey;
        }

        public override bool _CanDropData(Vector2 atPosition, Variant data)
        {
            var sourceKey = data.AsString();
            return !string.IsNullOrWhiteSpace(sourceKey) &&
                   !string.Equals(sourceKey, PackageKey, StringComparison.Ordinal);
        }

        public override void _DropData(Vector2 atPosition, Variant data)
        {
            var sourceKey = data.AsString();
            if (string.IsNullOrWhiteSpace(sourceKey) || string.Equals(sourceKey, PackageKey, StringComparison.Ordinal))
            {
                return;
            }

            _reorderCallback(sourceKey, PackageKey);
        }

        private void OnGuiInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton mouseButton &&
                mouseButton.ButtonIndex == MouseButton.Left &&
                mouseButton.Pressed)
            {
                _selectCallback(PackageKey);
            }
        }
    }
}
