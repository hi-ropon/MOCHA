using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Agents.Application;
using MOCHA.Agents.Domain;
using MOCHA.Agents.Infrastructure.Agents;
using MOCHA.Agents.Infrastructure.Clients;
using MOCHA.Agents.Infrastructure.Options;
using MOCHA.Agents.Infrastructure.Orchestration;
using MOCHA.Agents.Infrastructure.Tools;

namespace MOCHA.Tests;

[TestClass]
public class AgentFrameworkOrchestratorTests
{
    [TestMethod]
    public async Task オーケストレーター_最小応答を返す()
    {
        var fakeChatClient = new FakeChatClient();
        var factory = new FakeLlmChatClientFactory(fakeChatClient);
        var catalog = new AgentCatalog(new ITaskAgent[]
        {
            new PlcTaskAgent(),
            new IaiTaskAgent(),
            new OrientalTaskAgent()
        });
        var manualStore = new InMemoryManualStore();
        var tools = new OrganizerToolset(manualStore, NullLogger<OrganizerToolset>.Instance, NullLoggerFactory.Instance);
        var options = Options.Create(new LlmOptions
        {
            Provider = ProviderKind.OpenAI,
            Instructions = "echo"
        });

        IAgentOrchestrator orchestrator = new AgentFrameworkOrchestrator(
            factory,
            tools,
            options,
            NullLogger<AgentFrameworkOrchestrator>.Instance);

        var userTurn = ChatTurn.User("ping");
        var context = ChatContext.Empty("conv-1");

        var events = await orchestrator.ReplyAsync(userTurn, context);

        var list = new List<AgentEvent>();
        await foreach (var ev in events)
        {
            list.Add(ev);
        }

        Assert.IsTrue(list.Any());
        Assert.IsTrue(list.Any(e => e.Type == AgentEventType.Message && e.Text == "echo: ping"));
        Assert.IsTrue(list.Any(e => e.Type == AgentEventType.Completed && e.ConversationId == "conv-1"));
    }

    [TestMethod]
    public async Task オーケストレーター_ストリーミングで複数チャンクを返す()
    {
        var fakeChatClient = new FakeChatClient(new[] { "part-1 ", "part-2" });
        var factory = new FakeLlmChatClientFactory(fakeChatClient);
        var catalog = new AgentCatalog(new ITaskAgent[]
        {
            new PlcTaskAgent(),
            new IaiTaskAgent(),
            new OrientalTaskAgent()
        });
        var manualStore = new InMemoryManualStore();
        var tools = new OrganizerToolset(manualStore, NullLogger<OrganizerToolset>.Instance, NullLoggerFactory.Instance);
        var options = Options.Create(new LlmOptions
        {
            Provider = ProviderKind.OpenAI,
            Instructions = "echo"
        });

        IAgentOrchestrator orchestrator = new AgentFrameworkOrchestrator(
            factory,
            tools,
            options,
            NullLogger<AgentFrameworkOrchestrator>.Instance);

        var userTurn = ChatTurn.User("ping");
        var context = ChatContext.Empty("conv-1");

        var events = await orchestrator.ReplyAsync(userTurn, context);
        var chunks = new List<string>();

        await foreach (var ev in events)
        {
            if (ev.Type == AgentEventType.Message && ev.Text is not null)
            {
                chunks.Add(ev.Text);
            }
        }

        CollectionAssert.AreEqual(new[] { "part-1 ", "part-2" }, chunks);
    }

    [TestMethod]
    public void ファクトリ_プロバイダーごとに生成される()
    {
        var openAiOptions = Options.Create(new LlmOptions
        {
            Provider = ProviderKind.OpenAI,
            ApiKey = "dummy-key",
            ModelOrDeployment = "gpt-4o-mini"
        });
        var azureOptions = Options.Create(new LlmOptions
        {
            Provider = ProviderKind.AzureOpenAI,
            ApiKey = "dummy-key",
            Endpoint = "https://dummy.openai.azure.com/",
            ModelOrDeployment = "gpt-4o-mini"
        });

        var openAiFactory = new LlmChatClientFactory(openAiOptions, NullLogger<LlmChatClientFactory>.Instance);
        var azureFactory = new LlmChatClientFactory(azureOptions, NullLogger<LlmChatClientFactory>.Instance);

        Assert.IsNotNull(openAiFactory.Create());
        Assert.IsNotNull(azureFactory.Create());
    }

    private sealed class FakeLlmChatClientFactory : ILlmChatClientFactory
    {
        private readonly IChatClient _client;

        public FakeLlmChatClientFactory(IChatClient client)
        {
            _client = client;
        }

        IChatClient ILlmChatClientFactory.Create() => _client;
    }

    private sealed class FakeChatClient : IChatClient
    {
        private const string _conversationId = "conv-1";
        private readonly IReadOnlyList<string>? _streamingChunks;

        public FakeChatClient(IEnumerable<string>? streamingChunks = null)
        {
            _streamingChunks = streamingChunks?.ToList();
        }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var list = messages.ToList();
            var user = list.LastOrDefault(m =>
                m.Role == ChatRole.User &&
                !m.Text.StartsWith("Respond with a JSON value", StringComparison.OrdinalIgnoreCase));

            user ??= list.LastOrDefault(m => m.Role == ChatRole.User);
            var text = user?.Text ?? list.FirstOrDefault()?.Text ?? string.Empty;
            var responseMessage = new ChatMessage(ChatRole.Assistant, $"echo: {text}");
            var response = new ChatResponse(responseMessage)
            {
                ConversationId = _conversationId
            };
            return Task.FromResult(response);
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var list = messages.ToList();
            var user = list.LastOrDefault(m =>
                m.Role == ChatRole.User &&
                !m.Text.StartsWith("Respond with a JSON value", StringComparison.OrdinalIgnoreCase));

            user ??= list.LastOrDefault(m => m.Role == ChatRole.User);
            var text = user?.Text ?? list.FirstOrDefault()?.Text ?? string.Empty;
            var chunks = _streamingChunks is { Count: > 0 }
                ? _streamingChunks
                : new[] { $"echo: {text}" };

            return StreamChunks(chunks, cancellationToken);
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }

        private static async IAsyncEnumerable<ChatResponseUpdate> StreamChunks(
            IEnumerable<string> chunks,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var chunk in chunks)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return new ChatResponseUpdate(ChatRole.Assistant, chunk);
            }
        }
    }

    private sealed class InMemoryManualStore : IManualStore
    {
        public Task<ManualContent?> ReadAsync(string agentName, string relativePath, int? maxBytes = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ManualContent?>(new ManualContent(relativePath, "dummy", 5));
        }

        public Task<IReadOnlyList<ManualHit>> SearchAsync(string agentName, string query, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ManualHit> hits = new List<ManualHit>
            {
                new("dummy", "path", 1)
            };
            return Task.FromResult(hits);
        }
    }
}
