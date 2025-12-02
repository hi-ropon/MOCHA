namespace MOCHA.Models.Feedback;

/// <summary>
/// フィードバック登録リクエスト
/// </summary>
public sealed class FeedbackRequest
{
    /// <summary>対象会話ID</summary>
    public string ConversationId { get; set; } = string.Empty;
    /// <summary>対象メッセージインデックス</summary>
    public int MessageIndex { get; set; }
    /// <summary>評価種別</summary>
    public FeedbackRating Rating { get; set; }
    /// <summary>任意コメント</summary>
    public string? Comment { get; set; }
}
