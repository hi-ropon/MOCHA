using Microsoft.EntityFrameworkCore;
using MOCHA.Data;
using MOCHA.Models.Chat;

namespace MOCHA.Services.Chat;

public class ChatRepository : IChatRepository
{
    private readonly IChatDbContext _dbContext;

    public ChatRepository(IChatDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<ConversationSummary>> GetSummariesAsync(string userObjectId, string? agentNumber, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Conversations
            .Where(x => x.UserObjectId == userObjectId);

        if (agentNumber is null)
        {
            query = query.Where(x => x.AgentNumber == null);
        }
        else
        {
            query = query.Where(x => x.AgentNumber == agentNumber);
        }

        var list = await query
            .Select(x => new ConversationSummary(x.Id, x.Title, x.UpdatedAt, x.AgentNumber, x.UserObjectId))
            .ToListAsync(cancellationToken);

        return list
            .OrderByDescending(x => x.UpdatedAt)
            .ToList();
    }

    public async Task UpsertConversationAsync(string userObjectId, string conversationId, string title, string? agentNumber, CancellationToken cancellationToken = default)
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
                AgentNumber = agentNumber,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }
        else
        {
            existing.Title = trimmed;
            existing.AgentNumber ??= agentNumber;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddMessageAsync(string userObjectId, string conversationId, ChatMessage message, string? agentNumber, CancellationToken cancellationToken = default)
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
                AgentNumber = agentNumber,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }
        else
        {
            conversation.Title = message.Content.Length > 30 ? message.Content[..30] + "…" : message.Content;
            conversation.AgentNumber ??= agentNumber;
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

    public async Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(string userObjectId, string conversationId, string? agentNumber = null, CancellationToken cancellationToken = default)
    {
        var messagesQuery = _dbContext.Messages
            .Where(x => x.UserObjectId == userObjectId && x.ConversationId == conversationId);

        if (agentNumber is null)
        {
            messagesQuery = messagesQuery.Where(x =>
                _dbContext.Conversations.Any(c =>
                    c.Id == x.ConversationId &&
                    c.UserObjectId == userObjectId &&
                    c.AgentNumber == null));
        }
        else
        {
            messagesQuery = messagesQuery.Where(x =>
                _dbContext.Conversations.Any(c =>
                    c.Id == x.ConversationId &&
                    c.UserObjectId == userObjectId &&
                    c.AgentNumber == agentNumber));
        }

        var list = await messagesQuery
            .Select(x => new
            {
                x.Role,
                x.Content,
                x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return list
            .OrderBy(x => x.CreatedAt)
            .Select(x => new ChatMessage(ParseRole(x.Role), x.Content))
            .ToList();
    }

    public async Task DeleteConversationAsync(string userObjectId, string conversationId, string? agentNumber, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.Conversations
            .FirstOrDefaultAsync(x => x.Id == conversationId && x.UserObjectId == userObjectId, cancellationToken);

        if (existing is null)
        {
            return;
        }

        if (!string.Equals(existing.AgentNumber, agentNumber, StringComparison.Ordinal))
        {
            return;
        }

        _dbContext.Conversations.Remove(existing);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static ChatRole ParseRole(string role)
    {
        return Enum.TryParse<ChatRole>(role, ignoreCase: true, out var parsed)
            ? parsed
            : ChatRole.Assistant;
    }
}
