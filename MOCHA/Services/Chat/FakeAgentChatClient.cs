using System.Runtime.CompilerServices;
using MOCHA.Models.Chat;

namespace MOCHA.Services.Chat;

/// <summary>
/// 実接続なしでも振る舞いを再現できるフェイク実装。
/// テストやローカル開発で注入する。
/// </summary>
internal sealed class FakeAgentChatClient : IAgentChatClient
{
    private readonly Func<ChatTurn, IEnumerable<ChatStreamEvent>> _script;

    /// <summary>
    /// 任意のスクリプトを注入して初期化する。未指定の場合は既定スクリプトを使用。
    /// </summary>
    /// <param name="script">チャットターンを受け取りイベント列を返すスクリプト。</param>
    public FakeAgentChatClient(Func<ChatTurn, IEnumerable<ChatStreamEvent>>? script = null)
    {
        _script = script ?? DefaultScript;
    }

    /// <summary>
    /// フェイクのスクリプトを実行し、イベントストリームを返す。
    /// </summary>
    /// <param name="turn">受信したターン。</param>
    /// <param name="cancellationToken">キャンセル通知。</param>
    /// <returns>生成されたイベントストリーム。</returns>
    public Task<IAsyncEnumerable<ChatStreamEvent>> SendAsync(ChatTurn turn, CancellationToken cancellationToken = default)
    {
        async IAsyncEnumerable<ChatStreamEvent> Enumerate([EnumeratorCancellation] CancellationToken ct = default)
        {
            foreach (var ev in _script(turn))
            {
                ct.ThrowIfCancellationRequested();
                yield return ev;
            }
        }

        return Task.FromResult<IAsyncEnumerable<ChatStreamEvent>>(Enumerate(cancellationToken));
    }

    /// <summary>
    /// アクション結果の送信をシミュレートする（何も行わない）。
    /// </summary>
    /// <param name="result">送信する結果。</param>
    /// <param name="cancellationToken">キャンセル通知。</param>
    /// <returns>完了済みタスク。</returns>
    public Task SubmitActionResultAsync(AgentActionResult result, CancellationToken cancellationToken = default)
    {
        // フェイクなので何もしない。必要に応じてロギングする。
        return Task.CompletedTask;
    }

    /// <summary>
    /// 受信メッセージに応じて簡易的なイベントを返す既定スクリプト。
    /// </summary>
    /// <param name="turn">受信したターン。</param>
    /// <returns>生成されたイベント列。</returns>
    private static IEnumerable<ChatStreamEvent> DefaultScript(ChatTurn turn)
    {
        // 最初のユーザー発話を確認し、簡易ルールでアクション要求を返す。
        var latest = turn.Messages.LastOrDefault();
        if (latest is null)
        {
            yield return ChatStreamEvent.Fail("empty turn");
            yield break;
        }

        var text = latest.Content;
        var conversationId = turn.ConversationId ?? Guid.NewGuid().ToString("N");
        // まずは受信確認のメッセージを返す
        yield return ChatStreamEvent.FromMessage(
            new ChatMessage(ChatRole.Assistant, $"(fake) 了解: {text}"));

        if (text.Contains("read", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("D100", StringComparison.OrdinalIgnoreCase))
        {
            var payload = new Dictionary<string, object?>
            {
                ["question"] = text,
                ["device"] = "D",
                ["addr"] = 100,
                ["length"] = 1,
                ["values"] = new List<int> { 42 },
                ["success"] = true
            };
            yield return new ChatStreamEvent(
                ChatStreamEventType.ActionRequest,
                ActionRequest: new AgentActionRequest(
                    "invoke_plc_agent",
                    conversationId,
                    new Dictionary<string, object?>
                    {
                        ["question"] = text
                    }));
            yield return new ChatStreamEvent(
                ChatStreamEventType.ToolResult,
                ActionResult: new AgentActionResult(
                    "invoke_plc_agent",
                    conversationId,
                    true,
                    payload));
        }
        else
        {
            yield return ChatStreamEvent.FromMessage(
                new ChatMessage(ChatRole.Assistant, $"Echo: {text}"));
        }

        yield return ChatStreamEvent.Completed(conversationId);
    }
}
