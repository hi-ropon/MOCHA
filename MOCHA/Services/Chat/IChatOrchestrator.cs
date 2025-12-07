using MOCHA.Models.Chat;

namespace MOCHA.Services.Chat;

/// <summary>
/// チャットの進行とツール実行を調停するインターフェース
/// </summary>
public interface IChatOrchestrator
{
    /// <summary>
    /// ユーザー発話の処理とエージェント応答ストリーム生成
    /// </summary>
    /// <param name="user">ユーザー情報</param>
    /// <param name="conversationId">既存会話ID（未指定の場合は新規）</param>
    /// <param name="text">発話内容</param>
    /// <param name="agentNumber">装置エージェント番号</param>
    /// <param name="attachments">画像添付</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>チャットイベントのストリーム</returns>
    IAsyncEnumerable<ChatStreamEvent> HandleUserMessageAsync(
        UserContext user,
        string? conversationId,
        string text,
        string? agentNumber,
        IReadOnlyList<ImageAttachment>? attachments = null,
        CancellationToken cancellationToken = default);
}
