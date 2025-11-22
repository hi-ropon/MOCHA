namespace MOCHA.Models.Chat;

public class ConversationSummary
{
    public ConversationSummary(string id, string title, DateTimeOffset updatedAt)
    {
        Id = id;
        Title = title;
        UpdatedAt = updatedAt;
    }

    public string Id { get; set; }
    public string Title { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
