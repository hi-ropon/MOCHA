using System;
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
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Data;
using MOCHA.Factories;
using MOCHA.Models.Auth;
using System.Threading;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

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
        if (response.StatusCode == HttpStatusCode.RedirectKeepVerb)
        {
            var redirectLocation = response.Headers.Location;
            Assert.IsNotNull(redirectLocation);
            response = await client.GetAsync(redirectLocation);
        }

        Assert.IsTrue(response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.RedirectKeepVerb);
        StringAssert.StartsWith(response.Headers.Location?.AbsolutePath, "/login");
    }

    /// <summary>
    /// ログイン画面のRememberMe非表示確認
    /// </summary>
    [TestMethod]
    public async Task ログイン画面表示_RememberMeが含まれない()
    {
        using var factory = new AuthWebApplicationFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/login");
        if (response.StatusCode == HttpStatusCode.RedirectKeepVerb)
        {
            var redirectLocation = response.Headers.Location;
            Assert.IsNotNull(redirectLocation);
            response = await client.GetAsync(redirectLocation);
        }

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();

        Assert.IsFalse(body.Contains("Remember Me", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(body.Contains("RememberMe", StringComparison.OrdinalIgnoreCase));
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

        var antiforgery = await AntiforgeryTokenFetcher.FetchAsync(client, "/signup");
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Email"] = "dev-user@example.com",
            ["Input.Password"] = "Passw0rd!",
            ["Input.ConfirmPassword"] = "Passw0rd!",
            ["ReturnUrl"] = "/",
            ["__RequestVerificationToken"] = antiforgery
        });

        var postResponse = await PostAllowingKeepVerbRedirectAsync(client, "/signup?returnUrl=%2F", content);

        Assert.IsTrue(postResponse.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.RedirectKeepVerb);
        var cookieHeader = postResponse.Headers.TryGetValues("Set-Cookie", out var values) ? values.FirstOrDefault() : null;
        Assert.IsNotNull(cookieHeader);
        client.DefaultRequestHeaders.Add("Cookie", cookieHeader.Split(';')[0]);

        var homeResponse = await GetAllowingRedirectAsync(client, "/");

        homeResponse.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// 重複メールアドレス登録時のエラー表示確認
    /// </summary>
    [TestMethod]
    public async Task 重複しているメールアドレスは登録不可()
    {
        using var factory = new AuthWebApplicationFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        const string email = "dup@example.com";
        var antiforgery = await AntiforgeryTokenFetcher.FetchAsync(client, "/signup");
        var firstContent = CreateSignupContent(email, "Passw0rd!", "/", antiforgery);

        var firstResponse = await PostAllowingKeepVerbRedirectAsync(client, "/signup?returnUrl=%2F", firstContent);

        Assert.IsTrue(firstResponse.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.RedirectKeepVerb);

        antiforgery = await AntiforgeryTokenFetcher.FetchAsync(client, "/signup");
        var secondContent = CreateSignupContent(email, "Passw0rd!", "/", antiforgery);

        var secondResponse = await PostAllowingKeepVerbRedirectAsync(client, "/signup?returnUrl=%2F", secondContent);
        var body = WebUtility.HtmlDecode(await secondResponse.Content.ReadAsStringAsync());

        Assert.AreEqual(HttpStatusCode.OK, secondResponse.StatusCode);
        StringAssert.Contains(body, "同じメールアドレスのユーザーが既に存在します");
    }

    /// <summary>
    /// メールアドレス形式不正時のバリデーション確認
    /// </summary>
    [TestMethod]
    public async Task メールアドレスにはアットマークが必要()
    {
        using var factory = new AuthWebApplicationFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var antiforgery = await AntiforgeryTokenFetcher.FetchAsync(client, "/signup");
        var content = CreateSignupContent("invalid-email", "Passw0rd!", "/", antiforgery);

        var response = await PostAllowingKeepVerbRedirectAsync(client, "/signup?returnUrl=%2F", content);
        var body = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        StringAssert.Contains(body, "入力を確認してください");
    }

    /// <summary>
    /// パスワード不一致時の登録不可確認
    /// </summary>
    [TestMethod]
    public async Task パスワード不一致は登録不可()
    {
        using var factory = new AuthWebApplicationFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var antiforgery = await AntiforgeryTokenFetcher.FetchAsync(client, "/signup");
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Email"] = "mismatch@example.com",
            ["Input.Password"] = "Passw0rd!",
            ["Input.ConfirmPassword"] = "Passw0rd?",
            ["ReturnUrl"] = "/",
            ["__RequestVerificationToken"] = antiforgery
        });

        var response = await PostAllowingKeepVerbRedirectAsync(client, "/signup?returnUrl=%2F", content);
        var body = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        StringAssert.Contains(body, "パスワードが一致しません");
    }

    /// <summary>
    /// パスワード長不足時のバリデーション確認
    /// </summary>
    [TestMethod]
    public async Task パスワードは6文字以上()
    {
        using var factory = new AuthWebApplicationFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var antiforgery = await AntiforgeryTokenFetcher.FetchAsync(client, "/signup");
        var content = CreateSignupContent("shortpass@example.com", "12345", "/", antiforgery);

        var response = await PostAllowingKeepVerbRedirectAsync(client, "/signup?returnUrl=%2F", content);
        var body = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        StringAssert.Contains(body, "入力を確認してください");
    }

    /// <summary>
    /// サインアップフォーム送信内容生成
    /// </summary>
    /// <param name="email">メールアドレス</param>
    /// <param name="password">パスワード</param>
    /// <param name="returnUrl">遷移先 URL</param>
    /// <param name="token">CSRF トークン</param>
    /// <returns>フォームコンテンツ</returns>
    private static FormUrlEncodedContent CreateSignupContent(string email, string password, string returnUrl, string token) =>
        new(new Dictionary<string, string>
        {
            ["Input.Email"] = email,
            ["Input.Password"] = password,
            ["Input.ConfirmPassword"] = password,
            ["ReturnUrl"] = returnUrl,
            ["__RequestVerificationToken"] = token
        });

    /// <summary>
    /// GET 時にリダイレクトを一度だけ許容するヘルパー
    /// </summary>
    /// <param name="client">HTTP クライアント</param>
    /// <param name="path">取得先パス</param>
    /// <returns>最終レスポンス</returns>
    private static async Task<HttpResponseMessage> GetAllowingRedirectAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        if (response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.RedirectKeepVerb)
        {
            var redirectLocation = response.Headers.Location;
            Assert.IsNotNull(redirectLocation);
            var nextUri = redirectLocation.IsAbsoluteUri
                ? redirectLocation
                : new Uri(client.BaseAddress ?? new Uri("https://localhost"), redirectLocation);
            response = await client.GetAsync(nextUri);
        }

        return response;
    }

    /// <summary>
    /// POST 時に RedirectKeepVerb を踏んだ場合に 1 度だけ追従するヘルパー
    /// </summary>
    /// <param name="client">HTTP クライアント</param>
    /// <param name="path">送信先パス</param>
    /// <param name="content">送信内容</param>
    /// <returns>最終レスポンス</returns>
    private static async Task<HttpResponseMessage> PostAllowingKeepVerbRedirectAsync(HttpClient client, string path, FormUrlEncodedContent content)
    {
        var response = await client.PostAsync(path, content);
        if (response.StatusCode == HttpStatusCode.RedirectKeepVerb)
        {
            var redirectLocation = response.Headers.Location;
            Assert.IsNotNull(redirectLocation);
            var nextUri = redirectLocation.IsAbsoluteUri
                ? redirectLocation
                : new Uri(client.BaseAddress ?? new Uri("https://localhost"), redirectLocation);
            response = await client.PostAsync(nextUri, content);
        }

        return response;
    }
}

/// <summary>
/// 開発用認証テスト向けの WebApplicationFactory
/// </summary>
internal sealed class AuthWebApplicationFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// テスト用 WebHost 構成
    /// </summary>
    /// <param name="builder">ホストビルダー</param>
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
            services.AddAntiforgery(options =>
            {
                options.Cookie.SecurePolicy = CookieSecurePolicy.None;
            });
        });
    }

    /// <summary>
    /// InMemory DbContext 差し替え
    /// </summary>
    /// <param name="services">サービスコレクション</param>
    private static void ReplaceDbContext(IServiceCollection services)
    {
        ServiceDescriptor? descriptor;
        while ((descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(DbContextOptions<ChatDbContext>))) != null)
        {
            services.Remove(descriptor);
        }

        services.RemoveAll<IConfigureOptions<DbContextOptions<ChatDbContext>>>();
        services.RemoveAll<IDbContextOptionsConfiguration<ChatDbContext>>();
        services.RemoveAll<IDatabaseProvider>();
        while ((descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IDbContextFactory<ChatDbContext>))) != null)
        {
            services.Remove(descriptor);
        }
        while ((descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ChatDbContext))) != null)
        {
            services.Remove(descriptor);
        }

        services.RemoveAll<IChatDbContext>();

        services.AddEntityFrameworkInMemoryDatabase();
        services.AddDbContextFactory<ChatDbContext>(options =>
        {
            options.UseInMemoryDatabase("DevAuthTests");
        });
        services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<ChatDbContext>>().CreateDbContext());
        services.AddScoped<IChatDbContext>(sp => sp.GetRequiredService<ChatDbContext>());
    }

    /// <summary>
    /// DB 初期化サービス差し替え
    /// </summary>
    /// <param name="services">サービスコレクション</param>
    private static void ReplaceInitializer(IServiceCollection services)
    {
        var initializer = services.SingleOrDefault(d => d.ServiceType == typeof(IDatabaseInitializer));
        if (initializer != null)
        {
            services.Remove(initializer);
        }

        services.AddSingleton<IDatabaseInitializer, NoOpInitializer>();
    }

    /// <summary>
    /// 初期化を行わないスタブ
    /// </summary>
    private sealed class NoOpInitializer : IDatabaseInitializer
    {
        /// <summary>
        /// 初期化なしの空実装
        /// </summary>
        /// <param name="cancellationToken">キャンセル通知</param>
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}

/// <summary>
/// Antiforgery トークン取得ヘルパー
/// </summary>
internal static class AntiforgeryTokenFetcher
{
    private static readonly Regex _tokenRegex = new("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Antiforgery トークン取得
    /// </summary>
    /// <param name="client">HTTP クライアント</param>
    /// <param name="path">取得先パス</param>
    /// <returns>取得したトークン</returns>
    public static async Task<string> FetchAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        var redirectCount = 0;
        while (response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.RedirectKeepVerb)
        {
            if (redirectCount++ > 3)
            {
                break;
            }

            var redirectLocation = response.Headers.Location;
            Assert.IsNotNull(redirectLocation);
            var nextUri = redirectLocation.IsAbsoluteUri
                ? redirectLocation
                : new Uri(client.BaseAddress ?? new Uri("https://localhost"), redirectLocation);
            response = await client.GetAsync(nextUri);
        }

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
