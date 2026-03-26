#nullable enable
using System.Text.Json;
using STS2_Editor.Scripts.Editor.AI;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: ModStudioAiBridge <request.json> <response.json>");
    return 1;
}

var requestPath = Path.GetFullPath(args[0]);
var responsePath = Path.GetFullPath(args[1]);
var request = JsonSerializer.Deserialize<BridgeRequest>(await File.ReadAllTextAsync(requestPath), new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true
}) ?? new BridgeRequest();

var client = new OpenAiCompatibleChatClient();
var settings = new AiClientSettings
{
    BaseUrl = request.BaseUrl,
    ApiKey = request.ApiKey,
    Model = request.Model
};
settings.Normalize();

AiChatCompletionResult result;
if (request.Stream)
{
    result = await client.CompleteStreamingAsync(
        settings,
        request.Messages,
        update => { },
        CancellationToken.None);
}
else
{
    result = await client.CompleteAsync(settings, request.Messages, CancellationToken.None);
}

Directory.CreateDirectory(Path.GetDirectoryName(responsePath) ?? AppContext.BaseDirectory);
await File.WriteAllTextAsync(responsePath, JsonSerializer.Serialize(result, new JsonSerializerOptions
{
    WriteIndented = true
}));
return result.IsSuccess ? 0 : 1;

internal sealed class BridgeRequest
{
    public string BaseUrl { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public bool Stream { get; set; }

    public List<AiChatMessage> Messages { get; set; } = new();
}
