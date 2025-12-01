using System;

namespace MOCHA.Models.Feedback;

/// <summary>
/// 回答へのフィードバック種別
/// </summary>
public enum FeedbackRating
{
    Good,
    Bad
}

/// <summary>
/// 付与されたフィードバックの記録
/// </summary>
/// <param name="ConversationId">対象会話ID</param>
/// <param name="MessageIndex">対象メッセージのインデックス</param>
/// <param name="Rating">評価</param>
/// <param name="Comment">任意コメント</param>
/// <param name="UserObjectId">付与ユーザーID</param>
/// <param name="CreatedAt">付与日時</param>
public sealed record FeedbackEntry(
    string ConversationId,
    int MessageIndex,
    FeedbackRating Rating,
    string? Comment,
    string UserObjectId,
    DateTimeOffset CreatedAt);

/// <summary>
/// 会話単位のフィードバック集計
/// </summary>
/// <param name="GoodCount">Good 件数</param>
/// <param name="BadCount">Bad 件数</param>
public sealed record FeedbackSummary(int GoodCount, int BadCount)
{
    /// <summary>
    /// Bad 割合（0-1）
    /// </summary>
    public double BadRate => GoodCount + BadCount == 0
        ? 0
        : (double)BadCount / (GoodCount + BadCount);
}
