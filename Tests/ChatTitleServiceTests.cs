using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Models.Chat;
using MOCHA.Services.Chat;

namespace MOCHA.Tests;

/// <summary>
/// タイトル生成オーケストレーターの振る舞いを検証するテスト
/// </summary>
[TestClass]
public class ChatTitleServiceTests
{
    [TestMethod]
    public async Task 初回だけ生成を実行する()
    {
        var generator = new StubGenerator("異音調査");
        var repository = new RecordingChatRepository();
        var history = new ConversationHistoryState(repository);
        var service = new ChatTitleService(generator, repository, history, NullLogger<ChatTitleService>.Instance);
        var user = new UserContext("user-1", "User");

        await service.RequestAsync(user, "conv-1", "モーターから異音がする", "AG-01");
        await service.RequestAsync(user, "conv-1", "再送信しても同じ", "AG-01");

        Assert.AreEqual(1, generator.CallCount, "生成は1回だけ実行されること");
        var summary = history.Summaries.Single();
        Assert.AreEqual("異音調査", summary.Title);
        Assert.AreEqual("異音調査", repository.Summaries.Single().Title);
    }

    [TestMethod]
    public async Task 生成失敗後は次回呼び出しで再試行する()
    {
        var generator = new StubGenerator(new InvalidOperationException("fail"), "再試行成功");
        var repository = new RecordingChatRepository();
        var history = new ConversationHistoryState(repository);
        var service = new ChatTitleService(generator, repository, history, NullLogger<ChatTitleService>.Instance);
        var user = new UserContext("user-1", "User");

        await service.RequestAsync(user, "conv-2", "タイトル失敗テスト", "AG-01");

        Assert.AreEqual(1, generator.CallCount, "失敗しても例外は伝播しないこと");
        Assert.IsFalse(history.Summaries.Any(), "失敗時は履歴を更新しないこと");

        await service.RequestAsync(user, "conv-2", "もう一度生成", "AG-01");

        Assert.AreEqual(2, generator.CallCount, "再試行で2回目が実行されること");
        Assert.AreEqual("再試行成功", history.Summaries.Single().Title);
    }

    [TestMethod]
    public async Task 生成済みなら再実行しない()
    {
        var generator = new StubGenerator("一回目のタイトル", "二回目は使われない");
        var repository = new RecordingChatRepository();
        var history = new ConversationHistoryState(repository);
        var service = new ChatTitleService(generator, repository, history, NullLogger<ChatTitleService>.Instance);
        var user = new UserContext("user-1", "User");

        await service.RequestAsync(user, "conv-3", "最初の発話", "AG-01");
        await service.RequestAsync(user, "conv-3", "二度目の発話", "AG-01");

        Assert.AreEqual(1, generator.CallCount, "既に生成済みなら再実行しないこと");
        Assert.AreEqual("一回目のタイトル", history.Summaries.Single().Title);
    }

    private sealed class StubGenerator : IChatTitleGenerator
    {
        private readonly Queue<Func<Task<string>>> _steps;

        public int CallCount { get; private set; }

        public StubGenerator(params object[] returns)
        {
            _steps = new Queue<Func<Task<string>>>();
            foreach (var result in returns)
            {
                _steps.Enqueue(result switch
                {
                    Exception ex => () => Task.FromException<string>(ex),
                    string s => () => Task.FromResult(s),
                    _ => () => Task.FromResult(string.Empty)
                });
            }
        }

        public Task<string> GenerateAsync(ChatTitleRequest request, CancellationToken cancellationToken = default)
        {
            CallCount++;
            if (_steps.TryDequeue(out var next))
            {
                return next();
            }

            return Task.FromResult(string.Empty);
        }
    }

    private sealed class RecordingChatRepository : IChatRepository
    {
        private readonly List<ConversationSummary> _summaries = new();
        private readonly object _lock = new();

        public IReadOnlyList<ConversationSummary> Summaries
        {
            get
            {
                lock (_lock)
                {
                    return _summaries.ToList();
                }
            }
        }

        public Task AddMessageAsync(string userObjectId, string conversationId, ChatMessage message, string? agentNumber, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteConversationAsync(string userObjectId, string conversationId, string? agentNumber, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                _summaries.RemoveAll(x => x.Id == conversationId && x.UserId == userObjectId && x.AgentNumber == agentNumber);
            }
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(string userObjectId, string conversationId, string? agentNumber = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ChatMessage>>(new List<ChatMessage>());
        }

        public Task<IReadOnlyList<ConversationSummary>> GetSummariesAsync(string userObjectId, string? agentNumber, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                return Task.FromResult<IReadOnlyList<ConversationSummary>>(
                    _summaries.Where(s => s.UserId == userObjectId && s.AgentNumber == agentNumber).ToList());
            }
        }

        public Task UpsertConversationAsync(string userObjectId, string conversationId, string title, string? agentNumber, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                var existing = _summaries.FirstOrDefault(x => x.Id == conversationId && x.UserId == userObjectId && x.AgentNumber == agentNumber);
                var trimmed = title.Length > 30 ? title[..30] + "…" : title;
                if (existing is null)
                {
                    _summaries.Add(new ConversationSummary(conversationId, trimmed, DateTimeOffset.UtcNow, agentNumber, userObjectId));
                }
                else
                {
                    existing.Title = trimmed;
                    existing.UpdatedAt = DateTimeOffset.UtcNow;
                }
            }
            return Task.CompletedTask;
        }
    }
}
