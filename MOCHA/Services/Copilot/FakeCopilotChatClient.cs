using System.Runtime.CompilerServices;
using MOCHA.Models.Chat;

namespace MOCHA.Services.Copilot;

/// <summary>
/// Copilot Studio に未接続でも振る舞いを再現できるフェイク実装。
/// テストやローカル開発で注入する。
/// </summary>
public sealed class FakeCopilotChatClient : ICopilotChatClient
{
    private readonly Func<ChatTurn, IEnumerable<ChatStreamEvent>> _script;

    public FakeCopilotChatClient(Func<ChatTurn, IEnumerable<ChatStreamEvent>>? script = null)
    {
        _script = script ?? DefaultScript;
    }

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

    public Task SubmitActionResultAsync(CopilotActionResult result, CancellationToken cancellationToken = default)
    {
        // フェイクなので何もしない。必要に応じてロギングする。
        return Task.CompletedTask;
    }

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
        // まずは受信確認のメッセージを返す
        yield return ChatStreamEvent.FromMessage(
            new ChatMessage(ChatRole.Assistant, $"(fake) 了解: {text}"));

        if (text.Contains("read", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("D100", StringComparison.OrdinalIgnoreCase))
        {
            var payload = new Dictionary<string, object?>
            {
                ["device"] = "D",
                ["addr"] = 100,
                ["length"] = 1
            };
            yield return new ChatStreamEvent(
                ChatStreamEventType.ActionRequest,
                ActionRequest: new CopilotActionRequest(
                    "read_device",
                    turn.ConversationId ?? Guid.NewGuid().ToString("N"),
                    payload));
        }
        else
        {
            yield return ChatStreamEvent.FromMessage(
                new ChatMessage(ChatRole.Assistant, $"Echo: {text}"));
        }

        yield return ChatStreamEvent.Completed(turn.ConversationId);
    }
}
