using MOCHA.Models.Chat;

namespace MOCHA.Services.Chat;

/// <summary>
/// エージェントチャット送受信を抽象化するクライアントインターフェース。
/// </summary>
public interface ICopilotChatClient
{
    /// <summary>
    /// エージェントにメッセージを送り、ストリームでイベントを受け取る。
    /// </summary>
    /// <param name="turn">送信するターン。</param>
    /// <param name="cancellationToken">キャンセル通知。</param>
    /// <returns>ストリーミングイベントの列。</returns>
    Task<IAsyncEnumerable<ChatStreamEvent>> SendAsync(ChatTurn turn, CancellationToken cancellationToken = default);

    /// <summary>
    /// ツール実行結果をエージェント側へ返す。
    /// </summary>
    /// <param name="result">実行結果。</param>
    /// <param name="cancellationToken">キャンセル通知。</param>
    /// <returns>送信タスク。</returns>
    Task SubmitActionResultAsync(CopilotActionResult result, CancellationToken cancellationToken = default);
}
