using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace STS2_Editor.Scripts.Editor.Runtime;

internal static class RuntimeProofHarness
{
    private const string ForcedEventArgPrefix = "--modstudio-proof-event=";
    private static readonly string? ForcedEventId = ParseForcedEventId();
    private static int _forcedEventConsumed;

    public static bool HasForcedEvent => !string.IsNullOrWhiteSpace(ForcedEventId);

    public static bool TryPeekForcedEventId(out string eventId)
    {
        eventId = ForcedEventId ?? string.Empty;
        return !string.IsNullOrWhiteSpace(eventId) && _forcedEventConsumed == 0;
    }

    public static void MarkForcedEventConsumed()
    {
        Interlocked.Exchange(ref _forcedEventConsumed, 1);
    }

    public static async Task<bool> TryEnterForcedEventAsync(string eventId)
    {
        var runState = RunManager.Instance.DebugOnlyGetState();
        if (!RunManager.Instance.IsInProgress || runState == null)
        {
            Log.Warn($"[ModStudio.Proof] Could not enter proof event '{eventId}' because no run is active.");
            return false;
        }

        var eventModel = ModelDb.AllEvents
            .Concat<EventModel>(ModelDb.AllAncients)
            .FirstOrDefault(model => string.Equals(model.Id.Entry, eventId, StringComparison.OrdinalIgnoreCase));
        if (eventModel == null)
        {
            Log.Warn($"[ModStudio.Proof] Could not resolve proof event '{eventId}'.");
            return false;
        }

        var player = LocalContext.GetMe(runState);
        if (player == null)
        {
            Log.Warn($"[ModStudio.Proof] Could not enter proof event '{eventId}' because local player was unavailable.");
            return false;
        }

        var mapPointType = eventModel is AncientEventModel ? MapPointType.Ancient : MapPointType.Unknown;
        player.RunState.AppendToMapPointHistory(mapPointType, RoomType.Event, eventModel.Id);
        Log.Info($"[ModStudio.Proof] Entering proof event '{eventModel.Id.Entry}' from autoslay map handler.");
        await RunManager.Instance.EnterRoom(new EventRoom(eventModel));
        return true;
    }

    private static string? ParseForcedEventId()
    {
        foreach (var arg in Environment.GetCommandLineArgs())
        {
            if (arg.StartsWith(ForcedEventArgPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var value = arg[ForcedEventArgPrefix.Length..].Trim();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.ToUpperInvariant();
                }
            }
        }

        return null;
    }
}
