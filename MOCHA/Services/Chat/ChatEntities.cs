namespace MOCHA.Services.Chat;

/// <summary>
/// 永続化用の会話エンティティ
/// </summary>
internal sealed class ChatConversationEntity
{
    /// <summary>
    /// 会話ID（主キー）
    /// </summary>
    public string Id { get; set; } = default!;
    /// <summary>
    /// 所有ユーザーのID
    /// </summary>
    public string UserObjectId { get; set; } = default!;
    /// <summary>
    /// 会話タイトル
    /// </summary>
    public string Title { get; set; } = string.Empty;
    /// <summary>
    /// 関連する装置エージェント番号
    /// </summary>
    public string? AgentNumber { get; set; }
    /// <summary>
    /// 最終更新日時
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// 会話に紐づくメッセージ一覧
    /// </summary>
    public List<ChatMessageEntity> Messages { get; set; } = new();
}

/// <summary>
/// 永続化用のメッセージエンティティ
/// </summary>
internal sealed class ChatMessageEntity
{
    /// <summary>
    /// メッセージID（主キー）
    /// </summary>
    public int Id { get; set; }
    /// <summary>
    /// 親会話のID
    /// </summary>
    public string ConversationId { get; set; } = default!;
    /// <summary>
    /// 所有ユーザーID
    /// </summary>
    public string UserObjectId { get; set; } = default!;
    /// <summary>
    /// 発話ロール
    /// </summary>
    public string Role { get; set; } = string.Empty;
    /// <summary>
    /// メッセージ本文
    /// </summary>
    public string Content { get; set; } = string.Empty;
    /// <summary>
    /// 作成日時
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// 親会話へのナビゲーションプロパティ
    /// </summary>
    public ChatConversationEntity? Conversation { get; set; }

    /// <summary>
    /// 添付一覧
    /// </summary>
    public List<ChatAttachmentEntity> Attachments { get; set; } = new();
}

/// <summary>
/// メッセージに紐づく画像添付エンティティ
/// </summary>
internal sealed class ChatAttachmentEntity
{
    /// <summary>
    /// 添付ID（主キー）
    /// </summary>
    public string Id { get; set; } = default!;

    /// <summary>
    /// 親メッセージID
    /// </summary>
    public int MessageId { get; set; }

    /// <summary>
    /// 会話ID
    /// </summary>
    public string ConversationId { get; set; } = default!;

    /// <summary>
    /// 所有ユーザーID
    /// </summary>
    public string UserObjectId { get; set; } = default!;

    /// <summary>
    /// ファイル名
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// コンテンツタイプ
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// バイトサイズ
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// 小サイズプレビュー Base64
    /// </summary>
    public string ThumbSmallBase64 { get; set; } = string.Empty;

    /// <summary>
    /// 中サイズプレビュー Base64
    /// </summary>
    public string ThumbMediumBase64 { get; set; } = string.Empty;

    /// <summary>
    /// 作成日時
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// 親メッセージへのナビゲーション
    /// </summary>
    public ChatMessageEntity? Message { get; set; }
}
