namespace MOCHA.Agents.Domain;

/// <summary>
/// 会話の1ターン
/// </summary>
public sealed record ChatTurn(AuthorRole Role, string Content, DateTimeOffset Timestamp)
{
    /// <summary>
    /// 添付一覧
    /// </summary>
    public IReadOnlyList<ChatAttachment> Attachments { get; init; } = Array.Empty<ChatAttachment>();

    /// <summary>
    /// ユーザーターン生成
    /// </summary>
    /// <param name="content">発話内容</param>
    /// <param name="attachments">添付画像</param>
    /// <param name="timestamp">発話時刻</param>
    /// <returns>ユーザーターン</returns>
    public static ChatTurn User(string content, IEnumerable<ChatAttachment>? attachments = null, DateTimeOffset? timestamp = null) =>
        new ChatTurn(AuthorRole.User, content, timestamp ?? DateTimeOffset.UtcNow)
        {
            Attachments = attachments?.ToList() ?? new List<ChatAttachment>()
        };

    /// <summary>
    /// アシスタントターン生成
    /// </summary>
    /// <param name="content">応答内容</param>
    /// <param name="timestamp">応答時刻</param>
    /// <returns>アシスタントターン</returns>
    public static ChatTurn Assistant(string content, DateTimeOffset? timestamp = null) =>
        new(AuthorRole.Assistant, content, timestamp ?? DateTimeOffset.UtcNow);
}
