using MOCHA.Models.Chat;

namespace MOCHA.Services.Chat;

/// <summary>
/// 会話の永続化を担うリポジトリインターフェース。
/// </summary>
public interface IChatRepository
{
    /// <summary>
    /// ユーザーとエージェントで絞り込んだ会話要約を取得する。
    /// </summary>
    /// <param name="userObjectId">ユーザーID。</param>
    /// <param name="agentNumber">エージェント番号。</param>
    /// <param name="cancellationToken">キャンセル通知。</param>
    /// <returns>会話要約リスト。</returns>
    Task<IReadOnlyList<ConversationSummary>> GetSummariesAsync(string userObjectId, string? agentNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// 会話タイトルを保存または更新する。
    /// </summary>
    /// <param name="userObjectId">ユーザーID。</param>
    /// <param name="conversationId">会話ID。</param>
    /// <param name="title">会話タイトル。</param>
    /// <param name="agentNumber">エージェント番号。</param>
    /// <param name="cancellationToken">キャンセル通知。</param>
    Task UpsertConversationAsync(string userObjectId, string conversationId, string title, string? agentNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// 会話にメッセージを追加する。
    /// </summary>
    /// <param name="userObjectId">ユーザーID。</param>
    /// <param name="conversationId">会話ID。</param>
    /// <param name="message">追加するメッセージ。</param>
    /// <param name="agentNumber">エージェント番号。</param>
    /// <param name="cancellationToken">キャンセル通知。</param>
    Task AddMessageAsync(string userObjectId, string conversationId, ChatMessage message, string? agentNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// 会話に紐づくメッセージ一覧を取得する。
    /// </summary>
    /// <param name="userObjectId">ユーザーID。</param>
    /// <param name="conversationId">会話ID。</param>
    /// <param name="agentNumber">エージェント番号。</param>
    /// <param name="cancellationToken">キャンセル通知。</param>
    /// <returns>メッセージ一覧。</returns>
    Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(string userObjectId, string conversationId, string? agentNumber = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 指定会話を削除する。
    /// </summary>
    /// <param name="userObjectId">ユーザーID。</param>
    /// <param name="conversationId">会話ID。</param>
    /// <param name="agentNumber">エージェント番号。</param>
    /// <param name="cancellationToken">キャンセル通知。</param>
    Task DeleteConversationAsync(string userObjectId, string conversationId, string? agentNumber, CancellationToken cancellationToken = default);
}
