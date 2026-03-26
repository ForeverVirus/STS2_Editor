#nullable enable
using System.Diagnostics;
using System.Text.Json;

var invocation = ParseArgs(args);
var repoRoot = invocation.RepoRoot;
var gameRoot = @"F:\SteamLibrary\steamapps\common\Slay the Spire 2";
var gameExe = Path.Combine(gameRoot, "SlayTheSpire2.exe");
var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SlayTheSpire2", "logs", "godot.log");
var requestRoot = Path.Combine(repoRoot, "coverage", "ai-runtime-proof");
Directory.CreateDirectory(requestRoot);
var requestPath = Path.Combine(requestRoot, "ai-proof-request.json");
var bridgeProjectPath = Path.Combine(repoRoot, "tools", "ModStudioAiBridge", "ModStudioAiBridge.csproj");
var bridgeBuildInfo = new ProcessStartInfo("dotnet")
{
    WorkingDirectory = repoRoot,
    UseShellExecute = false,
    RedirectStandardOutput = true,
    RedirectStandardError = true
};
bridgeBuildInfo.ArgumentList.Add("build");
bridgeBuildInfo.ArgumentList.Add(bridgeProjectPath);
bridgeBuildInfo.ArgumentList.Add("-c");
bridgeBuildInfo.ArgumentList.Add("Debug");
using (var buildProcess = Process.Start(bridgeBuildInfo) ?? throw new InvalidOperationException("Could not build ModStudioAiBridge."))
{
    var stdout = buildProcess.StandardOutput.ReadToEnd();
    var stderr = buildProcess.StandardError.ReadToEnd();
    buildProcess.WaitForExit();
    if (buildProcess.ExitCode != 0)
    {
        throw new InvalidOperationException($"ModStudioAiBridge build failed.{Environment.NewLine}{stdout}{Environment.NewLine}{stderr}");
    }
}
var bridgeDllPath = Path.Combine(repoRoot, "tools", "ModStudioAiBridge", "bin", "Debug", "net9.0", "ModStudioAiBridge.dll");

File.WriteAllText(requestPath, JsonSerializer.Serialize(new
{
    baseUrl = invocation.BaseUrl,
    apiKey = invocation.ApiKey,
    model = invocation.Model,
    userPrompt = invocation.UserPrompt,
    bridgeDllPath
}, new JsonSerializerOptions { WriteIndented = true }));

var before = ReadLog(logPath);
var startInfo = new ProcessStartInfo(gameExe)
{
    WorkingDirectory = gameRoot,
    UseShellExecute = false,
    RedirectStandardOutput = true,
    RedirectStandardError = true
};
startInfo.ArgumentList.Add($"--modstudio-ai-proof={requestPath}");

using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start SlayTheSpire2.exe.");
var deadline = DateTime.UtcNow.AddSeconds(180);
var successMarker = "[ModStudio.AIProof] PASS";
var failMarker = "[ModStudio.AIProof] FAIL";
string[] lines = Array.Empty<string>();

while (DateTime.UtcNow < deadline)
{
    Thread.Sleep(2000);
    lines = ReadLog(logPath);
    var delta = lines.Length >= before.Length ? lines.Skip(before.Length).ToArray() : lines;
    if (delta.Any(line => line.Contains(successMarker, StringComparison.Ordinal)))
    {
        TryKill(process);
        Console.WriteLine("PASS");
        foreach (var line in delta.Where(line => line.Contains("[ModStudio.AIProof]", StringComparison.Ordinal)))
        {
            Console.WriteLine(line);
        }

        return 0;
    }

    if (delta.Any(line => line.Contains(failMarker, StringComparison.Ordinal)))
    {
        TryKill(process);
        Console.WriteLine("FAIL");
        foreach (var line in delta.Where(line => line.Contains("[ModStudio.AIProof]", StringComparison.Ordinal)))
        {
            Console.WriteLine(line);
        }

        return 1;
    }
}

TryKill(process);
Console.WriteLine("FAIL");
Console.WriteLine("Timed out waiting for AI proof markers.");
return 1;

static Invocation ParseArgs(string[] args)
{
    var repoRoot = Directory.GetCurrentDirectory();
    var baseUrl = "https://sub.jlypx.de/v1/chat/completions";
    var apiKey = string.Empty;
    var model = "gpt-5.4";
    var userPrompt = "Return type=edit_plan only. Modify the selected card ai_runtime_proof_card. Set title to 'AI Runtime Proof Card' and description to 'Deal 9 damage.'. Remove the direct connection from entry_card:next to exit_card:in. Add a combat.damage node with display name 'Damage', amount=9, target=current_target, props=none. Then connect entry_card:next -> the damage node in, and damage node out -> exit_card:in.";

    for (var index = 0; index < args.Length; index++)
    {
        switch (args[index])
        {
            case "--repo-root":
                repoRoot = Path.GetFullPath(args[++index]);
                break;
            case "--base-url":
                baseUrl = args[++index];
                break;
            case "--api-key":
                apiKey = args[++index];
                break;
            case "--model":
                model = args[++index];
                break;
            case "--prompt":
                userPrompt = args[++index];
                break;
        }
    }

    if (string.IsNullOrWhiteSpace(apiKey))
    {
        throw new InvalidOperationException("--api-key is required.");
    }

    return new Invocation
    {
        RepoRoot = repoRoot,
        BaseUrl = baseUrl,
        ApiKey = apiKey,
        Model = model,
        UserPrompt = userPrompt
    };
}

static string[] ReadLog(string path)
{
    if (!File.Exists(path))
    {
        return Array.Empty<string>();
    }

    for (var attempt = 0; attempt < 40; attempt++)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd().Split([Environment.NewLine], StringSplitOptions.None);
        }
        catch (IOException) when (attempt < 39)
        {
            Thread.Sleep(250);
        }
    }

    return Array.Empty<string>();
}

static void TryKill(Process process)
{
    try
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit(5000);
        }
    }
    catch
    {
    }
}

internal sealed class Invocation
{
    public string RepoRoot { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public string UserPrompt { get; set; } = string.Empty;
}
