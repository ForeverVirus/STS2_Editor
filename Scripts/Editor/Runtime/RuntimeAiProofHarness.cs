using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes;
using STS2_Editor.Scripts.Editor.AI;
using STS2_Editor.Scripts.Editor.Core.Models;
using STS2_Editor.Scripts.Editor.Core.Services;
using STS2_Editor.Scripts.Editor.Graph;

namespace STS2_Editor.Scripts.Editor.Runtime;

internal static class RuntimeAiProofHarness
{
    private const string ProofArgPrefix = "--modstudio-ai-proof=";
    private static readonly string? ProofRequestPath = ParseProofRequestPath();
    private static ModelMetadataService? _metadataService;
    private static ProjectAssetBindingService? _assetBindingService;
    private static BehaviorGraphRegistry? _graphRegistry;
    private static int _scheduled;

    public static void ApplyIfRequested(
        ModelMetadataService metadataService,
        ProjectAssetBindingService assetBindingService,
        BehaviorGraphRegistry graphRegistry)
    {
        Log.Info($"[ModStudio.AIProof] CommandLine={string.Join(" | ", System.Environment.GetCommandLineArgs())}");
        if (!string.IsNullOrWhiteSpace(ProofRequestPath))
        {
            Log.Info($"[ModStudio.AIProof] Requested proof path='{ProofRequestPath}'");
        }

        if (string.IsNullOrWhiteSpace(ProofRequestPath) ||
            Interlocked.CompareExchange(ref _scheduled, 0, 0) != 0)
        {
            return;
        }

        _metadataService = metadataService;
        _assetBindingService = assetBindingService;
        _graphRegistry = graphRegistry;
        Log.Info("[ModStudio.AIProof] Proof request registered. Waiting for main menu readiness before starting.");
        _ = Task.Run(WaitForMainMenuAndExecuteAsync);
    }

    public static void NotifyMainMenuReady()
    {
        if (string.IsNullOrWhiteSpace(ProofRequestPath) ||
            _metadataService == null ||
            _assetBindingService == null ||
            _graphRegistry == null ||
            Interlocked.Exchange(ref _scheduled, 1) != 0)
        {
            return;
        }

        Log.Info("[ModStudio.AIProof] Main menu ready. Scheduling background AI proof task.");
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(4)).ConfigureAwait(false);
            await ExecuteAsync(_metadataService, _assetBindingService, _graphRegistry).ConfigureAwait(false);
        });
    }

    private static async Task WaitForMainMenuAndExecuteAsync()
    {
        for (var attempt = 0; attempt < 180; attempt++)
        {
            if (_metadataService == null || _assetBindingService == null || _graphRegistry == null)
            {
                return;
            }

            if (NGame.Instance?.MainMenu != null)
            {
                Log.Info("[ModStudio.AIProof] Main menu detected via polling.");
                NotifyMainMenuReady();
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
        }

        Log.Warn("[ModStudio.AIProof] Main menu was not detected before timeout.");
    }

    private static async Task ExecuteAsync(
        ModelMetadataService metadataService,
        ProjectAssetBindingService assetBindingService,
        BehaviorGraphRegistry graphRegistry)
    {
        try
        {
            Log.Info("[ModStudio.AIProof] ExecuteAsync entered.");
            if (string.IsNullOrWhiteSpace(ProofRequestPath) || !File.Exists(ProofRequestPath))
            {
                Log.Warn($"[ModStudio.AIProof] Proof request file not found: {ProofRequestPath}");
                return;
            }
            Log.Info("[ModStudio.AIProof] Proof request file exists.");

            var request = JsonSerializer.Deserialize<AiProofRequest>(
                              await File.ReadAllTextAsync(ProofRequestPath),
                              new JsonSerializerOptions
                              {
                                  PropertyNameCaseInsensitive = true
                              }) ??
                          new AiProofRequest();
            Log.Info($"[ModStudio.AIProof] Request loaded baseUrl='{request.BaseUrl}' model='{request.Model}' promptLength={request.UserPrompt?.Length ?? 0}.");
            if (string.IsNullOrWhiteSpace(request.BaseUrl) ||
                string.IsNullOrWhiteSpace(request.ApiKey) ||
                string.IsNullOrWhiteSpace(request.Model))
            {
                Log.Warn("[ModStudio.AIProof] Proof request was missing base_url, api_key, or model.");
                return;
            }
            Log.Info("[ModStudio.AIProof] Request configuration validated.");

            var project = BuildProofProject();
            Log.Info("[ModStudio.AIProof] Proof project created.");
            var executionContext = new AiExecutionContext
            {
                CurrentKind = ModStudioEntityKind.Card,
                CurrentEntityId = ProofCardId
            };
            var settings = new AiClientSettings
            {
                BaseUrl = request.BaseUrl,
                ApiKey = request.ApiKey,
                Model = request.Model
            };
            settings.Normalize();
            Log.Info("[ModStudio.AIProof] Client settings normalized.");
            var client = new OpenAiCompatibleChatClient();
            var contextService = new AiProjectContextService(metadataService, assetBindingService, graphRegistry);
            var executor = new AiEditExecutor(metadataService, assetBindingService, graphRegistry);
            var ambientContext = contextService.BuildAmbientContext(project, executionContext);
            Log.Info("[ModStudio.AIProof] Client/context/executor created.");
            var sessionMessages = new List<AiChatMessage>
            {
                AiChatMessage.Create("system", BuildSystemPrompt(), visible: false),
                AiChatMessage.Create("user", $"Ambient editor context JSON:\n{ambientContext}", visible: false),
                AiChatMessage.Create("user", request.UserPrompt)
            };
            Log.Info($"[ModStudio.AIProof] Built proof project and request payload contextLength={ambientContext.Length}.");

            for (var queryRound = 0; queryRound < 4; queryRound++)
            {
                Log.Info($"[ModStudio.AIProof] Request round {queryRound + 1} starting.");
                var completion = await client.CompleteAsync(settings, sessionMessages, CancellationToken.None);
                Log.Info($"[ModStudio.AIProof] Request round {queryRound + 1} completed success={completion.IsSuccess} status={completion.StatusCode}.");
                if (!completion.IsSuccess)
                {
                    Log.Warn($"[ModStudio.AIProof] FAIL request={completion.ErrorMessage}");
                    return;
                }

                if (!AiProtocolParser.TryParseEnvelope(completion.ResponseText, out var envelope, out var parseError))
                {
                    Log.Warn($"[ModStudio.AIProof] FAIL parse={parseError} raw={completion.ResponseText}");
                    return;
                }
                Log.Info($"[ModStudio.AIProof] Parsed envelope type={envelope.Type} ops={envelope.Operations.Count} queries={envelope.Queries.Count}.");

                if (string.Equals(envelope.Type, "query", StringComparison.Ordinal))
                {
                    var queryJson = contextService.ExecuteQueries(project, executionContext, envelope.Queries);
                    sessionMessages.Add(AiChatMessage.Create("user", $"Local query result JSON:\n{queryJson}", visible: false));
                    Log.Info("[ModStudio.AIProof] Attached local query result.");
                    continue;
                }

                if (!string.Equals(envelope.Type, "edit_plan", StringComparison.Ordinal))
                {
                    Log.Warn($"[ModStudio.AIProof] FAIL expected edit_plan but got {envelope.Type}");
                    return;
                }

                var plan = new AiEditPlan
                {
                    AssistantMessage = envelope.AssistantMessage,
                    NeedsClarification = envelope.NeedsClarification,
                    Warnings = envelope.Warnings,
                    Operations = envelope.Operations
                };
                Log.Info("[ModStudio.AIProof] Starting preview execution.");
                var preview = executor.Preview(project, plan, executionContext);
                if (!preview.IsValid)
                {
                    Log.Warn($"[ModStudio.AIProof] FAIL preview={string.Join(" | ", preview.ErrorLines)}");
                    return;
                }
                Log.Info("[ModStudio.AIProof] Preview execution completed successfully.");

                var envelopeAfter = preview.ProjectSnapshot.Overrides.First(item =>
                    item.EntityKind == ModStudioEntityKind.Card &&
                    string.Equals(item.EntityId, ProofCardId, StringComparison.Ordinal));
                preview.ProjectSnapshot.Graphs.TryGetValue(envelopeAfter.GraphId ?? string.Empty, out var graphAfter);
                var title = envelopeAfter.Metadata.TryGetValue("title", out var titleValue) ? titleValue : string.Empty;
                var description = envelopeAfter.Metadata.TryGetValue("description", out var descriptionValue) ? descriptionValue : string.Empty;
                var damageNode = graphAfter?.Nodes.FirstOrDefault(node =>
                    string.Equals(node.NodeType, "combat.damage", StringComparison.OrdinalIgnoreCase));
                var amount = damageNode?.Properties.TryGetValue("amount", out var amountValue) == true ? amountValue : string.Empty;
                Log.Info($"[ModStudio.AIProof] PASS title='{title}' description='{description}' operations={plan.Operations.Count} graphNodes={graphAfter?.Nodes.Count ?? 0} damageAmount='{amount}'");
                return;
            }

            Log.Warn("[ModStudio.AIProof] FAIL query round limit exceeded.");
        }
        catch (Exception ex)
        {
            Log.Warn($"[ModStudio.AIProof] FAIL exception={ex}");
        }
    }

    private static EditorProject BuildProofProject()
    {
        var project = new EditorProject
        {
            Manifest = new EditorProjectManifest
            {
                ProjectId = "runtime_ai_proof",
                Name = "Runtime AI Proof"
            }
        };
        project.Overrides.Add(new EntityOverrideEnvelope
        {
            EntityKind = ModStudioEntityKind.Card,
            EntityId = ProofCardId,
            BehaviorSource = BehaviorSource.Graph,
            GraphId = ProofGraphId,
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["title"] = "Proof Card",
                ["description"] = "Initial description.",
                ["type"] = "Attack",
                ["rarity"] = "Common",
                ["target_type"] = "AnyEnemy",
                ["energy_cost"] = "1"
            }
        });
        project.Graphs[ProofGraphId] = BehaviorGraphTemplateFactory.CreateDefaultScaffold(ProofGraphId, ModStudioEntityKind.Card, "Proof Card", "Initial description.");
        return project;
    }

    private static string BuildSystemPrompt()
    {
        return string.Join(System.Environment.NewLine, new[]
        {
            "You are an AI editor for Mod Studio.",
            "Always respond with exactly one JSON object and no surrounding prose.",
            "Allowed top-level response types are: reply, query, edit_plan.",
            "Use edit_plan only when you are ready to propose concrete project edits.",
            "Do not ask questions. Do not use query unless absolutely necessary.",
            "Allowed edit operation types: create_entity, set_basic_fields, set_behavior_mode, set_asset_binding, clear_asset_binding, ensure_graph, set_graph_meta, set_graph_entry, add_graph_node, update_graph_node, remove_graph_node, connect_graph_nodes, disconnect_graph_nodes."
        });
    }

    private static string? ParseProofRequestPath()
    {
        foreach (var arg in System.Environment.GetCommandLineArgs())
        {
            if (!arg.StartsWith(ProofArgPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var rawPath = arg[ProofArgPrefix.Length..].Trim().Trim('"');
            if (!string.IsNullOrWhiteSpace(rawPath))
            {
                return Path.GetFullPath(rawPath);
            }
        }

        return null;
    }

    private const string ProofCardId = "ai_runtime_proof_card";
    private const string ProofGraphId = "ai_runtime_proof_graph";

    private sealed class AiProofRequest
    {
        public string BaseUrl { get; set; } = string.Empty;

        public string ApiKey { get; set; } = string.Empty;

        public string Model { get; set; } = string.Empty;

        public string UserPrompt { get; set; } =
            "Return type=edit_plan only. Modify the selected card ai_runtime_proof_card. Set title to 'AI Runtime Proof Card' and description to 'Deal 9 damage.'. Remove the direct connection from entry_card:next to exit_card:in. Add a combat.damage node with display name 'Damage', amount=9, target=current_target, props=none. Then connect entry_card:next -> the damage node in, and damage node out -> exit_card:in.";
    }
}
