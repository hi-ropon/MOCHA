namespace MOCHA.Agents.Domain;

/// <summary>
/// 会話の1ターン
/// </summary>
public sealed record ChatTurn(AuthorRole Role, string Content, DateTimeOffset Timestamp)
{
    /// <summary>
    /// ユーザーターン生成
    /// </summary>
    /// <param name="content">発話内容</param>
    /// <param name="timestamp">発話時刻</param>
    /// <returns>ユーザーターン</returns>
    public static ChatTurn User(string content, DateTimeOffset? timestamp = null) =>
        new(AuthorRole.User, content, timestamp ?? DateTimeOffset.UtcNow);

    /// <summary>
    /// アシスタントターン生成
    /// </summary>
    /// <param name="content">応答内容</param>
    /// <param name="timestamp">応答時刻</param>
    /// <returns>アシスタントターン</returns>
    public static ChatTurn Assistant(string content, DateTimeOffset? timestamp = null) =>
        new(AuthorRole.Assistant, content, timestamp ?? DateTimeOffset.UtcNow);
}
