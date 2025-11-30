using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Agents.Infrastructure.Clients;
using MOCHA.Services.Chat;

namespace MOCHA.Tests;

/// <summary>
/// ClientChat を用いたタイトル生成のフォーマットを検証するテスト
/// </summary>
[TestClass]
public class ClientChatTitleGeneratorTests
{
    [TestMethod]
    public async Task 句読点を除去して最大長に丸める()
    {
        var client = new StubChatClient("装置の異音について。長い説明文が続きます。");
        var generator = new ClientChatTitleGenerator(new StubChatClientFactory(client), NullLogger<ClientChatTitleGenerator>.Instance);

        var title = await generator.GenerateAsync(new ChatTitleRequest("conv", "モーターから異音がするので確認してほしい"));

        Assert.AreEqual("装置の異音について", title);
    }

    [TestMethod]
    public async Task 空の応答なら例外を送出する()
    {
        var client = new StubChatClient(string.Empty);
        var generator = new ClientChatTitleGenerator(new StubChatClientFactory(client), NullLogger<ClientChatTitleGenerator>.Instance);

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => generator.GenerateAsync(new ChatTitleRequest("conv", "タイトル生成")));
    }

    private sealed class StubChatClientFactory : ILlmChatClientFactory
    {
        private readonly IChatClient _client;

        public StubChatClientFactory(IChatClient client)
        {
            _client = client;
        }

        public IChatClient Create() => _client;
    }

    private sealed class StubChatClient : IChatClient
    {
        private readonly string _response;

        public StubChatClient(string response)
        {
            _response = response;
        }

        public void Dispose()
        {
        }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, _response));
            return Task.FromResult(response);
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            return AsyncEnumerable.Empty<ChatResponseUpdate>();
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
    }
}
