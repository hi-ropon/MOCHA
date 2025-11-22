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
        await foreach (var ev in orchestrator.HandleUserMessageAsync(new UserContext("test-user", "Test User"), null, text))
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

    private sealed class InMemoryChatRepository : IChatRepository
    {
        private readonly List<ConversationSummary> _conversations = new();
        private readonly List<ChatMessage> _messages = new();
        private readonly object _lock = new();

        public Task<IReadOnlyList<ConversationSummary>> GetSummariesAsync(string userObjectId, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                return Task.FromResult<IReadOnlyList<ConversationSummary>>(
                    _conversations
                        .OrderByDescending(c => c.UpdatedAt)
                        .ToList());
            }
        }

        public Task UpsertConversationAsync(string userObjectId, string conversationId, string title, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                var trimmed = title.Length > 30 ? title[..30] + "…" : title;
                var existing = _conversations.FirstOrDefault(c => c.Id == conversationId);
                if (existing is null)
                {
                    _conversations.Add(new ConversationSummary(conversationId, trimmed, DateTimeOffset.UtcNow));
                }
                else
                {
                    existing.Title = trimmed;
                    existing.UpdatedAt = DateTimeOffset.UtcNow;
                }
            }
            return Task.CompletedTask;
        }

        public Task AddMessageAsync(string userObjectId, string conversationId, ChatMessage message, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                _messages.Add(message);
            }
            return Task.CompletedTask;
        }
    }
}
