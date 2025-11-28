using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Agents.Application;
using MOCHA.Models.Chat;
using MOCHA.Services.Chat;
using MOCHA.Services.Copilot;
using MOCHA.Services.Plc;
using MOCHA.Agents.Domain;
using ChatTurnModel = MOCHA.Models.Chat.ChatTurn;

namespace MOCHA.Tests;

/// <summary>
/// フェイククライアントを用いたチャットフローの動作を検証するテスト。
/// </summary>
[TestClass]
public class FakeChatFlowTests
{
    /// <summary>
    /// プレーンテキスト送信でアシスタント応答と完了イベントが返ることを確認する。
    /// </summary>
    [TestMethod]
    public async Task プレーンテキストならアシスタント応答が返る()
    {
        var orchestrator = CreateOrchestrator();
        var events = await CollectAsync(orchestrator, "こんにちは");

        Assert.IsTrue(events.Any(e => e.Type == ChatStreamEventType.Message && e.Message?.Role == ChatRole.Assistant));
        Assert.IsTrue(events.Any(e => e.Type == ChatStreamEventType.Completed));
    }

    /// <summary>
    /// 読み取りキーワードを含む発話でツール経由の結果が返ることを確認する。
    /// </summary>
    [TestMethod]
    public async Task 読み取りキーワードならツール経由で値を返す()
    {
        var orchestrator = CreateOrchestrator();
        var events = await CollectAsync(orchestrator, "Please read D100");

        Assert.IsTrue(events.Any(e => e.Type == ChatStreamEventType.ActionRequest));
        Assert.IsTrue(events.Any(e => e.Type == ChatStreamEventType.ToolResult));
        Assert.IsTrue(events.Any(e =>
            e.Type == ChatStreamEventType.Message &&
            e.Message?.Role == ChatRole.Assistant &&
            e.Message.Content.Contains("D100") &&
            e.Message.Content.Contains("42")));
        Assert.IsTrue(events.Any(e =>
            e.Type == ChatStreamEventType.Message &&
            e.Message?.Role == ChatRole.Assistant &&
            e.Message.Content.Contains("Agent") &&
            e.Message.Content.Contains("D100")));
        Assert.IsTrue(events.Any(e =>
            e.Type == ChatStreamEventType.Message &&
            e.Message?.Role == ChatRole.Assistant &&
            e.Message.Content.Contains("fake Agent") &&
            e.Message.Content.Contains("D100")));
        Assert.IsTrue(events.Any(e => e.Type == ChatStreamEventType.Completed));
    }

    /// <summary>
    /// 読み取り設定が欠落していても既定値で処理されることを確認する。
    /// </summary>
    [TestMethod]
    public async Task 読み取り設定が欠落しても既定値で処理する()
    {
        var plc = new FakePlcGatewayClient(new Dictionary<string, IReadOnlyList<int>> { ["D0"] = new List<int> { 7 } });
        var orchestrator = CreateOrchestrator(turn => new[]
        {
            ChatStreamEvent.FromMessage(new ChatMessage(ChatRole.Assistant, "ack")),
            new ChatStreamEvent(ChatStreamEventType.ActionRequest, ActionRequest: new CopilotActionRequest("read_device", turn.ConversationId ?? "conv", new Dictionary<string, object?>())),
            ChatStreamEvent.Completed(turn.ConversationId)
        }, plc);

        var events = await CollectAsync(orchestrator, "read default");

        var toolResult = events.First(e => e.Type == ChatStreamEventType.ToolResult).ActionResult!;
        Assert.AreEqual("D", toolResult.Payload["device"]);
        Assert.AreEqual(0, (int)toolResult.Payload["addr"]);
        Assert.IsTrue((bool)toolResult.Payload["success"]);
        Assert.IsTrue(events.Any(e => e.Type == ChatStreamEventType.Message && e.Message?.Content.Contains("D0") == true));
    }

    /// <summary>
    /// 一括読み取りアクションがフェイクで成功することを確認する。
    /// </summary>
    [TestMethod]
    public async Task 一括読み取りが成功する()
    {
        var orchestrator = CreateOrchestrator(turn => new[]
        {
            ChatStreamEvent.FromMessage(new ChatMessage(ChatRole.Assistant, "batch start")),
            new ChatStreamEvent(
                ChatStreamEventType.ActionRequest,
                ActionRequest: new CopilotActionRequest(
                    "batch_read_devices",
                    turn.ConversationId ?? "conv",
                    new Dictionary<string, object?>
                    {
                        ["devices"] = new List<string> { "D100", "M10" }
                    })),
            ChatStreamEvent.Completed(turn.ConversationId)
        });

        var events = await CollectAsync(orchestrator, "batch");

        var toolResult = events.First(e => e.Type == ChatStreamEventType.ToolResult).ActionResult!;
        Assert.IsTrue((bool)toolResult.Payload["success"]);
        var results = toolResult.Payload["results"] as IEnumerable<object?> ?? Array.Empty<object?>();
        Assert.AreEqual(2, results.Count());
    }

    /// <summary>
    /// オーケストレーターのストリームイベントを収集するヘルパー。
    /// </summary>
    private static async Task<List<ChatStreamEvent>> CollectAsync(ChatOrchestrator orchestrator, string text)
    {
        var list = new List<ChatStreamEvent>();
        await foreach (var ev in orchestrator.HandleUserMessageAsync(new UserContext("test-user", "Test User"), null, text, "AG-01"))
        {
            list.Add(ev);
        }

        return list;
    }

    /// <summary>
    /// フェイククライアントとインメモリリポジトリを組み合わせたオーケストレーターを生成する。
    /// </summary>
    private static ChatOrchestrator CreateOrchestrator(Func<ChatTurnModel, IEnumerable<ChatStreamEvent>>? script = null, FakePlcGatewayClient? plc = null)
    {
        var repo = new InMemoryChatRepository();
        var history = new ConversationHistoryState(repo);
        return new ChatOrchestrator(new FakeCopilotChatClient(script), plc ?? new FakePlcGatewayClient(), repo, history, new DummyManualStore());
    }

    /// <summary>
    /// チャット送信で履歴にユーザーとアシスタントのメッセージが保存されることを確認する。
    /// </summary>
    [TestMethod]
    public async Task チャット送信すると履歴にメッセージが保存される()
    {
        var repo = new InMemoryChatRepository();
        var history = new ConversationHistoryState(repo);
        var orchestrator = new ChatOrchestrator(new FakeCopilotChatClient(), new FakePlcGatewayClient(), repo, history, new DummyManualStore());
        var user = new UserContext("test-user", "Test User");
        var conversationId = "conv-1";

        await foreach (var _ in orchestrator.HandleUserMessageAsync(user, conversationId, "こんにちは", "AG-01"))
        {
            // consume
        }

        var saved = await repo.GetMessagesAsync(user.UserId, conversationId, "AG-01");

        Assert.IsTrue(saved.Count >= 2, "ユーザーとアシスタントのメッセージが保存されていること");
        Assert.IsTrue(saved.Any(m => m.Role == ChatRole.User && m.Content.Contains("こんにちは")));
        Assert.IsTrue(saved.Any(m => m.Role == ChatRole.Assistant));
    }

    /// <summary>
    /// 履歴削除でサマリとメッセージが除去されることを確認する。
    /// </summary>
    [TestMethod]
    public async Task 履歴削除するとサマリとメッセージが消える()
    {
        var repo = new InMemoryChatRepository();
        var history = new ConversationHistoryState(repo);
        var orchestrator = new ChatOrchestrator(new FakeCopilotChatClient(), new FakePlcGatewayClient(), repo, history, new DummyManualStore());
        var user = new UserContext("test-user", "Test User");
        var conversationId = "conv-del";

        await foreach (var _ in orchestrator.HandleUserMessageAsync(user, conversationId, "削除テスト", "AG-01"))
        {
        }

        await history.LoadAsync(user.UserId, "AG-01");
        Assert.IsTrue(history.Summaries.Any(s => s.Id == conversationId));

        await history.DeleteAsync(user.UserId, conversationId, "AG-01");

        Assert.IsFalse(history.Summaries.Any(s => s.Id == conversationId));
        var messages = await repo.GetMessagesAsync(user.UserId, conversationId, "AG-01");
        Assert.AreEqual(0, messages.Count);
    }

    /// <summary>
    /// 会話がエージェント番号付きで保存されることを確認する。
    /// </summary>
    [TestMethod]
    public async Task エージェント番号付きで会話が保存される()
    {
        var repo = new InMemoryChatRepository();
        var history = new ConversationHistoryState(repo);
        var orchestrator = new ChatOrchestrator(new FakeCopilotChatClient(), new FakePlcGatewayClient(), repo, history, new DummyManualStore());
        var user = new UserContext("test-user", "Test User");
        var conversationId = "conv-agent";

        await foreach (var _ in orchestrator.HandleUserMessageAsync(user, conversationId, "エージェント指定テスト", "AG-77"))
        {
        }

        await history.LoadAsync(user.UserId, "AG-77");
        Assert.IsTrue(history.Summaries.Any(s => s.Id == conversationId && s.AgentNumber == "AG-77"));
    }

    private sealed class DummyManualStore : IManualStore
    {
        public Task<ManualContent?> ReadAsync(string agentName, string relativePath, int? maxBytes = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ManualContent?>(new ManualContent(relativePath, "dummy manual", 12));
        }

        public Task<IReadOnlyList<ManualHit>> SearchAsync(string agentName, string query, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ManualHit> hits = new List<ManualHit> { new("dummy manual", "dummy.txt", 1.0) };
            return Task.FromResult(hits);
        }
    }

    /// <summary>
    /// インメモリで会話とメッセージを保持するテスト用リポジトリ。
    /// </summary>
    private sealed class InMemoryChatRepository : IChatRepository
    {
        private readonly List<ConversationEntry> _conversations = new();
        private readonly List<MessageEntry> _messages = new();
        private readonly object _lock = new();

        /// <summary>
        /// ユーザーとエージェントで絞り込んだ会話要約を返す。
        /// </summary>
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

        /// <summary>
        /// 会話を追加または更新する。
        /// </summary>
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

        /// <summary>
        /// メッセージを保存する。
        /// </summary>
        public Task AddMessageAsync(string userObjectId, string conversationId, ChatMessage message, string? agentNumber, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                _messages.Add(new MessageEntry(userObjectId, conversationId, message, agentNumber));
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// 指定会話のメッセージ一覧を返す。
        /// </summary>
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

        /// <summary>
        /// 会話とそのメッセージを削除する。
        /// </summary>
        public Task DeleteConversationAsync(string userObjectId, string conversationId, string? agentNumber, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                _conversations.RemoveAll(c => c.UserId == userObjectId && c.Summary.Id == conversationId && c.Summary.AgentNumber == agentNumber);
                _messages.RemoveAll(m => m.UserId == userObjectId && m.ConversationId == conversationId && m.AgentNumber == agentNumber);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// インメモリの会話サマリを保持するエントリ。
        /// </summary>
        private sealed record ConversationEntry(string UserId, ConversationSummary Summary);
        /// <summary>
        /// インメモリのメッセージを保持するエントリ。
        /// </summary>
        private sealed record MessageEntry(string UserId, string ConversationId, ChatMessage Message, string? AgentNumber);
    }
}
