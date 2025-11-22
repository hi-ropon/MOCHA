using Microsoft.EntityFrameworkCore;
using MOCHA.Data;
using MOCHA.Models.Chat;

namespace MOCHA.Services.Chat;

/// <summary>
/// EF Core を利用した会話永続化リポジトリ。
/// </summary>
public class ChatRepository : IChatRepository
{
    private readonly IChatDbContext _dbContext;

    /// <summary>
    /// DbContext を注入してリポジトリを初期化する。
    /// </summary>
    /// <param name="dbContext">チャット用 DbContext。</param>
    public ChatRepository(IChatDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// ユーザー・エージェントに応じた会話要約一覧を取得する。
    /// </summary>
    /// <param name="userObjectId">ユーザーID。</param>
    /// <param name="agentNumber">エージェント番号。</param>
    /// <param name="cancellationToken">キャンセル通知。</param>
    /// <returns>会話要約リスト。</returns>
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

    /// <summary>
    /// 会話タイトルを挿入または更新し、更新日時を記録する。
    /// </summary>
    /// <param name="userObjectId">ユーザーID。</param>
    /// <param name="conversationId">会話ID。</param>
    /// <param name="title">タイトル。</param>
    /// <param name="agentNumber">エージェント番号。</param>
    /// <param name="cancellationToken">キャンセル通知。</param>
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

    /// <summary>
    /// メッセージを保存し、会話のタイトルと更新日時も合わせて更新する。
    /// </summary>
    /// <param name="userObjectId">ユーザーID。</param>
    /// <param name="conversationId">会話ID。</param>
    /// <param name="message">保存するメッセージ。</param>
    /// <param name="agentNumber">エージェント番号。</param>
    /// <param name="cancellationToken">キャンセル通知。</param>
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

    /// <summary>
    /// 会話に紐づくメッセージを時系列で取得する。
    /// </summary>
    /// <param name="userObjectId">ユーザーID。</param>
    /// <param name="conversationId">会話ID。</param>
    /// <param name="agentNumber">エージェント番号。</param>
    /// <param name="cancellationToken">キャンセル通知。</param>
    /// <returns>メッセージ一覧。</returns>
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

    /// <summary>
    /// 会話を削除する。エージェント番号も一致しない場合は削除しない。
    /// </summary>
    /// <param name="userObjectId">ユーザーID。</param>
    /// <param name="conversationId">会話ID。</param>
    /// <param name="agentNumber">エージェント番号。</param>
    /// <param name="cancellationToken">キャンセル通知。</param>
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

    /// <summary>
    /// 文字列のロール名を列挙型に変換する。失敗時は Assistant とする。
    /// </summary>
    /// <param name="role">ロール名文字列。</param>
    /// <returns>変換結果。</returns>
    private static ChatRole ParseRole(string role)
    {
        return Enum.TryParse<ChatRole>(role, ignoreCase: true, out var parsed)
            ? parsed
            : ChatRole.Assistant;
    }
}
