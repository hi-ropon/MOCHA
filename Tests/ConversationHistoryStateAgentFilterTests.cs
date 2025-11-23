using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Models.Chat;
using MOCHA.Services.Chat;

namespace MOCHA.Tests;

/// <summary>
/// ConversationHistoryState のエージェント別フィルタリングを検証するテスト。
/// </summary>
[TestClass]
public class ConversationHistoryStateAgentFilterTests
{
    /// <summary>
    /// 選択したエージェントの履歴のみ読み込まれることを確認する。
    /// </summary>
    [TestMethod]
    public async Task 選択したエージェントのみ履歴を読み込む()
    {
        var repo = new InMemoryChatRepositoryWithAgent();
        var state = new ConversationHistoryState(repo);
        var userId = "user-filter";

        await state.LoadAsync(userId, "A001");
        Assert.AreEqual(0, state.Summaries.Count);

        await state.UpsertAsync(userId, "c1", "Aの会話", "A001");
        await state.UpsertAsync(userId, "c2", "Bの会話", "B002");

        await state.LoadAsync(userId, "A001");
        Assert.AreEqual(1, state.Summaries.Count);
        Assert.AreEqual("A001", state.Summaries[0].AgentNumber);

        await state.LoadAsync(userId, "B002");
        Assert.AreEqual(1, state.Summaries.Count);
        Assert.AreEqual("c2", state.Summaries[0].Id);
    }

    /// <summary>
    /// エージェント番号でフィルタリングするテスト用のインメモリリポジトリ。
    /// </summary>
    private sealed class InMemoryChatRepositoryWithAgent : IChatRepository
    {
        private readonly List<ConversationSummary> _summaries = new();
        private readonly object _lock = new();

        /// <summary>
        /// ユーザーとエージェントで絞り込んだ会話一覧を返す。
        /// </summary>
        public Task<IReadOnlyList<ConversationSummary>> GetSummariesAsync(string userObjectId, string? agentNumber, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                return Task.FromResult<IReadOnlyList<ConversationSummary>>(
                    _summaries.Where(s =>
                            string.Equals(userObjectId, s.UserId, StringComparison.Ordinal) &&
                            string.Equals(agentNumber, s.AgentNumber, StringComparison.Ordinal))
                        .OrderByDescending(s => s.UpdatedAt)
                        .ToList());
            }
        }

        /// <summary>
        /// 会話を追加または更新する。
        /// </summary>
        public Task UpsertConversationAsync(string userObjectId, string conversationId, string title, string? agentNumber, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                var existing = _summaries.FirstOrDefault(s => s.Id == conversationId && s.UserId == userObjectId);
                if (existing is null)
                {
                    _summaries.Add(new ConversationSummary(conversationId, title, DateTimeOffset.UtcNow, agentNumber, userObjectId));
                }
                else
                {
                    existing.Title = title;
                    existing.AgentNumber = agentNumber;
                    existing.UpdatedAt = DateTimeOffset.UtcNow;
                }
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// メッセージ追加を会話の更新として扱う。
        /// </summary>
        public Task AddMessageAsync(string userObjectId, string conversationId, ChatMessage message, string? agentNumber, CancellationToken cancellationToken = default)
        {
            return UpsertConversationAsync(userObjectId, conversationId, message.Content, agentNumber, cancellationToken);
        }

        /// <summary>
        /// テスト用のため空のメッセージ一覧を返す。
        /// </summary>
        public Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(string userObjectId, string conversationId, string? agentNumber = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ChatMessage>>(Array.Empty<ChatMessage>());
        }

        /// <summary>
        /// 指定された会話を削除する。
        /// </summary>
        public Task DeleteConversationAsync(string userObjectId, string conversationId, string? agentNumber, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                _summaries.RemoveAll(s => s.Id == conversationId && s.UserId == userObjectId && s.AgentNumber == agentNumber);
            }
            return Task.CompletedTask;
        }
    }
}
