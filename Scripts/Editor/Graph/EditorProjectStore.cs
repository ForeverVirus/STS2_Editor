using System.Text.Json;
using STS2_Editor.Scripts.Editor.Core.Models;
using STS2_Editor.Scripts.Editor.Core.Utilities;
using STS2_Editor.Scripts.Editor.Packaging;

namespace STS2_Editor.Scripts.Editor;

public sealed class EditorProjectStore
{
    private readonly Dictionary<string, string> _projectRootsById = new(StringComparer.Ordinal);

    public string ProjectsRootPath => ModStudioPaths.LegacyProjectsPath;

    public EditorProjectManifest CreateProjectManifest(string name, string author = "", string description = "", string targetGameVersion = "unknown")
    {
        var projectId = Guid.NewGuid().ToString("N");
        return new EditorProjectManifest
        {
            ProjectId = projectId,
            Name = string.IsNullOrWhiteSpace(name) ? "New Mod Studio Project" : name,
            Author = author,
            Description = description,
            TargetGameVersion = targetGameVersion,
            EditorVersion = "phase1",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    public EditorProject CreateProject(string name, string author = "", string description = "", string targetGameVersion = "unknown")
    {
        var projectRootPath = EnsureUniqueSiblingProjectRoot(
            Path.Combine(ModStudioPaths.LegacyProjectsPath, PackagingPathUtility.NormalizeFileName(string.IsNullOrWhiteSpace(name) ? "New Mod Studio Project" : name)));
        return CreateProject(projectRootPath, name, author, description, targetGameVersion, overwriteExistingProject: false);
    }

    public EditorProject CreateProject(string projectPathOrDirectory, string name, string author = "", string description = "", string targetGameVersion = "unknown", bool overwriteExistingProject = false)
    {
        var projectRootPath = ResolveProjectRootPath(projectPathOrDirectory);
        if (string.IsNullOrWhiteSpace(projectRootPath))
        {
            throw new ArgumentException("Project root path is required.", nameof(projectPathOrDirectory));
        }

        var projectFilePath = ModStudioPaths.GetProjectFilePathFromRoot(projectRootPath);
        if (File.Exists(projectFilePath) && !overwriteExistingProject)
        {
            throw new InvalidOperationException($"Project file '{projectFilePath}' already exists.");
        }

        Directory.CreateDirectory(projectRootPath);
        Directory.CreateDirectory(ModStudioPaths.GetProjectAssetsDirectoryFromRoot(projectRootPath));

        var project = new EditorProject
        {
            Manifest = CreateProjectManifest(name, author, description, targetGameVersion),
            SourceOfTruthIsRuntimeModelDb = true
        };

        Save(project, projectRootPath);
        return project;
    }

    public void Save(EditorProject project)
    {
        Save(project, null);
    }

    public void Save(EditorProject project, string? projectPathOrDirectory)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (string.IsNullOrWhiteSpace(project.Manifest.ProjectId))
        {
            project.Manifest.ProjectId = Guid.NewGuid().ToString("N");
        }

        project.Manifest.UpdatedAtUtc = DateTimeOffset.UtcNow;

        var projectRootPath = ResolveProjectRootPath(projectPathOrDirectory, project);
        if (string.IsNullOrWhiteSpace(projectRootPath))
        {
            throw new InvalidOperationException("Project root path could not be resolved.");
        }

        Directory.CreateDirectory(projectRootPath);
        Directory.CreateDirectory(ModStudioPaths.GetProjectAssetsDirectoryFromRoot(projectRootPath));

        ModStudioJson.Save(ModStudioPaths.GetProjectFilePathFromRoot(projectRootPath), project);
        RememberProjectLocation(project.Manifest.ProjectId, projectRootPath);
    }

    public bool DeleteProject(string projectPathOrDirectory)
    {
        var projectRootPath = ResolveProjectRootPath(projectPathOrDirectory);
        if (string.IsNullOrWhiteSpace(projectRootPath) || !Directory.Exists(projectRootPath))
        {
            return false;
        }

        Directory.Delete(projectRootPath, recursive: true);
        return true;
    }

    public EditorProject DuplicateProject(string sourceProjectPathOrDirectory, string? newName = null)
    {
        if (!TryLoad(sourceProjectPathOrDirectory, out var project) || project is null)
        {
            throw new InvalidOperationException($"Project '{sourceProjectPathOrDirectory}' could not be loaded for duplication.");
        }

        var sourceRootPath = GetKnownProjectRootPath(project);
        var targetRootPath = EnsureUniqueSiblingProjectRoot(
            Path.Combine(
                string.IsNullOrWhiteSpace(sourceRootPath) ? ModStudioPaths.LegacyProjectsPath : Path.GetDirectoryName(sourceRootPath) ?? ModStudioPaths.LegacyProjectsPath,
                PackagingPathUtility.NormalizeFileName(string.IsNullOrWhiteSpace(newName) ? $"{project.Manifest.Name} Copy" : newName)));

        var duplicated = new EditorProject
        {
            Manifest = new EditorProjectManifest
            {
                ProjectId = Guid.NewGuid().ToString("N"),
                Name = string.IsNullOrWhiteSpace(newName) ? $"{project.Manifest.Name} Copy" : newName,
                Author = project.Manifest.Author,
                Description = project.Manifest.Description,
                EditorVersion = project.Manifest.EditorVersion,
                TargetGameVersion = project.Manifest.TargetGameVersion,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            },
            Overrides = project.Overrides.Select(CloneOverride).ToList(),
            Graphs = project.Graphs.ToDictionary(
                pair => pair.Key,
                pair => CloneGraph(pair.Value),
                StringComparer.Ordinal),
            ProjectAssets = project.ProjectAssets.Select(CloneAsset).ToList(),
            SourceOfTruthIsRuntimeModelDb = project.SourceOfTruthIsRuntimeModelDb
        };

        RehomeManagedAssetsForDuplicate(project, duplicated, sourceRootPath, targetRootPath);
        Save(duplicated, targetRootPath);
        return duplicated;
    }

    public bool TryLoad(string projectPathOrDirectory, out EditorProject? project)
    {
        project = null;

        if (TryLoadResolved(projectPathOrDirectory, out var resolvedRootPath, out var resolvedProjectFilePath, out project) && project is not null)
        {
            RememberProjectLocation(project.Manifest.ProjectId, resolvedRootPath);
            ModStudioSettingsStore.RecordRecentProject(resolvedRootPath);
            return true;
        }

        if (TryLoadLegacy(projectPathOrDirectory, out resolvedRootPath, out resolvedProjectFilePath, out project) && project is not null)
        {
            RememberProjectLocation(project.Manifest.ProjectId, resolvedRootPath);
            ModStudioSettingsStore.RecordRecentProject(resolvedRootPath);
            return true;
        }

        return false;
    }

    public IReadOnlyList<EditorProjectManifest> EnumerateProjectManifests()
    {
        var manifests = new List<EditorProjectManifest>();
        foreach (var projectRootPath in GetKnownProjectRoots())
        {
            if (!TryLoadManifest(projectRootPath, out var manifest))
            {
                continue;
            }

            manifests.Add(manifest);
        }

        return manifests
            .OrderBy(manifest => manifest.Name, StringComparer.Ordinal)
            .ThenBy(manifest => manifest.ProjectId, StringComparer.Ordinal)
            .ToList();
    }

    public string GetProjectDirectory(string projectPathOrDirectory)
    {
        return ResolveProjectRootPath(projectPathOrDirectory);
    }

    public string GetProjectFilePath(string projectPathOrDirectory)
    {
        return ResolveProjectFilePath(projectPathOrDirectory);
    }

    public string GetProjectAssetsDirectory(string projectPathOrDirectory)
    {
        return Path.Combine(ResolveProjectRootPath(projectPathOrDirectory), "assets");
    }

    private bool TryLoadResolved(string projectPathOrDirectory, out string projectRootPath, out string projectFilePath, out EditorProject? project)
    {
        project = null;
        if (!ModStudioPaths.TryResolveProjectPaths(projectPathOrDirectory, out projectRootPath, out projectFilePath))
        {
            return false;
        }

        return TryLoadFromFile(projectFilePath, projectRootPath, out project);
    }

    private bool TryLoadLegacy(string projectPathOrDirectory, out string projectRootPath, out string projectFilePath, out EditorProject? project)
    {
        project = null;
        var legacyProjectId = projectPathOrDirectory.Trim();
        projectRootPath = ModStudioPaths.GetLegacyProjectDirectory(legacyProjectId);
        projectFilePath = ModStudioPaths.GetLegacyProjectFilePath(legacyProjectId);
        return TryLoadFromFile(projectFilePath, projectRootPath, out project);
    }

    private bool TryLoadFromFile(string projectFilePath, string projectRootPath, out EditorProject? project)
    {
        project = null;
        if (!File.Exists(projectFilePath))
        {
            return false;
        }

        project = JsonSerializer.Deserialize<EditorProject>(File.ReadAllText(projectFilePath), ModStudioJson.Options);
        if (project is null)
        {
            return false;
        }

        RememberProjectLocation(project.Manifest.ProjectId, projectRootPath);
        return true;
    }

    private bool TryLoadManifest(string projectPathOrDirectory, out EditorProjectManifest manifest)
    {
        manifest = new EditorProjectManifest();
        if (!TryLoadManifestInternal(projectPathOrDirectory, out var project) || project?.Manifest is null)
        {
            return false;
        }

        manifest = project.Manifest;
        return true;
    }

    private bool TryLoadManifestInternal(string projectPathOrDirectory, out EditorProject? project)
    {
        project = null;
        if (ModStudioPaths.TryResolveProjectPaths(projectPathOrDirectory, out var resolvedRootPath, out var resolvedProjectFilePath))
        {
            return TryLoadFromFile(resolvedProjectFilePath, resolvedRootPath, out project);
        }

        var legacyProjectId = projectPathOrDirectory.Trim();
        var legacyRootPath = ModStudioPaths.GetLegacyProjectDirectory(legacyProjectId);
        var legacyProjectFilePath = ModStudioPaths.GetLegacyProjectFilePath(legacyProjectId);
        return TryLoadFromFile(legacyProjectFilePath, legacyRootPath, out project);
    }

    private IEnumerable<string> GetKnownProjectRoots()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var settings = ModStudioSettingsStore.Load();

        foreach (var cachedRoot in _projectRootsById.Values)
        {
            AddIfValidRoot(roots, cachedRoot);
        }

        AddIfValidRoot(roots, settings.LastProjectPath);
        foreach (var recentPath in settings.RecentProjectPaths)
        {
            AddIfValidRoot(roots, recentPath);
        }

        if (Directory.Exists(ModStudioPaths.LegacyProjectsPath))
        {
            foreach (var legacyDirectory in Directory.EnumerateDirectories(ModStudioPaths.LegacyProjectsPath))
            {
                AddIfValidRoot(roots, legacyDirectory);
            }
        }

        return roots
            .OrderBy(root => root, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AddIfValidRoot(ICollection<string> roots, string? projectPathOrDirectory)
    {
        if (string.IsNullOrWhiteSpace(projectPathOrDirectory))
        {
            return;
        }

        if (!ModStudioPaths.TryResolveProjectPaths(projectPathOrDirectory, out var projectRootPath, out var projectFilePath))
        {
            return;
        }

        if (File.Exists(projectFilePath) || Directory.Exists(projectRootPath))
        {
            roots.Add(projectRootPath);
        }
    }

    private string ResolveProjectRootPath(string? projectPathOrDirectory, EditorProject? project = null)
    {
        if (ModStudioPaths.TryResolveProjectPaths(projectPathOrDirectory, out var projectRootPath, out _))
        {
            return projectRootPath;
        }

        if (project != null && _projectRootsById.TryGetValue(project.Manifest.ProjectId, out var cachedRootPath))
        {
            return cachedRootPath;
        }

        if (!string.IsNullOrWhiteSpace(projectPathOrDirectory))
        {
            if (!ModStudioPaths.LooksLikeFileSystemPath(projectPathOrDirectory))
            {
                return ModStudioPaths.GetLegacyProjectDirectory(projectPathOrDirectory);
            }

            return ModStudioPaths.NormalizeProjectRootPath(projectPathOrDirectory);
        }

        if (project != null)
        {
            return ModStudioPaths.GetLegacyProjectDirectory(project.Manifest.ProjectId);
        }

        return string.Empty;
    }

    private string ResolveProjectFilePath(string? projectPathOrDirectory)
    {
        if (ModStudioPaths.TryResolveProjectPaths(projectPathOrDirectory, out var projectRootPath, out var projectFilePath))
        {
            return projectFilePath;
        }

        if (!string.IsNullOrWhiteSpace(projectPathOrDirectory))
        {
            if (ModStudioPaths.LooksLikeFileSystemPath(projectPathOrDirectory))
            {
                return ModStudioPaths.GetProjectFilePathFromRoot(projectPathOrDirectory);
            }

            return ModStudioPaths.GetLegacyProjectFilePath(projectPathOrDirectory);
        }

        return string.Empty;
    }

    private string GetKnownProjectRootPath(EditorProject project)
    {
        if (_projectRootsById.TryGetValue(project.Manifest.ProjectId, out var projectRootPath))
        {
            return projectRootPath;
        }

        return ModStudioPaths.GetLegacyProjectDirectory(project.Manifest.ProjectId);
    }

    private void RememberProjectLocation(string projectId, string projectRootPath)
    {
        if (string.IsNullOrWhiteSpace(projectId) || string.IsNullOrWhiteSpace(projectRootPath))
        {
            return;
        }

        _projectRootsById[projectId] = ModStudioPaths.NormalizeProjectRootPath(projectRootPath);
    }

    private static string EnsureUniqueSiblingProjectRoot(string targetProjectRootPath)
    {
        if (string.IsNullOrWhiteSpace(targetProjectRootPath))
        {
            return string.Empty;
        }

        var normalizedTarget = ModStudioPaths.NormalizeProjectRootPath(targetProjectRootPath);
        if (!Directory.Exists(normalizedTarget) && !File.Exists(ModStudioPaths.GetProjectFilePathFromRoot(normalizedTarget)))
        {
            return normalizedTarget;
        }

        var parentDirectory = Path.GetDirectoryName(normalizedTarget);
        if (string.IsNullOrWhiteSpace(parentDirectory))
        {
            parentDirectory = ModStudioPaths.LegacyProjectsPath;
        }

        var baseName = Path.GetFileName(normalizedTarget);
        for (var index = 2; index < 1000; index++)
        {
            var candidate = Path.Combine(parentDirectory, $"{baseName}-{index}");
            if (!Directory.Exists(candidate) && !File.Exists(ModStudioPaths.GetProjectFilePathFromRoot(candidate)))
            {
                return candidate;
            }
        }

        return Path.Combine(parentDirectory, $"{baseName}-{Guid.NewGuid():N}");
    }

    private static EntityOverrideEnvelope CloneOverride(EntityOverrideEnvelope source)
    {
        return new EntityOverrideEnvelope
        {
            EntityKind = source.EntityKind,
            EntityId = source.EntityId,
            BehaviorSource = source.BehaviorSource,
            GraphId = source.GraphId,
            MonsterAi = MonsterAiDefinitionCloner.Clone(source.MonsterAi),
            Notes = source.Notes,
            Metadata = new Dictionary<string, string>(source.Metadata, StringComparer.Ordinal),
            Assets = source.Assets.Select(CloneAsset).ToList()
        };
    }

    private static Graph.BehaviorGraphDefinition CloneGraph(Graph.BehaviorGraphDefinition source)
    {
        return new Graph.BehaviorGraphDefinition
        {
            GraphId = source.GraphId,
            Name = source.Name,
            Description = source.Description,
            Version = source.Version,
            EntityKind = source.EntityKind,
            EntryNodeId = source.EntryNodeId,
            Nodes = source.Nodes
                .Select(node => new Graph.BehaviorGraphNodeDefinition
                {
                    NodeId = node.NodeId,
                    NodeType = node.NodeType,
                    DisplayName = node.DisplayName,
                    Description = node.Description,
                    Properties = new Dictionary<string, string>(node.Properties, StringComparer.Ordinal)
                })
                .ToList(),
            Connections = source.Connections
                .Select(connection => new Graph.BehaviorGraphConnectionDefinition
                {
                    FromNodeId = connection.FromNodeId,
                    FromPortId = connection.FromPortId,
                    ToNodeId = connection.ToNodeId,
                    ToPortId = connection.ToPortId
                })
                .ToList(),
            Metadata = new Dictionary<string, string>(source.Metadata, StringComparer.Ordinal)
        };
    }

    private static AssetRef CloneAsset(AssetRef source)
    {
        return new AssetRef
        {
            Id = source.Id,
            SourceType = source.SourceType,
            LogicalRole = source.LogicalRole,
            SourcePath = source.SourcePath,
            ManagedPath = source.ManagedPath,
            PackagePath = source.PackagePath,
            FileName = source.FileName
        };
    }

    private static void RehomeManagedAssetsForDuplicate(EditorProject sourceProject, EditorProject duplicatedProject, string sourceRootPath, string targetRootPath)
    {
        Directory.CreateDirectory(ModStudioPaths.GetProjectAssetsDirectoryFromRoot(targetRootPath));

        var pathMap = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var asset in duplicatedProject.ProjectAssets
                     .Concat(duplicatedProject.Overrides.SelectMany(overrideEnvelope => overrideEnvelope.Assets))
                     .Where(asset => !string.IsNullOrWhiteSpace(asset.ManagedPath))
                     .GroupBy(asset => asset.Id, StringComparer.Ordinal)
                     .Select(group => group.First()))
        {
            if (!File.Exists(asset.ManagedPath))
            {
                continue;
            }

            var sourceManagedPath = asset.ManagedPath;
            var copyManagedAsset = sourceManagedPath.StartsWith(sourceRootPath, StringComparison.OrdinalIgnoreCase) ||
                                   sourceManagedPath.StartsWith(ModStudioPaths.RootPath, StringComparison.OrdinalIgnoreCase);
            if (!copyManagedAsset)
            {
                continue;
            }

            var fileName = string.IsNullOrWhiteSpace(asset.FileName)
                ? Path.GetFileName(sourceManagedPath)
                : asset.FileName;
            var targetDirectory = Path.Combine(ModStudioPaths.GetProjectAssetsDirectoryFromRoot(targetRootPath), asset.Id);
            Directory.CreateDirectory(targetDirectory);
            var targetPath = Path.Combine(targetDirectory, fileName);
            File.Copy(sourceManagedPath, targetPath, overwrite: true);
            pathMap[asset.Id] = targetPath;
        }

        if (pathMap.Count == 0)
        {
            return;
        }

        foreach (var asset in duplicatedProject.ProjectAssets)
        {
            if (pathMap.TryGetValue(asset.Id, out var targetPath))
            {
                asset.ManagedPath = targetPath;
            }
        }

        foreach (var asset in duplicatedProject.Overrides.SelectMany(overrideEnvelope => overrideEnvelope.Assets))
        {
            if (pathMap.TryGetValue(asset.Id, out var targetPath))
            {
                asset.ManagedPath = targetPath;
            }
        }
    }
}
