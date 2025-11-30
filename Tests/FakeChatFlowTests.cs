using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Agents.Application;
using MOCHA.Models.Chat;
using MOCHA.Services.Plc;
using MOCHA.Agents.Domain;
using ChatTurnModel = MOCHA.Models.Chat.ChatTurn;
using MOCHA.Services.Chat;

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
        var toolResult = events.First(e => e.Type == ChatStreamEventType.ToolResult).ActionResult!;
        Assert.AreEqual("D", toolResult.Payload["device"]);
        Assert.AreEqual(100, (int)toolResult.Payload["addr"]);
        var values = (toolResult.Payload["values"] as IEnumerable<int> ?? Array.Empty<int>()).ToList();
        Assert.IsTrue(values.Contains(42));
        var assistantMessages = events.Where(e =>
            e.Type == ChatStreamEventType.Message &&
            e.Message?.Role == ChatRole.Assistant).ToList();
        Assert.IsTrue(assistantMessages.Any(), "アシスタント応答が1件以上あること");
        Assert.IsTrue(assistantMessages.All(m => !m.Message!.Content.Contains("fake Agent")), "フェイク応答を含まないこと");
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
            new ChatStreamEvent(ChatStreamEventType.ActionRequest, ActionRequest: new AgentActionRequest("read_device", turn.ConversationId ?? "conv", new Dictionary<string, object?>())),
            ChatStreamEvent.Completed(turn.ConversationId)
        }, plc);

        var events = await CollectAsync(orchestrator, "read default");

        var toolResult = events.First(e => e.Type == ChatStreamEventType.ToolResult).ActionResult!;
        Assert.AreEqual("D", toolResult.Payload["device"]);
        Assert.AreEqual(0, (int)toolResult.Payload["addr"]);
        Assert.IsTrue((bool)toolResult.Payload["success"]);
        var values = (toolResult.Payload["values"] as IEnumerable<int> ?? Array.Empty<int>()).ToList();
        Assert.AreEqual(7, values.Single());
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
                ActionRequest: new AgentActionRequest(
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
    /// エージェント側から届く ToolResult も保存されることを確認する。
    /// </summary>
    [TestMethod]
    public async Task エージェントのToolResultも保存する()
    {
        var repo = new InMemoryChatRepository();
        var history = new ConversationHistoryState(repo);
        var orchestrator = new ChatOrchestrator(
            new FakeAgentChatClient(turn =>
            {
                var convId = turn.ConversationId ?? "conv-tool";
                return new[]
                {
                    ChatStreamEvent.FromMessage(new ChatMessage(ChatRole.Assistant, "thinking")),
                    new ChatStreamEvent(
                        ChatStreamEventType.ActionRequest,
                        ActionRequest: new AgentActionRequest(
                            "find_manuals",
                            convId,
                            new Dictionary<string, object?>
                            {
                                ["agentName"] = "iaiAgent",
                                ["query"] = "test"
                            })),
                    new ChatStreamEvent(
                        ChatStreamEventType.ToolResult,
                        ActionResult: new AgentActionResult(
                            "find_manuals",
                            convId,
                            true,
                            new Dictionary<string, object?>
                            {
                                ["raw"] = "agent-side"
                            })),
                    ChatStreamEvent.Completed(convId)
                };
            }),
            new FakePlcGatewayClient(),
            repo,
            history,
            new DummyManualStore());

        var user = new UserContext("persist-user", "Persist User");
        var conversationId = "conv-agent-tool";

        await foreach (var _ in orchestrator.HandleUserMessageAsync(user, conversationId, "manuals please", "AG-01"))
        {
        }

        var saved = await repo.GetMessagesAsync(user.UserId, conversationId, "AG-01");
        var toolResults = saved.Where(m => m.Role == ChatRole.Tool && m.Content.StartsWith("[result]")).ToList();

        Assert.AreEqual(2, toolResults.Count, "オーケストレーター分とエージェント分の両方を保持すること");
        Assert.IsTrue(toolResults.Any(r => r.Content.Contains("agent-side")), "エージェントの結果が保存されていること");
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
        return new ChatOrchestrator(new FakeAgentChatClient(script), plc ?? new FakePlcGatewayClient(), repo, history, new DummyManualStore());
    }

    /// <summary>
    /// チャット送信で履歴にユーザーとアシスタントのメッセージが保存されることを確認する。
    /// </summary>
    [TestMethod]
    public async Task チャット送信すると履歴にメッセージが保存される()
    {
        var repo = new InMemoryChatRepository();
        var history = new ConversationHistoryState(repo);
        var orchestrator = new ChatOrchestrator(new FakeAgentChatClient(), new FakePlcGatewayClient(), repo, history, new DummyManualStore());
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
    /// ストリーミングのチャンクは1件のアシスタント発話として永続化されることを確認する。
    /// </summary>
    [TestMethod]
    public async Task ストリームはまとめて1件保存する()
    {
        var repo = new InMemoryChatRepository();
        var history = new ConversationHistoryState(repo);
        var orchestrator = new ChatOrchestrator(
            new FakeAgentChatClient(turn => new[]
            {
                ChatStreamEvent.FromMessage(new ChatMessage(ChatRole.Assistant, "chunk-1")),
                ChatStreamEvent.FromMessage(new ChatMessage(ChatRole.Assistant, "chunk-2")),
                ChatStreamEvent.Completed(turn.ConversationId ?? "conv-stream")
            }),
            new FakePlcGatewayClient(),
            repo,
            history,
            new DummyManualStore());

        var user = new UserContext("stream-user", "Stream User");
        var conversationId = "conv-stream";

        await foreach (var _ in orchestrator.HandleUserMessageAsync(user, conversationId, "hello", "AG-01"))
        {
        }

        var saved = await repo.GetMessagesAsync(user.UserId, conversationId, "AG-01");
        var assistantMessages = saved.Where(m => m.Role == ChatRole.Assistant).ToList();

        Assert.AreEqual(1, assistantMessages.Count, "アシスタント発話が1件だけ保存されること");
        Assert.AreEqual("chunk-1chunk-2", assistantMessages.Single().Content);
    }

    /// <summary>
    /// 履歴削除でサマリとメッセージが除去されることを確認する。
    /// </summary>
    [TestMethod]
    public async Task 履歴削除するとサマリとメッセージが消える()
    {
        var repo = new InMemoryChatRepository();
        var history = new ConversationHistoryState(repo);
        var orchestrator = new ChatOrchestrator(new FakeAgentChatClient(), new FakePlcGatewayClient(), repo, history, new DummyManualStore());
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
        var orchestrator = new ChatOrchestrator(new FakeAgentChatClient(), new FakePlcGatewayClient(), repo, history, new DummyManualStore());
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
