#nullable enable
using System.Text.Json;
using MegaCrit.Sts2.Core.Models;
using STS2_Editor.Scripts.Editor.Core.Models;
using STS2_Editor.Scripts.Editor.Graph;

var repoRoot = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])
    ? Path.GetFullPath(args[0])
    : FindRepoRoot(Environment.CurrentDirectory);

InitializeModelDb();

var coverageRoot = Path.Combine(repoRoot, "coverage", "description-roundtrip");
var docsReferenceRoot = Path.Combine(repoRoot, "docs", "reference");
Directory.CreateDirectory(coverageRoot);
Directory.CreateDirectory(docsReferenceRoot);

var jsonPath = Path.Combine(coverageRoot, "description_roundtrip.json");
var markdownPath = Path.Combine(docsReferenceRoot, "description_roundtrip.md");

var sourceEntries = ScanSourceEntries(repoRoot);
var entries = RunDescriptionRoundtrip(sourceEntries);

var jsonOptions = new JsonSerializerOptions
{
    WriteIndented = true
};

File.WriteAllText(jsonPath, JsonSerializer.Serialize(entries, jsonOptions));
File.WriteAllText(markdownPath, BuildMarkdown(entries));

Console.WriteLine("Stage 61 description roundtrip complete");
Console.WriteLine($"Repo: {repoRoot}");
Console.WriteLine($"Entries: {entries.Count}");
Console.WriteLine($"JSON: {jsonPath}");
Console.WriteLine($"Markdown: {markdownPath}");

return 0;

static void InitializeModelDb()
{
    try
    {
        ModelDb.Init();
        ModelDb.InitIds();
    }
    catch
    {
        // Keep source-driven fallback behavior in console harnesses.
    }
}

static string FindRepoRoot(string startDirectory)
{
    var directory = new DirectoryInfo(startDirectory);
    while (directory != null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "STS2_Editor.csproj")))
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    throw new DirectoryNotFoundException("Could not locate repository root containing STS2_Editor.csproj.");
}

static IReadOnlyDictionary<string, SourceEntry> ScanSourceEntries(string repoRoot)
{
    var results = new Dictionary<string, SourceEntry>(StringComparer.OrdinalIgnoreCase);
    foreach (var scope in GetSourceScopes(repoRoot))
    {
        if (!Directory.Exists(scope.Path))
        {
            continue;
        }

        foreach (var file in Directory.EnumerateFiles(scope.Path, "*.cs", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(file);
            if (!IsRelevantSourceFile(scope.KindName, source))
            {
                continue;
            }

            results[$"{scope.KindName}:{Path.GetFileNameWithoutExtension(file)}"] = new SourceEntry
            {
                KindName = scope.KindName,
                EntityId = Path.GetFileNameWithoutExtension(file),
                SourcePath = Path.GetRelativePath(repoRoot, file).Replace('\\', '/')
            };
        }
    }

    return results;
}

static IReadOnlyList<DescriptionRoundtripEntry> RunDescriptionRoundtrip(IReadOnlyDictionary<string, SourceEntry> sourceEntries)
{
    var autoGraphService = new NativeBehaviorAutoGraphService();
    var generator = new GraphDescriptionGenerator();
    var entries = new List<DescriptionRoundtripEntry>();

    foreach (var scope in GetRuntimeScopes(sourceEntries))
    {
        foreach (var entityId in scope.EntityIds.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(id => id, StringComparer.OrdinalIgnoreCase))
        {
            var sourceEntry = FindSourceEntry(sourceEntries, scope.KindName, entityId);
            var entry = new DescriptionRoundtripEntry
            {
                EntityKind = scope.KindName,
                EntityId = entityId,
                SourcePath = sourceEntry?.SourcePath ?? string.Empty
            };

            if (!autoGraphService.TryCreateGraph(scope.Kind, entityId, out var autoGraph) || autoGraph?.Graph == null)
            {
                entry.Status = "unsupported";
                entry.Notes.Add("Auto-graph generation failed.");
                entries.Add(entry);
                continue;
            }

            entry.GraphId = autoGraph.Graph.GraphId;
            entry.NodeTypes = autoGraph.Graph.Nodes
                .Select(node => node.NodeType)
                .Where(nodeType => !string.IsNullOrWhiteSpace(nodeType))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(nodeType => nodeType, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var sourceModel = ResolveSourceModel(scope.Kind, entityId);
            var previewContext = BuildPreviewContext(scope.Kind, entityId, sourceModel);
            var generation = generator.Generate(autoGraph.Graph, sourceModel, previewContext);

            entry.TemplateDescription = string.IsNullOrWhiteSpace(generation.TemplateDescription)
                ? generation.PreviewDescription
                : generation.TemplateDescription;
            entry.PreviewDescription = generation.PreviewDescription;
            entry.Description = generation.Description;
            entry.UnsupportedNodeTypes = generation.UnsupportedNodeTypes.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
            entry.Notes = autoGraph.Notes
                .Concat(string.IsNullOrWhiteSpace(entry.PreviewDescription) ? new[] { "Preview description was empty." } : Array.Empty<string>())
                .Concat(generation.UnsupportedNodeTypes.Count > 0 ? new[] { $"Unsupported node types: {string.Join(", ", generation.UnsupportedNodeTypes)}" } : Array.Empty<string>())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            entry.Status = generation.IsComplete &&
                           !string.IsNullOrWhiteSpace(entry.PreviewDescription)
                ? "supported"
                : "partial";
            entries.Add(entry);
        }
    }

    return entries
        .OrderBy(entry => entry.EntityKind, StringComparer.OrdinalIgnoreCase)
        .ThenBy(entry => entry.EntityId, StringComparer.OrdinalIgnoreCase)
        .ToList();
}

static AbstractModel? ResolveSourceModel(ModStudioEntityKind kind, string entityId)
{
    try
    {
        return kind switch
        {
            ModStudioEntityKind.Card => ModelDb.AllCards.FirstOrDefault(candidate => string.Equals(candidate.Id.Entry, entityId, StringComparison.OrdinalIgnoreCase)),
            ModStudioEntityKind.Relic => ModelDb.AllRelics.FirstOrDefault(candidate => string.Equals(candidate.Id.Entry, entityId, StringComparison.OrdinalIgnoreCase)),
            ModStudioEntityKind.Potion => ModelDb.AllPotions.FirstOrDefault(candidate => string.Equals(candidate.Id.Entry, entityId, StringComparison.OrdinalIgnoreCase)),
            ModStudioEntityKind.Event => ModelDb.AllEvents.FirstOrDefault(candidate => string.Equals(candidate.Id.Entry, entityId, StringComparison.OrdinalIgnoreCase)),
            ModStudioEntityKind.Enchantment => ModelDb.DebugEnchantments.FirstOrDefault(candidate => string.Equals(candidate.Id.Entry, entityId, StringComparison.OrdinalIgnoreCase)),
            _ => null
        };
    }
    catch
    {
        return null;
    }
}

static DynamicPreviewContext BuildPreviewContext(ModStudioEntityKind kind, string entityId, AbstractModel? sourceModel)
{
    const decimal block = 8m;
    const decimal stars = 3m;
    const decimal energy = 3m;
    const int hand = 3;
    const int drawPile = 10;
    const int discardPile = 5;
    const int exhaustPile = 1;
    const decimal missingHp = 12m;

    var preview = new DynamicPreviewContext
    {
        EntityKind = kind,
        EntityId = entityId,
        Upgraded = sourceModel is CardModel card && card.IsUpgraded,
        TargetSelector = "current_target",
        CurrentBlock = block,
        CurrentStars = stars,
        CurrentEnergy = energy,
        HandCount = hand,
        DrawPileCount = drawPile,
        DiscardPileCount = discardPile,
        ExhaustPileCount = exhaustPile,
        MissingHp = missingHp
    };

    preview.FormulaMultipliers["hand_count"] = hand;
    preview.FormulaMultipliers["cards"] = hand;
    preview.FormulaMultipliers["stars"] = stars;
    preview.FormulaMultipliers["energy"] = energy;
    preview.FormulaMultipliers["current_block"] = block;
    preview.FormulaMultipliers["draw_pile"] = drawPile;
    preview.FormulaMultipliers["discard_pile"] = discardPile;
    preview.FormulaMultipliers["exhaust_pile"] = exhaustPile;
    preview.FormulaMultipliers["missing_hp"] = missingHp;
    return preview;
}

static SourceEntry? FindSourceEntry(IReadOnlyDictionary<string, SourceEntry> sourceEntries, string kindName, string entityId)
{
    if (sourceEntries.TryGetValue($"{kindName}:{entityId}", out var exact))
    {
        return exact;
    }

    var normalizedEntityId = NormalizeEntityId(entityId);
    return sourceEntries.Values.FirstOrDefault(entry =>
        string.Equals(entry.KindName, kindName, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(NormalizeEntityId(entry.EntityId), normalizedEntityId, StringComparison.OrdinalIgnoreCase));
}

static IReadOnlyList<SourceScope> GetSourceScopes(string repoRoot)
{
    return new[]
    {
        new SourceScope("Card", Path.Combine(repoRoot, "sts2", "MegaCrit.Sts2.Core.Models.Cards")),
        new SourceScope("Relic", Path.Combine(repoRoot, "sts2", "MegaCrit.Sts2.Core.Models.Relics")),
        new SourceScope("Potion", Path.Combine(repoRoot, "sts2", "MegaCrit.Sts2.Core.Models.Potions")),
        new SourceScope("Event", Path.Combine(repoRoot, "sts2", "MegaCrit.Sts2.Core.Models.Events")),
        new SourceScope("Enchantment", Path.Combine(repoRoot, "sts2", "MegaCrit.Sts2.Core.Models.Enchantments"))
    };
}

static IReadOnlyList<RuntimeScope> GetRuntimeScopes(IReadOnlyDictionary<string, SourceEntry> sourceEntries)
{
    return new[]
    {
        new RuntimeScope("Card", ModStudioEntityKind.Card, GetEntityIds(sourceEntries, "Card", () => ModelDb.AllCards.Select(model => model.Id.Entry))),
        new RuntimeScope("Relic", ModStudioEntityKind.Relic, GetEntityIds(sourceEntries, "Relic", () => ModelDb.AllRelics.Select(model => model.Id.Entry))),
        new RuntimeScope("Potion", ModStudioEntityKind.Potion, GetEntityIds(sourceEntries, "Potion", () => ModelDb.AllPotions.Select(model => model.Id.Entry))),
        new RuntimeScope("Event", ModStudioEntityKind.Event, GetEntityIds(sourceEntries, "Event", () => ModelDb.AllEvents.Select(model => model.Id.Entry))),
        new RuntimeScope("Enchantment", ModStudioEntityKind.Enchantment, GetEntityIds(sourceEntries, "Enchantment", () => ModelDb.DebugEnchantments.Select(model => model.Id.Entry)))
    };
}

static IReadOnlyList<string> GetEntityIds(IReadOnlyDictionary<string, SourceEntry> sourceEntries, string kindName, Func<IEnumerable<string>> runtimeFactory)
{
    var ids = new List<string>();
    var normalized = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    try
    {
        foreach (var entityId in runtimeFactory())
        {
            if (string.IsNullOrWhiteSpace(entityId))
            {
                continue;
            }

            if (normalized.Add(NormalizeEntityId(entityId)))
            {
                ids.Add(entityId);
            }
        }
    }
    catch
    {
        // Source fallback still works in console harnesses.
    }

    foreach (var sourceEntry in sourceEntries.Values.Where(entry => string.Equals(entry.KindName, kindName, StringComparison.OrdinalIgnoreCase)))
    {
        if (normalized.Add(NormalizeEntityId(sourceEntry.EntityId)))
        {
            ids.Add(sourceEntry.EntityId);
        }
    }

    return ids.OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToList();
}

static string NormalizeEntityId(string entityId)
{
    return string.IsNullOrWhiteSpace(entityId)
        ? string.Empty
        : entityId.Replace("_", string.Empty, StringComparison.Ordinal).Trim().ToUpperInvariant();
}

static bool IsRelevantSourceFile(string kindName, string source)
{
    var modelType = kindName switch
    {
        "Card" => "CardModel",
        "Relic" => "RelicModel",
        "Potion" => "PotionModel",
        "Event" => "EventModel",
        "Enchantment" => "EnchantmentModel",
        _ => string.Empty
    };

    return string.IsNullOrWhiteSpace(modelType) ||
           source.Contains($": {modelType}", StringComparison.Ordinal) ||
           source.Contains($":{modelType}", StringComparison.Ordinal);
}

static string BuildMarkdown(IReadOnlyList<DescriptionRoundtripEntry> entries)
{
    var lines = new List<string>
    {
        "# Description Roundtrip",
        "",
        "## Summary"
    };

    foreach (var group in entries.GroupBy(entry => entry.EntityKind, StringComparer.OrdinalIgnoreCase))
    {
        var total = group.Count();
        var supported = group.Count(entry => entry.Status == "supported");
        var partial = group.Count(entry => entry.Status == "partial");
        lines.Add($"- `{group.Key}`: total `{total}`, supported `{supported}`, partial `{partial}`");
    }

    lines.Add("");
    lines.Add("## Incomplete Samples");
    var incomplete = entries.Where(entry => entry.Status != "supported").Take(40).ToList();
    if (incomplete.Count == 0)
    {
        lines.Add("- none");
    }
    else
    {
        foreach (var entry in incomplete)
        {
            var unsupported = entry.UnsupportedNodeTypes.Count == 0 ? "-" : string.Join(", ", entry.UnsupportedNodeTypes);
            var notes = entry.Notes.Count == 0 ? "-" : string.Join(" | ", entry.Notes.Take(3));
            lines.Add($"- `{entry.EntityKind}:{entry.EntityId}` -> unsupported nodes `{unsupported}`, notes `{notes}`");
        }
    }

    return string.Join(Environment.NewLine, lines);
}

internal sealed record SourceScope(string KindName, string Path);

internal sealed record RuntimeScope(string KindName, ModStudioEntityKind Kind, IReadOnlyList<string> EntityIds);

internal sealed class SourceEntry
{
    public string KindName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
}

internal sealed class DescriptionRoundtripEntry
{
    public string EntityKind { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public string Status { get; set; } = "partial";
    public string GraphId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TemplateDescription { get; set; } = string.Empty;
    public string PreviewDescription { get; set; } = string.Empty;
    public List<string> NodeTypes { get; set; } = new();
    public List<string> UnsupportedNodeTypes { get; set; } = new();
    public List<string> Notes { get; set; } = new();
}
