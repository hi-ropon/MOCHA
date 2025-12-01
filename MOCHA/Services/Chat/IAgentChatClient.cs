using MOCHA.Models.Chat;

namespace MOCHA.Services.Chat;

/// <summary>
/// エージェントチャット送受信を抽象化するクライアントインターフェース
/// </summary>
public interface IAgentChatClient
{
    /// <summary>
    /// エージェントへのメッセージ送信とイベントストリーム受信
    /// </summary>
    /// <param name="turn">送信するターン</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>ストリーミングイベントの列</returns>
    Task<IAsyncEnumerable<ChatStreamEvent>> SendAsync(ChatTurn turn, CancellationToken cancellationToken = default);

    /// <summary>
    /// ツール実行結果のエージェント側への送信
    /// </summary>
    /// <param name="result">実行結果</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>送信タスク</returns>
    Task SubmitActionResultAsync(AgentActionResult result, CancellationToken cancellationToken = default);
}
