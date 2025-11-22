using MOCHA.Models.Chat;
using MOCHA.Services.Chat;
using MOCHA.Services.Copilot;
using MOCHA.Services.Plc;
using Xunit;

namespace MOCHA.Tests;

public class FakeChatFlowTests
{
    [Fact]
    public async Task プレーンテキストならアシスタント応答が返る()
    {
        var orchestrator = CreateOrchestrator();
        var events = await CollectAsync(orchestrator, "こんにちは");

        Assert.Contains(events, e => e.Type == ChatStreamEventType.Message && e.Message?.Role == ChatRole.Assistant);
        Assert.Contains(events, e => e.Type == ChatStreamEventType.Completed);
    }

    [Fact]
    public async Task 読み取りキーワードならツール経由で値を返す()
    {
        var orchestrator = CreateOrchestrator();
        var events = await CollectAsync(orchestrator, "Please read D100");

        Assert.Contains(events, e => e.Type == ChatStreamEventType.ActionRequest);
        Assert.Contains(events, e => e.Type == ChatStreamEventType.ToolResult);
        Assert.Contains(events, e =>
            e.Type == ChatStreamEventType.Message &&
            e.Message?.Role == ChatRole.Assistant &&
            e.Message.Content.Contains("D100") &&
            e.Message.Content.Contains("42"));
        Assert.Contains(events, e =>
            e.Type == ChatStreamEventType.Message &&
            e.Message?.Role == ChatRole.Assistant &&
            e.Message.Content.Contains("Copilot Studio") &&
            e.Message.Content.Contains("D100"));
        Assert.Contains(events, e =>
            e.Type == ChatStreamEventType.Message &&
            e.Message?.Role == ChatRole.Assistant &&
            e.Message.Content.Contains("fake Copilot") &&
            e.Message.Content.Contains("D100"));
        Assert.Contains(events, e => e.Type == ChatStreamEventType.Completed);
    }

    private static async Task<List<ChatStreamEvent>> CollectAsync(ChatOrchestrator orchestrator, string text)
    {
        var list = new List<ChatStreamEvent>();
        await foreach (var ev in orchestrator.HandleUserMessageAsync(new UserContext("test-user", "Test User"), null, text, "AG-01"))
        {
            list.Add(ev);
        }

        return list;
    }

    private static ChatOrchestrator CreateOrchestrator()
    {
        var repo = new InMemoryChatRepository();
        var history = new ConversationHistoryState(repo);
        return new ChatOrchestrator(new FakeCopilotChatClient(), new FakePlcGatewayClient(), repo, history);
    }

    [Fact]
    public async Task チャット送信すると履歴にメッセージが保存される()
    {
        var repo = new InMemoryChatRepository();
        var history = new ConversationHistoryState(repo);
        var orchestrator = new ChatOrchestrator(new FakeCopilotChatClient(), new FakePlcGatewayClient(), repo, history);
        var user = new UserContext("test-user", "Test User");
        var conversationId = "conv-1";

        await foreach (var _ in orchestrator.HandleUserMessageAsync(user, conversationId, "こんにちは", "AG-01"))
        {
            // consume
        }

        var saved = await repo.GetMessagesAsync(user.UserId, conversationId, "AG-01");

        Assert.True(saved.Count >= 2, "ユーザーとアシスタントのメッセージが保存されていること");
        Assert.Contains(saved, m => m.Role == ChatRole.User && m.Content.Contains("こんにちは"));
        Assert.Contains(saved, m => m.Role == ChatRole.Assistant);
    }

    [Fact]
    public async Task 履歴削除するとサマリとメッセージが消える()
    {
        var repo = new InMemoryChatRepository();
        var history = new ConversationHistoryState(repo);
        var orchestrator = new ChatOrchestrator(new FakeCopilotChatClient(), new FakePlcGatewayClient(), repo, history);
        var user = new UserContext("test-user", "Test User");
        var conversationId = "conv-del";

        await foreach (var _ in orchestrator.HandleUserMessageAsync(user, conversationId, "削除テスト", "AG-01"))
        {
        }

        await history.LoadAsync(user.UserId, "AG-01");
        Assert.Contains(history.Summaries, s => s.Id == conversationId);

        await history.DeleteAsync(user.UserId, conversationId, "AG-01");

        Assert.DoesNotContain(history.Summaries, s => s.Id == conversationId);
        var messages = await repo.GetMessagesAsync(user.UserId, conversationId, "AG-01");
        Assert.Empty(messages);
    }

    [Fact]
    public async Task エージェント番号付きで会話が保存される()
    {
        var repo = new InMemoryChatRepository();
        var history = new ConversationHistoryState(repo);
        var orchestrator = new ChatOrchestrator(new FakeCopilotChatClient(), new FakePlcGatewayClient(), repo, history);
        var user = new UserContext("test-user", "Test User");
        var conversationId = "conv-agent";

        await foreach (var _ in orchestrator.HandleUserMessageAsync(user, conversationId, "エージェント指定テスト", "AG-77"))
        {
        }

        await history.LoadAsync(user.UserId, "AG-77");
        Assert.Contains(history.Summaries, s => s.Id == conversationId && s.AgentNumber == "AG-77");
    }

    private sealed class InMemoryChatRepository : IChatRepository
    {
        private readonly List<ConversationEntry> _conversations = new();
        private readonly List<MessageEntry> _messages = new();
        private readonly object _lock = new();

        public Task<IReadOnlyList<ConversationSummary>> GetSummariesAsync(string userObjectId, string? agentNumber, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                return Task.FromResult<IReadOnlyList<ConversationSummary>>(
                    _conversations
                        .Where(c => c.UserId == userObjectId && c.Summary.AgentNumber == agentNumber)
                        .Select(c => c.Summary)
                        .OrderByDescending(c => c.UpdatedAt)
                        .ToList());
            }
        }

        public Task UpsertConversationAsync(string userObjectId, string conversationId, string title, string? agentNumber, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                var trimmed = title.Length > 30 ? title[..30] + "…" : title;
                var existing = _conversations.FirstOrDefault(c => c.UserId == userObjectId && c.Summary.Id == conversationId);
                if (existing is null)
                {
                    _conversations.Add(new ConversationEntry(
                        userObjectId,
                        new ConversationSummary(conversationId, trimmed, DateTimeOffset.UtcNow, agentNumber, userObjectId)));
                }
                else
                {
                    existing.Summary.Title = trimmed;
                    existing.Summary.UpdatedAt = DateTimeOffset.UtcNow;
                    existing.Summary.AgentNumber = agentNumber ?? existing.Summary.AgentNumber;
                }
            }
            return Task.CompletedTask;
        }

        public Task AddMessageAsync(string userObjectId, string conversationId, ChatMessage message, string? agentNumber, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                _messages.Add(new MessageEntry(userObjectId, conversationId, message, agentNumber));
            }
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(string userObjectId, string conversationId, string? agentNumber = null, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                return Task.FromResult<IReadOnlyList<ChatMessage>>(
                    _messages
                        .Where(x => x.UserId == userObjectId && x.ConversationId == conversationId && x.AgentNumber == agentNumber)
                        .Select(x => x.Message)
                        .ToList());
            }
        }

        public Task DeleteConversationAsync(string userObjectId, string conversationId, string? agentNumber, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                _conversations.RemoveAll(c => c.UserId == userObjectId && c.Summary.Id == conversationId && c.Summary.AgentNumber == agentNumber);
                _messages.RemoveAll(m => m.UserId == userObjectId && m.ConversationId == conversationId && m.AgentNumber == agentNumber);
            }

            return Task.CompletedTask;
        }

        private sealed record ConversationEntry(string UserId, ConversationSummary Summary);
        private sealed record MessageEntry(string UserId, string ConversationId, ChatMessage Message, string? AgentNumber);
    }
}
