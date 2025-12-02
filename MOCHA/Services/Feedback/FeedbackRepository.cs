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

    /// <summary>
    /// 既存フィードバック取得
    /// </summary>
    /// <param name="conversationId">会話ID</param>
    /// <param name="messageIndex">メッセージインデックス</param>
    /// <param name="userObjectId">ユーザーID</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>該当エントリ</returns>
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

    /// <summary>
    /// フィードバック追加
    /// </summary>
    /// <param name="entry">保存するレコード</param>
    /// <param name="cancellationToken">キャンセル通知</param>
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

    /// <summary>
    /// 会話単位の集計取得
    /// </summary>
    /// <param name="conversationId">会話ID</param>
    /// <param name="userObjectId">ユーザーID</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>集計</returns>
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

    /// <summary>
    /// 直近の Bad フィードバック取得
    /// </summary>
    /// <param name="userObjectId">ユーザーID</param>
    /// <param name="take">取得件数</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>Bad フィードバック一覧</returns>
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

    /// <summary>
    /// 会話内の評価済みメッセージ取得
    /// </summary>
    /// <param name="conversationId">会話ID</param>
    /// <param name="userObjectId">ユーザーID</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>メッセージインデックスと評価のマップ</returns>
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

    /// <summary>
    /// フィードバック削除
    /// </summary>
    /// <param name="conversationId">会話ID</param>
    /// <param name="messageIndex">メッセージインデックス</param>
    /// <param name="userObjectId">ユーザーID</param>
    /// <param name="cancellationToken">キャンセル通知</param>
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

    /// <summary>
    /// エンティティからドメインモデルへの変換
    /// </summary>
    /// <param name="entity">フィードバックエンティティ</param>
    /// <returns>ドメインモデル</returns>
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
