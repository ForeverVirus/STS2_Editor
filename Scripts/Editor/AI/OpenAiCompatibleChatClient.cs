using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace STS2_Editor.Scripts.Editor.AI;

public interface IAiChatClient
{
    Task<AiChatCompletionResult> CompleteAsync(AiClientSettings settings, IReadOnlyList<AiChatMessage> messages, CancellationToken cancellationToken);

    Task<AiChatCompletionResult> CompleteStreamingAsync(
        AiClientSettings settings,
        IReadOnlyList<AiChatMessage> messages,
        Action<AiStreamingUpdate>? onUpdate,
        CancellationToken cancellationToken);
}

public sealed class OpenAiCompatibleChatClient : IAiChatClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public Task<AiChatCompletionResult> CompleteStreamingAsync(
        AiClientSettings settings,
        IReadOnlyList<AiChatMessage> messages,
        Action<AiStreamingUpdate>? onUpdate,
        CancellationToken cancellationToken)
    {
        return CompleteAsync(settings, messages, cancellationToken);
    }

    public async Task<AiChatCompletionResult> CompleteAsync(AiClientSettings settings, IReadOnlyList<AiChatMessage> messages, CancellationToken cancellationToken)
    {
        settings.Normalize();
        if (!settings.IsConfigured)
        {
            return new AiChatCompletionResult
            {
                ErrorMessage = "AI client settings are incomplete."
            };
        }

        try
        {
            using var httpClient = new HttpClient
            {
                BaseAddress = new Uri(settings.BaseUrl.TrimEnd('/') + "/"),
                Timeout = TimeSpan.FromSeconds(120)
            };
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
            using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    model = settings.Model,
                    temperature = 0.2,
                    messages = messages.Select(message => new
                    {
                        role = message.Role,
                        content = message.Content
                    }).ToArray()
                }), Encoding.UTF8, "application/json")
            };

            using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new AiChatCompletionResult
                {
                    ErrorMessage = ExtractErrorMessage(responseText),
                    IsContextLengthError = LooksLikeContextOverflow(responseText),
                    StatusCode = (int)response.StatusCode
                };
            }

            var parsed = JsonSerializer.Deserialize<ChatCompletionResponse>(responseText, JsonOptions);
            var message = parsed?.Choices?.FirstOrDefault()?.Message;
            var content = message?.Content?.Trim();
            if (string.IsNullOrWhiteSpace(content))
            {
                return new AiChatCompletionResult
                {
                    ErrorMessage = "AI returned an empty completion.",
                    StatusCode = (int)response.StatusCode
                };
            }

            return new AiChatCompletionResult
            {
                IsSuccess = true,
                ResponseText = content,
                ReasoningText = message?.ReasoningContent?.Trim() ?? string.Empty,
                StatusCode = (int)response.StatusCode
            };
        }
        catch (OperationCanceledException)
        {
            return new AiChatCompletionResult
            {
                ErrorMessage = "AI request was canceled."
            };
        }
        catch (Exception ex)
        {
            return new AiChatCompletionResult
            {
                ErrorMessage = ex.Message
            };
        }
    }

    private static string ExtractErrorMessage(string responseText)
    {
        try
        {
            using var document = JsonDocument.Parse(responseText);
            if (document.RootElement.TryGetProperty("error", out var errorElement))
            {
                if (errorElement.ValueKind == JsonValueKind.String)
                {
                    return errorElement.GetString() ?? "AI request failed.";
                }

                if (errorElement.TryGetProperty("message", out var messageElement))
                {
                    return messageElement.GetString() ?? "AI request failed.";
                }
            }
        }
        catch
        {
        }

        return string.IsNullOrWhiteSpace(responseText) ? "AI request failed." : responseText;
    }

    private static bool LooksLikeContextOverflow(string responseText)
    {
        return responseText.Contains("context length", StringComparison.OrdinalIgnoreCase) ||
               responseText.Contains("maximum context length", StringComparison.OrdinalIgnoreCase) ||
               responseText.Contains("context_length_exceeded", StringComparison.OrdinalIgnoreCase) ||
               responseText.Contains("too many tokens", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ChatCompletionResponse
    {
        public List<Choice>? Choices { get; set; }
    }

    private sealed class Choice
    {
        public ChatMessage? Message { get; set; }
    }

    private sealed class ChatMessage
    {
        public string? Content { get; set; }

        public string? ReasoningContent { get; set; }
    }
}
