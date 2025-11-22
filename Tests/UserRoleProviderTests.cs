using MOCHA.Models.Auth;
using MOCHA.Services.Auth;
using Xunit;

namespace MOCHA.Tests;

/// <summary>
/// InMemoryUserRoleProvider のロール操作を検証するテスト。
/// </summary>
public class UserRoleProviderTests
{
    /// <summary>
    /// 複数ロール付与後に正しく取得できることを確認する。
    /// </summary>
    [Fact]
    public async Task GetRolesAsync_複数ロールを返す()
    {
        var provider = new InMemoryUserRoleProvider();
        await provider.AssignAsync("u1", UserRoleId.Predefined.Administrator);
        await provider.AssignAsync("u1", UserRoleId.Predefined.Developer);

        var roles = await provider.GetRolesAsync("u1");

        Assert.Contains(UserRoleId.Predefined.Administrator, roles);
        Assert.Contains(UserRoleId.Predefined.Developer, roles);
        Assert.Equal(2, roles.Count);
    }

    /// <summary>
    /// 同一ロールの重複付与が一つにまとめられることを確認する。
    /// </summary>
    [Fact]
    public async Task AssignAsync_重複付与は一つだけ()
    {
        var provider = new InMemoryUserRoleProvider();

        await provider.AssignAsync("u1", UserRoleId.Predefined.Operator);
        await provider.AssignAsync("u1", UserRoleId.Predefined.Operator);

        var roles = await provider.GetRolesAsync("u1");

        Assert.Single(roles);
        Assert.Contains(UserRoleId.Predefined.Operator, roles);
    }

    /// <summary>
    /// 存在しないロールを削除しても例外にならないことを確認する。
    /// </summary>
    [Fact]
    public async Task RemoveAsync_存在しないロールでも例外にならない()
    {
        var provider = new InMemoryUserRoleProvider();
        await provider.AssignAsync("u1", UserRoleId.Predefined.Developer);

        await provider.RemoveAsync("u1", UserRoleId.Predefined.Administrator);
        var roles = await provider.GetRolesAsync("u1");

        Assert.Contains(UserRoleId.Predefined.Developer, roles);
    }

    /// <summary>
    /// ロール判定が大文字小文字を無視して行われることを確認する。
    /// </summary>
    [Fact]
    public async Task IsInRoleAsync_大文字小文字を無視して判定する()
    {
        var provider = new InMemoryUserRoleProvider();
        await provider.AssignAsync("u1", UserRoleId.Predefined.Developer);

        var result = await provider.IsInRoleAsync("u1", "developer");

        Assert.True(result);
    }
}
