using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Data;
using MOCHA.Factories;
using MOCHA.Models.Auth;
using System.Threading;

namespace MOCHA.Tests;

/// <summary>
/// 開発用クッキー認証の簡易動作を検証するテスト
/// </summary>
[TestClass]
public class DevAuthTests
{
    /// <summary>
    /// 未認証アクセスがログインにリダイレクトされることを確認する
    /// </summary>
    [TestMethod]
    public async Task 未認証はログインにリダイレクトされる()
    {
        using var factory = new AuthWebApplicationFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/");

        Assert.AreEqual(HttpStatusCode.Redirect, response.StatusCode);
        StringAssert.StartsWith(response.Headers.Location?.AbsolutePath, "/login");
    }

    /// <summary>
    /// ログイン成功後にトップページへ到達できることを確認する
    /// </summary>
    [TestMethod]
    public async Task ログイン後にトップへ到達できる()
    {
        using var factory = new AuthWebApplicationFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var token = await AntiforgeryTokenFetcher.FetchAsync(client, "/signup");
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Email"] = "dev-user@example.com",
            ["Input.Password"] = "Passw0rd!",
            ["ReturnUrl"] = "/",
            ["__RequestVerificationToken"] = token
        });

        var postResponse = await client.PostAsync("/signup?returnUrl=%2F", content);

        Assert.AreEqual(HttpStatusCode.Redirect, postResponse.StatusCode);
        var cookieHeader = postResponse.Headers.TryGetValues("Set-Cookie", out var values) ? values.FirstOrDefault() : null;
        Assert.IsNotNull(cookieHeader);
        client.DefaultRequestHeaders.Add("Cookie", cookieHeader.Split(';')[0]);

        var homeResponse = await client.GetAsync("/");

        homeResponse.EnsureSuccessStatusCode();
    }

    [TestMethod]
    public async Task 重複しているメールアドレスは登録不可()
    {

    }

    [TestMethod]
    public async Task メールアドレスにはアットマークが必要()
    {

    }

    [TestMethod]
    public async Task パスワードは6文字以上()
    {

    }
}

internal sealed class AuthWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((context, config) =>
        {
            var overrides = new Dictionary<string, string?>
            {
                ["AzureAd:Enabled"] = "false",
                ["DevAuth:Enabled"] = "true",
                ["Authentication:DefaultScheme"] = DevAuthDefaults.scheme,
                ["Authentication:DefaultChallengeScheme"] = DevAuthDefaults.scheme
            };
            config.AddInMemoryCollection(overrides);
        });

        builder.ConfigureTestServices(services =>
        {
            ReplaceDbContext(services);
            ReplaceInitializer(services);
            services.Configure<DevAuthOptions>(options => { options.Enabled = true; });
        });
    }

    private static void ReplaceDbContext(IServiceCollection services)
    {
        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ChatDbContext>));
        if (descriptor != null)
        {
            services.Remove(descriptor);
        }

        services.AddDbContext<ChatDbContext>(options =>
        {
            options.UseInMemoryDatabase("DevAuthTests");
        });
    }

    private static void ReplaceInitializer(IServiceCollection services)
    {
        var initializer = services.SingleOrDefault(d => d.ServiceType == typeof(IDatabaseInitializer));
        if (initializer != null)
        {
            services.Remove(initializer);
        }

        services.AddSingleton<IDatabaseInitializer, NoOpInitializer>();
    }

    private sealed class NoOpInitializer : IDatabaseInitializer
    {
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}

internal static class AntiforgeryTokenFetcher
{
    private static readonly Regex _tokenRegex = new("__RequestVerificationToken\" value=\"([^\"]+)\"", RegexOptions.Compiled);

    public static async Task<string> FetchAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        var match = _tokenRegex.Match(body);
        if (!match.Success)
        {
            throw new InvalidOperationException("Antiforgery token not found");
        }

        return match.Groups[1].Value;
    }
}
