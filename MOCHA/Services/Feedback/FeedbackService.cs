using MOCHA.Models.Chat;
using MOCHA.Models.Feedback;
using MOCHA.Services.Chat;

namespace MOCHA.Services.Feedback;

/// <summary>
/// フィードバック集約と検証を行うサービス
/// </summary>
internal sealed class FeedbackService : IFeedbackService
{
    private readonly IFeedbackRepository _repository;
    private readonly IChatRepository _chatRepository;

    /// <summary>
    /// 依存リポジトリ注入による初期化
    /// </summary>
    /// <param name="repository">フィードバックリポジトリ</param>
    /// <param name="chatRepository">チャットリポジトリ</param>
    public FeedbackService(
        IFeedbackRepository repository,
        IChatRepository chatRepository)
    {
        _repository = repository;
        _chatRepository = chatRepository;
    }

    /// <summary>
    /// フィードバック登録（同一評価再送時は削除）
    /// </summary>
    /// <param name="userObjectId">ユーザーID</param>
    /// <param name="conversationId">会話ID</param>
    /// <param name="messageIndex">対象メッセージインデックス</param>
    /// <param name="rating">評価</param>
    /// <param name="comment">任意コメント</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>登録されたエントリ</returns>
    public async Task<FeedbackEntry> SubmitAsync(
        string userObjectId,
        string conversationId,
        int messageIndex,
        FeedbackRating rating,
        string? comment,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userObjectId))
        {
            throw new InvalidOperationException("ユーザーIDが必要です");
        }

        if (string.IsNullOrWhiteSpace(conversationId))
        {
            throw new InvalidOperationException("会話IDが必要です");
        }

        if (messageIndex < 0)
        {
            throw new InvalidOperationException("メッセージインデックスが不正です");
        }

        var messages = await _chatRepository.GetMessagesAsync(userObjectId, conversationId, cancellationToken: cancellationToken);
        if (messageIndex >= messages.Count)
        {
            throw new InvalidOperationException("対象メッセージが見つかりません");
        }

        var target = messages[messageIndex];
        if (target.Role != ChatRole.Assistant)
        {
            throw new InvalidOperationException("アシスタントメッセージのみ評価できます");
        }

        var existing = await _repository.GetAsync(conversationId, messageIndex, userObjectId, cancellationToken);
        if (existing is not null && existing.Rating == rating)
        {
            await _repository.DeleteAsync(conversationId, messageIndex, userObjectId, cancellationToken);
            return existing;
        }
        else if (existing is not null && existing.Rating != rating)
        {
            await _repository.DeleteAsync(conversationId, messageIndex, userObjectId, cancellationToken);
        }

        var trimmedComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        var entry = new FeedbackEntry(
            conversationId,
            messageIndex,
            rating,
            trimmedComment,
            userObjectId,
            DateTimeOffset.UtcNow);

        await _repository.AddAsync(entry, cancellationToken);
        return entry;
    }

    /// <summary>
    /// 会話単位のフィードバック集計取得
    /// </summary>
    /// <param name="userObjectId">ユーザーID</param>
    /// <param name="conversationId">会話ID</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>集計</returns>
    public Task<FeedbackSummary> GetSummaryAsync(string userObjectId, string conversationId, CancellationToken cancellationToken = default)
    {
        return _repository.GetSummaryAsync(conversationId, userObjectId, cancellationToken);
    }

    /// <summary>
    /// 直近の Bad フィードバック取得
    /// </summary>
    /// <param name="userObjectId">ユーザーID</param>
    /// <param name="take">取得件数</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>Bad フィードバック一覧</returns>
    public Task<IReadOnlyList<FeedbackEntry>> GetRecentBadAsync(string userObjectId, int take, CancellationToken cancellationToken = default)
    {
        return _repository.GetRecentBadAsync(userObjectId, take, cancellationToken);
    }

    /// <summary>
    /// 会話内の評価済みメッセージ一覧取得
    /// </summary>
    /// <param name="userObjectId">ユーザーID</param>
    /// <param name="conversationId">会話ID</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>メッセージインデックスと評価のマップ</returns>
    public Task<IReadOnlyDictionary<int, FeedbackRating>> GetRatingsAsync(string userObjectId, string conversationId, CancellationToken cancellationToken = default)
    {
        return _repository.GetRatingsAsync(conversationId, userObjectId, cancellationToken);
    }

    /// <summary>
    /// 指定フィードバック削除
    /// </summary>
    /// <param name="userObjectId">ユーザーID</param>
    /// <param name="conversationId">会話ID</param>
    /// <param name="messageIndex">メッセージインデックス</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    public Task RemoveAsync(string userObjectId, string conversationId, int messageIndex, CancellationToken cancellationToken = default)
    {
        return _repository.DeleteAsync(conversationId, messageIndex, userObjectId, cancellationToken);
    }
}
