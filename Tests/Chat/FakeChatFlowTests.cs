using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Models.Chat;
using MOCHA.Services.Agents;
using ChatTurnModel = MOCHA.Models.Chat.ChatTurn;
using MOCHA.Services.Chat;

namespace MOCHA.Tests;

/// <summary>
/// フェイククライアントを用いたチャットフローの動作検証テスト
/// </summary>
[TestClass]
public class FakeChatFlowTests
{
    /// <summary>
    /// プレーンテキスト送信時のアシスタント応答と完了イベント返却確認
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
    /// 読み取りキーワード含み発話時のツール経由結果返却確認
    /// </summary>
    [TestMethod]
    public async Task 読み取りキーワードならツール経由で値を返す()
    {
        var orchestrator = CreateOrchestrator();
        var events = await CollectAsync(orchestrator, "Please read D100");

        Assert.IsTrue(events.Any(e => e.Type == ChatStreamEventType.ActionRequest));
        Assert.IsTrue(events.Any(e => e.Type == ChatStreamEventType.ToolResult));
        var toolResult = events.First(e => e.Type == ChatStreamEventType.ToolResult).ActionResult!;
        Assert.AreEqual("invoke_plc_agent", toolResult.ActionName);
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
    /// 読み取り設定欠落時の既定値処理確認
    /// </summary>
    [TestMethod]
    public async Task 読み取り設定が欠落しても既定値で処理する()
    {
        var orchestrator = CreateOrchestrator(turn => new[]
        {
            ChatStreamEvent.FromMessage(new ChatMessage(ChatRole.Assistant, "ack")),
            new ChatStreamEvent(
                ChatStreamEventType.ActionRequest,
                ActionRequest: new AgentActionRequest(
                    "invoke_plc_agent",
                    turn.ConversationId ?? "conv",
                    new Dictionary<string, object?>())),
            new ChatStreamEvent(
                ChatStreamEventType.ToolResult,
                ActionResult: new AgentActionResult(
                    "invoke_plc_agent",
                    turn.ConversationId ?? "conv",
                    true,
                    new Dictionary<string, object?>
                    {
                        ["device"] = "D",
                        ["addr"] = 0,
                        ["length"] = 1,
                        ["values"] = new List<int> { 7 },
                        ["success"] = true
                    })),
            ChatStreamEvent.Completed(turn.ConversationId)
        });

        var events = await CollectAsync(orchestrator, "read default");

        var toolResult = events.First(e => e.Type == ChatStreamEventType.ToolResult).ActionResult!;
        Assert.AreEqual("D", toolResult.Payload["device"]);
        Assert.AreEqual(0, (int)toolResult.Payload["addr"]);
        Assert.IsTrue((bool)toolResult.Payload["success"]);
        var values = (toolResult.Payload["values"] as IEnumerable<int> ?? Array.Empty<int>()).ToList();
        Assert.AreEqual(7, values.Single());
    }

    /// <summary>
    /// 一括読み取りアクションのフェイク成功確認
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
                    "invoke_plc_agent",
                    turn.ConversationId ?? "conv",
                    new Dictionary<string, object?>
                    {
                        ["devices"] = new List<string> { "D100", "M10" }
                    })),
            new ChatStreamEvent(
                ChatStreamEventType.ToolResult,
                ActionResult: new AgentActionResult(
                    "invoke_plc_agent",
                    turn.ConversationId ?? "conv",
                    true,
                    new Dictionary<string, object?>
                    {
                        ["devices"] = new List<string> { "D100", "M10" },
                        ["results"] = new List<object?>
                        {
                            new { Device = "D100", Values = new List<int> { 1, 2 }, Success = true, Error = (string?)null },
                            new { Device = "M10", Values = new List<int> { 3 }, Success = true, Error = (string?)null }
                        },
                        ["success"] = true
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
    /// エージェント側から届く ToolResult 保存確認
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
            repo,
            history,
            new NoopChatTitleService(),
            new PlcConnectionState());

        var user = new UserContext("persist-user", "Persist User");
        var conversationId = "conv-agent-tool";

        await foreach (var _ in orchestrator.HandleUserMessageAsync(user, conversationId, "manuals please", "AG-01"))
        {
        }

        var saved = await repo.GetMessagesAsync(user.UserId, conversationId, "AG-01");
        var toolResults = saved.Where(m => m.Role == ChatRole.Tool && m.Content.StartsWith("[result]")).ToList();

        Assert.AreEqual(1, toolResults.Count, "エージェントの結果を保持すること");
        Assert.IsTrue(toolResults.Any(r => r.Content.Contains("agent-side")), "エージェントの結果が保存されていること");
    }

    /// <summary>
    /// オーケストレーターのストリームイベント収集ヘルパー
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
    /// フェイククライアントとインメモリリポジトリを組み合わせたオーケストレーター生成
    /// </summary>
    private static ChatOrchestrator CreateOrchestrator(Func<ChatTurnModel, IEnumerable<ChatStreamEvent>>? script = null)
    {
        var repo = new InMemoryChatRepository();
        var history = new ConversationHistoryState(repo);
        var plcState = new PlcConnectionState();
        return new ChatOrchestrator(new FakeAgentChatClient(script), repo, history, new NoopChatTitleService(), plcState);
    }

    /// <summary>
    /// チャット送信時に履歴へユーザーとアシスタントのメッセージが保存される確認
    /// </summary>
    [TestMethod]
    public async Task チャット送信すると履歴にメッセージが保存される()
    {
        var repo = new InMemoryChatRepository();
        var history = new ConversationHistoryState(repo);
        var orchestrator = new ChatOrchestrator(new FakeAgentChatClient(), repo, history, new NoopChatTitleService(), new PlcConnectionState());
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
    /// ストリーミングチャンクの1件発話としての永続化確認
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
            repo,
            history,
            new NoopChatTitleService(),
            new PlcConnectionState());

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
    /// 履歴削除時にサマリとメッセージが除去される確認
    /// </summary>
    [TestMethod]
    public async Task 履歴削除するとサマリとメッセージが消える()
    {
        var repo = new InMemoryChatRepository();
        var history = new ConversationHistoryState(repo);
        var orchestrator = new ChatOrchestrator(new FakeAgentChatClient(), repo, history, new NoopChatTitleService(), new PlcConnectionState());
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
    /// 会話がエージェント番号付きで保存される確認
    /// </summary>
    [TestMethod]
    public async Task エージェント番号付きで会話が保存される()
    {
        var repo = new InMemoryChatRepository();
        var history = new ConversationHistoryState(repo);
        var orchestrator = new ChatOrchestrator(new FakeAgentChatClient(), repo, history, new NoopChatTitleService(), new PlcConnectionState());
        var user = new UserContext("test-user", "Test User");
        var conversationId = "conv-agent";

        await foreach (var _ in orchestrator.HandleUserMessageAsync(user, conversationId, "エージェント指定テスト", "AG-77"))
        {
        }

        await history.LoadAsync(user.UserId, "AG-77");
        Assert.IsTrue(history.Summaries.Any(s => s.Id == conversationId && s.AgentNumber == "AG-77"));
    }

    /// <summary>
    /// PLCオンライン設定がチャットターンへ渡される確認
    /// </summary>
    [TestMethod]
    public async Task PLCオフライン設定で読み取りが無効になる()
    {
        var repo = new InMemoryChatRepository();
        var history = new ConversationHistoryState(repo);
        var plcState = new PlcConnectionState();
        plcState.SetOnline("AG-99", false);
        var client = new CapturingAgentChatClient();
        var orchestrator = new ChatOrchestrator(client, repo, history, new NoopChatTitleService(), plcState);
        var user = new UserContext("plc-user", "PLC User");

        await foreach (var _ in orchestrator.HandleUserMessageAsync(user, null, "ping", "AG-99"))
        {
        }

        Assert.IsNotNull(client.CapturedTurn);
        Assert.IsFalse(client.CapturedTurn!.PlcOnline);
    }

    private sealed class NoopChatTitleService : IChatTitleService
    {
        public Task RequestAsync(UserContext user, string conversationId, string userMessage, string? agentNumber, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class CapturingAgentChatClient : IAgentChatClient
    {
        public ChatTurnModel? CapturedTurn { get; private set; }

        public Task<IAsyncEnumerable<ChatStreamEvent>> SendAsync(ChatTurnModel turn, CancellationToken cancellationToken = default)
        {
            CapturedTurn = turn;

            async IAsyncEnumerable<ChatStreamEvent> Enumerate([EnumeratorCancellation] CancellationToken ct = default)
            {
                ct.ThrowIfCancellationRequested();
                yield return ChatStreamEvent.Completed(turn.ConversationId ?? "captured");
                await Task.CompletedTask;
            }

            return Task.FromResult<IAsyncEnumerable<ChatStreamEvent>>(Enumerate(cancellationToken));
        }

        public Task SubmitActionResultAsync(AgentActionResult result, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// インメモリで会話とメッセージを保持するテスト用リポジトリ
    /// </summary>
    private sealed class InMemoryChatRepository : IChatRepository
    {
        private readonly List<ConversationEntry> _conversations = new();
        private readonly List<MessageEntry> _messages = new();
        private readonly object _lock = new();

        /// <summary>
        /// ユーザーとエージェントで絞り込んだ会話要約を返す処理
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
        /// 会話の追加または更新
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
        /// メッセージ保存
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
        /// 指定会話のメッセージ一覧を返す処理
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
        /// 会話とそのメッセージを削除する処理
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
        /// インメモリの会話サマリを保持するエントリ
        /// </summary>
        private sealed record ConversationEntry(string UserId, ConversationSummary Summary);
        /// <summary>
        /// インメモリのメッセージを保持するエントリ
        /// </summary>
        private sealed record MessageEntry(string UserId, string ConversationId, ChatMessage Message, string? AgentNumber);
    }
}
