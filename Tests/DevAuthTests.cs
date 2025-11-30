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

        Assert.AreEqual(HttpStatusCode.Redirect, response.StatusCode);
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
        using var factory = new AuthWebApplicationFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        const string email = "dup@example.com";
        var antiforgery = await AntiforgeryTokenFetcher.FetchAsync(client, "/signup");
        var firstContent = CreateSignupContent(email, "Passw0rd!", "/", antiforgery);

        var firstResponse = await client.PostAsync("/signup?returnUrl=%2F", firstContent);

        Assert.AreEqual(HttpStatusCode.Redirect, firstResponse.StatusCode);

        antiforgery = await AntiforgeryTokenFetcher.FetchAsync(client, "/signup");
        var secondContent = CreateSignupContent(email, "Passw0rd!", "/", antiforgery);

        var secondResponse = await client.PostAsync("/signup?returnUrl=%2F", secondContent);
        var body = WebUtility.HtmlDecode(await secondResponse.Content.ReadAsStringAsync());

        Assert.AreEqual(HttpStatusCode.OK, secondResponse.StatusCode);
        StringAssert.Contains(body, "同じメールアドレスのユーザーが既に存在します");
    }

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

        var response = await client.PostAsync("/signup?returnUrl=%2F", content);
        var body = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        StringAssert.Contains(body, "入力を確認してください");
    }

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

        var response = await client.PostAsync("/signup?returnUrl=%2F", content);
        var body = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        StringAssert.Contains(body, "パスワードが一致しません");
    }

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

        var response = await client.PostAsync("/signup?returnUrl=%2F", content);
        var body = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        StringAssert.Contains(body, "入力を確認してください");
    }

    private static FormUrlEncodedContent CreateSignupContent(string email, string password, string returnUrl, string token) =>
        new(new Dictionary<string, string>
        {
            ["Input.Email"] = email,
            ["Input.Password"] = password,
            ["Input.ConfirmPassword"] = password,
            ["ReturnUrl"] = returnUrl,
            ["__RequestVerificationToken"] = token
        });
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
            services.AddAntiforgery(options =>
            {
                options.Cookie.SecurePolicy = CookieSecurePolicy.None;
            });
        });
    }

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
    private static readonly Regex _tokenRegex = new("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"", RegexOptions.Compiled | RegexOptions.IgnoreCase);

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
