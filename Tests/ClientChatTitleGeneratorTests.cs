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
    /// <summary>
    /// 句読点除去と最大長丸めの確認
    /// </summary>
    [TestMethod]
    public async Task 句読点を除去して最大長に丸める()
    {
        var client = new StubChatClient("装置の異音について。長い説明文が続きます。");
        var generator = new ClientChatTitleGenerator(new StubChatClientFactory(client), NullLogger<ClientChatTitleGenerator>.Instance);

        var title = await generator.GenerateAsync(new ChatTitleRequest("conv", "モーターから異音がするので確認してほしい"));

        Assert.AreEqual("装置の異音について", title);
    }

    /// <summary>
    /// 空応答時に例外を送出する確認
    /// </summary>
    [TestMethod]
    public async Task 空の応答なら例外を送出する()
    {
        var client = new StubChatClient(string.Empty);
        var generator = new ClientChatTitleGenerator(new StubChatClientFactory(client), NullLogger<ClientChatTitleGenerator>.Instance);

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => generator.GenerateAsync(new ChatTitleRequest("conv", "タイトル生成")));
    }

    /// <summary>
    /// 固定クライアントを返すスタブファクトリー
    /// </summary>
    private sealed class StubChatClientFactory : ILlmChatClientFactory
    {
        private readonly IChatClient _client;

        /// <summary>
        /// クライアント注入による初期化
        /// </summary>
        /// <param name="client">チャットクライアント</param>
        public StubChatClientFactory(IChatClient client)
        {
            _client = client;
        }

        /// <summary>
        /// クライアント生成
        /// </summary>
        /// <returns>注入済みクライアント</returns>
        public IChatClient Create() => _client;
    }

    /// <summary>
    /// 固定応答を返すスタブチャットクライアント
    /// </summary>
    private sealed class StubChatClient : IChatClient
    {
        private readonly string _response;

        /// <summary>
        /// 返却メッセージ指定による初期化
        /// </summary>
        /// <param name="response">返却メッセージ</param>
        public StubChatClient(string response)
        {
            _response = response;
        }

        /// <summary>
        /// リソース破棄（何もしない）
        /// </summary>
        public void Dispose()
        {
        }

        /// <summary>
        /// 単発応答生成
        /// </summary>
        /// <param name="messages">入力メッセージ</param>
        /// <param name="options">オプション</param>
        /// <param name="cancellationToken">キャンセル通知</param>
        /// <returns>応答</returns>
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, _response));
            return Task.FromResult(response);
        }

        /// <summary>
        /// ストリーミング応答生成（空）
        /// </summary>
        /// <param name="messages">入力メッセージ</param>
        /// <param name="options">オプション</param>
        /// <param name="cancellationToken">キャンセル通知</param>
        /// <returns>空ストリーム</returns>
        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            return AsyncEnumerable.Empty<ChatResponseUpdate>();
        }

        /// <summary>
        /// サービス解決は未対応
        /// </summary>
        public object? GetService(Type serviceType, object? serviceKey = null) => null;
    }
}
