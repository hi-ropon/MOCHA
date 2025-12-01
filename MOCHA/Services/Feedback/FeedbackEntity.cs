using System;

namespace MOCHA.Services.Feedback;

/// <summary>
/// フィードバック永続化エンティティ
/// </summary>
internal sealed class FeedbackEntity
{
    /// <summary>
    /// 主キー
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 会話ID
    /// </summary>
    public string ConversationId { get; set; } = string.Empty;

    /// <summary>
    /// 対象メッセージのインデックス
    /// </summary>
    public int MessageIndex { get; set; }

    /// <summary>
    /// 評価を付与したユーザーID
    /// </summary>
    public string UserObjectId { get; set; } = string.Empty;

    /// <summary>
    /// 評価種別
    /// </summary>
    public string Rating { get; set; } = string.Empty;

    /// <summary>
    /// 任意コメント
    /// </summary>
    public string? Comment { get; set; }

    /// <summary>
    /// 付与日時
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
}
