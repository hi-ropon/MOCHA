namespace MOCHA.Agents.Domain;

/// <summary>
/// 会話の1ターンを表す。
/// </summary>
public sealed record ChatTurn(AuthorRole Role, string Content, DateTimeOffset Timestamp)
{
    public static ChatTurn User(string content, DateTimeOffset? timestamp = null) =>
        new(AuthorRole.User, content, timestamp ?? DateTimeOffset.UtcNow);

    public static ChatTurn Assistant(string content, DateTimeOffset? timestamp = null) =>
        new(AuthorRole.Assistant, content, timestamp ?? DateTimeOffset.UtcNow);
}
