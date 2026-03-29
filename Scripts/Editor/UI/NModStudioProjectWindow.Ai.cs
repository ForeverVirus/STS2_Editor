using System.Text;
using Godot;
using STS2_Editor.Scripts.Editor.AI;
using STS2_Editor.Scripts.Editor.Core.Utilities;
using static STS2_Editor.Scripts.Editor.UI.ModStudioUiFactory;

namespace STS2_Editor.Scripts.Editor.UI;

public sealed partial class NModStudioProjectWindow
{
    private const int AiEstimatedCharacterBudget = 48000;
    private const int AiQueryRoundLimit = 4;

    private IAiChatClient? _aiChatClient;
    private AiProjectContextService? _aiContextService;
    private AiEditExecutor? _aiEditExecutor;
    private ModStudioAiConfigDialog? _aiConfigDialog;
    private ModStudioAiChatPanel? _aiChatPanel;
    private AiChatSession? _aiSession;
    private AiPlanPreview? _pendingAiPreview;
    private CancellationTokenSource? _aiRequestCts;
    private bool _aiBusy;

    private void EnsureAiServicesInitialized()
    {
        _aiChatClient ??= new OpenAiCompatibleChatClient();
        _aiContextService ??= new AiProjectContextService(_metadataService, _assetBindingService, _graphRegistry);
        _aiEditExecutor ??= new AiEditExecutor(_metadataService, _assetBindingService, _graphRegistry);
    }

    private void OpenAiAssistant()
    {
        if (_project == null)
        {
            OS.Alert(
                Dual("请先打开或创建一个项目，再使用 AI 助手。", "Open or create a project before using the AI assistant."),
                Dual("未打开项目", "No Project"));
            return;
        }

        EnsureAiServicesInitialized();
        var settings = LoadAiSettings();
        if (!settings.IsConfigured)
        {
            ShowAiSettingsDialog();
            return;
        }

        _aiChatPanel?.ShowPanel();
        RefreshAiTranscript();
        RefreshAiLayout();
    }

    private void ShowAiSettingsDialog()
    {
        EnsureAiServicesInitialized();
        _aiConfigDialog?.ShowDialog(LoadAiSettings());
    }

    private AiClientSettings LoadAiSettings()
    {
        return AiClientSettings.FromSettings(ModStudioSettingsStore.Load());
    }

    private void HandleAiSettingsSaved(AiClientSettings settings)
    {
        var persisted = ModStudioSettingsStore.Load();
        settings.ApplyTo(persisted);
        ModStudioSettingsStore.Save(persisted);
        _aiChatPanel?.SetBusy(false, Dual("AI 设置已保存。", "AI settings saved."));
    }

    private void HandleAiChatClosed()
    {
        _aiRequestCts?.Cancel();
        _aiBusy = false;
        _aiChatPanel?.SetBusy(false, string.Empty);
        _aiChatPanel?.ClearStreamingPreview();
    }

    private async void HandleAiChatSendRequested(string text)
    {
        if (_aiBusy)
        {
            return;
        }

        if (string.Equals(text.Trim(), "/new", StringComparison.OrdinalIgnoreCase))
        {
            ResetAiSession(Dual("已开启新会话。", "Started a new session."), clearPendingPreview: true);
            return;
        }

        if (_project == null)
        {
            return;
        }

        EnsureAiServicesInitialized();
        var settings = LoadAiSettings();
        if (!settings.IsConfigured)
        {
            ShowAiSettingsDialog();
            return;
        }

        FlushAiDraftsToProject();
        var session = EnsureAiSession();
        session.AddMessage(AiChatMessage.Create("user", text));
        _aiChatPanel?.ClearInput();
        RefreshAiTranscript();

        _aiBusy = true;
        _aiRequestCts?.Cancel();
        _aiRequestCts = new CancellationTokenSource();
        _aiChatPanel?.ClearStreamingPreview();
        _aiChatPanel?.SetBusy(true, Dual("AI 正在分析...", "AI is thinking..."));

        try
        {
            var handled = await RunAiConversationLoopAsync(settings, _aiRequestCts.Token);
            if (!handled)
            {
                AppendAiSystemMessage(Dual("AI 未返回可解析结果。", "AI did not return a parsable result."));
            }
        }
        finally
        {
            _aiBusy = false;
            _aiChatPanel?.SetBusy(false, string.Empty);
            _aiChatPanel?.ClearStreamingPreview();
        }
    }

    private AiChatSession EnsureAiSession(string carrySummary = "")
    {
        if (_aiSession != null)
        {
            return _aiSession;
        }

        _aiSession = new AiChatSession();
        _aiSession.AddMessage(AiChatMessage.Create("system", BuildAiSystemPrompt(), visible: false));
        if (!string.IsNullOrWhiteSpace(carrySummary))
        {
            _aiSession.AddMessage(AiChatMessage.Create("system", carrySummary, visible: false));
            _aiSession.RollingSummary = carrySummary;
        }

        RefreshAiTranscript();
        return _aiSession;
    }

    private void ResetAiSession(string announcement, bool clearPendingPreview)
    {
        var carrySummary = _aiSession?.RollingSummary ?? string.Empty;
        _aiSession = null;
        if (clearPendingPreview)
        {
            _pendingAiPreview = null;
        }

        EnsureAiSession(carrySummary);
        AppendAiSystemMessage(announcement);
        RefreshAiTranscript();
    }

    private void RefreshAiTranscript()
    {
        _aiChatPanel?.SetMessages(_aiSession?.Messages?.ToList() ?? new List<AiChatMessage>());
        _aiChatPanel?.SetPendingPreview(_pendingAiPreview);
    }

    private void RefreshAiLayout()
    {
        _aiChatPanel?.UpdateLayout(GetViewportRect().Size);
    }

    private async Task<bool> RunAiConversationLoopAsync(AiClientSettings settings, CancellationToken cancellationToken)
    {
        if (_project == null || _aiSession == null || _aiChatClient == null || _aiContextService == null || _aiEditExecutor == null)
        {
            return false;
        }

        var rolloverAttempt = 0;
        while (rolloverAttempt < 2)
        {
            MaybeRollAiSessionForBudget();
            var session = EnsureAiSession();
            var executionContext = GetAiExecutionContext();

            for (var queryRound = 0; queryRound < AiQueryRoundLimit; queryRound++)
            {
                var requestMessages = BuildAiRequestMessages(session, executionContext);
                var result = await CompleteWithStreamingFallbackAsync(settings, requestMessages, cancellationToken);
                if (!result.IsSuccess)
                {
                    if (result.IsContextLengthError && rolloverAttempt == 0)
                    {
                        RollAiSessionForOverflow();
                        rolloverAttempt++;
                        goto RetryConversation;
                    }

                    AppendAiSystemMessage($"{Dual("AI 请求失败", "AI request failed")}: {result.ErrorMessage}");
                    return true;
                }

                if (!AiProtocolParser.TryParseEnvelope(result.ResponseText, out var envelope, out var parseError))
                {
                    AppendAiAssistantMessage(result.ResponseText);
                    AppendAiSystemMessage($"{Dual("无法解析 AI 响应", "Could not parse AI response")}: {parseError}");
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(envelope.AssistantMessage))
                {
                    AppendAiAssistantMessage(envelope.AssistantMessage);
                }

                foreach (var warning in envelope.Warnings)
                {
                    AppendAiSystemMessage($"{Dual("警告", "Warning")}: {warning}");
                }

                switch (envelope.Type)
                {
                    case "reply":
                        return true;
                    case "query":
                        var queryResult = _aiContextService.ExecuteQueries(_project, executionContext, envelope.Queries);
                        session.AddMessage(AiChatMessage.Create("user", $"Local query result JSON:\n{queryResult}", visible: false));
                        AppendAiSystemMessage(Dual("已补充本地上下文。", "Local context attached."));
                        break;
                    case "edit_plan":
                        var plan = new AiEditPlan
                        {
                            AssistantMessage = envelope.AssistantMessage,
                            NeedsClarification = envelope.NeedsClarification,
                            Warnings = envelope.Warnings,
                            Operations = envelope.Operations
                        };
                        _pendingAiPreview = _aiEditExecutor.Preview(_project, plan, executionContext);
                        RefreshAiTranscript();
                        return true;
                }
            }

            AppendAiSystemMessage(Dual("AI 查询轮次超过上限，请缩小需求范围后重试。", "AI exceeded the local query round limit. Narrow the request and try again."));
            return true;

        RetryConversation:
            continue;
        }

        return true;
    }

    private async Task<AiChatCompletionResult> CompleteWithStreamingFallbackAsync(
        AiClientSettings settings,
        IReadOnlyList<AiChatMessage> requestMessages,
        CancellationToken cancellationToken)
    {
        if (_aiChatClient == null)
        {
            return new AiChatCompletionResult
            {
                ErrorMessage = "AI client is not initialized."
            };
        }

        var streamingResult = await _aiChatClient.CompleteStreamingAsync(
            settings,
            requestMessages,
            update => CallDeferred(nameof(ApplyAiStreamingUpdate), update.ReasoningText, update.ContentText),
            cancellationToken);
        if (streamingResult.IsSuccess)
        {
            return streamingResult;
        }

        _aiChatPanel?.ClearStreamingPreview();
        return await _aiChatClient.CompleteAsync(settings, requestMessages, cancellationToken);
    }

    private void ApplyAiStreamingUpdate(string reasoningText, string contentText)
    {
        _aiChatPanel?.SetStreamingPreview(reasoningText, contentText);
        if (!string.IsNullOrWhiteSpace(reasoningText))
        {
            _aiChatPanel?.SetBusy(true, Dual("AI 正在思考...", "AI is reasoning..."));
            return;
        }

        if (!string.IsNullOrWhiteSpace(contentText))
        {
            _aiChatPanel?.SetBusy(true, Dual("AI 正在输出...", "AI is responding..."));
        }
    }

    private IReadOnlyList<AiChatMessage> BuildAiRequestMessages(AiChatSession session, AiExecutionContext executionContext)
    {
        var ambientContext = _aiContextService?.BuildAmbientContext(_project!, executionContext) ?? string.Empty;
        return session.Messages
            .Where(message => message.Role != "system" || !message.IsVisibleInTranscript)
            .Select(message => AiChatMessage.Create(message.Role, message.Content, message.IsVisibleInTranscript))
            .Append(AiChatMessage.Create("user", $"Ambient editor context JSON:\n{ambientContext}", visible: false))
            .ToList();
    }

    private void MaybeRollAiSessionForBudget()
    {
        if (_aiSession != null && _aiSession.EstimatedCharacterCount > AiEstimatedCharacterBudget)
        {
            RollAiSessionForOverflow();
        }
    }

    private void RollAiSessionForOverflow()
    {
        var summary = BuildAiCarrySummary();
        _aiSession = null;
        EnsureAiSession(summary);
        AppendAiSystemMessage(Dual("上下文已接近上限，已自动切换到新会话。", "Context was near the limit. A new session has been started automatically."));
    }

    private string BuildAiCarrySummary()
    {
        if (_aiSession == null)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.AppendLine("Previous session summary:");
        foreach (var summary in _aiSession.AppliedSummaries.TakeLast(12))
        {
            builder.AppendLine($"- {summary}");
        }

        var recentVisibleMessages = _aiSession.Messages.Where(message => message.IsVisibleInTranscript).TakeLast(6);
        foreach (var message in recentVisibleMessages)
        {
            builder.AppendLine($"[{message.Role}] {message.Content}");
        }

        return builder.ToString().Trim();
    }

    private void HandleApplyAiPreviewRequested()
    {
        if (_project == null || _pendingAiPreview == null || !_pendingAiPreview.IsValid || _aiEditExecutor == null)
        {
            return;
        }

        FlushAiDraftsToProject();
        var executionContext = GetAiExecutionContext();
        var appliedPreview = _aiEditExecutor.Preview(_project, _pendingAiPreview.Plan, executionContext);
        if (!appliedPreview.IsValid)
        {
            _pendingAiPreview = appliedPreview;
            RefreshAiTranscript();
            AppendAiSystemMessage(Dual("应用前重新校验失败，预览已刷新。", "Revalidation failed before apply. The preview has been refreshed."));
            return;
        }

        _project = appliedPreview.ProjectSnapshot;
        FieldChoiceProvider.SetCurrentProject(_project);
        _pendingAiPreview = null;
        _aiSession?.AddAppliedSummaries(appliedPreview.SummaryLines);
        ApplyAiResultFocus(appliedPreview);
        MarkDirty();
        RefreshAiTranscript();
        AppendAiSystemMessage(Dual("AI 预览已应用到当前项目。", "The AI preview has been applied to the current project."));
    }

    private void HandleDiscardAiPreviewRequested()
    {
        _pendingAiPreview = null;
        RefreshAiTranscript();
        AppendAiSystemMessage(Dual("已丢弃待应用预览。", "The pending preview has been discarded."));
    }

    private void ApplyAiResultFocus(AiPlanPreview preview)
    {
        _browserItemsCache.Clear();
        _currentViewCache = null;
        _cachedCompiledEventGraph = null;
        _cachedCompiledEventResult = null;
        if (preview.FocusEntityKind.HasValue && !string.IsNullOrWhiteSpace(preview.FocusEntityId))
        {
            _currentKind = preview.FocusEntityKind.Value;
            RefreshBrowserItems(selectFirstIfPossible: false);
            SelectEntity(preview.FocusEntityId);
            _browserPanel?.SetSelection(_currentKind, preview.FocusEntityId);
            return;
        }

        if (!string.IsNullOrWhiteSpace(_currentEntityId))
        {
            RefreshBrowserItems(selectFirstIfPossible: false);
            SelectEntity(_currentEntityId);
            _browserPanel?.SetSelection(_currentKind, _currentEntityId);
            return;
        }

        RefreshBrowserItems(selectFirstIfPossible: true);
    }

    private void AppendAiAssistantMessage(string text)
    {
        EnsureAiSession().AddMessage(AiChatMessage.Create("assistant", text));
        RefreshAiTranscript();
    }

    private void AppendAiSystemMessage(string text)
    {
        EnsureAiSession().AddMessage(AiChatMessage.Create("system", text));
        RefreshAiTranscript();
    }

    private void FlushAiDraftsToProject()
    {
        if (_basicDraftDirty)
        {
            SaveBasic();
        }

        if (_graphDraftDirty)
        {
            SaveGraph();
        }
    }

    private AiExecutionContext GetAiExecutionContext()
    {
        return new AiExecutionContext
        {
            CurrentKind = _currentKind,
            CurrentEntityId = _currentEntityId ?? _currentItem?.EntityId ?? string.Empty,
            SelectedGraphNodeId = _centerEditor?.GraphEditor?.CanvasView?.SelectedNodeId ?? string.Empty
        };
    }

    private string BuildAiSystemPrompt()
    {
        return string.Join(System.Environment.NewLine, new[]
        {
            "You are an AI editor for Mod Studio.",
            "Always respond with exactly one JSON object and no surrounding prose.",
            "Allowed top-level response types are: reply, query, edit_plan.",
            "Use query when you need more local editor context before planning edits.",
            "Use reply when you need clarification or when the user is only asking a question.",
            "Use edit_plan only when you are ready to propose concrete project edits.",
            "The host will preview all edit_plan operations before apply.",
            "Do not write code patches or file paths. Only describe project edits.",
            "Allowed query types: get_current_selection, list_project_entities, get_entity_snapshot, get_graph_snapshot, list_node_types, get_node_schema, list_asset_choices.",
            "Allowed edit operation types: create_entity, set_basic_fields, set_behavior_mode, set_asset_binding, clear_asset_binding, ensure_graph, set_graph_meta, set_graph_entry, add_graph_node, update_graph_node, remove_graph_node, connect_graph_nodes, disconnect_graph_nodes.",
            "When targeting a field that reads from graph state, use literal values or $state.xxx in values; when naming state keys, use bare names without $state.",
            "If a target is ambiguous, return type=reply with needs_clarification=true and ask for the missing id or selection."
        });
    }
}
