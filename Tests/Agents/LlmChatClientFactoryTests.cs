using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Agents.Infrastructure.Clients;
using MOCHA.Agents.Infrastructure.Options;

namespace MOCHA.Tests;

/// <summary>
/// LlmChatClientFactory のクライアント生成動作を検証するテスト
/// </summary>
[TestClass]
public class LlmChatClientFactoryTests
{
    /// <summary>
    /// Azure OpenAI 生成時にプロキシが無効化されることを確認
    /// </summary>
    [TestMethod]
    public void AzureOpenAI_プロキシが無効化される()
    {
        var handler = new SocketsHttpHandler
        {
            UseProxy = true,
            Proxy = new WebProxy("http://localhost:3128")
        };

        var options = Options.Create(new LlmOptions
        {
            Provider = ProviderKind.AzureOpenAI,
            ApiKey = "dummy-key",
            Endpoint = "https://dummy.openai.azure.com/"
        });

        var factory = new LlmChatClientFactory(
            options,
            NullLogger<LlmChatClientFactory>.Instance,
            () => handler);

        var client = factory.Create();

        Assert.IsNotNull(client);
        Assert.IsFalse(handler.UseProxy);
        Assert.IsNull(handler.Proxy);
    }

    /// <summary>
    /// OpenAI 生成ではプロキシ無効化処理が呼ばれないことを確認
    /// </summary>
    [TestMethod]
    public void OpenAI_プロキシ設定は変更されない()
    {
        var handlerCalled = false;
        var options = Options.Create(new LlmOptions
        {
            Provider = ProviderKind.OpenAI,
            ApiKey = "dummy-key",
            ModelOrDeployment = "gpt-4o-mini"
        });

        var factory = new LlmChatClientFactory(
            options,
            NullLogger<LlmChatClientFactory>.Instance,
            () =>
            {
                handlerCalled = true;
                return new SocketsHttpHandler();
            });

        var client = factory.Create();

        Assert.IsNotNull(client);
        Assert.IsFalse(handlerCalled);
    }
}
