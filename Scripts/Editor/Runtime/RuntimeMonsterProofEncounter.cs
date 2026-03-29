using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace STS2_Editor.Scripts.Editor.Runtime;

internal sealed class RuntimeMonsterProofEncounter : EncounterModel
{
    private readonly string _monsterId;

    public RuntimeMonsterProofEncounter(string monsterId)
    {
        _monsterId = monsterId;
    }

    public override RoomType RoomType => RoomType.Monster;

    public override IEnumerable<MonsterModel> AllPossibleMonsters
    {
        get
        {
            var monster = ResolveMonster();
            return monster == null
                ? Array.Empty<MonsterModel>()
                : [monster];
        }
    }

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        var monster = ResolveMonster()?.ToMutable()
            ?? throw new InvalidOperationException($"Could not resolve proof monster '{_monsterId}'.");
        return [(monster, null)];
    }

    private MonsterModel? ResolveMonster()
    {
        return ModelDb.Monsters.FirstOrDefault(model => string.Equals(model.Id.Entry, _monsterId, StringComparison.OrdinalIgnoreCase));
    }
}
