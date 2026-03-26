using System.Text.Json;

namespace STS2_Editor.Scripts.Editor.AI;

public static class AiProtocolParser
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static bool TryParseEnvelope(string rawText, out AiAssistantEnvelope envelope, out string error)
    {
        envelope = new AiAssistantEnvelope();
        error = string.Empty;

        var jsonText = ExtractJson(rawText);
        if (string.IsNullOrWhiteSpace(jsonText))
        {
            error = "AI response did not contain a JSON object.";
            return false;
        }

        try
        {
            envelope = JsonSerializer.Deserialize<AiAssistantEnvelope>(jsonText, Options) ?? new AiAssistantEnvelope();
            envelope.Type = (envelope.Type ?? string.Empty).Trim().ToLowerInvariant();
            if (envelope.Type is not ("reply" or "query" or "edit_plan"))
            {
                error = $"Unsupported AI response type '{envelope.Type}'.";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static string SerializeQueryResult(object value)
    {
        return JsonSerializer.Serialize(value, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    private static string ExtractJson(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return string.Empty;
        }

        var trimmed = rawText.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var firstLineEnd = trimmed.IndexOf('\n');
            if (firstLineEnd >= 0)
            {
                trimmed = trimmed[(firstLineEnd + 1)..];
            }

            var closingFenceIndex = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (closingFenceIndex >= 0)
            {
                trimmed = trimmed[..closingFenceIndex];
            }
        }

        var firstBraceIndex = trimmed.IndexOf('{');
        var lastBraceIndex = trimmed.LastIndexOf('}');
        if (firstBraceIndex < 0 || lastBraceIndex <= firstBraceIndex)
        {
            return string.Empty;
        }

        return trimmed.Substring(firstBraceIndex, lastBraceIndex - firstBraceIndex + 1);
    }
}
