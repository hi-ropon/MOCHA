using MOCHA.Models.Chat;
using MOCHA.Services.Chat;
using Xunit;

namespace MOCHA.Tests;

public class ConversationHistoryStateAgentFilterTests
{
    [Fact]
    public async Task 選択したエージェントのみ履歴を読み込む()
    {
        var repo = new InMemoryChatRepositoryWithAgent();
        var state = new ConversationHistoryState(repo);
        var userId = "user-filter";

        await state.LoadAsync(userId, "A001");
        Assert.Empty(state.Summaries);

        await state.UpsertAsync(userId, "c1", "Aの会話", "A001");
        await state.UpsertAsync(userId, "c2", "Bの会話", "B002");

        await state.LoadAsync(userId, "A001");
        Assert.Single(state.Summaries);
        Assert.Equal("A001", state.Summaries[0].AgentNumber);

        await state.LoadAsync(userId, "B002");
        Assert.Single(state.Summaries);
        Assert.Equal("c2", state.Summaries[0].Id);
    }

    private sealed class InMemoryChatRepositoryWithAgent : IChatRepository
    {
        private readonly List<ConversationSummary> _summaries = new();
        private readonly object _lock = new();

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

        public Task AddMessageAsync(string userObjectId, string conversationId, ChatMessage message, string? agentNumber, CancellationToken cancellationToken = default)
        {
            return UpsertConversationAsync(userObjectId, conversationId, message.Content, agentNumber, cancellationToken);
        }

        public Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(string userObjectId, string conversationId, string? agentNumber = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ChatMessage>>(Array.Empty<ChatMessage>());
        }

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
