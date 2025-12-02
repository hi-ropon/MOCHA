using MOCHA.Models.Feedback;

namespace MOCHA.Services.Feedback;

/// <summary>
/// フィードバック永続化リポジトリ
/// </summary>
internal interface IFeedbackRepository
{
    /// <summary>
    /// 既存のフィードバック取得
    /// </summary>
    /// <param name="conversationId">会話ID</param>
    /// <param name="messageIndex">メッセージインデックス</param>
    /// <param name="userObjectId">ユーザーID</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    Task<FeedbackEntry?> GetAsync(string conversationId, int messageIndex, string userObjectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// フィードバック追加
    /// </summary>
    /// <param name="entry">保存するレコード</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    Task AddAsync(FeedbackEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// 会話単位の集計取得
    /// </summary>
    /// <param name="conversationId">会話ID</param>
    /// <param name="userObjectId">ユーザーID</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    Task<FeedbackSummary> GetSummaryAsync(string conversationId, string userObjectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 直近の Bad 取得
    /// </summary>
    /// <param name="userObjectId">ユーザーID</param>
    /// <param name="take">取得件数</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    Task<IReadOnlyList<FeedbackEntry>> GetRecentBadAsync(string userObjectId, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// 会話内のメッセージごとの評価取得
    /// </summary>
    /// <param name="conversationId">会話ID</param>
    /// <param name="userObjectId">ユーザーID</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    Task<IReadOnlyDictionary<int, FeedbackRating>> GetRatingsAsync(string conversationId, string userObjectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// フィードバック削除
    /// </summary>
    /// <param name="conversationId">会話ID</param>
    /// <param name="messageIndex">メッセージインデックス</param>
    /// <param name="userObjectId">ユーザーID</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    Task DeleteAsync(string conversationId, int messageIndex, string userObjectId, CancellationToken cancellationToken = default);
}
