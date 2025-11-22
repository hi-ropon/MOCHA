using Microsoft.EntityFrameworkCore;
using MOCHA.Data;
using MOCHA.Models.Chat;

namespace MOCHA.Services.Chat;

public class ChatRepository : IChatRepository
{
    private readonly ChatDbContext _dbContext;

    public ChatRepository(ChatDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<ConversationSummary>> GetSummariesAsync(string userObjectId, CancellationToken cancellationToken = default)
    {
        var list = await _dbContext.Conversations
            .Where(x => x.UserObjectId == userObjectId)
            .Select(x => new ConversationSummary(x.Id, x.Title, x.UpdatedAt))
            .ToListAsync(cancellationToken);

        return list
            .OrderByDescending(x => x.UpdatedAt)
            .ToList();
    }

    public async Task UpsertConversationAsync(string userObjectId, string conversationId, string title, CancellationToken cancellationToken = default)
    {
        var trimmed = title.Length > 30 ? title[..30] + "…" : title;
        var existing = await _dbContext.Conversations
            .FirstOrDefaultAsync(x => x.Id == conversationId && x.UserObjectId == userObjectId, cancellationToken);

        if (existing is null)
        {
            _dbContext.Conversations.Add(new ChatConversationEntity
            {
                Id = conversationId,
                UserObjectId = userObjectId,
                Title = trimmed,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }
        else
        {
            existing.Title = trimmed;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddMessageAsync(string userObjectId, string conversationId, ChatMessage message, CancellationToken cancellationToken = default)
    {
        var conversation = await _dbContext.Conversations
            .FirstOrDefaultAsync(x => x.Id == conversationId && x.UserObjectId == userObjectId, cancellationToken);

        if (conversation is null)
        {
            _dbContext.Conversations.Add(new ChatConversationEntity
            {
                Id = conversationId,
                UserObjectId = userObjectId,
                Title = message.Content.Length > 30 ? message.Content[..30] + "…" : message.Content,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }
        else
        {
            conversation.Title = message.Content.Length > 30 ? message.Content[..30] + "…" : message.Content;
            conversation.UpdatedAt = DateTimeOffset.UtcNow;
        }

        _dbContext.Messages.Add(new ChatMessageEntity
        {
            ConversationId = conversationId,
            UserObjectId = userObjectId,
            Role = message.Role.ToString(),
            Content = message.Content,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
