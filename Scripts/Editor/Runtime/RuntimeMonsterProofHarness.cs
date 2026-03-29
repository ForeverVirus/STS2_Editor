using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace STS2_Editor.Scripts.Editor.Runtime;

internal static class RuntimeMonsterProofHarness
{
    private const string ForcedMonsterArgPrefix = "--modstudio-proof-monster=";
    private const string ForcedEncounterArgPrefix = "--modstudio-proof-encounter=";
    private const string TargetMonsterArgPrefix = "--modstudio-proof-target-monster=";
    private static readonly string? ForcedMonsterId = ParseForcedMonsterId();
    private static readonly string? ForcedEncounterId = ParseForcedEncounterId();
    private static readonly string? TargetMonsterId = ParseTargetMonsterId();
    private static int _forcedEncounterConsumed;
    private static int _monsterMoveObserved;

    public static bool HasForcedEncounter => !string.IsNullOrWhiteSpace(ForcedEncounterId) || !string.IsNullOrWhiteSpace(ForcedMonsterId);

    public static bool ShouldDelayPlayerActions()
    {
        return HasForcedEncounter && Volatile.Read(ref _monsterMoveObserved) == 0;
    }

    public static bool TryPeekForcedMonsterId(out string monsterId)
    {
        monsterId = ForcedMonsterId ?? string.Empty;
        return !string.IsNullOrWhiteSpace(monsterId) && _forcedEncounterConsumed == 0;
    }

    public static bool TryPeekForcedEncounterId(out string encounterId)
    {
        encounterId = ForcedEncounterId ?? string.Empty;
        return !string.IsNullOrWhiteSpace(encounterId) && _forcedEncounterConsumed == 0;
    }

    public static void MarkForcedEncounterConsumed()
    {
        Interlocked.Exchange(ref _forcedEncounterConsumed, 1);
    }

    public static async Task<bool> TryEnterForcedEncounterAsync(string encounterId)
    {
        var runState = RunManager.Instance.DebugOnlyGetState();
        if (!RunManager.Instance.IsInProgress || runState == null)
        {
            Log.Warn($"[ModStudio.MonsterProof] Could not enter proof encounter '{encounterId}' because no run is active.");
            return false;
        }

        var encounter = ModelDb.AllEncounters.FirstOrDefault(model => string.Equals(model.Id.Entry, encounterId, StringComparison.OrdinalIgnoreCase));
        if (encounter == null)
        {
            Log.Warn($"[ModStudio.MonsterProof] Could not resolve proof encounter '{encounterId}'.");
            return false;
        }

        Log.Info($"[ModStudio.MonsterProof] Entering proof encounter '{encounter.Id.Entry}' from autoslay map handler.");
        runState.AppendToMapPointHistory(MapPointType.Monster, encounter.RoomType, encounter.Id);
        await RunManager.Instance.EnterRoom(new CombatRoom(encounter.ToMutable(), runState));
        return true;
    }

    public static async Task<bool> TryEnterForcedMonsterAsync(string monsterId)
    {
        var runState = RunManager.Instance.DebugOnlyGetState();
        if (!RunManager.Instance.IsInProgress || runState == null)
        {
            Log.Warn($"[ModStudio.MonsterProof] Could not enter proof monster '{monsterId}' because no run is active.");
            return false;
        }

        var monster = ModelDb.Monsters.FirstOrDefault(model => string.Equals(model.Id.Entry, monsterId, StringComparison.OrdinalIgnoreCase));
        if (monster == null)
        {
            Log.Warn($"[ModStudio.MonsterProof] Could not resolve proof monster '{monsterId}'.");
            return false;
        }

        Log.Info($"[ModStudio.MonsterProof] Entering proof monster '{monster.Id.Entry}' from autoslay map handler.");
        runState.AppendToMapPointHistory(MapPointType.Monster, MegaCrit.Sts2.Core.Rooms.RoomType.Monster, ModelId.none);
        var encounter = new RuntimeMonsterProofEncounter(monster.Id.Entry).ToMutable();
        await RunManager.Instance.EnterRoom(new CombatRoom(encounter, runState));
        return true;
    }

    public static void LogMove(string monsterId, string turnId)
    {
        if (!HasForcedEncounter)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(TargetMonsterId) ||
            string.Equals(TargetMonsterId, monsterId, StringComparison.OrdinalIgnoreCase))
        {
            Interlocked.Exchange(ref _monsterMoveObserved, 1);
        }

        Log.Info($"[ModStudio.MonsterProof] MOVE monster={monsterId} turn={turnId}");
    }

    public static void LogForceTransition(string monsterId, string targetTurnId)
    {
        if (!HasForcedEncounter)
        {
            return;
        }

        Log.Info($"[ModStudio.MonsterProof] TRANSITION monster={monsterId} target_turn={targetTurnId}");
    }

    public static void LogForcePhase(string monsterId, string targetPhaseId)
    {
        if (!HasForcedEncounter)
        {
            return;
        }

        Log.Info($"[ModStudio.MonsterProof] PHASE monster={monsterId} target_phase={targetPhaseId}");
    }

    private static string? ParseForcedEncounterId()
    {
        foreach (var arg in Environment.GetCommandLineArgs())
        {
            if (arg.StartsWith(ForcedEncounterArgPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var value = arg[ForcedEncounterArgPrefix.Length..].Trim();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.ToUpperInvariant();
                }
            }
        }

        return null;
    }

    private static string? ParseForcedMonsterId()
    {
        foreach (var arg in Environment.GetCommandLineArgs())
        {
            if (arg.StartsWith(ForcedMonsterArgPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var value = arg[ForcedMonsterArgPrefix.Length..].Trim();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.ToUpperInvariant();
                }
            }
        }

        return null;
    }

    private static string? ParseTargetMonsterId()
    {
        foreach (var arg in Environment.GetCommandLineArgs())
        {
            if (arg.StartsWith(TargetMonsterArgPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var value = arg[TargetMonsterArgPrefix.Length..].Trim();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.ToUpperInvariant();
                }
            }
        }

        return null;
    }
}
