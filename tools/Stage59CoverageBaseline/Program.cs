#nullable enable
using System.Text.Json;
using System.Text.RegularExpressions;
using MegaCrit.Sts2.Core.Models;
using STS2_Editor.Scripts.Editor.Core.Models;
using STS2_Editor.Scripts.Editor.Graph;
using STS2_Editor.Scripts.Editor.Runtime;

var repoRoot = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])
    ? Path.GetFullPath(args[0])
    : FindRepoRoot(Environment.CurrentDirectory);

InitializeModelDb();

var coverageRoot = Path.Combine(repoRoot, "coverage", "baseline");
var docsReferenceRoot = Path.Combine(repoRoot, "docs", "reference");
Directory.CreateDirectory(coverageRoot);
Directory.CreateDirectory(docsReferenceRoot);

var baselineJsonPath = Path.Combine(coverageRoot, "coverage_baseline.json");
var descriptionJsonPath = Path.Combine(coverageRoot, "description_semantics_baseline.json");
var debugAuditJsonPath = Path.Combine(coverageRoot, "debug_fallback_audit.json");
var baselineMarkdownPath = Path.Combine(docsReferenceRoot, "coverage_baseline.md");
var descriptionMarkdownPath = Path.Combine(docsReferenceRoot, "description_semantics_baseline.md");
var debugAuditMarkdownPath = Path.Combine(docsReferenceRoot, "debug_fallback_audit.md");
var passiveAllowlistMarkdownPath = Path.Combine(docsReferenceRoot, "passive_only_allowlist.md");

var sourceEntries = ScanSourceEntries(repoRoot);
var entries = ResolveRuntimeCoverageEntries(sourceEntries);
var descriptionBaseline = ScanDescriptionSemantics(repoRoot);
var debugFallbackEntries = entries.Where(IsDebugFallbackOnly).ToList();
var passiveOnlyEntries = entries.Where(IsPassiveOnly).ToList();

var jsonOptions = new JsonSerializerOptions
{
    WriteIndented = true
};

File.WriteAllText(baselineJsonPath, JsonSerializer.Serialize(entries, jsonOptions));
File.WriteAllText(descriptionJsonPath, JsonSerializer.Serialize(descriptionBaseline, jsonOptions));
File.WriteAllText(debugAuditJsonPath, JsonSerializer.Serialize(debugFallbackEntries, jsonOptions));
File.WriteAllText(baselineMarkdownPath, BuildCoverageMarkdown(entries));
File.WriteAllText(descriptionMarkdownPath, BuildDescriptionMarkdown(descriptionBaseline));
File.WriteAllText(debugAuditMarkdownPath, BuildDebugAuditMarkdown(debugFallbackEntries));
File.WriteAllText(passiveAllowlistMarkdownPath, BuildPassiveAllowlistMarkdown(passiveOnlyEntries));

Console.WriteLine("Stage 59 coverage baseline complete");
Console.WriteLine($"Repo: {repoRoot}");
Console.WriteLine($"Entries: {entries.Count}");
Console.WriteLine($"Coverage baseline: {baselineJsonPath}");
Console.WriteLine($"Description baseline: {descriptionJsonPath}");
Console.WriteLine($"Debug fallback audit: {debugAuditJsonPath}");
Console.WriteLine($"Reference report: {baselineMarkdownPath}");
Console.WriteLine($"Description report: {descriptionMarkdownPath}");

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
        // Keep the source-scan fallback when the console environment still
        // cannot fully initialize the game model database.
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

static IReadOnlyDictionary<string, SourceCoverageEntry> ScanSourceEntries(string repoRoot)
{
    var recognizedCalls = BuildRecognizedCalls();
    var results = new Dictionary<string, SourceCoverageEntry>(StringComparer.OrdinalIgnoreCase);

    foreach (var scope in GetSourceScopes(repoRoot))
    {
        if (!Directory.Exists(scope.Path))
        {
            continue;
        }

        foreach (var file in Directory.EnumerateFiles(scope.Path, "*.cs", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(file);
            if (!IsRelevantSourceFile(scope.Kind, source))
            {
                continue;
            }

            var callPairs = ExtractCommandCalls(source);
            var recognized = callPairs.Where(recognizedCalls.Contains).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList();
            var unrecognized = callPairs.Where(pair => !recognizedCalls.Contains(pair)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList();
            var semantics = DetectDescriptionSemantics(source).OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList();
            if (semantics.Count == 0)
            {
                semantics.Add("plain_text");
            }

            var entry = new SourceCoverageEntry
            {
                EntityKind = scope.Kind,
                EntityId = Path.GetFileNameWithoutExtension(file),
                SourcePath = Path.GetRelativePath(repoRoot, file).Replace('\\', '/'),
                CommandCalls = callPairs.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList(),
                RecognizedCalls = recognized,
                UnrecognizedCalls = unrecognized,
                DescriptionSemantics = semantics
            };

            results[BuildEntryKey(scope.Kind, entry.EntityId)] = entry;
        }
    }

    return results;
}

static IReadOnlyList<CoverageEntry> ResolveRuntimeCoverageEntries(IReadOnlyDictionary<string, SourceCoverageEntry> sourceEntries)
{
    var registry = new BehaviorGraphRegistry();
    registry.RegisterBuiltIns();

    var autoGraphService = new NativeBehaviorAutoGraphService();
    var eventCompiler = new EventGraphCompiler();
    var results = new List<CoverageEntry>();

    foreach (var scope in GetRuntimeScopes(sourceEntries))
    {
        foreach (var entityId in scope.EntityIds
                     .Where(id => !string.IsNullOrWhiteSpace(id))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(id => id, StringComparer.OrdinalIgnoreCase))
        {
            var sourceEntry = FindSourceEntry(sourceEntries, scope.KindName, entityId);
            var entry = CreateCoverageEntry(scope.KindName, entityId, sourceEntry);

            NativeBehaviorAutoGraphResult? autoGraphResult;
            try
            {
                if (!autoGraphService.TryCreateGraph(scope.Kind, entityId, out autoGraphResult) ||
                    autoGraphResult?.Graph == null)
                {
                    entry.AutoGraphStatus = "unsupported";
                    entry.FailureReasons = BuildFailureReasons(entry, isPartial: false, graphAvailable: false, validation: null, compileResult: null, autoGraphResult: null);
                    results.Add(entry);
                    continue;
                }
            }
            catch (Exception ex)
            {
                entry.AutoGraphStatus = "unsupported";
                entry.Notes.Add(ex.Message);
                entry.FailureReasons = BuildFailureReasons(entry, isPartial: false, graphAvailable: false, validation: null, compileResult: null, autoGraphResult: null);
                results.Add(entry);
                continue;
            }

            entry.HasExistingGraph = true;
            entry.GraphId = autoGraphResult.Graph.GraphId;
            entry.GraphNodeTypes = autoGraphResult.Graph.Nodes
                .Select(node => node.NodeType)
                .Where(nodeType => !string.IsNullOrWhiteSpace(nodeType))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(nodeType => nodeType, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var validation = registry.Validate(autoGraphResult.Graph);
            EventGraphValidationResult? compileResult = null;
            if (scope.Kind == ModStudioEntityKind.Event)
            {
                compileResult = eventCompiler.Compile(autoGraphResult.Graph);
            }

            entry.Notes = autoGraphResult.Notes
                .Concat(validation.Errors)
                .Concat(validation.Warnings)
                .Concat(compileResult?.Errors ?? Array.Empty<string>())
                .Concat(compileResult?.Warnings ?? Array.Empty<string>())
                .Where(note => !string.IsNullOrWhiteSpace(note))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(note => note, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var failureReasons = BuildFailureReasons(entry, autoGraphResult.IsPartial, graphAvailable: true, validation, compileResult, autoGraphResult);
            entry.FailureReasons = failureReasons;
            entry.AutoGraphStatus = DetermineStatus(autoGraphResult.IsPartial, validation, compileResult, failureReasons);
            results.Add(entry);
        }
    }

    return results
        .OrderBy(entry => entry.EntityKind, StringComparer.OrdinalIgnoreCase)
        .ThenBy(entry => entry.EntityId, StringComparer.OrdinalIgnoreCase)
        .ToList();
}

static CoverageEntry CreateCoverageEntry(string kindName, string entityId, SourceCoverageEntry? sourceEntry)
{
    return new CoverageEntry
    {
        EntityKind = kindName,
        EntityId = entityId,
        SourcePath = sourceEntry?.SourcePath ?? string.Empty,
        CommandCalls = sourceEntry?.CommandCalls ?? new List<string>(),
        RecognizedCalls = sourceEntry?.RecognizedCalls ?? new List<string>(),
        UnrecognizedCalls = sourceEntry?.UnrecognizedCalls ?? new List<string>(),
        DescriptionSemantics = sourceEntry?.DescriptionSemantics.Count > 0
            ? sourceEntry.DescriptionSemantics
            : new List<string> { "plain_text" }
    };
}

static SourceCoverageEntry? FindSourceEntry(
    IReadOnlyDictionary<string, SourceCoverageEntry> sourceEntries,
    string kindName,
    string entityId)
{
    if (sourceEntries.TryGetValue(BuildEntryKey(kindName, entityId), out var exactEntry))
    {
        return exactEntry;
    }

    var normalizedEntityId = NormalizeEntityId(entityId);
    return sourceEntries.Values.FirstOrDefault(entry =>
        string.Equals(entry.EntityKind, kindName, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(NormalizeEntityId(entry.EntityId), normalizedEntityId, StringComparison.OrdinalIgnoreCase));
}

static List<string> BuildFailureReasons(
    CoverageEntry entry,
    bool isPartial,
    bool graphAvailable,
    BehaviorGraphValidationResult? validation,
    EventGraphValidationResult? compileResult,
    NativeBehaviorAutoGraphResult? autoGraphResult)
{
    var reasons = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    if (!graphAvailable)
    {
        if (string.Equals(entry.EntityKind, "Event", StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("missing_event_compiler_mapping");
        }
        else if (entry.UnrecognizedCalls.Count > 0)
        {
            reasons.Add("missing_auto_import_mapping");
        }
        else
        {
            reasons.Add("missing_graph");
        }
    }

    if (graphAvailable && IsDebugFallbackOnly(entry) && !IsPassiveOnly(entry))
    {
        reasons.Add("placeholder_graph");
    }

    if (validation != null && !validation.IsValid)
    {
        reasons.Add("invalid_graph");
    }

    if (compileResult != null && (!compileResult.IsValid || compileResult.Errors.Count > 0))
    {
        reasons.Add("missing_event_compiler_mapping");
    }

    if (entry.UnrecognizedCalls.Count > 0)
    {
        if (!string.Equals(entry.EntityKind, "Event", StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("missing_auto_import_mapping");
        }
    }

    if (entry.DescriptionSemantics.Contains("smart_description", StringComparer.OrdinalIgnoreCase) &&
        autoGraphResult == null)
    {
        reasons.Add("missing_description_semantics");
    }

    return reasons
        .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
        .ToList();
}

static string DetermineStatus(
    bool isPartial,
    BehaviorGraphValidationResult validation,
    EventGraphValidationResult? compileResult,
    IReadOnlyCollection<string> failureReasons)
{
    if (!validation.IsValid)
    {
        return "partial";
    }

    if (compileResult != null && (!compileResult.IsValid || compileResult.Errors.Count > 0))
    {
        return "partial";
    }

    if (failureReasons.Contains("placeholder_graph", StringComparer.OrdinalIgnoreCase))
    {
        return "unsupported";
    }

    if (failureReasons.Count > 0)
    {
        return "partial";
    }

    return "supported";
}

static DescriptionBaseline ScanDescriptionSemantics(string repoRoot)
{
    var baseline = new DescriptionBaseline();
    var localizationRoot = Path.Combine(repoRoot, "STS2_Proj", "localization");
    if (!Directory.Exists(localizationRoot))
    {
        return baseline;
    }

    var tokenRegex = new Regex(@"\{(?<token>[A-Za-z0-9_]+)(?::(?<formatter>[A-Za-z0-9_]+)\(\))?\}", RegexOptions.Compiled);
    var allowedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "cards.json",
        "relics.json",
        "potions.json",
        "events.json",
        "enchantments.json"
    };

    foreach (var file in Directory.EnumerateFiles(localizationRoot, "*.json", SearchOption.AllDirectories)
                 .Where(path => allowedFiles.Contains(Path.GetFileName(path))))
    {
        var content = File.ReadAllText(file);
        foreach (Match match in tokenRegex.Matches(content))
        {
            if (!match.Success)
            {
                continue;
            }

            var token = match.Groups["token"].Value;
            if (!string.IsNullOrWhiteSpace(token))
            {
                baseline.Tokens.Add(token);
            }

            var formatter = match.Groups["formatter"].Value;
            if (!string.IsNullOrWhiteSpace(formatter))
            {
                baseline.Formatters.Add(formatter);
            }
        }
    }

    foreach (var file in Directory.EnumerateFiles(Path.Combine(repoRoot, "sts2"), "*.cs", SearchOption.AllDirectories))
    {
        var content = File.ReadAllText(file);
        if (content.Contains("SmartDescription", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("smartDescription", StringComparison.OrdinalIgnoreCase))
        {
            baseline.HasSmartDescription = true;
            baseline.SmartDescriptionSources.Add(Path.GetRelativePath(repoRoot, file).Replace('\\', '/'));
        }
    }

    baseline.Tokens = baseline.Tokens
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
        .ToList();
    baseline.Formatters = baseline.Formatters
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
        .ToList();
    baseline.SmartDescriptionSources = baseline.SmartDescriptionSources
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
        .ToList();
    return baseline;
}

static IReadOnlyCollection<string> BuildRecognizedCalls()
{
    return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "DamageCmd.Attack",
        "CreatureCmd.GainBlock",
        "CreatureCmd.Heal",
        "CreatureCmd.Damage",
        "CreatureCmd.LoseBlock",
        "CreatureCmd.GainMaxHp",
        "CreatureCmd.LoseMaxHp",
        "CreatureCmd.Kill",
        "CreatureCmd.Stun",
        "CreatureCmd.SetCurrentHp",
        "CardPileCmd.Draw",
        "CardPileCmd.Add",
        "CardPileCmd.AddGeneratedCardToCombat",
        "CardPileCmd.AddGeneratedCardsToCombat",
        "CardPileCmd.AddToCombatAndPreview",
        "CardPileCmd.AddCurseToDeck",
        "CardPileCmd.AddCursesToDeck",
        "CardPileCmd.RemoveFromDeck",
        "CardPileCmd.AutoPlayFromDrawPile",
        "CardPileCmd.Shuffle",
        "CardPileCmd.ShuffleIfNecessary",
        "CardCmd.Discard",
        "CardCmd.DiscardAndDraw",
        "CardCmd.Exhaust",
        "CardCmd.Transform",
        "CardCmd.TransformTo",
        "CardCmd.TransformToRandom",
        "CardCmd.Upgrade",
        "CardCmd.Downgrade",
        "CardCmd.Enchant",
        "CardCmd.RemoveKeyword",
        "CardCmd.AutoPlay",
        "CardCmd.ApplyKeyword",
        "CardCmd.ApplySingleTurnSly",
        "PlayerCmd.GainEnergy",
        "PlayerCmd.GainGold",
        "PlayerCmd.GainStars",
        "PlayerCmd.LoseEnergy",
        "PlayerCmd.LoseGold",
        "PlayerCmd.GainMaxPotionCount",
        "PlayerCmd.AddPet",
        "PlayerCmd.CompleteQuest",
        "PlayerCmd.MimicRestSiteHeal",
        "PlayerCmd.EndTurn",
        "PowerCmd.Apply",
        "PowerCmd.Remove",
        "PowerCmd.ModifyAmount",
        "RelicCmd.Obtain",
        "RelicCmd.Remove",
        "RelicCmd.Replace",
        "RelicCmd.Melt",
        "PotionCmd.TryToProcure",
        "PotionCmd.Discard",
        "OrbCmd.Channel",
        "OrbCmd.AddSlots",
        "OrbCmd.RemoveSlots",
        "OrbCmd.EvokeNext",
        "OrbCmd.Passive",
        "OstyCmd.Summon"
    };
}

static IReadOnlyCollection<string> ExtractCommandCalls(string source)
{
    var regex = new Regex(@"(?<type>DamageCmd|CreatureCmd|CardCmd|CardPileCmd|PlayerCmd|PowerCmd|RelicCmd|PotionCmd|OrbCmd|OstyCmd)\.(?<name>[A-Za-z0-9_]+)", RegexOptions.Compiled);
    var ignoredCalls = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "CreatureCmd.TriggerAnim",
        "CardCmd.Preview",
        "CardCmd.PreviewCardPileAdd"
    };

    return regex.Matches(source)
        .Select(match => $"{match.Groups["type"].Value}.{match.Groups["name"].Value}")
        .Where(call => !ignoredCalls.Contains(call))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();
}

static IReadOnlyCollection<string> DetectDescriptionSemantics(string source)
{
    var semantics = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    if (Regex.IsMatch(source, @"DamageVar|BlockVar|HealVar|CardsVar|GoldVar|StarsVar|EnergyVar|DynamicVars", RegexOptions.IgnoreCase))
    {
        semantics.Add("dynamic_var");
    }

    if (Regex.IsMatch(source, @"Calculated[A-Za-z]+Var|CalculationBase|CalculationExtra|ExtraDamage", RegexOptions.IgnoreCase))
    {
        semantics.Add("formula_var");
    }

    if (Regex.IsMatch(source, @"SmartDescription|smartDescription", RegexOptions.IgnoreCase))
    {
        semantics.Add("smart_description");
    }

    return semantics;
}

static IReadOnlyList<SourceCoverageScope> GetSourceScopes(string repoRoot)
{
    return new[]
    {
        new SourceCoverageScope("Card", Path.Combine(repoRoot, "sts2", "MegaCrit.Sts2.Core.Models.Cards")),
        new SourceCoverageScope("Relic", Path.Combine(repoRoot, "sts2", "MegaCrit.Sts2.Core.Models.Relics")),
        new SourceCoverageScope("Potion", Path.Combine(repoRoot, "sts2", "MegaCrit.Sts2.Core.Models.Potions")),
        new SourceCoverageScope("Event", Path.Combine(repoRoot, "sts2", "MegaCrit.Sts2.Core.Models.Events")),
        new SourceCoverageScope("Enchantment", Path.Combine(repoRoot, "sts2", "MegaCrit.Sts2.Core.Models.Enchantments"))
    };
}

static IReadOnlyList<RuntimeCoverageScope> GetRuntimeScopes(IReadOnlyDictionary<string, SourceCoverageEntry> sourceEntries)
{
    return new[]
    {
        new RuntimeCoverageScope("Card", ModStudioEntityKind.Card, GetEntityIds(sourceEntries, "Card", () => ModelDb.AllCards.Select(model => model.Id.Entry))),
        new RuntimeCoverageScope("Relic", ModStudioEntityKind.Relic, GetEntityIds(sourceEntries, "Relic", () => ModelDb.AllRelics.Select(model => model.Id.Entry))),
        new RuntimeCoverageScope("Potion", ModStudioEntityKind.Potion, GetEntityIds(sourceEntries, "Potion", () => ModelDb.AllPotions.Select(model => model.Id.Entry))),
        new RuntimeCoverageScope("Event", ModStudioEntityKind.Event, GetEntityIds(sourceEntries, "Event", () => ModelDb.AllEvents.Select(model => model.Id.Entry))),
        new RuntimeCoverageScope("Enchantment", ModStudioEntityKind.Enchantment, GetEntityIds(sourceEntries, "Enchantment", () => ModelDb.DebugEnchantments.Select(model => model.Id.Entry)))
    };
}

static IReadOnlyList<string> GetEntityIds(
    IReadOnlyDictionary<string, SourceCoverageEntry> sourceEntries,
    string kindName,
    Func<IEnumerable<string>> runtimeFactory)
{
    var ids = new List<string>();
    var normalizedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    try
    {
        foreach (var entityId in runtimeFactory())
        {
            if (string.IsNullOrWhiteSpace(entityId))
            {
                continue;
            }

            var normalizedEntityId = NormalizeEntityId(entityId);
            if (normalizedIds.Add(normalizedEntityId))
            {
                ids.Add(entityId);
            }
        }
    }
    catch
    {
        // The console harness cannot always bootstrap every ModelDb collection.
        // Source-scan ids remain a valid fallback for coverage auditing.
    }

    foreach (var entry in sourceEntries.Values.Where(entry => string.Equals(entry.EntityKind, kindName, StringComparison.OrdinalIgnoreCase)))
    {
        var normalizedEntityId = NormalizeEntityId(entry.EntityId);
        if (normalizedIds.Add(normalizedEntityId))
        {
            ids.Add(entry.EntityId);
        }
    }

    return ids
        .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
        .ToList();
}

static string NormalizeEntityId(string entityId)
{
    return string.IsNullOrWhiteSpace(entityId)
        ? string.Empty
        : entityId.Replace("_", string.Empty, StringComparison.Ordinal).Trim().ToUpperInvariant();
}

static string BuildCoverageMarkdown(IReadOnlyList<CoverageEntry> entries)
{
    var lines = new List<string>
    {
        "# Coverage Baseline",
        "",
        "## Summary"
    };

    foreach (var group in entries.GroupBy(entry => entry.EntityKind, StringComparer.OrdinalIgnoreCase))
    {
        var total = group.Count();
        var supported = group.Count(entry => entry.AutoGraphStatus == "supported");
        var partial = group.Count(entry => entry.AutoGraphStatus == "partial");
        var unsupported = group.Count(entry => entry.AutoGraphStatus == "unsupported");
        lines.Add($"- `{group.Key}`: total `{total}`, supported `{supported}`, partial `{partial}`, unsupported `{unsupported}`");
    }

    lines.Add("");
    lines.Add("## Top Missing Reasons");
    foreach (var pair in entries.SelectMany(entry => entry.FailureReasons).GroupBy(value => value, StringComparer.OrdinalIgnoreCase).OrderByDescending(group => group.Count()))
    {
        lines.Add($"- `{pair.Key}`: `{pair.Count()}`");
    }

    lines.Add("");
    lines.Add("## Uncovered Samples");
    foreach (var entry in entries.Where(entry => entry.AutoGraphStatus != "supported").Take(80))
    {
        var calls = entry.UnrecognizedCalls.Count == 0 ? "-" : string.Join(", ", entry.UnrecognizedCalls);
        var reasons = entry.FailureReasons.Count == 0 ? "-" : string.Join(", ", entry.FailureReasons);
        var semantics = entry.DescriptionSemantics.Count == 0 ? "plain_text" : string.Join(", ", entry.DescriptionSemantics);
        var notes = entry.Notes.Count == 0 ? "-" : string.Join(" | ", entry.Notes.Take(3));
        lines.Add($"- `{entry.EntityKind}:{entry.EntityId}` -> status `{entry.AutoGraphStatus}`, reasons `{reasons}`, unrecognized calls `{calls}`, semantics `{semantics}`, notes `{notes}`");
    }

    return string.Join(Environment.NewLine, lines);
}

static string BuildDebugAuditMarkdown(IReadOnlyList<CoverageEntry> entries)
{
    var lines = new List<string>
    {
        "# Debug Fallback Audit",
        "",
        "## Summary"
    };

    if (entries.Count == 0)
    {
        lines.Add("- none");
        return string.Join(Environment.NewLine, lines);
    }

    foreach (var group in entries.GroupBy(entry => entry.EntityKind, StringComparer.OrdinalIgnoreCase).OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
    {
        lines.Add($"- `{group.Key}`: `{group.Count()}`");
    }

    lines.Add("");
    lines.Add("## Entities");
    foreach (var entry in entries.OrderBy(entry => entry.EntityKind, StringComparer.OrdinalIgnoreCase).ThenBy(entry => entry.EntityId, StringComparer.OrdinalIgnoreCase))
    {
        lines.Add($"- `{entry.EntityKind}:{entry.EntityId}` -> `{entry.GraphId}`");
    }

    return string.Join(Environment.NewLine, lines);
}

static string BuildPassiveAllowlistMarkdown(IReadOnlyList<CoverageEntry> entries)
{
    var lines = new List<string>
    {
        "# Passive Only Allowlist",
        "",
        "## Summary"
    };

    if (entries.Count == 0)
    {
        lines.Add("- none");
        return string.Join(Environment.NewLine, lines);
    }

    foreach (var entry in entries.OrderBy(entry => entry.EntityKind, StringComparer.OrdinalIgnoreCase).ThenBy(entry => entry.EntityId, StringComparer.OrdinalIgnoreCase))
    {
        lines.Add($"- `{entry.EntityKind}:{entry.EntityId}`");
    }

    return string.Join(Environment.NewLine, lines);
}

static bool IsDebugFallbackOnly(CoverageEntry entry)
{
    var gameplayNodeTypes = entry.GraphNodeTypes
        .Where(nodeType => !string.Equals(nodeType, "flow.entry", StringComparison.OrdinalIgnoreCase) &&
                           !string.Equals(nodeType, "flow.exit", StringComparison.OrdinalIgnoreCase))
        .ToList();
    return gameplayNodeTypes.Count == 1 &&
           string.Equals(gameplayNodeTypes[0], "debug.log", StringComparison.OrdinalIgnoreCase);
}

static bool IsPassiveOnly(CoverageEntry entry)
{
    return entry.EntityKind switch
    {
        "Card" => entry.EntityId is
            "ASCENDERS_BANE" or
            "CLUMSY" or
            "CURSE_OF_THE_BELL" or
            "DAZED" or
            "DEPRECATED_CARD" or
            "FOLLY" or
            "GREED" or
            "INJURY" or
            "POOR_SLEEP" or
            "SOOT" or
            "WOUND" or
            "WRITHE",
        "Potion" => entry.EntityId is "DEPRECATED_POTION",
        "Event" => false,
        "Enchantment" => false,
        "Relic" => entry.EntityId is "CIRCLET" or "DEPRECATED_RELIC" or "FAKE_MERCHANTS_RUG" or "WONGO_CUSTOMER_APPRECIATION_BADGE",
        _ => false
    };
}

static string BuildDescriptionMarkdown(DescriptionBaseline baseline)
{
    var lines = new List<string>
    {
        "# Description Semantics Baseline",
        "",
        "## Summary",
        $"- Tokens discovered: `{baseline.Tokens.Count}`",
        $"- Formatters discovered: `{baseline.Formatters.Count}`",
        $"- smartDescription present: `{baseline.HasSmartDescription}`",
        ""
    };

    lines.Add("## Tokens");
    foreach (var token in baseline.Tokens)
    {
        lines.Add($"- `{token}`");
    }

    lines.Add("");
    lines.Add("## Formatters");
    foreach (var formatter in baseline.Formatters)
    {
        lines.Add($"- `{formatter}`");
    }

    lines.Add("");
    lines.Add("## smartDescription Sources");
    if (baseline.SmartDescriptionSources.Count == 0)
    {
        lines.Add("- none");
    }
    else
    {
        foreach (var source in baseline.SmartDescriptionSources.Take(80))
        {
            lines.Add($"- `{source}`");
        }
    }

    return string.Join(Environment.NewLine, lines);
}

static string BuildEntryKey(string kind, string entityId)
{
    return $"{kind}:{entityId}";
}

static bool IsRelevantSourceFile(string kind, string source)
{
    var modelType = kind switch
    {
        "Card" => "CardModel",
        "Potion" => "PotionModel",
        "Relic" => "RelicModel",
        "Event" => "EventModel",
        "Enchantment" => "EnchantmentModel",
        _ => string.Empty
    };

    return string.IsNullOrWhiteSpace(modelType) ||
           source.Contains($": {modelType}", StringComparison.Ordinal) ||
           source.Contains($":{modelType}", StringComparison.Ordinal);
}

internal sealed record SourceCoverageScope(string Kind, string Path);

internal sealed record RuntimeCoverageScope(string KindName, ModStudioEntityKind Kind, IEnumerable<string> EntityIds);

internal sealed class SourceCoverageEntry
{
    public string EntityKind { get; set; } = string.Empty;

    public string EntityId { get; set; } = string.Empty;

    public string SourcePath { get; set; } = string.Empty;

    public List<string> CommandCalls { get; set; } = new();

    public List<string> RecognizedCalls { get; set; } = new();

    public List<string> UnrecognizedCalls { get; set; } = new();

    public List<string> DescriptionSemantics { get; set; } = new();
}

internal sealed class CoverageEntry
{
    public string EntityKind { get; set; } = string.Empty;

    public string EntityId { get; set; } = string.Empty;

    public string SourcePath { get; set; } = string.Empty;

    public bool HasExistingGraph { get; set; }

    public string AutoGraphStatus { get; set; } = "unsupported";

    public string GraphId { get; set; } = string.Empty;

    public List<string> GraphNodeTypes { get; set; } = new();

    public List<string> CommandCalls { get; set; } = new();

    public List<string> RecognizedCalls { get; set; } = new();

    public List<string> UnrecognizedCalls { get; set; } = new();

    public List<string> FailureReasons { get; set; } = new();

    public List<string> DescriptionSemantics { get; set; } = new();

    public List<string> Notes { get; set; } = new();
}

internal sealed class DescriptionBaseline
{
    public List<string> Tokens { get; set; } = new();

    public List<string> Formatters { get; set; } = new();

    public bool HasSmartDescription { get; set; }

    public List<string> SmartDescriptionSources { get; set; } = new();
}
