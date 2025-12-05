using System.ClientModel;
using System.ClientModel.Primitives;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MOCHA.Agents.Infrastructure.Options;
using OpenAI;
using AzureOpenAIClientOptions = Azure.AI.OpenAI.AzureOpenAIClientOptions;
using OpenAiClientOptions = OpenAI.OpenAIClientOptions;

namespace MOCHA.Agents.Infrastructure.Clients;

/// <summary>
/// 設定に応じて OpenAI / Azure OpenAI の IChatClient を生成するファクトリー
/// </summary>
public sealed class LlmChatClientFactory : ILlmChatClientFactory
{
    private readonly LlmOptions _options;
    private readonly ILogger<LlmChatClientFactory> _logger;
    private readonly Func<SocketsHttpHandler> _httpMessageHandlerFactory;

    /// <summary>
    /// オプションとロガー注入による初期化
    /// </summary>
    /// <param name="optionsAccessor">LLM 設定</param>
    /// <param name="logger">ロガー</param>
    /// <param name="httpMessageHandlerFactory">Azure OpenAI 用 HTTP ハンドラファクトリー</param>
    public LlmChatClientFactory(
        IOptions<LlmOptions> optionsAccessor,
        ILogger<LlmChatClientFactory> logger,
        Func<SocketsHttpHandler>? httpMessageHandlerFactory = null)
    {
        _options = optionsAccessor.Value ?? throw new ArgumentNullException(nameof(optionsAccessor));
        _logger = logger;
        _httpMessageHandlerFactory = httpMessageHandlerFactory ?? CreateProxyDisabledHandler;
    }

    /// <summary>
    /// 設定に基づくチャットクライアント生成
    /// </summary>
    /// <returns>生成したチャットクライアント</returns>
    public IChatClient Create()
    {
        return _options.Provider switch
        {
            ProviderKind.AzureOpenAI => CreateAzureOpenAiClient(),
            _ => CreateOpenAiClient()
        };
    }

    /// <summary>
    /// OpenAI クライアント生成
    /// </summary>
    /// <returns>生成したクライアント</returns>
    private IChatClient CreateOpenAiClient()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogWarning("OpenAI の ApiKey が設定されていないためローカルエコークライアントを使用します。");
            return new LocalEchoChatClient();
        }

        var clientOptions = new OpenAiClientOptions();
        if (!string.IsNullOrWhiteSpace(_options.Endpoint))
        {
            clientOptions.Endpoint = new Uri(_options.Endpoint);
        }

        var client = new OpenAIClient(new ApiKeyCredential(_options.ApiKey), clientOptions);
        var model = _options.ModelOrDeployment ?? "gpt-4o-mini";
        var chatClient = client.GetChatClient(model);
        return chatClient.AsIChatClient();
    }

    /// <summary>
    /// Azure OpenAI クライアント生成
    /// </summary>
    /// <returns>生成したクライアント</returns>
    private IChatClient CreateAzureOpenAiClient()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogWarning("Azure OpenAI の ApiKey が設定されていないためローカルエコークライアントを使用します。");
            return new LocalEchoChatClient();
        }

        if (string.IsNullOrWhiteSpace(_options.Endpoint))
        {
            _logger.LogWarning("Azure OpenAI の Endpoint が設定されていないためローカルエコークライアントを使用します。");
            return new LocalEchoChatClient();
        }

        var handler = _httpMessageHandlerFactory.Invoke();
        handler.UseProxy = false;
        handler.Proxy = null;

        var httpClient = new HttpClient(handler, disposeHandler: true);
        var clientOptions = new AzureOpenAIClientOptions
        {
            Transport = new HttpClientPipelineTransport(httpClient, true, loggerFactory: null)
        };

        var client = new AzureOpenAIClient(new Uri(_options.Endpoint), new AzureKeyCredential(_options.ApiKey), clientOptions);
        var deployment = _options.ModelOrDeployment ?? "gpt-5-mini";
        var chatClient = client.GetChatClient(deployment);
        return chatClient.AsIChatClient();
    }

    /// <summary>
    /// 設定不足時に動作を継続するための簡易エコー実装
    /// </summary>
    private sealed class LocalEchoChatClient : IChatClient
    {
        /// <summary>
        /// 単発応答生成
        /// </summary>
        /// <param name="messages">入力メッセージ</param>
        /// <param name="options">オプション</param>
        /// <param name="cancellationToken">キャンセル通知</param>
        /// <returns>エコー応答</returns>
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            var lastUser = messages.LastOrDefault(m => m.Role == ChatRole.User);
            var text = lastUser?.Text ?? "(no input)";
            var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, $"[local echo] {text}"))
            {
                ConversationId = Guid.NewGuid().ToString("N")
            };
            return Task.FromResult(response);
        }

        /// <summary>
        /// ストリーミング応答生成
        /// </summary>
        /// <param name="messages">入力メッセージ</param>
        /// <param name="options">オプション</param>
        /// <param name="cancellationToken">キャンセル通知</param>
        /// <returns>応答ストリーム</returns>
        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            var lastUser = messages.LastOrDefault(m => m.Role == ChatRole.User);
            var text = lastUser?.Text ?? "(no input)";
            return EchoAsync($"[local echo] {text}", cancellationToken);
        }

        /// <summary>
        /// サービス取得は未対応
        /// </summary>
        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose()
        {
        }

        /// <summary>
        /// エコー応答ストリーミング
        /// </summary>
        /// <param name="content">返却コンテンツ</param>
        /// <param name="cancellationToken">キャンセル通知</param>
        /// <returns>ストリーミング応答</returns>
        private static async IAsyncEnumerable<ChatResponseUpdate> EchoAsync(
            string content,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new ChatResponseUpdate(ChatRole.Assistant, content);
        }
    }

    /// <summary>
    /// プロキシ無効化済みの HTTP ハンドラ生成
    /// </summary>
    /// <returns>生成したハンドラ</returns>
    private static SocketsHttpHandler CreateProxyDisabledHandler()
    {
        return new SocketsHttpHandler
        {
            UseProxy = false,
            Proxy = null
        };
    }
}
