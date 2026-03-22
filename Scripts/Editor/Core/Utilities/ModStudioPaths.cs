using Godot;

namespace STS2_Editor.Scripts.Editor.Core.Utilities;

public static class ModStudioPaths
{
    public static string RootPath => ProjectSettings.GlobalizePath("user://sts2_editor");

    public static string ProjectsPath => Path.Combine(RootPath, "projects");

    public static string PackagesPath => Path.Combine(RootPath, "packages");

    public static string InstalledPackagesPath => Path.Combine(PackagesPath, "installed");

    public static string ImportsPath => Path.Combine(RootPath, "imports");

    public static string ExportsPath => Path.Combine(RootPath, "exports");

    public static string CachePath => Path.Combine(RootPath, "cache");

    public static string PackageStatePath => Path.Combine(PackagesPath, "package_state.json");

    public static string GetProjectDirectory(string projectId)
    {
        return Path.Combine(ProjectsPath, projectId);
    }

    public static string GetProjectFilePath(string projectId)
    {
        return Path.Combine(GetProjectDirectory(projectId), "project.json");
    }

    public static string GetProjectAssetsDirectory(string projectId)
    {
        return Path.Combine(GetProjectDirectory(projectId), "assets");
    }

    public static string GetInstalledPackageFilePath(string fileName)
    {
        return Path.Combine(InstalledPackagesPath, fileName);
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
    }
}
