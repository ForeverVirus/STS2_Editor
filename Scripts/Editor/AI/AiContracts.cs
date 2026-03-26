using System.Text.Json.Serialization;
using STS2_Editor.Scripts.Editor.Core.Models;

namespace STS2_Editor.Scripts.Editor.AI;

public sealed class AiClientSettings
{
    public string BaseUrl { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    [JsonIgnore]
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(BaseUrl) &&
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.IsNullOrWhiteSpace(Model);

    public void Normalize()
    {
        BaseUrl = (BaseUrl ?? string.Empty).Trim().TrimEnd('/');
        ApiKey = (ApiKey ?? string.Empty).Trim();
        Model = (Model ?? string.Empty).Trim();
    }

    public static AiClientSettings FromSettings(ModStudioSettings settings)
    {
        var result = new AiClientSettings
        {
            BaseUrl = settings.AiBaseUrl,
            ApiKey = settings.AiApiKey,
            Model = settings.AiModel
        };
        result.Normalize();
        return result;
    }

    public void ApplyTo(ModStudioSettings settings)
    {
        Normalize();
        settings.AiBaseUrl = BaseUrl;
        settings.AiApiKey = ApiKey;
        settings.AiModel = Model;
    }
}

public sealed class AiChatMessage
{
    public string Role { get; set; } = "user";

    public string Content { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    [JsonIgnore]
    public bool IsVisibleInTranscript { get; set; } = true;

    public static AiChatMessage Create(string role, string content, bool visible = true)
    {
        return new AiChatMessage
        {
            Role = role,
            Content = content ?? string.Empty,
            IsVisibleInTranscript = visible
        };
    }
}

public sealed class AiChatSession
{
    public string SessionId { get; set; } = Guid.NewGuid().ToString("N");

    public List<AiChatMessage> Messages { get; } = new();

    public List<string> AppliedSummaries { get; } = new();

    public string RollingSummary { get; set; } = string.Empty;

    public int EstimatedCharacterCount => Messages.Sum(message => message.Content?.Length ?? 0) + (RollingSummary?.Length ?? 0);

    public void AddMessage(AiChatMessage message)
    {
        Messages.Add(message);
    }

    public void AddAppliedSummaries(IEnumerable<string> summaries)
    {
        foreach (var summary in summaries.Where(summary => !string.IsNullOrWhiteSpace(summary)))
        {
            AppliedSummaries.Add(summary);
        }

        while (AppliedSummaries.Count > 24)
        {
            AppliedSummaries.RemoveAt(0);
        }
    }
}

public sealed class AiAssistantEnvelope
{
    public string Type { get; set; } = "reply";

    public string AssistantMessage { get; set; } = string.Empty;

    public bool NeedsClarification { get; set; }

    public List<string> Warnings { get; set; } = new();

    public List<AiQueryRequest> Queries { get; set; } = new();

    public List<AiEditOperation> Operations { get; set; } = new();
}

public sealed class AiQueryRequest
{
    public string QueryType { get; set; } = string.Empty;

    public string EntityKind { get; set; } = string.Empty;

    public string EntityId { get; set; } = string.Empty;

    public string EntityRef { get; set; } = string.Empty;

    public string NodeType { get; set; } = string.Empty;

    public string NodeId { get; set; } = string.Empty;

    public string NodeRef { get; set; } = string.Empty;
}

public sealed class AiEditPlan
{
    public string AssistantMessage { get; set; } = string.Empty;

    public bool NeedsClarification { get; set; }

    public List<string> Warnings { get; set; } = new();

    public List<AiEditOperation> Operations { get; set; } = new();
}

public sealed class AiEditOperation
{
    public string Type { get; set; } = string.Empty;

    public string OpRef { get; set; } = string.Empty;

    public string EntityKind { get; set; } = string.Empty;

    public string EntityId { get; set; } = string.Empty;

    public string EntityRef { get; set; } = string.Empty;

    public Dictionary<string, string> Fields { get; set; } = new(StringComparer.Ordinal);

    public string BehaviorMode { get; set; } = string.Empty;

    public string AssetSourceKind { get; set; } = string.Empty;

    public string AssetValue { get; set; } = string.Empty;

    public string GraphId { get; set; } = string.Empty;

    public string GraphName { get; set; } = string.Empty;

    public string GraphDescription { get; set; } = string.Empty;

    public string NodeType { get; set; } = string.Empty;

    public string NodeId { get; set; } = string.Empty;

    public string NodeRef { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public Dictionary<string, string> Properties { get; set; } = new(StringComparer.Ordinal);

    public string NearNodeId { get; set; } = string.Empty;

    public string NearNodeRef { get; set; } = string.Empty;

    public string FromNodeId { get; set; } = string.Empty;

    public string FromNodeRef { get; set; } = string.Empty;

    public string FromPortId { get; set; } = string.Empty;

    public string ToNodeId { get; set; } = string.Empty;

    public string ToNodeRef { get; set; } = string.Empty;

    public string ToPortId { get; set; } = string.Empty;
}

public sealed class AiPlanPreview
{
    public bool IsValid => ErrorLines.Count == 0;

    public EditorProject ProjectSnapshot { get; set; } = new();

    public AiEditPlan Plan { get; set; } = new();

    public List<string> SummaryLines { get; } = new();

    public List<string> WarningLines { get; } = new();

    public List<string> ErrorLines { get; } = new();

    public string FocusEntityId { get; set; } = string.Empty;

    public ModStudioEntityKind? FocusEntityKind { get; set; }
}

public sealed class AiChatCompletionResult
{
    public bool IsSuccess { get; set; }

    public string ResponseText { get; set; } = string.Empty;

    public string ReasoningText { get; set; } = string.Empty;

    public string ErrorMessage { get; set; } = string.Empty;

    public bool IsContextLengthError { get; set; }

    public int StatusCode { get; set; }
}

public sealed class AiStreamingUpdate
{
    public string ReasoningText { get; set; } = string.Empty;

    public string ContentText { get; set; } = string.Empty;

    public bool HasReasoningDelta { get; set; }

    public bool HasContentDelta { get; set; }

    public int ChunkIndex { get; set; }
}

public sealed class AiExecutionContext
{
    public ModStudioEntityKind CurrentKind { get; set; }

    public string CurrentEntityId { get; set; } = string.Empty;

    public string SelectedGraphNodeId { get; set; } = string.Empty;
}
