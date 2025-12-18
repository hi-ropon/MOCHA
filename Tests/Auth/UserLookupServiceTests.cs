using System.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Data;
using MOCHA.Services.Auth;

namespace MOCHA.Tests;

/// <summary>
/// ユーザー検索サービスの振る舞いを検証するテスト
/// </summary>
[TestClass]
public class UserLookupServiceTests
{
    /// <summary>
    /// 対象ユーザーが取得できることを確認する
    /// </summary>
    [TestMethod]
    public async Task 存在するユーザーを返す()
    {
        var service = CreateService(nameof(存在するユーザーを返す), context =>
        {
            context.DevUsers.Add(new DevUserEntity
            {
                Email = "user@example.com",
                DisplayName = "User Display",
                PasswordHash = "hash"
            });
            context.SaveChanges();
        });

        var result = await service.FindByIdentifierAsync("user@example.com");

        Assert.IsNotNull(result);
        Assert.AreEqual("user@example.com", result!.UserId);
        Assert.AreEqual("User Display", result.DisplayName);
    }

    /// <summary>
    /// 存在しないユーザーは null になることを確認する
    /// </summary>
    [TestMethod]
    public async Task 存在しないユーザーはnull()
    {
        var service = CreateService(nameof(存在しないユーザーはnull));

        var result = await service.FindByIdentifierAsync("missing");

        Assert.IsNull(result);
    }

    private static IUserLookupService CreateService(string databaseName, Action<ChatDbContext>? seed = null)
    {
        var options = new DbContextOptionsBuilder<ChatDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        if (seed is not null)
        {
            using var context = new ChatDbContext(options);
            seed(context);
        }

        var factory = new TestDbContextFactory(options);
        var strategy = new DevUserLookupStrategy(factory);
        return new UserLookupService(new[] { strategy });
    }

    private sealed class TestDbContextFactory : IDbContextFactory<ChatDbContext>
    {
        private readonly DbContextOptions<ChatDbContext> _options;

        public TestDbContextFactory(DbContextOptions<ChatDbContext> options)
        {
            _options = options;
        }

        public ChatDbContext CreateDbContext()
        {
            return new ChatDbContext(_options);
        }

        public ValueTask<ChatDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            return new ValueTask<ChatDbContext>(new ChatDbContext(_options));
        }
    }
}
