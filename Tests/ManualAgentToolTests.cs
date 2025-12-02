using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Agents.Application;
using MOCHA.Agents.Domain;
using MOCHA.Agents.Infrastructure.Clients;
using MOCHA.Agents.Infrastructure.Tools;

namespace MOCHA.Tests;

/// <summary>
/// ManualAgentTool のサブエージェント実行を検証するテスト
/// </summary>
[TestClass]
public class ManualAgentToolTests
{
    /// <summary>
    /// Oriental エージェント経由でフェイク応答を返す
    /// </summary>
    [TestMethod]
    public async Task Orientalエージェント_フェイク応答を返す()
    {
        var factory = new FakeFactory(new FakeChatClient(new[] { "[oriental]" }));
        var manualTools = new ManualToolset(new FakeManualStore(), NullLogger<ManualToolset>.Instance);
        var tool = new ManualAgentTool(factory, manualTools, NullLogger<ManualAgentTool>.Instance);

        using var _ = manualTools.UseContext("conv-oriental", _ => { });
        var result = await tool.RunAsync("orientalAgent", "冷却ファンの確認");

        StringAssert.Contains(result, "[oriental]");
    }

    /// <summary>
    /// エージェント名省略時に IAI エージェントとして応答する
    /// </summary>
    [TestMethod]
    public async Task エージェント名省略_IAIで応答する()
    {
        var factory = new FakeFactory(new FakeChatClient(new[] { "IAI-response" }));
        var manualTools = new ManualToolset(new FakeManualStore(), NullLogger<ManualToolset>.Instance);
        var tool = new ManualAgentTool(factory, manualTools, NullLogger<ManualAgentTool>.Instance);

        using var _ = manualTools.UseContext("conv-iai", _ => { });
        var result = await tool.RunAsync(string.Empty, "RCON 設定");

        StringAssert.Contains(result, "IAI-response");
    }

    /// <summary>
    /// ストリーミング途中のサブエージェント出力がユーザーに漏れないことを確認
    /// </summary>
    [TestMethod]
    public async Task サブエージェントストリーム_メッセージイベントを出さない()
    {
        var factory = new FakeFactory(new FakeChatClient(new[] { "chunk-1", "chunk-2" }));
        var manualTools = new ManualToolset(new FakeManualStore(), NullLogger<ManualToolset>.Instance);
        var tool = new ManualAgentTool(factory, manualTools, NullLogger<ManualAgentTool>.Instance);
        var events = new List<AgentEvent>();

        using var _ = tool.UseContext("conv-stream", ev => events.Add(ev));
        var result = await tool.RunAsync("iaiAgent", "テスト質問");

        Assert.IsFalse(events.Any(e => e.Type == AgentEventType.Message), "サブエージェントのストリーム応答が漏洩しています。");
        StringAssert.Contains(result, "chunk-1");
        StringAssert.Contains(result, "chunk-2");
    }

    private sealed class FakeFactory : ILlmChatClientFactory
    {
        private readonly IChatClient _client;

        public FakeFactory(IChatClient client)
        {
            _client = client;
        }

        public IChatClient Create() => _client;
    }

    private sealed class FakeChatClient : IChatClient
    {
        private readonly IReadOnlyList<string> _chunks;

        public FakeChatClient(IEnumerable<string> chunks)
        {
            _chunks = new List<string>(chunks);
        }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            var responseMessage = new ChatMessage(ChatRole.Assistant, string.Join("", _chunks));
            return Task.FromResult(new ChatResponse(responseMessage));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            return StreamChunks(_chunks, cancellationToken);
        }

        public object? GetService(System.Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }

        private static async IAsyncEnumerable<ChatResponseUpdate> StreamChunks(IEnumerable<string> chunks, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var chunk in chunks)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return new ChatResponseUpdate(ChatRole.Assistant, chunk);
            }

            await Task.CompletedTask;
        }
    }

    private sealed class FakeManualStore : IManualStore
    {
        public Task<ManualContent?> ReadAsync(string agentName, string relativePath, int? maxBytes = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ManualContent?>(new ManualContent(relativePath, "dummy", 10));
        }

        public Task<IReadOnlyList<ManualHit>> SearchAsync(string agentName, string query, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ManualHit> hits = new List<ManualHit> { new("dummy", "path", 1.0) };
            return Task.FromResult(hits);
        }
    }
}
