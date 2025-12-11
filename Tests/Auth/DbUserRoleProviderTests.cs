using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Data;
using MOCHA.Models.Auth;
using MOCHA.Services.Auth;

namespace MOCHA.Tests;

/// <summary>
/// DbUserRoleProvider のロール操作検証テスト
/// </summary>
[TestClass]
public class DbUserRoleProviderTests
{
    /// <summary>
    /// インメモリ DB を使ったコンテキストファクトリ生成ヘルパー
    /// </summary>
    private static IDbContextFactory<ChatDbContext> CreateFactory(string dbName)
    {
        var options = new DbContextOptionsBuilder<ChatDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new PooledDbContextFactory<ChatDbContext>(options);
    }

    /// <summary>
    /// 同じロールを複数回付与しても重複保存されない確認
    /// </summary>
    [TestMethod]
    public async Task 重複せず保存される()
    {
        var provider = new DbUserRoleProvider(CreateFactory(nameof(重複せず保存される)));

        await provider.AssignAsync("u1", UserRoleId.Predefined.Administrator);
        await provider.AssignAsync("u1", UserRoleId.Predefined.Administrator);

        var roles = await provider.GetRolesAsync("u1");

        Assert.AreEqual(1, roles.Count);
        Assert.IsTrue(roles.Contains(UserRoleId.Predefined.Administrator));
    }

    /// <summary>
    /// 削除操作でロールが取り除かれる確認
    /// </summary>
    [TestMethod]
    public async Task 削除操作でロールが取り除かれる()
    {
        var provider = new DbUserRoleProvider(CreateFactory(nameof(削除操作でロールが取り除かれる)));

        await provider.AssignAsync("u1", UserRoleId.Predefined.Operator);
        await provider.RemoveAsync("u1", UserRoleId.Predefined.Operator);

        var roles = await provider.GetRolesAsync("u1");

        Assert.AreEqual(0, roles.Count);
    }

    /// <summary>
    /// ロール判定が大文字小文字を無視して行われる確認
    /// </summary>
    [TestMethod]
    public async Task 大小文字を無視する()
    {
        var provider = new DbUserRoleProvider(CreateFactory(nameof(大小文字を無視する)));

        await provider.AssignAsync("u1", UserRoleId.Predefined.Developer);

        var result = await provider.IsInRoleAsync("u1", "developer");

        Assert.IsTrue(result);
    }
}
