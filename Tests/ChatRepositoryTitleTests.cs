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
        await using var context = CreateContext("repo-title");
        var repository = new ChatRepository(context);
        var userId = "user-1";
        var conversationId = "conv-repo";
        var agentNumber = "AG-01";

        await repository.AddMessageAsync(userId, conversationId, new ChatMessage(ChatRole.User, "最初の発話"), agentNumber);
        await repository.UpsertConversationAsync(userId, conversationId, "生成済みタイトル", agentNumber);
        await repository.AddMessageAsync(userId, conversationId, new ChatMessage(ChatRole.User, "次の発話"), agentNumber);

        var summary = (await repository.GetSummariesAsync(userId, agentNumber)).Single();
        Assert.AreEqual("生成済みタイトル", summary.Title);
    }

    private static ChatDbContext CreateContext(string name)
    {
        var options = new DbContextOptionsBuilder<ChatDbContext>()
            .UseInMemoryDatabase(name)
            .Options;

        return new ChatDbContext(options);
    }
}
