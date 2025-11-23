namespace MOCHA.Models.Chat;

/// <summary>
/// 会話履歴の一覧表示に利用する要約情報。
/// </summary>
internal sealed class ConversationSummary
{
    /// <summary>
    /// 一意なID、タイトル、更新日時、ひも付くエージェントとユーザーを指定して初期化する。
    /// </summary>
    /// <param name="id">会話ID。</param>
    /// <param name="title">会話タイトル。</param>
    /// <param name="updatedAt">最終更新日時。</param>
    /// <param name="agentNumber">関連する装置エージェント番号。</param>
    /// <param name="userId">作成者のユーザーID。</param>
    public ConversationSummary(string id, string title, DateTimeOffset updatedAt, string? agentNumber = null, string? userId = null)
    {
        Id = id;
        Title = title;
        UpdatedAt = updatedAt;
        AgentNumber = agentNumber;
        UserId = userId;
    }

    /// <summary>
    /// 会話ID。
    /// </summary>
    public string Id { get; set; }
    /// <summary>
    /// 会話タイトル。
    /// </summary>
    public string Title { get; set; }
    /// <summary>
    /// 最終更新日時。
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }
    /// <summary>
    /// 関連する装置エージェント番号。
    /// </summary>
    public string? AgentNumber { get; set; }
    /// <summary>
    /// 作成者のユーザーID。
    /// </summary>
    public string? UserId { get; set; }
}
