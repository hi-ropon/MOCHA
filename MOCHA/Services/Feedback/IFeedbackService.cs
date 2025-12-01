using MOCHA.Models.Feedback;

namespace MOCHA.Services.Feedback;

/// <summary>
/// 回答フィードバックを扱うアプリケーションサービス
/// </summary>
public interface IFeedbackService
{
    /// <summary>
    /// フィードバックを追加する
    /// </summary>
    /// <param name="userObjectId">評価するユーザーID</param>
    /// <param name="conversationId">会話ID</param>
    /// <param name="messageIndex">対象メッセージインデックス</param>
    /// <param name="rating">評価</param>
    /// <param name="comment">任意コメント</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    Task<FeedbackEntry> SubmitAsync(string userObjectId, string conversationId, int messageIndex, FeedbackRating rating, string? comment, CancellationToken cancellationToken = default);

    /// <summary>
    /// 会話単位の集計を取得する
    /// </summary>
    /// <param name="userObjectId">ユーザーID</param>
    /// <param name="conversationId">会話ID</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    Task<FeedbackSummary> GetSummaryAsync(string userObjectId, string conversationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 最新の Bad ログを取得する
    /// </summary>
    /// <param name="userObjectId">ユーザーID</param>
    /// <param name="take">取得件数</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    Task<IReadOnlyList<FeedbackEntry>> GetRecentBadAsync(string userObjectId, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// 会話内の評価済みメッセージを取得する
    /// </summary>
    /// <param name="userObjectId">ユーザーID</param>
    /// <param name="conversationId">会話ID</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    Task<IReadOnlyDictionary<int, FeedbackRating>> GetRatingsAsync(string userObjectId, string conversationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 付与済みフィードバックを削除する
    /// </summary>
    /// <param name="userObjectId">ユーザーID</param>
    /// <param name="conversationId">会話ID</param>
    /// <param name="messageIndex">メッセージインデックス</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    Task RemoveAsync(string userObjectId, string conversationId, int messageIndex, CancellationToken cancellationToken = default);
}
