namespace MOCHA.Models.Chat;

public class ConversationSummary
{
    public ConversationSummary(string id, string title, DateTimeOffset updatedAt, string? agentNumber = null, string? userId = null)
    {
        Id = id;
        Title = title;
        UpdatedAt = updatedAt;
        AgentNumber = agentNumber;
        UserId = userId;
    }

    public string Id { get; set; }
    public string Title { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string? AgentNumber { get; set; }
    public string? UserId { get; set; }
}
