using STS2_Editor.Scripts.Editor.Core.Models;

namespace STS2_Editor.Scripts.Editor.Graph;

public static class BehaviorGraphPaletteFilter
{
    private static readonly string[] CommonPrefixes =
    [
        "flow.",
        "value.",
        "debug."
    ];

    private static readonly string[] GameplayPrefixes =
    [
        "combat.",
        "player.",
        "card.",
        "cardpile.",
        "orb.",
        "power.",
        "creature."
    ];

    private static readonly string[] EventPrefixes =
    [
        "event.",
        "reward."
    ];

    private static readonly string[] MonsterPrefixes =
    [
        "combat.",
        "creature.",
        "power.",
        "monster."
    ];

    public static IEnumerable<BehaviorGraphNodeDefinitionDescriptor> FilterForEntityKind(
        IEnumerable<BehaviorGraphNodeDefinitionDescriptor> definitions,
        ModStudioEntityKind kind)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        return definitions.Where(definition => IsAllowed(kind, definition.NodeType));
    }

    public static bool IsAllowed(ModStudioEntityKind kind, string? nodeType)
    {
        if (string.IsNullOrWhiteSpace(nodeType))
        {
            return false;
        }

        if (HasAnyPrefix(nodeType, CommonPrefixes))
        {
            return true;
        }

        return kind switch
        {
            ModStudioEntityKind.Card => HasAnyPrefix(nodeType, GameplayPrefixes),
            ModStudioEntityKind.Relic => HasAnyPrefix(nodeType, GameplayPrefixes),
            ModStudioEntityKind.Potion => HasAnyPrefix(nodeType, GameplayPrefixes),
            ModStudioEntityKind.Enchantment => HasAnyPrefix(nodeType, GameplayPrefixes),
            ModStudioEntityKind.Event => HasAnyPrefix(nodeType, EventPrefixes),
            ModStudioEntityKind.Monster => HasAnyPrefix(nodeType, MonsterPrefixes),
            _ => false
        };
    }

    private static bool HasAnyPrefix(string nodeType, IEnumerable<string> prefixes)
    {
        return prefixes.Any(prefix => nodeType.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }
}
