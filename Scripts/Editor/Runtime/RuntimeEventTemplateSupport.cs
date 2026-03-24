using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.ValueProps;

namespace STS2_Editor.Scripts.Editor.Runtime;

internal static class RuntimeEventTemplateSupport
{
    private const string StartPageMetadataKey = "event_start_page_id";
    private const string EventPagePrefix = "event_page.";
    private const string EventOptionPrefix = "event_option.";
    private const string ResumePageEncounterStateKey = "modstudio.resume_page_id";

    private static readonly MethodInfo SetEventStateMethod = AccessTools.Method(
        typeof(EventModel),
        "SetEventState",
        new[] { typeof(LocString), typeof(IEnumerable<EventOption>) })!;
    private static readonly MethodInfo? SetEventOptionIsProceedMethod = AccessTools.PropertySetter(typeof(EventOption), nameof(EventOption.IsProceed));
    private static readonly MethodInfo? EnterRoomWithoutExitingCurrentRoomMethod = AccessTools.Method(
        typeof(RunManager),
        nameof(RunManager.EnterRoomWithoutExitingCurrentRoom),
        new[] { typeof(AbstractRoom), typeof(bool) });
    private static readonly MethodInfo? EventNodeSetter = AccessTools.PropertySetter(typeof(EventModel), "Node");
        private static readonly FieldInfo? EnteringEventCombatField = AccessTools.Field(typeof(EventModel), "EnteringEventCombat");
        private static readonly ConditionalWeakTable<EventModel, RuntimeEventTemplateState> RuntimeStates = new();
        private static readonly Dictionary<string, string> PersistedResumePageIds = new(StringComparer.Ordinal);
        private static readonly object PersistedResumePageSync = new();
    private const string RewardKindKey = "reward_kind";
    private const string RewardAmountKey = "reward_amount";
    private const string RewardTargetKey = "reward_target";
    private const string RewardPropsKey = "reward_props";
    private const string RewardPowerIdKey = "reward_power_id";

    public static bool HasTemplate(EventModel eventModel)
    {
        return TryGetTemplate(eventModel.Id.Entry, out _);
    }

    public static bool TryHandleSetInitialEventState(EventModel eventModel, bool isPreFinished)
    {
        if (!TryGetTemplate(eventModel.Id.Entry, out var template))
        {
            return false;
        }

        var initialPageId = ResolveStartPageId(template, isPreFinished);
        Log.Info($"[ModStudio.Event] Initializing template event {eventModel.Id.Entry} -> page {initialPageId}");
        return TrySetPage(eventModel, template, initialPageId);
    }

    public static bool TryHandleResume(EventModel eventModel, AbstractRoom exitedRoom)
    {
        if (!TryGetTemplate(eventModel.Id.Entry, out var template))
        {
            return false;
        }

        var state = RuntimeStates.GetOrCreateValue(eventModel);
        var resumePageId = state.PendingResumePageId;
        if (string.IsNullOrWhiteSpace(resumePageId))
        {
            resumePageId = TakePersistedResumePage(eventModel.Id.Entry);
        }

        if (string.IsNullOrWhiteSpace(resumePageId))
        {
            return false;
        }

        state.PendingResumePageId = null;
        state.LastExitedRoomType = exitedRoom.RoomType.ToString();
        Log.Info($"[ModStudio.Event] Resuming template event {eventModel.Id.Entry} -> page {resumePageId}");
        return TrySetPage(eventModel, template, resumePageId!);
    }

    public static void TryWriteCombatRoomState(CombatRoom room, SerializableRoom serializableRoom)
    {
        if (room.ParentEventId == null)
        {
            return;
        }

        var resumePageId = PeekPersistedResumePage(room.ParentEventId.Entry);
        if (string.IsNullOrWhiteSpace(resumePageId))
        {
            return;
        }

        serializableRoom.EncounterState[ResumePageEncounterStateKey] = resumePageId;
    }

    public static void TryReadCombatRoomState(SerializableRoom serializableRoom)
    {
        if (serializableRoom.ParentEventId == null ||
            !serializableRoom.EncounterState.TryGetValue(ResumePageEncounterStateKey, out var resumePageId) ||
            string.IsNullOrWhiteSpace(resumePageId))
        {
            return;
        }

        SetPersistedResumePage(serializableRoom.ParentEventId.Entry, resumePageId);
    }

    public static bool TryGetEventText(string eventId, string key, out string value)
    {
        value = string.Empty;
        if (!TryGetTemplate(eventId, out var template))
        {
            return false;
        }

        if (TryMatchPageDescriptionKey(key, eventId, out var pageId))
        {
            if (TryGetPage(template, pageId, out var page) && !string.IsNullOrWhiteSpace(page.Description))
            {
                value = page.Description;
                return true;
            }

            return false;
        }

        if (TryMatchOptionTextKey(key, eventId, "title", out pageId, out var optionId))
        {
            if (TryGetOption(template, pageId, optionId, out var option) && !string.IsNullOrWhiteSpace(option.Title))
            {
                value = option.Title;
                return true;
            }

            return false;
        }

        if (TryMatchOptionTextKey(key, eventId, "description", out pageId, out optionId))
        {
            if (TryGetOption(template, pageId, optionId, out var option) && !string.IsNullOrWhiteSpace(option.Description))
            {
                value = option.Description;
                return true;
            }

            return false;
        }

        return false;
    }

    private static string ResolveStartPageId(RuntimeEventTemplateDefinition template, bool isPreFinished)
    {
        if (isPreFinished && template.Pages.ContainsKey("DONE"))
        {
            return "DONE";
        }

        if (!string.IsNullOrWhiteSpace(template.StartPageId))
        {
            return template.StartPageId;
        }

        return template.Pages.ContainsKey("INITIAL")
            ? "INITIAL"
            : template.Pages.Keys.FirstOrDefault() ?? "INITIAL";
    }

    private static bool TrySetPage(EventModel eventModel, RuntimeEventTemplateDefinition template, string pageId)
    {
        if (!TryGetPage(template, pageId, out var page))
        {
            Log.Warn($"Mod Studio event template could not find page '{pageId}' for event '{eventModel.Id.Entry}'.");
            return false;
        }

        var options = BuildOptions(eventModel, template, page).ToList();
        var descriptionKey = BuildPageDescriptionKey(eventModel.Id.Entry, page.PageId);
        var description = new LocString("events", descriptionKey);
        SetEventStateMethod.Invoke(eventModel, new object[] { description, options });
        Log.Info($"[ModStudio.Event] Applied template page {eventModel.Id.Entry}:{page.PageId} options={options.Count}");

        var state = RuntimeStates.GetOrCreateValue(eventModel);
        state.CurrentPageId = page.PageId;
        return true;
    }

    private static IEnumerable<EventOption> BuildOptions(
        EventModel eventModel,
        RuntimeEventTemplateDefinition template,
        RuntimeEventTemplatePageDefinition page)
    {
        foreach (var option in page.GetOrderedOptions())
        {
            var optionKey = BuildOptionRootKey(eventModel.Id.Entry, page.PageId, option.OptionId);
            var eventOption = new EventOption(
                eventModel,
                () => ExecuteOptionAsync(eventModel, template, page.PageId, option),
                BuildOptionTitleLocString(optionKey),
                BuildOptionDescriptionLocString(optionKey),
                optionKey,
                Array.Empty<IHoverTip>());

            if (option.IsProceed)
            {
                SetEventOptionIsProceedMethod?.Invoke(eventOption, new object?[] { true });
            }

            if (!option.ShouldSaveChoiceToHistory)
            {
                eventOption.ThatWontSaveToChoiceHistory();
            }

            yield return eventOption;
        }
    }

    private static async Task ExecuteOptionAsync(
        EventModel eventModel,
        RuntimeEventTemplateDefinition template,
        string currentPageId,
        RuntimeEventTemplateOptionDefinition option)
    {
        Log.Info($"[ModStudio.Event] Executing option {eventModel.Id.Entry}:{currentPageId}:{option.OptionId}");
        await ApplyRewardAsync(eventModel, option);
        if (!string.IsNullOrWhiteSpace(option.NextPageId))
        {
            TrySetPage(eventModel, template, option.NextPageId);
        }

        if (!string.IsNullOrWhiteSpace(option.EncounterId))
        {
            await StartCombatAsync(eventModel, option);
            return;
        }

        if (option.IsProceed && string.IsNullOrWhiteSpace(option.NextPageId))
        {
            Log.Info($"[ModStudio.Event] Proceeding out of template event {eventModel.Id.Entry} via option {option.OptionId}");
            await NEventRoom.Proceed();
        }
    }

    private static async Task ApplyRewardAsync(EventModel eventModel, RuntimeEventTemplateOptionDefinition option)
    {
        if (eventModel.Owner == null || string.IsNullOrWhiteSpace(option.RewardKind))
        {
            return;
        }

        var rewardKind = option.RewardKind.Trim().ToLowerInvariant();
        var amount = ParseDecimal(option.RewardAmount, 1m);
        var target = ResolveRewardTarget(eventModel, option);
        switch (rewardKind)
        {
            case "gold":
                await PlayerCmd.GainGold(amount, eventModel.Owner);
                return;
            case "energy":
                await PlayerCmd.GainEnergy(amount, eventModel.Owner);
                return;
            case "stars":
                await PlayerCmd.GainStars(amount, eventModel.Owner);
                return;
            case "block":
                if (target != null)
                {
                    await CreatureCmd.GainBlock(target, amount, ParseValueProps(option.RewardProps), cardPlay: null);
                }
                return;
            case "heal":
                if (target != null)
                {
                    await CreatureCmd.Heal(target, amount);
                }
                return;
            case "max_hp":
                if (target != null)
                {
                    await CreatureCmd.GainMaxHp(target, amount);
                }
                return;
            case "power":
                if (target != null)
                {
                    var powerId = option.RewardPowerId;
                    if (!string.IsNullOrWhiteSpace(powerId))
                    {
                        var canonicalPower = ModelDb.AllPowers.FirstOrDefault(item =>
                            string.Equals(item.Id.Entry, powerId, StringComparison.OrdinalIgnoreCase));
                        if (canonicalPower != null)
                        {
                            await PowerCmd.Apply(canonicalPower.ToMutable(), target, amount, eventModel.Owner.Creature, cardSource: null);
                        }
                    }
                }
                return;
            case "damage":
            case "draw":
            case "card":
            case "relic":
            case "potion":
            case "remove_card":
            case "special_card":
                Log.Warn($"[ModStudio.Event] Reward kind '{rewardKind}' on event '{eventModel.Id.Entry}' is not supported for immediate runtime application.");
                return;
            default:
                Log.Warn($"[ModStudio.Event] Unknown reward kind '{rewardKind}' on event '{eventModel.Id.Entry}'.");
                return;
        }
    }

    private static Creature? ResolveRewardTarget(EventModel eventModel, RuntimeEventTemplateOptionDefinition option)
    {
        if (eventModel.Owner == null)
        {
            return null;
        }

        var selector = string.IsNullOrWhiteSpace(option.RewardTarget)
            ? "self"
            : option.RewardTarget.Trim().ToLowerInvariant();
        return selector switch
        {
            "self" or "owner" or "owner_creature" or "source_creature" => eventModel.Owner.Creature,
            "current_target" or "target" => eventModel.Owner.Creature,
            _ => eventModel.Owner.Creature
        };
    }

    private static async Task StartCombatAsync(EventModel eventModel, RuntimeEventTemplateOptionDefinition option)
    {
        if (eventModel.Owner?.RunState == null)
        {
            Log.Warn($"Mod Studio event template could not start combat for '{eventModel.Id.Entry}' because owner/run state was missing.");
            return;
        }

        var encounter = ModelDb.AllEncounters.FirstOrDefault(item =>
            string.Equals(item.Id.Entry, option.EncounterId, StringComparison.OrdinalIgnoreCase));
        if (encounter == null)
        {
            Log.Warn($"Mod Studio event template could not resolve encounter '{option.EncounterId}' for event '{eventModel.Id.Entry}'.");
            return;
        }

        var state = RuntimeStates.GetOrCreateValue(eventModel);
        state.PendingResumePageId = option.ResumePageId;
        SetPersistedResumePage(eventModel.Id.Entry, option.ResumePageId);
        Log.Info($"[ModStudio.Event] Starting template combat {eventModel.Id.Entry} encounter={option.EncounterId} resume={option.ResumePageId ?? "<none>"}");

        InvokeEnteringEventCombat(eventModel);
        EventNodeSetter?.Invoke(eventModel, new object?[] { null });

        if (!LocalContext.IsMe(eventModel.Owner))
        {
            return;
        }

        var room = new CombatRoom(encounter.ToMutable(), eventModel.Owner.RunState)
        {
            ParentEventId = eventModel.Id,
            ShouldResumeParentEventAfterCombat = !string.IsNullOrWhiteSpace(option.ResumePageId)
        };

        if (EnterRoomWithoutExitingCurrentRoomMethod?.Invoke(RunManager.Instance, new object[] { room, true }) is Task task)
        {
            await task;
        }
    }

    private static void InvokeEnteringEventCombat(EventModel eventModel)
    {
        if (EnteringEventCombatField?.GetValue(eventModel) is Action callback)
        {
            callback.Invoke();
        }
    }

    private static bool TryGetTemplate(string eventId, out RuntimeEventTemplateDefinition template)
    {
        template = new RuntimeEventTemplateDefinition();
        if (!ModStudioBootstrap.RuntimeRegistry.TryGetOverride(Core.Models.ModStudioEntityKind.Event, eventId, out var envelope) ||
            envelope?.Metadata == null)
        {
            return false;
        }

        return TryBuildTemplate(envelope.Metadata, out template);
    }

    private static bool TryBuildTemplate(IReadOnlyDictionary<string, string> metadata, out RuntimeEventTemplateDefinition template)
    {
        template = new RuntimeEventTemplateDefinition
        {
            StartPageId = metadata.TryGetValue(StartPageMetadataKey, out var startPageId) ? startPageId.Trim() : "INITIAL"
        };

        foreach (var pair in metadata)
        {
            if (pair.Key.StartsWith(EventPagePrefix, StringComparison.Ordinal))
            {
                ParsePageMetadata(template, pair.Key, pair.Value);
                continue;
            }

            if (pair.Key.StartsWith(EventOptionPrefix, StringComparison.Ordinal))
            {
                ParseOptionMetadata(template, pair.Key, pair.Value);
            }
        }

        return template.Pages.Count > 0;
    }

    private static void ParsePageMetadata(RuntimeEventTemplateDefinition template, string key, string value)
    {
        var segments = key.Split('.', StringSplitOptions.None);
        if (segments.Length < 3)
        {
            return;
        }

        var pageId = segments[1];
        var property = string.Join('.', segments.Skip(2));
        var page = template.GetOrAddPage(pageId);
        switch (property)
        {
            case "description":
                page.Description = value;
                break;
            case "option_order":
                page.OptionOrder = value
                    .Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .ToList();
                break;
        }
    }

    private static void ParseOptionMetadata(RuntimeEventTemplateDefinition template, string key, string value)
    {
        var segments = key.Split('.', StringSplitOptions.None);
        if (segments.Length < 4)
        {
            return;
        }

        var pageId = segments[1];
        var optionId = segments[2];
        var property = string.Join('.', segments.Skip(3));
        var option = template.GetOrAddPage(pageId).GetOrAddOption(optionId);
        switch (property)
        {
            case "title":
                option.Title = value;
                break;
            case "description":
                option.Description = value;
                break;
            case "next_page_id":
                option.NextPageId = value.Trim();
                break;
            case "encounter_id":
                option.EncounterId = value.Trim();
                break;
            case "resume_page_id":
                option.ResumePageId = value.Trim();
                break;
            case "is_proceed":
                if (bool.TryParse(value, out var isProceed))
                {
                    option.IsProceed = isProceed;
                }
                break;
            case "save_choice_to_history":
                if (bool.TryParse(value, out var shouldSaveChoice))
                {
                    option.ShouldSaveChoiceToHistory = shouldSaveChoice;
                }
                break;
            case RewardKindKey:
                option.RewardKind = value.Trim();
                break;
            case RewardAmountKey:
                option.RewardAmount = value.Trim();
                break;
            case RewardTargetKey:
                option.RewardTarget = value.Trim();
                break;
            case RewardPropsKey:
                option.RewardProps = value.Trim();
                break;
            case RewardPowerIdKey:
                option.RewardPowerId = value.Trim();
                break;
        }
    }

    private static decimal ParseDecimal(string? value, decimal fallback)
    {
        return decimal.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static ValueProp ParseValueProps(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue) || string.Equals(rawValue, "none", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        return Enum.TryParse<ValueProp>(rawValue.Replace("|", ",", StringComparison.Ordinal), ignoreCase: true, out var props)
            ? props
            : 0;
    }

    private static bool TryGetPage(RuntimeEventTemplateDefinition template, string pageId, out RuntimeEventTemplatePageDefinition page)
    {
        return template.Pages.TryGetValue(pageId, out page!);
    }

    private static bool TryGetOption(RuntimeEventTemplateDefinition template, string pageId, string optionId, out RuntimeEventTemplateOptionDefinition option)
    {
        option = null!;
        return TryGetPage(template, pageId, out var page) &&
               page.Options.TryGetValue(optionId, out option!);
    }

    private static bool TryMatchPageDescriptionKey(string key, string eventId, out string pageId)
    {
        const string prefix = ".pages.";
        const string suffix = ".description";
        pageId = string.Empty;
        if (!key.StartsWith(eventId + prefix, StringComparison.Ordinal) ||
            !key.EndsWith(suffix, StringComparison.Ordinal))
        {
            return false;
        }

        var middle = key[(eventId.Length + prefix.Length)..^suffix.Length];
        if (middle.Contains(".options.", StringComparison.Ordinal))
        {
            return false;
        }

        pageId = middle;
        return !string.IsNullOrWhiteSpace(pageId);
    }

    private static bool TryMatchOptionTextKey(string key, string eventId, string propertyName, out string pageId, out string optionId)
    {
        pageId = string.Empty;
        optionId = string.Empty;
        var prefix = $"{eventId}.pages.";
        var marker = ".options.";
        var suffix = "." + propertyName;
        if (!key.StartsWith(prefix, StringComparison.Ordinal) ||
            !key.EndsWith(suffix, StringComparison.Ordinal))
        {
            return false;
        }

        var body = key[prefix.Length..^suffix.Length];
        var markerIndex = body.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex <= 0)
        {
            return false;
        }

        pageId = body[..markerIndex];
        optionId = body[(markerIndex + marker.Length)..];
        return !string.IsNullOrWhiteSpace(pageId) && !string.IsNullOrWhiteSpace(optionId);
    }

    private static string BuildPageDescriptionKey(string eventId, string pageId)
    {
        return $"{eventId}.pages.{pageId}.description";
    }

    private static string BuildOptionRootKey(string eventId, string pageId, string optionId)
    {
        return $"{eventId}.pages.{pageId}.options.{optionId}";
    }

    private static LocString BuildOptionTitleLocString(string optionKey)
    {
        return new LocString("events", optionKey + ".title");
    }

    private static LocString BuildOptionDescriptionLocString(string optionKey)
    {
        return new LocString("events", optionKey + ".description");
    }

    private static void SetPersistedResumePage(string eventId, string? pageId)
    {
        lock (PersistedResumePageSync)
        {
            if (string.IsNullOrWhiteSpace(pageId))
            {
                PersistedResumePageIds.Remove(eventId);
            }
            else
            {
                PersistedResumePageIds[eventId] = pageId;
            }
        }
    }

    private static string? PeekPersistedResumePage(string eventId)
    {
        lock (PersistedResumePageSync)
        {
            return PersistedResumePageIds.TryGetValue(eventId, out var pageId) ? pageId : null;
        }
    }

    private static string? TakePersistedResumePage(string eventId)
    {
        lock (PersistedResumePageSync)
        {
            if (!PersistedResumePageIds.TryGetValue(eventId, out var pageId))
            {
                return null;
            }

            PersistedResumePageIds.Remove(eventId);
            return pageId;
        }
    }

    private sealed class RuntimeEventTemplateState
    {
        public string? CurrentPageId { get; set; }

        public string? PendingResumePageId { get; set; }

        public string? LastExitedRoomType { get; set; }
    }

    private sealed class RuntimeEventTemplateDefinition
    {
        public string StartPageId { get; set; } = "INITIAL";

        public Dictionary<string, RuntimeEventTemplatePageDefinition> Pages { get; } = new(StringComparer.Ordinal);

        public RuntimeEventTemplatePageDefinition GetOrAddPage(string pageId)
        {
            if (!Pages.TryGetValue(pageId, out var page))
            {
                page = new RuntimeEventTemplatePageDefinition { PageId = pageId };
                Pages[pageId] = page;
            }

            return page;
        }
    }

    private sealed class RuntimeEventTemplatePageDefinition
    {
        public string PageId { get; set; } = "INITIAL";

        public string Description { get; set; } = string.Empty;

        public List<string> OptionOrder { get; set; } = new();

        public Dictionary<string, RuntimeEventTemplateOptionDefinition> Options { get; } = new(StringComparer.Ordinal);

        public RuntimeEventTemplateOptionDefinition GetOrAddOption(string optionId)
        {
            if (!Options.TryGetValue(optionId, out var option))
            {
                option = new RuntimeEventTemplateOptionDefinition { OptionId = optionId };
                Options[optionId] = option;
            }

            return option;
        }

        public IEnumerable<RuntimeEventTemplateOptionDefinition> GetOrderedOptions()
        {
            if (OptionOrder.Count == 0)
            {
                foreach (var option in Options.Values.OrderBy(option => option.OptionId, StringComparer.Ordinal))
                {
                    yield return option;
                }

                yield break;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var optionId in OptionOrder)
            {
                if (Options.TryGetValue(optionId, out var option))
                {
                    seen.Add(optionId);
                    yield return option;
                }
            }

            foreach (var option in Options.Values.OrderBy(option => option.OptionId, StringComparer.Ordinal))
            {
                if (seen.Add(option.OptionId))
                {
                    yield return option;
                }
            }
        }
    }

    private sealed class RuntimeEventTemplateOptionDefinition
    {
        public string OptionId { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string? NextPageId { get; set; }

        public string? EncounterId { get; set; }

        public string? ResumePageId { get; set; }

        public bool IsProceed { get; set; }

        public bool ShouldSaveChoiceToHistory { get; set; } = true;

        public string? RewardKind { get; set; }

        public string? RewardAmount { get; set; }

        public string? RewardTarget { get; set; }

        public string? RewardProps { get; set; }

        public string? RewardPowerId { get; set; }
    }
}
