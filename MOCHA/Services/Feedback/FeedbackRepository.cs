using Microsoft.EntityFrameworkCore;
using MOCHA.Data;
using MOCHA.Models.Feedback;

namespace MOCHA.Services.Feedback;

/// <summary>
/// EF Core を利用したフィードバック永続化リポジトリ
/// </summary>
internal sealed class FeedbackRepository : IFeedbackRepository
{
    private readonly IDbContextFactory<ChatDbContext> _dbContextFactory;

    /// <summary>
    /// DbContextFactory を受け取り初期化する
    /// </summary>
    /// <param name="dbContextFactory">チャット用 DbContextFactory</param>
    public FeedbackRepository(IDbContextFactory<ChatDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<FeedbackEntry?> GetAsync(string conversationId, int messageIndex, string userObjectId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.Feedbacks
            .FirstOrDefaultAsync(x =>
                x.ConversationId == conversationId &&
                x.MessageIndex == messageIndex &&
                x.UserObjectId == userObjectId,
                cancellationToken);

        return entity is null ? null : Map(entity);
    }

    public async Task AddAsync(FeedbackEntry entry, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        db.Feedbacks.Add(new FeedbackEntity
        {
            ConversationId = entry.ConversationId,
            MessageIndex = entry.MessageIndex,
            Rating = entry.Rating.ToString(),
            Comment = entry.Comment,
            UserObjectId = entry.UserObjectId,
            CreatedAt = entry.CreatedAt
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<FeedbackSummary> GetSummaryAsync(string conversationId, string userObjectId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var grouped = await db.Feedbacks
            .Where(x => x.ConversationId == conversationId && x.UserObjectId == userObjectId)
            .GroupBy(x => x.Rating)
            .Select(g => new { Rating = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var good = grouped.FirstOrDefault(x => x.Rating == FeedbackRating.Good.ToString())?.Count ?? 0;
        var bad = grouped.FirstOrDefault(x => x.Rating == FeedbackRating.Bad.ToString())?.Count ?? 0;
        return new FeedbackSummary(good, bad);
    }

    public async Task<IReadOnlyList<FeedbackEntry>> GetRecentBadAsync(string userObjectId, int take, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var list = await db.Feedbacks
            .Where(x => x.UserObjectId == userObjectId && x.Rating == FeedbackRating.Bad.ToString())
            .OrderByDescending(x => x.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

        return list
            .Select(Map)
            .ToList();
    }

    public async Task<IReadOnlyDictionary<int, FeedbackRating>> GetRatingsAsync(string conversationId, string userObjectId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var list = await db.Feedbacks
            .Where(x => x.ConversationId == conversationId && x.UserObjectId == userObjectId)
            .Select(x => new { x.MessageIndex, x.Rating })
            .ToListAsync(cancellationToken);

        return list.ToDictionary(
            x => x.MessageIndex,
            x => Enum.TryParse<FeedbackRating>(x.Rating, out var rating) ? rating : FeedbackRating.Good);
    }

    public async Task DeleteAsync(string conversationId, int messageIndex, string userObjectId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.Feedbacks
            .FirstOrDefaultAsync(x =>
                x.ConversationId == conversationId &&
                x.MessageIndex == messageIndex &&
                x.UserObjectId == userObjectId,
                cancellationToken);

        if (existing is null)
        {
            return;
        }

        db.Feedbacks.Remove(existing);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static FeedbackEntry Map(FeedbackEntity entity)
    {
        return new FeedbackEntry(
            entity.ConversationId,
            entity.MessageIndex,
            Enum.TryParse<FeedbackRating>(entity.Rating, out var rating) ? rating : FeedbackRating.Good,
            entity.Comment,
            entity.UserObjectId,
            entity.CreatedAt);
    }
}
