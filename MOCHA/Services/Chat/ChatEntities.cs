namespace MOCHA.Services.Chat;

public class ChatConversationEntity
{
    public string Id { get; set; } = default!;
    public string UserObjectId { get; set; } = default!;
    public string Title { get; set; } = string.Empty;
    public string? AgentNumber { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public List<ChatMessageEntity> Messages { get; set; } = new();
}

public class ChatMessageEntity
{
    public int Id { get; set; }
    public string ConversationId { get; set; } = default!;
    public string UserObjectId { get; set; } = default!;
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }

    public ChatConversationEntity? Conversation { get; set; }
}
