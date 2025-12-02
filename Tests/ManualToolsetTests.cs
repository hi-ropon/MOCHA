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
using MOCHA.Agents.Domain.Plc;
using MOCHA.Agents.Infrastructure.Clients;
using MOCHA.Agents.Infrastructure.Tools;
using MOCHA.Agents.Infrastructure.Plc;

namespace MOCHA.Tests;

/// <summary>
/// マニュアル系ツールとサブエージェントの構成を検証するテスト
/// </summary>
[TestClass]
public class ManualToolsetTests
{
    /// <summary>
    /// ManualToolset が find/read を提供することを確認
    /// </summary>
    [TestMethod]
    public void ManualToolset_マニュアルツールを提供する()
    {
        var manualTools = new ManualToolset(new DummyManualStore(), NullLogger<ManualToolset>.Instance);

        Assert.AreEqual(2, manualTools.All.Count);
    }

    /// <summary>
    /// OrganizerToolset がサブエージェント呼び出しのみ提供することを確認
    /// </summary>
    [TestMethod]
    public void OrganizerToolset_サブエージェントツールのみ()
    {
        var factory = new DummyFactory(new DummyChatClient());
        var manualTools = new ManualToolset(new DummyManualStore(), NullLogger<ManualToolset>.Instance);
        var manualAgentTool = new ManualAgentTool(factory, manualTools, NullLogger<ManualAgentTool>.Instance);
        var plcTool = new PlcAgentTool(NullLogger<PlcAgentTool>.Instance);
        var plcStore = new PlcDataStore();
        var plcAnalyzer = new PlcProgramAnalyzer(plcStore);
        var plcReasoner = new PlcReasoner();
        var plcManual = new PlcManualService(new DummyManualStore());
        var plcToolset = new PlcToolset(plcStore, new DummyGateway(), plcAnalyzer, plcReasoner, plcManual, NullLogger<PlcToolset>.Instance);
        var organizerToolset = new OrganizerToolset(manualTools, manualAgentTool, plcTool, plcToolset, NullLogger<OrganizerToolset>.Instance);

        Assert.AreEqual(3, organizerToolset.All.Count);
    }

    /// <summary>
    /// ManualAgentTool が ChatClientAgent を経由して結果を返すことを確認
    /// </summary>
    [TestMethod]
    public async Task ManualAgentTool_ChatClient経由で応答する()
    {
        var factory = new DummyFactory(new DummyChatClient(new[] { "tool-result" }));
        var manualTools = new ManualToolset(new DummyManualStore(), NullLogger<ManualToolset>.Instance);
        var tool = new ManualAgentTool(factory, manualTools, NullLogger<ManualAgentTool>.Instance);

        using var _ = manualTools.UseContext("conv-test", _ => { });
        var result = await tool.RunAsync("iaiAgent", "ping");

        StringAssert.Contains(result, "tool-result");
    }

    private sealed class DummyFactory : ILlmChatClientFactory
    {
        private readonly IChatClient _client;

        public DummyFactory(IChatClient client)
        {
            _client = client;
        }

        public IChatClient Create() => _client;
    }

    private sealed class DummyChatClient : IChatClient
    {
        private readonly IReadOnlyList<string>? _chunks;

        public DummyChatClient(IEnumerable<string>? chunks = null)
        {
            _chunks = chunks?.ToList();
        }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            var responseMessage = new ChatMessage(ChatRole.Assistant, "dummy");
            return Task.FromResult(new ChatResponse(responseMessage));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            var chunks = _chunks ?? new[] { "dummy" };
            return StreamChunks(chunks, cancellationToken);
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

    private sealed class DummyManualStore : IManualStore
    {
        public Task<ManualContent?> ReadAsync(string agentName, string relativePath, int? maxBytes = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ManualContent?>(new ManualContent(relativePath, "dummy-content", 10));
        }

        public Task<IReadOnlyList<ManualHit>> SearchAsync(string agentName, string query, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ManualHit> hits = new List<ManualHit> { new("dummy", "path", 1.0) };
            return Task.FromResult(hits);
        }
    }

    private sealed class DummyGateway : IPlcGatewayClient
    {
        public Task<BatchReadResult> ReadBatchAsync(BatchReadRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new BatchReadResult(Array.Empty<DeviceReadResult>()));
        }

        public Task<DeviceReadResult> ReadAsync(DeviceReadRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new DeviceReadResult(request.Spec, Array.Empty<int>(), true, null));
        }
    }
}
