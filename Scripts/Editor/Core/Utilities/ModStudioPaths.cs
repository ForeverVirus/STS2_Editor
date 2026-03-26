using Godot;

namespace STS2_Editor.Scripts.Editor.Core.Utilities;

public static class ModStudioPaths
{
    private const string EditorModFolderName = "STS2_Editor";

    public static string RootPath => ProjectSettings.GlobalizePath("user://sts2_editor");

    public static string SettingsFilePath => Path.Combine(RootPath, "settings.json");

    public static string SettingsPath => SettingsFilePath;

    public static string LegacyProjectsPath => Path.Combine(RootPath, "projects");

    public static string ProjectsPath => Path.Combine(RootPath, "projects");

    public static string PackagesPath => Path.Combine(RootPath, "packages");

    public static string InstalledPackagesPath => Path.Combine(PackagesPath, "installed");

    public static string ImportsPath => Path.Combine(RootPath, "imports");

    public static string ExportsPath => Path.Combine(RootPath, "exports");

    public static string CachePath => Path.Combine(RootPath, "cache");

    public static string PackageStatePath => Path.Combine(PackagesPath, "package_state.json");

    public static string RuntimePackageCachePath => Path.Combine(CachePath, "runtime_packages");

    public static string GameExecutableDirectory
    {
        get
        {
            var executablePath = OS.GetExecutablePath();
            var directory = Path.GetDirectoryName(executablePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                return Path.GetFullPath(directory);
            }

            return Path.GetFullPath(AppContext.BaseDirectory);
        }
    }

    public static string InstalledModRootPath => Path.Combine(GameExecutableDirectory, "mods", EditorModFolderName);

    public static string InstalledModDocsPath => Path.Combine(InstalledModRootPath, "docs");

    public static string PresetStateKeysGuidePath => Path.Combine(InstalledModDocsPath, "preset_state_keys_guide.txt");

    public static string PublishedPackagesRootPath => Path.Combine(InstalledModRootPath, "mods");

    public static string GetLegacyProjectDirectory(string projectId)
    {
        return Path.Combine(LegacyProjectsPath, projectId);
    }

    public static string GetLegacyProjectFilePath(string projectId)
    {
        return Path.Combine(GetLegacyProjectDirectory(projectId), "project.json");
    }

    public static string NormalizePath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        return Path.TrimEndingDirectorySeparator(fullPath);
    }

    public static string NormalizeProjectRootPath(string projectPathOrDirectory)
    {
        if (string.IsNullOrWhiteSpace(projectPathOrDirectory))
        {
            return string.Empty;
        }

        var trimmed = projectPathOrDirectory.Trim();
        if (IsProjectFilePath(trimmed))
        {
            return NormalizePath(Path.GetDirectoryName(Path.GetFullPath(trimmed)) ?? trimmed);
        }

        return NormalizePath(trimmed);
    }

    public static bool IsProjectFilePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        return Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase) &&
               Path.GetFileName(path).Equals("project.json", StringComparison.OrdinalIgnoreCase);
    }

    public static bool LooksLikeFileSystemPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Contains(Path.DirectorySeparatorChar) ||
               value.Contains(Path.AltDirectorySeparatorChar) ||
               value.Contains(':') ||
               Path.HasExtension(value);
    }

    public static bool TryResolveProjectPaths(string? projectPathOrDirectory, out string projectRootPath, out string projectFilePath)
    {
        projectRootPath = string.Empty;
        projectFilePath = string.Empty;

        if (string.IsNullOrWhiteSpace(projectPathOrDirectory))
        {
            return false;
        }

        var trimmed = projectPathOrDirectory.Trim();
        try
        {
            if (IsProjectFilePath(trimmed))
            {
                projectFilePath = Path.GetFullPath(trimmed);
                projectRootPath = NormalizeProjectRootPath(projectFilePath);
                return true;
            }

            var normalizedRoot = NormalizeProjectRootPath(trimmed);
            if (Directory.Exists(normalizedRoot))
            {
                projectRootPath = normalizedRoot;
                projectFilePath = Path.Combine(projectRootPath, "project.json");
                return true;
            }

            if (File.Exists(trimmed) && IsProjectFilePath(trimmed))
            {
                projectFilePath = Path.GetFullPath(trimmed);
                projectRootPath = NormalizeProjectRootPath(projectFilePath);
                return true;
            }

            if (Directory.Exists(Path.GetDirectoryName(Path.GetFullPath(trimmed)) ?? string.Empty))
            {
                projectRootPath = normalizedRoot;
                projectFilePath = Path.Combine(projectRootPath, "project.json");
                return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    public static string GetProjectDirectory(string projectId)
    {
        return GetLegacyProjectDirectory(projectId);
    }

    public static string GetProjectFilePath(string projectId)
    {
        return GetLegacyProjectFilePath(projectId);
    }

    public static string GetProjectAssetsDirectory(string projectId)
    {
        return Path.Combine(GetLegacyProjectDirectory(projectId), "assets");
    }

    public static string GetProjectFilePathFromRoot(string projectRootPath)
    {
        return Path.Combine(NormalizeProjectRootPath(projectRootPath), "project.json");
    }

    public static string GetProjectAssetsDirectoryFromRoot(string projectRootPath)
    {
        return Path.Combine(NormalizeProjectRootPath(projectRootPath), "assets");
    }

    public static string GetProjectDirectoryFromPath(string projectPathOrDirectory)
    {
        return NormalizeProjectRootPath(projectPathOrDirectory);
    }

    public static string GetInstalledPackageFilePath(string fileName)
    {
        return Path.Combine(InstalledPackagesPath, fileName);
    }

    public static string GetInstalledPackageDirectory(string packageKey)
    {
        return Path.Combine(InstalledPackagesPath, packageKey);
    }

    public static string GetInstalledPackageAssetsDirectory(string packageKey)
    {
        return Path.Combine(GetInstalledPackageDirectory(packageKey), "assets");
    }

    public static string GetInstalledPackageAssetFilePath(string packageKey, string assetId, string fileName)
    {
        return Path.Combine(GetInstalledPackageAssetsDirectory(packageKey), assetId, fileName);
    }

    public static string GetRuntimePackageDirectory(string packageKey)
    {
        return Path.Combine(RuntimePackageCachePath, packageKey);
    }

    public static string GetRuntimePackageAssetsDirectory(string packageKey)
    {
        return Path.Combine(GetRuntimePackageDirectory(packageKey), "assets");
    }

    public static string GetRuntimePackageAssetFilePath(string packageKey, string assetId, string fileName)
    {
        return Path.Combine(GetRuntimePackageAssetsDirectory(packageKey), assetId, fileName);
    }

    public static void EnsureAllDirectories()
    {
        Directory.CreateDirectory(RootPath);
        Directory.CreateDirectory(ProjectsPath);
        Directory.CreateDirectory(PackagesPath);
        Directory.CreateDirectory(InstalledPackagesPath);
        Directory.CreateDirectory(ImportsPath);
        Directory.CreateDirectory(ExportsPath);
        Directory.CreateDirectory(CachePath);
        Directory.CreateDirectory(RuntimePackageCachePath);
    }
}
