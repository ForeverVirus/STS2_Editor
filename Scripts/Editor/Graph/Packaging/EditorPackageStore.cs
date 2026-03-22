using STS2_Editor.Scripts.Editor.Core.Models;
using STS2_Editor.Scripts.Editor.Core.Utilities;
using System.Text.Json;

namespace STS2_Editor.Scripts.Editor.Packaging;

public sealed class EditorPackageStore
{
    private readonly PackageArchiveService _archiveService;

    public EditorPackageStore(PackageArchiveService archiveService)
    {
        _archiveService = archiveService ?? throw new ArgumentNullException(nameof(archiveService));
    }

    public string InstalledPackagesRootPath => ModStudioPaths.InstalledPackagesPath;

    public IReadOnlyList<PackageSessionState> LoadSessionStates()
    {
        var sessionFilePath = GetSessionFilePath();
        if (!File.Exists(sessionFilePath))
        {
            return Array.Empty<PackageSessionState>();
        }

        var states = JsonSerializer.Deserialize<List<PackageSessionState>>(File.ReadAllText(sessionFilePath), ModStudioJson.Options);
        return states ?? new List<PackageSessionState>();
    }

    public void SaveSessionStates(IEnumerable<PackageSessionState> states)
    {
        var list = states.ToList();
        Directory.CreateDirectory(InstalledPackagesRootPath);
        ModStudioJson.Save(GetSessionFilePath(), list);
    }

    public bool TryImportPackage(string packageFilePath, out EditorPackageManifest? manifest, out EditorProject? project)
    {
        return _archiveService.TryImport(packageFilePath, out manifest, out project);
    }

    public string ExportProject(EditorProject project, PackageExportOptions? options = null, string? packageFilePath = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        return _archiveService.Export(project, options, packageFilePath);
    }

    public PackageInstallResult InstallPackage(string packageFilePath, bool enabledByDefault = true)
    {
        if (!_archiveService.TryImport(packageFilePath, out var manifest, out _ ) || manifest is null)
        {
            throw new InvalidOperationException($"Package archive '{packageFilePath}' could not be imported.");
        }

        Directory.CreateDirectory(InstalledPackagesRootPath);

        var normalizedFileName = PackagingPathUtility.NormalizeFileName($"{manifest.PackageKey}.sts2pack");
        var destinationPath = Path.Combine(InstalledPackagesRootPath, normalizedFileName);
        var replacedExistingInstall = File.Exists(destinationPath);
        File.Copy(packageFilePath, destinationPath, overwrite: true);

        var states = LoadSessionStates().ToList();
        var existing = states.FirstOrDefault(state => string.Equals(state.PackageKey, manifest.PackageKey, StringComparison.Ordinal));
        if (existing is null)
        {
            existing = new PackageSessionState
            {
                PackageKey = manifest.PackageKey,
                LoadOrder = states.Count
            };
            states.Add(existing);
        }

        existing.PackageId = manifest.PackageId;
        existing.DisplayName = manifest.DisplayName;
        existing.Version = manifest.Version;
        existing.Checksum = manifest.Checksum;
        existing.PackageFilePath = destinationPath;
        existing.Enabled = enabledByDefault;
        existing.SessionEnabled = enabledByDefault;
        existing.DisabledReason = string.Empty;

        SaveSessionStates(states
            .OrderBy(state => state.LoadOrder)
            .ThenBy(state => state.PackageKey, StringComparer.Ordinal)
            .ToList());

        return new PackageInstallResult
        {
            ReplacedExistingInstall = replacedExistingInstall,
            PackageState = existing
        };
    }

    public string GetPackageFolder(string packageKey)
    {
        return Path.Combine(InstalledPackagesRootPath, packageKey);
    }

    public string GetSessionFilePath()
    {
        return Path.Combine(InstalledPackagesRootPath, "session.json");
    }
}
