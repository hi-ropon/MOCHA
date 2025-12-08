using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using MOCHA.Agents.Application;
using MOCHA.Agents.Domain;
using MOCHA.Models.Chat;

using DomainChatTurn = MOCHA.Agents.Domain.ChatTurn;
using DomainAuthorRole = MOCHA.Agents.Domain.AuthorRole;
using DomainChatAttachment = MOCHA.Agents.Domain.ChatAttachment;
using ChatTurnModel = MOCHA.Models.Chat.ChatTurn;

namespace MOCHA.Services.Chat;

/// <summary>
/// IAgentOrchestrator を IAgentChatClient としてラップするアダプタ
/// </summary>
public sealed class AgentOrchestratorChatClient : IAgentChatClient
{
    private readonly IAgentOrchestrator _orchestrator;

    /// <summary>
    /// オーケストレーター注入による初期化
    /// </summary>
    /// <param name="orchestrator">エージェントオーケストレーター</param>
    public AgentOrchestratorChatClient(IAgentOrchestrator orchestrator)
    {
        this._orchestrator = orchestrator;
    }

    /// <summary>
    /// チャットターン送信
    /// </summary>
    /// <param name="turn">送信するターン</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>イベントストリーム</returns>
    public Task<IAsyncEnumerable<ChatStreamEvent>> SendAsync(ChatTurnModel turn, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(SendCoreAsync(turn, cancellationToken));
    }

    /// <summary>
    /// ツール結果送信は未対応のため no-op
    /// </summary>
    /// <param name="result">送信する結果</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    public Task SubmitActionResultAsync(AgentActionResult result, CancellationToken cancellationToken = default)
    {
        // いまのところツール結果の往復は行わないため no-op
        return Task.CompletedTask;
    }

    /// <summary>
    /// オーケストレーター呼び出しとイベント変換
    /// </summary>
    /// <param name="turn">送信するターン</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>イベントストリーム</returns>
    private async IAsyncEnumerable<ChatStreamEvent> SendCoreAsync(
        ChatTurnModel turn,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var conversationId = string.IsNullOrWhiteSpace(turn.ConversationId)
            ? Guid.NewGuid().ToString("N")
            : turn.ConversationId;

        var history = turn.Messages
            .Select(MapToDomainTurn)
            .ToList();

        var userTurn = history.LastOrDefault() ?? DomainChatTurn.User(string.Empty);
        var context = new ChatContext(conversationId, history)
        {
            AgentNumber = turn.AgentNumber,
            UserId = turn.UserId,
            PlcOnline = turn.PlcOnline
        };

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
                        ActionRequest: new AgentActionRequest(
                            ev.ToolCall.Name,
                            ev.ConversationId,
                            ParsePayload(ev.ToolCall.ArgumentsJson)));
                    break;
                case AgentEventType.ToolCallCompleted when ev.ToolResult is not null:
                    yield return new ChatStreamEvent(
                        ChatStreamEventType.ToolResult,
                        ActionResult: new AgentActionResult(
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

    private static DomainChatTurn MapToDomainTurn(ChatMessage message)
    {
        var attachments = MapAttachments(message.Attachments);
        return new DomainChatTurn(MapRole(message.Role), message.Content, DateTimeOffset.UtcNow)
        {
            Attachments = attachments
        };
    }

    private static IReadOnlyList<DomainChatAttachment> MapAttachments(IReadOnlyList<ImageAttachment>? attachments)
    {
        if (attachments is null || attachments.Count == 0)
        {
            return Array.Empty<DomainChatAttachment>();
        }

        var list = new List<DomainChatAttachment>(attachments.Count);
        foreach (var attachment in attachments)
        {
            var data = DecodeBase64Data(attachment);
            if (data is null)
            {
                continue;
            }

            list.Add(new DomainChatAttachment(attachment.FileName, attachment.ContentType, data));
        }

        return list;
    }

    private static byte[]? DecodeBase64Data(ImageAttachment attachment)
    {
        var source = !string.IsNullOrWhiteSpace(attachment.MediumBase64)
            ? attachment.MediumBase64
            : attachment.SmallBase64;

        if (string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        var commaIndex = source.IndexOf(',');
        var base64 = commaIndex >= 0 ? source[(commaIndex + 1)..] : source;
        try
        {
            return Convert.FromBase64String(base64);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// モデルロールからドメインロールへの変換
    /// </summary>
    /// <param name="role">チャットロール</param>
    /// <returns>ドメインロール</returns>
    private static DomainAuthorRole MapRole(ChatRole role) =>
        role switch
        {
            ChatRole.System => DomainAuthorRole.System,
            ChatRole.Assistant => DomainAuthorRole.Assistant,
            ChatRole.Tool => DomainAuthorRole.Tool,
            _ => DomainAuthorRole.User
        };

    /// <summary>
    /// JSON ペイロードの辞書化
    /// </summary>
    /// <param name="json">入力 JSON</param>
    /// <returns>辞書化したペイロード</returns>
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
