using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Data;
using MOCHA.Models.Chat;
using MOCHA.Models.Feedback;
using MOCHA.Services.Chat;
using MOCHA.Services.Feedback;

namespace MOCHA.Tests;

/// <summary>
/// フィードバックサービスのバリデーションを検証するテスト
/// </summary>
[TestClass]
public class FeedbackServiceTests
{
    [TestMethod]
    public async Task アシスタントメッセージ_評価をBadからGoodへ変更できる()
    {
        var factory = CreateFactory("feedback-1");
        var chatRepo = new ChatRepository(factory);
        var repository = new FeedbackRepository(factory);
        var service = new FeedbackService(repository, chatRepo);
        var userId = "user-1";
        var conversationId = "conv-1";

        await chatRepo.AddMessageAsync(userId, conversationId, new ChatMessage(ChatRole.User, "質問"), null);
        await chatRepo.AddMessageAsync(userId, conversationId, new ChatMessage(ChatRole.Assistant, "回答"), null);

        var entry = await service.SubmitAsync(userId, conversationId, 1, FeedbackRating.Bad, "理由", default);

        Assert.AreEqual(FeedbackRating.Bad, entry.Rating);
        Assert.AreEqual(1, entry.MessageIndex);

        var badSummary = await service.GetSummaryAsync(userId, conversationId);
        Assert.AreEqual(0, badSummary.GoodCount);
        Assert.AreEqual(1, badSummary.BadCount);
        Assert.AreEqual(1, badSummary.BadRate);

        var updated = await service.SubmitAsync(userId, conversationId, 1, FeedbackRating.Good, null, default);

        Assert.AreEqual(FeedbackRating.Good, updated.Rating);
        Assert.AreEqual(1, updated.MessageIndex);

        var goodSummary = await service.GetSummaryAsync(userId, conversationId);
        Assert.AreEqual(1, goodSummary.GoodCount);
        Assert.AreEqual(0, goodSummary.BadCount);
        Assert.AreEqual(0, goodSummary.BadRate);
    }

    [TestMethod]
    public async Task ユーザーメッセージには評価できない()
    {
        var factory = CreateFactory("feedback-2");
        var chatRepo = new ChatRepository(factory);
        var repository = new FeedbackRepository(factory);
        var service = new FeedbackService(repository, chatRepo);
        var userId = "user-2";
        var conversationId = "conv-2";

        await chatRepo.AddMessageAsync(userId, conversationId, new ChatMessage(ChatRole.User, "質問"), null);

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
            service.SubmitAsync(userId, conversationId, 0, FeedbackRating.Good, null, default));
    }

    [TestMethod]
    public async Task 同じ評価を再送すると削除される()
    {
        var factory = CreateFactory("feedback-3");
        var chatRepo = new ChatRepository(factory);
        var repository = new FeedbackRepository(factory);
        var service = new FeedbackService(repository, chatRepo);
        var userId = "user-3";
        var conversationId = "conv-3";

        await chatRepo.AddMessageAsync(userId, conversationId, new ChatMessage(ChatRole.User, "質問"), null);
        await chatRepo.AddMessageAsync(userId, conversationId, new ChatMessage(ChatRole.Assistant, "回答"), null);

        await service.SubmitAsync(userId, conversationId, 1, FeedbackRating.Good, null, default);
        await service.SubmitAsync(userId, conversationId, 1, FeedbackRating.Good, null, default);

        var ratings = await service.GetRatingsAsync(userId, conversationId, default);
        Assert.IsFalse(ratings.ContainsKey(1));
    }

    private static IDbContextFactory<ChatDbContext> CreateFactory(string name)
    {
        var options = new DbContextOptionsBuilder<ChatDbContext>()
            .UseInMemoryDatabase(name)
            .Options;

        return new InMemoryChatDbContextFactory(options);
    }

    private sealed class InMemoryChatDbContextFactory : IDbContextFactory<ChatDbContext>
    {
        private readonly DbContextOptions<ChatDbContext> _options;

        public InMemoryChatDbContextFactory(DbContextOptions<ChatDbContext> options)
        {
            _options = options;
        }

        public ChatDbContext CreateDbContext()
        {
            return new ChatDbContext(_options);
        }
    }
}
