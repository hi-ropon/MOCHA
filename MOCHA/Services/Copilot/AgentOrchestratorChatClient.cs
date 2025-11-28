using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using MOCHA.Agents.Application;
using MOCHA.Agents.Domain;
using MOCHA.Models.Chat;

using DomainChatTurn = MOCHA.Agents.Domain.ChatTurn;
using DomainAuthorRole = MOCHA.Agents.Domain.AuthorRole;
using ChatTurnModel = MOCHA.Models.Chat.ChatTurn;

namespace MOCHA.Services.Copilot;

/// <summary>
/// IAgentOrchestrator を ICopilotChatClient としてラップするアダプタ。
/// </summary>
public sealed class AgentOrchestratorChatClient : ICopilotChatClient
{
    private readonly IAgentOrchestrator _orchestrator;

    public AgentOrchestratorChatClient(IAgentOrchestrator orchestrator)
    {
        this._orchestrator = orchestrator;
    }

    public Task<IAsyncEnumerable<ChatStreamEvent>> SendAsync(ChatTurnModel turn, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(SendCoreAsync(turn, cancellationToken));
    }

    public Task SubmitActionResultAsync(CopilotActionResult result, CancellationToken cancellationToken = default)
    {
        // いまのところツール結果の往復は行わないため no-op
        return Task.CompletedTask;
    }

    private async IAsyncEnumerable<ChatStreamEvent> SendCoreAsync(
        ChatTurnModel turn,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var conversationId = string.IsNullOrWhiteSpace(turn.ConversationId)
            ? Guid.NewGuid().ToString("N")
            : turn.ConversationId;

        var history = turn.Messages
            .Select(m => new DomainChatTurn(MapRole(m.Role), m.Content, DateTimeOffset.UtcNow))
            .ToList();

        var userTurn = history.LastOrDefault() ?? DomainChatTurn.User(string.Empty);
        var context = new ChatContext(conversationId, history);

        var events = await _orchestrator.ReplyAsync(userTurn, context, cancellationToken);

        await foreach (var ev in events.WithCancellation(cancellationToken))
        {
            switch (ev.Type)
            {
                case AgentEventType.Message when !string.IsNullOrWhiteSpace(ev.Text):
                    yield return ChatStreamEvent.FromMessage(new ChatMessage(ChatRole.Assistant, ev.Text!));
                    break;
                case AgentEventType.ToolCallRequested when ev.ToolCall is not null:
                    yield return new ChatStreamEvent(
                        ChatStreamEventType.ActionRequest,
                        ActionRequest: new CopilotActionRequest(
                            ev.ToolCall.Name,
                            ev.ConversationId,
                            ParsePayload(ev.ToolCall.ArgumentsJson)));
                    break;
                case AgentEventType.ToolCallCompleted when ev.ToolResult is not null:
                    yield return new ChatStreamEvent(
                        ChatStreamEventType.ToolResult,
                        ActionResult: new CopilotActionResult(
                            ev.ToolResult.Name,
                            ev.ConversationId,
                            ev.ToolResult.Success,
                            ParsePayload(ev.ToolResult.PayloadJson),
                            ev.ToolResult.Error));
                    break;
                case AgentEventType.ProgressUpdated when !string.IsNullOrWhiteSpace(ev.Text):
                    yield return ChatStreamEvent.FromMessage(new ChatMessage(ChatRole.Assistant, ev.Text!));
                    break;
                case AgentEventType.Error:
                    yield return ChatStreamEvent.Fail(ev.Error ?? "agent error");
                    break;
                case AgentEventType.Completed:
                    yield return ChatStreamEvent.Completed(ev.ConversationId);
                    break;
            }
        }
    }

    private static DomainAuthorRole MapRole(ChatRole role) =>
        role switch
        {
            ChatRole.System => DomainAuthorRole.System,
            ChatRole.Assistant => DomainAuthorRole.Assistant,
            ChatRole.Tool => DomainAuthorRole.Tool,
            _ => DomainAuthorRole.User
        };

    private static IReadOnlyDictionary<string, object?> ParsePayload(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, object?>();
        }

        try
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return dict ?? new Dictionary<string, object?>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, object?> { ["raw"] = json };
        }
    }
}
