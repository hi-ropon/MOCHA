using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Data;
using MOCHA.Models.Chat;
using MOCHA.Services.Chat;

namespace MOCHA.Tests;

/// <summary>
/// タイトル生成後の永続化挙動を検証するテスト
/// </summary>
[TestClass]
public class ChatRepositoryTitleTests
{
    [TestMethod]
    public async Task 生成済みタイトルをメッセージ追加で上書きしない()
    {
        var factory = CreateFactory("repo-title");
        var repository = new ChatRepository(factory);
        var userId = "user-1";
        var conversationId = "conv-repo";
        var agentNumber = "AG-01";

        await repository.AddMessageAsync(userId, conversationId, new ChatMessage(ChatRole.User, "最初の発話"), agentNumber);
        await repository.UpsertConversationAsync(userId, conversationId, "生成済みタイトル", agentNumber);
        await repository.AddMessageAsync(userId, conversationId, new ChatMessage(ChatRole.User, "次の発話"), agentNumber);

        var summary = (await repository.GetSummariesAsync(userId, agentNumber)).Single();
        Assert.AreEqual("生成済みタイトル", summary.Title);
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

