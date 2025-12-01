using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using MOCHA.Models.Chat;

namespace MOCHA.Services.Chat;

/// <summary>
/// エージェントとのやり取りとツールイベントを保存するサービス
/// 実接続がなくてもフェイク実装で動作をテストできるように抽象化している
/// </summary>
internal sealed class ChatOrchestrator : IChatOrchestrator
{
    private readonly IAgentChatClient _agentChatClient;
    private readonly IChatRepository _chatRepository;
    private readonly ConversationHistoryState _history;
    private readonly IChatTitleService _chatTitleService;
    private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// 依存するクライアントと状態管理を受け取って初期化する
    /// </summary>
    /// <param name="agentChatClient">エージェント クライアント</param>
    /// <param name="chatRepository">チャットリポジトリ</param>
    /// <param name="history">会話履歴状態</param>
    /// <param name="chatTitleService">チャットタイトル生成サービス</param>
    public ChatOrchestrator(
        IAgentChatClient agentChatClient,
        IChatRepository chatRepository,
        ConversationHistoryState history,
        IChatTitleService chatTitleService)
    {
        _agentChatClient = agentChatClient;
        _chatRepository = chatRepository;
        _history = history;
        _chatTitleService = chatTitleService;
    }

    /// <summary>
    /// ユーザーの発話をエージェントに送り、ツール要求や応答をストリームで返す
    /// </summary>
    /// <param name="user">ユーザー情報</param>
    /// <param name="conversationId">既存の会話ID（未指定なら新規）</param>
    /// <param name="text">発話内容</param>
    /// <param name="agentNumber">装置エージェント番号</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>発生するチャットイベントの列</returns>
    public async IAsyncEnumerable<ChatStreamEvent> HandleUserMessageAsync(
        UserContext user,
        string? conversationId,
        string text,
        string? agentNumber,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var convId = string.IsNullOrWhiteSpace(conversationId)
            ? Guid.NewGuid().ToString("N")
            : conversationId;

        await _history.UpsertAsync(user.UserId, convId, text, agentNumber, cancellationToken, preserveExistingTitle: true);
        await _chatRepository.AddMessageAsync(user.UserId, convId, new ChatMessage(ChatRole.User, text), agentNumber, cancellationToken);
        _ = _chatTitleService.RequestAsync(user, convId, text, agentNumber, cancellationToken);

        var turn = new ChatTurn(convId, new List<ChatMessage>
        {
            new(ChatRole.User, text)
        });

        var stream = await _agentChatClient.SendAsync(turn, cancellationToken);
        var assistantBuffer = new StringBuilder();
        await foreach (var ev in stream.WithCancellation(cancellationToken))
        {
            if (ev.Type == ChatStreamEventType.ActionRequest && ev.ActionRequest is not null)
            {
                var actionRequest = ev.ActionRequest with { ConversationId = convId };

                await SaveMessageAsync(
                    user,
                    convId,
                    new ChatMessage(ChatRole.Tool, $"[action] {actionRequest.ActionName}: {JsonSerializer.Serialize(actionRequest.Payload, _serializerOptions)}"),
                    agentNumber,
                    cancellationToken);

                yield return new ChatStreamEvent(
                    ChatStreamEventType.ActionRequest,
                    ActionRequest: actionRequest); // UI に「ツール実行開始」を通知
            }
            else if (ev.Type == ChatStreamEventType.ToolResult && ev.ActionResult is not null)
            {
                var actionResult = ev.ActionResult with { ConversationId = ev.ActionResult.ConversationId ?? convId };
                await SaveMessageAsync(
                    user,
                    convId,
                    new ChatMessage(ChatRole.Tool, $"[result] {actionResult.ActionName}: {JsonSerializer.Serialize(actionResult.Payload, _serializerOptions)}"),
                    agentNumber,
                    cancellationToken);

                yield return new ChatStreamEvent(
                    ChatStreamEventType.ToolResult,
                    ActionResult: actionResult);
            }
            else
            {
                switch (ev.Type)
                {
                    case ChatStreamEventType.Message when ev.Message is not null:
                        if (ev.Message.Role == ChatRole.Assistant)
                        {
                            assistantBuffer.Append(ev.Message.Content);
                        }

                        yield return ChatStreamEvent.FromMessage(ev.Message);
                        break;
                    case ChatStreamEventType.Completed:
                        if (assistantBuffer.Length > 0)
                        {
                            var finalAssistant = new ChatMessage(ChatRole.Assistant, assistantBuffer.ToString());
                            await SaveMessageAsync(user, convId, finalAssistant, agentNumber, cancellationToken);
                        }

                        yield return ev;
                        assistantBuffer.Clear();
                        break;
                    default:
                        yield return ev;
                        break;
                }
            }
        }
    }

    /// <summary>
    /// メッセージをリポジトリと履歴状態に保存する
    /// </summary>
    /// <param name="user">ユーザー情報</param>
    /// <param name="conversationId">会話ID</param>
    /// <param name="message">保存するメッセージ</param>
    /// <param name="agentNumber">装置エージェント番号</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>保存したメッセージ</returns>
    private async Task<ChatMessage> SaveMessageAsync(UserContext user, string conversationId, ChatMessage message, string? agentNumber, CancellationToken cancellationToken)
    {
        await _chatRepository.AddMessageAsync(user.UserId, conversationId, message, agentNumber, cancellationToken);
        await _history.UpsertAsync(user.UserId, conversationId, message.Content, agentNumber, cancellationToken, preserveExistingTitle: true);
        return message;
    }
}
