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
    /// <summary>
    /// タイトル生成済みの会話にメッセージ追加してもタイトルを上書きしない確認
    /// </summary>
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

    /// <summary>
    /// インメモリ DB コンテキストファクトリー生成
    /// </summary>
    /// <param name="name">DB 名</param>
    /// <returns>コンテキストファクトリー</returns>
    private static IDbContextFactory<ChatDbContext> CreateFactory(string name)
    {
        var options = new DbContextOptionsBuilder<ChatDbContext>()
            .UseInMemoryDatabase(name)
            .Options;

        return new InMemoryChatDbContextFactory(options);
    }

    /// <summary>
    /// インメモリ用のコンテキストファクトリー
    /// </summary>
    private sealed class InMemoryChatDbContextFactory : IDbContextFactory<ChatDbContext>
    {
        private readonly DbContextOptions<ChatDbContext> _options;

        /// <summary>
        /// オプション指定による初期化
        /// </summary>
        /// <param name="options">DbContext オプション</param>
        public InMemoryChatDbContextFactory(DbContextOptions<ChatDbContext> options)
        {
            _options = options;
        }

        /// <summary>
        /// DbContext 生成
        /// </summary>
        /// <returns>生成したコンテキスト</returns>
        public ChatDbContext CreateDbContext()
        {
            return new ChatDbContext(_options);
        }
    }
}
