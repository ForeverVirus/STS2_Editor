using MegaCrit.Sts2.Core.Logging;
using STS2_Editor.Scripts.Editor.Core.Services;
using STS2_Editor.Scripts.Editor.Core.Utilities;
using STS2_Editor.Scripts.Editor.Graph;
using STS2_Editor.Scripts.Editor.Packaging;
using STS2_Editor.Scripts.Editor.Runtime;

namespace STS2_Editor.Scripts.Editor;

public static class ModStudioBootstrap
{
    private static bool _initialized;
    private static bool _runtimeInitialized;

    public static EditorProjectStore ProjectStore { get; private set; } = null!;

    public static PackageArchiveService PackageArchiveService { get; private set; } = null!;

    public static AssetImportService AssetImportService { get; private set; } = null!;

    public static EditorPackageStore PackageStore { get; private set; } = null!;

    public static BehaviorGraphRegistry GraphRegistry { get; private set; } = null!;

    public static BehaviorGraphExecutor GraphExecutor { get; private set; } = null!;

    public static EditorRuntimeRegistry RuntimeRegistry { get; private set; } = null!;

    public static RuntimeDynamicContentRegistry RuntimeDynamicContentRegistry { get; private set; } = null!;

    public static ModelMetadataService ModelMetadataService { get; private set; } = null!;

    public static ProjectAssetBindingService ProjectAssetBindingService { get; private set; } = null!;

    public static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;

        ModStudioPaths.EnsureAllDirectories();
        ModStudioLocalization.Initialize();

        ProjectStore = new EditorProjectStore();
        PackageArchiveService = new PackageArchiveService();
        AssetImportService = new AssetImportService();
        PackageStore = new EditorPackageStore(PackageArchiveService);
        GraphRegistry = new BehaviorGraphRegistry();
        GraphRegistry.RegisterBuiltIns();
        GraphExecutor = new BehaviorGraphExecutor(GraphRegistry);
        RuntimeRegistry = new EditorRuntimeRegistry(PackageArchiveService, PackageStore);
        RuntimeDynamicContentRegistry = new RuntimeDynamicContentRegistry();
        RuntimeRegistry.ResolutionChanged += RuntimeDynamicContentRegistry.Synchronize;
        ModelMetadataService = new ModelMetadataService();
        ProjectAssetBindingService = new ProjectAssetBindingService();

        Log.Info("Mod Studio bootstrap initialized.");
    }

    public static void EnsureRuntimeInitialized()
    {
        if (_runtimeInitialized)
        {
            return;
        }

        _runtimeInitialized = true;
        RuntimeRegistry.Initialize();
        Log.Info("Mod Studio runtime registry initialized after core model startup.");
    }
}
