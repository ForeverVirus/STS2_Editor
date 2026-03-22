using STS2_Editor.Scripts.Editor.Core.Models;
using STS2_Editor.Scripts.Editor.Core.Utilities;
using System.Text.Json;

namespace STS2_Editor.Scripts.Editor;

public sealed class EditorProjectStore
{
    public string ProjectsRootPath => ModStudioPaths.ProjectsPath;

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
        var project = new EditorProject
        {
            Manifest = CreateProjectManifest(name, author, description, targetGameVersion),
            SourceOfTruthIsRuntimeModelDb = true
        };

        Save(project);
        return project;
    }

    public void Save(EditorProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (string.IsNullOrWhiteSpace(project.Manifest.ProjectId))
        {
            project.Manifest.ProjectId = Guid.NewGuid().ToString("N");
        }

        project.Manifest.UpdatedAtUtc = DateTimeOffset.UtcNow;

        var projectDirectory = GetProjectDirectory(project.Manifest.ProjectId);
        Directory.CreateDirectory(projectDirectory);
        ModStudioJson.Save(Path.Combine(projectDirectory, "project.json"), project);
    }

    public bool DeleteProject(string projectId)
    {
        if (string.IsNullOrWhiteSpace(projectId))
        {
            return false;
        }

        var projectDirectory = GetProjectDirectory(projectId);
        if (!Directory.Exists(projectDirectory))
        {
            return false;
        }

        Directory.Delete(projectDirectory, recursive: true);
        return true;
    }

    public EditorProject DuplicateProject(string sourceProjectId, string? newName = null)
    {
        if (!TryLoad(sourceProjectId, out var project) || project is null)
        {
            throw new InvalidOperationException($"Project '{sourceProjectId}' could not be loaded for duplication.");
        }

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
            Overrides = project.Overrides
                .Select(CloneOverride)
                .ToList(),
            Graphs = project.Graphs.ToDictionary(
                pair => pair.Key,
                pair => CloneGraph(pair.Value),
                StringComparer.Ordinal),
            ProjectAssets = project.ProjectAssets
                .Select(CloneAsset)
                .ToList(),
            SourceOfTruthIsRuntimeModelDb = project.SourceOfTruthIsRuntimeModelDb
        };

        Save(duplicated);
        return duplicated;
    }

    public bool TryLoad(string projectId, out EditorProject? project)
    {
        project = null;
        var filePath = GetProjectFilePath(projectId);
        if (!File.Exists(filePath))
        {
            return false;
        }

        project = JsonSerializer.Deserialize<EditorProject>(File.ReadAllText(filePath), ModStudioJson.Options);
        return project is not null;
    }

    public IReadOnlyList<EditorProjectManifest> EnumerateProjectManifests()
    {
        if (!Directory.Exists(ProjectsRootPath))
        {
            return Array.Empty<EditorProjectManifest>();
        }

        var manifests = new List<EditorProjectManifest>();
        foreach (var projectDirectory in Directory.EnumerateDirectories(ProjectsRootPath))
        {
            var filePath = Path.Combine(projectDirectory, "project.json");
            if (!File.Exists(filePath))
            {
                continue;
            }

            var project = JsonSerializer.Deserialize<EditorProject>(File.ReadAllText(filePath), ModStudioJson.Options);
            if (project?.Manifest is not null)
            {
                manifests.Add(project.Manifest);
            }
        }

        return manifests
            .OrderBy(manifest => manifest.Name)
            .ThenBy(manifest => manifest.ProjectId)
            .ToList();
    }

    public string GetProjectDirectory(string projectId)
    {
        return Path.Combine(ProjectsRootPath, projectId);
    }

    public string GetProjectFilePath(string projectId)
    {
        return Path.Combine(GetProjectDirectory(projectId), "project.json");
    }

    private static EntityOverrideEnvelope CloneOverride(EntityOverrideEnvelope source)
    {
        return new EntityOverrideEnvelope
        {
            EntityKind = source.EntityKind,
            EntityId = source.EntityId,
            BehaviorSource = source.BehaviorSource,
            GraphId = source.GraphId,
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
}
