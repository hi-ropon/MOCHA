using Microsoft.EntityFrameworkCore;
using MOCHA.Data;
using MOCHA.Models.Auth;
using MOCHA.Services.Auth;
using Xunit;

namespace MOCHA.Tests;

/// <summary>
/// DbUserRoleProvider のロール操作を検証するテスト。
/// </summary>
public class DbUserRoleProviderTests
{
    /// <summary>
    /// インメモリ DB を使ったコンテキストを生成するヘルパー。
    /// </summary>
    private static ChatDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ChatDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new ChatDbContext(options);
    }

    /// <summary>
    /// 同じロールを複数回付与しても重複保存されないことを確認する。
    /// </summary>
    [Fact]
    public async Task AssignAsync_重複せず保存される()
    {
        await using var db = CreateContext(nameof(AssignAsync_重複せず保存される));
        var provider = new DbUserRoleProvider(db);

        await provider.AssignAsync("u1", UserRoleId.Predefined.Administrator);
        await provider.AssignAsync("u1", UserRoleId.Predefined.Administrator);

        var roles = await provider.GetRolesAsync("u1");

        Assert.Single(roles);
        Assert.Contains(UserRoleId.Predefined.Administrator, roles);
    }

    /// <summary>
    /// 削除操作でロールが取り除かれることを確認する。
    /// </summary>
    [Fact]
    public async Task RemoveAsync_削除される()
    {
        await using var db = CreateContext(nameof(RemoveAsync_削除される));
        var provider = new DbUserRoleProvider(db);

        await provider.AssignAsync("u1", UserRoleId.Predefined.Operator);
        await provider.RemoveAsync("u1", UserRoleId.Predefined.Operator);

        var roles = await provider.GetRolesAsync("u1");

        Assert.Empty(roles);
    }

    /// <summary>
    /// ロール判定が大文字小文字を無視して行われることを確認する。
    /// </summary>
    [Fact]
    public async Task IsInRoleAsync_大小文字を無視する()
    {
        await using var db = CreateContext(nameof(IsInRoleAsync_大小文字を無視する));
        var provider = new DbUserRoleProvider(db);

        await provider.AssignAsync("u1", UserRoleId.Predefined.Developer);

        var result = await provider.IsInRoleAsync("u1", "developer");

        Assert.True(result);
    }
}
