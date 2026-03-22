using STS2_Editor.Scripts.Editor.Core.Models;

namespace STS2_Editor.Scripts.Editor.Runtime;

internal static class RuntimeOverrideMetadata
{
    public static bool HasMetadata(ModStudioEntityKind kind, string entityId, string key)
    {
        return TryGetMetadataValue(kind, entityId, key, out _);
    }

    public static bool TryGetMetadata(ModStudioEntityKind kind, string entityId, string key, out string value)
    {
        value = string.Empty;
        if (!TryGetMetadataValue(kind, entityId, key, out value))
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(value);
    }

    public static string? GetMetadataOrNull(ModStudioEntityKind kind, string entityId, string key)
    {
        return TryGetMetadata(kind, entityId, key, out var value) ? value : null;
    }

    public static bool TryGetInt(ModStudioEntityKind kind, string entityId, string key, out int value)
    {
        value = 0;
        return TryGetMetadata(kind, entityId, key, out var raw) &&
               int.TryParse(raw, out value);
    }

    public static bool TryGetBool(ModStudioEntityKind kind, string entityId, string key, out bool value)
    {
        value = false;
        return TryGetMetadata(kind, entityId, key, out var raw) &&
               bool.TryParse(raw, out value);
    }

    public static bool TryGetEnum<TEnum>(ModStudioEntityKind kind, string entityId, string key, out TEnum value)
        where TEnum : struct, Enum
    {
        value = default;
        return TryGetMetadata(kind, entityId, key, out var raw) &&
               Enum.TryParse(raw, ignoreCase: true, out value);
    }

    public static IReadOnlyList<string> GetIdList(ModStudioEntityKind kind, string entityId, string key)
    {
        if (!TryGetMetadataValue(kind, entityId, key, out var raw))
        {
            return Array.Empty<string>();
        }

        return raw
            .Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    public static bool TryGetLocalizedText(string table, string key, out string value)
    {
        value = string.Empty;
        if (string.IsNullOrWhiteSpace(table) || string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        return table switch
        {
            "characters" => TryCharacterText(key, out value),
            "cards" => TryCardText(key, out value),
            "relics" => TryRelicText(key, out value),
            "potions" => TryPotionText(key, out value),
            "events" => TryEventText(key, out value),
            "enchantments" => TryEnchantmentText(key, out value),
            _ => false
        };
    }

    private static bool TryCharacterText(string key, out string value)
    {
        value = string.Empty;
        if (TryMatchSimpleKey(key, ".title", out var entityId))
        {
            return TryGetMetadata(ModStudioEntityKind.Character, entityId, "title", out value);
        }

        return false;
    }

    private static bool TryCardText(string key, out string value)
    {
        value = string.Empty;
        if (TryMatchSimpleKey(key, ".title", out var entityId))
        {
            return TryGetMetadata(ModStudioEntityKind.Card, entityId, "title", out value);
        }

        if (TryMatchSimpleKey(key, ".description", out entityId))
        {
            return TryGetMetadata(ModStudioEntityKind.Card, entityId, "description", out value);
        }

        return false;
    }

    private static bool TryRelicText(string key, out string value)
    {
        value = string.Empty;
        if (TryMatchSimpleKey(key, ".title", out var entityId))
        {
            return TryGetMetadata(ModStudioEntityKind.Relic, entityId, "title", out value);
        }

        if (TryMatchSimpleKey(key, ".description", out entityId))
        {
            return TryGetMetadata(ModStudioEntityKind.Relic, entityId, "description", out value);
        }

        if (TryMatchSimpleKey(key, ".eventDescription", out entityId))
        {
            return TryGetMetadata(ModStudioEntityKind.Relic, entityId, "event_description", out value);
        }

        return false;
    }

    private static bool TryPotionText(string key, out string value)
    {
        value = string.Empty;
        if (TryMatchSimpleKey(key, ".title", out var entityId))
        {
            return TryGetMetadata(ModStudioEntityKind.Potion, entityId, "title", out value);
        }

        if (TryMatchSimpleKey(key, ".description", out entityId))
        {
            return TryGetMetadata(ModStudioEntityKind.Potion, entityId, "description", out value);
        }

        return false;
    }

    private static bool TryEventText(string key, out string value)
    {
        value = string.Empty;
        if (TryMatchSimpleKey(key, ".title", out var entityId))
        {
            return TryGetMetadata(ModStudioEntityKind.Event, entityId, "title", out value);
        }

        const string initialDescriptionSuffix = ".pages.INITIAL.description";
        if (TryMatchSimpleKey(key, initialDescriptionSuffix, out entityId))
        {
            return TryGetMetadata(ModStudioEntityKind.Event, entityId, "initial_description", out value);
        }

        if (TryMatchEventPageDescriptionKey(key, out entityId, out _) &&
            RuntimeEventTemplateSupport.TryGetEventText(entityId, key, out value))
        {
            return true;
        }

        if (TryMatchEventOptionTextKey(key, "title", out entityId, out _, out _) &&
            RuntimeEventTemplateSupport.TryGetEventText(entityId, key, out value))
        {
            return true;
        }

        if (TryMatchEventOptionTextKey(key, "description", out entityId, out _, out _) &&
            RuntimeEventTemplateSupport.TryGetEventText(entityId, key, out value))
        {
            return true;
        }

        return false;
    }

    private static bool TryEnchantmentText(string key, out string value)
    {
        value = string.Empty;
        if (TryMatchSimpleKey(key, ".title", out var entityId))
        {
            return TryGetMetadata(ModStudioEntityKind.Enchantment, entityId, "title", out value);
        }

        if (TryMatchSimpleKey(key, ".description", out entityId))
        {
            return TryGetMetadata(ModStudioEntityKind.Enchantment, entityId, "description", out value);
        }

        if (TryMatchSimpleKey(key, ".extraCardText", out entityId))
        {
            return TryGetMetadata(ModStudioEntityKind.Enchantment, entityId, "extra_card_text", out value);
        }

        return false;
    }

    private static bool TryMatchSimpleKey(string key, string suffix, out string entityId)
    {
        entityId = string.Empty;
        if (!key.EndsWith(suffix, StringComparison.Ordinal))
        {
            return false;
        }

        entityId = key[..^suffix.Length];
        return !string.IsNullOrWhiteSpace(entityId);
    }

    private static bool TryMatchEventPageDescriptionKey(string key, out string entityId, out string pageId)
    {
        entityId = string.Empty;
        pageId = string.Empty;
        const string prefix = ".pages.";
        const string suffix = ".description";
        var prefixIndex = key.IndexOf(prefix, StringComparison.Ordinal);
        if (prefixIndex <= 0 || !key.EndsWith(suffix, StringComparison.Ordinal))
        {
            return false;
        }

        var body = key[(prefixIndex + prefix.Length)..^suffix.Length];
        if (body.Contains(".options.", StringComparison.Ordinal))
        {
            return false;
        }

        entityId = key[..prefixIndex];
        pageId = body;
        return !string.IsNullOrWhiteSpace(entityId) && !string.IsNullOrWhiteSpace(pageId);
    }

    private static bool TryMatchEventOptionTextKey(string key, string propertyName, out string entityId, out string pageId, out string optionId)
    {
        entityId = string.Empty;
        pageId = string.Empty;
        optionId = string.Empty;
        const string pagePrefix = ".pages.";
        const string optionsMarker = ".options.";
        var suffix = "." + propertyName;
        var pagePrefixIndex = key.IndexOf(pagePrefix, StringComparison.Ordinal);
        if (pagePrefixIndex <= 0 || !key.EndsWith(suffix, StringComparison.Ordinal))
        {
            return false;
        }

        var body = key[(pagePrefixIndex + pagePrefix.Length)..^suffix.Length];
        var optionsIndex = body.IndexOf(optionsMarker, StringComparison.Ordinal);
        if (optionsIndex <= 0)
        {
            return false;
        }

        entityId = key[..pagePrefixIndex];
        pageId = body[..optionsIndex];
        optionId = body[(optionsIndex + optionsMarker.Length)..];
        return !string.IsNullOrWhiteSpace(entityId) &&
               !string.IsNullOrWhiteSpace(pageId) &&
               !string.IsNullOrWhiteSpace(optionId);
    }

    private static bool TryGetMetadataValue(ModStudioEntityKind kind, string entityId, string key, out string value)
    {
        value = string.Empty;
        if (string.IsNullOrWhiteSpace(entityId) || string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        try
        {
            if (!ModStudioBootstrap.RuntimeRegistry.TryGetOverride(kind, entityId, out var envelope) ||
                envelope is null ||
                envelope.Metadata is null)
            {
                return false;
            }

            return envelope.Metadata.TryGetValue(key, out value!);
        }
        catch
        {
            value = string.Empty;
            return false;
        }
    }
}
