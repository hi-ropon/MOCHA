using MOCHA.Models.Chat;

namespace MOCHA.Services.Copilot;

public interface ICopilotChatClient
{
    /// <summary>
    /// Copilot Studio (Microsoft 365 Agents SDK) にメッセージを送り、ストリームでイベントを受け取る。
    /// </summary>
    Task<IAsyncEnumerable<ChatStreamEvent>> SendAsync(ChatTurn turn, CancellationToken cancellationToken = default);

    /// <summary>
    /// ツール実行結果を Copilot 側へ返す。
    /// </summary>
    Task SubmitActionResultAsync(CopilotActionResult result, CancellationToken cancellationToken = default);
}
