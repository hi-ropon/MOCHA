namespace MOCHA.Services.Chat;

/// <summary>
/// 永続化用の会話エンティティ。
/// </summary>
internal sealed class ChatConversationEntity
{
    /// <summary>
    /// 会話ID（主キー）。
    /// </summary>
    public string Id { get; set; } = default!;
    /// <summary>
    /// 所有ユーザーのID。
    /// </summary>
    public string UserObjectId { get; set; } = default!;
    /// <summary>
    /// 会話タイトル。
    /// </summary>
    public string Title { get; set; } = string.Empty;
    /// <summary>
    /// 関連する装置エージェント番号。
    /// </summary>
    public string? AgentNumber { get; set; }
    /// <summary>
    /// 最終更新日時。
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// 会話に紐づくメッセージ一覧。
    /// </summary>
    public List<ChatMessageEntity> Messages { get; set; } = new();
}

/// <summary>
/// 永続化用のメッセージエンティティ。
/// </summary>
internal sealed class ChatMessageEntity
{
    /// <summary>
    /// メッセージID（主キー）。
    /// </summary>
    public int Id { get; set; }
    /// <summary>
    /// 親会話のID。
    /// </summary>
    public string ConversationId { get; set; } = default!;
    /// <summary>
    /// 所有ユーザーID。
    /// </summary>
    public string UserObjectId { get; set; } = default!;
    /// <summary>
    /// 発話ロール。
    /// </summary>
    public string Role { get; set; } = string.Empty;
    /// <summary>
    /// メッセージ本文。
    /// </summary>
    public string Content { get; set; } = string.Empty;
    /// <summary>
    /// 作成日時。
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// 親会話へのナビゲーションプロパティ。
    /// </summary>
    public ChatConversationEntity? Conversation { get; set; }
}
