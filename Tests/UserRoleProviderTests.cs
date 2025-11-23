using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Models.Auth;
using MOCHA.Services.Auth;

namespace MOCHA.Tests;

/// <summary>
/// InMemoryUserRoleProvider のロール操作を検証するテスト。
/// </summary>
[TestClass]
public class UserRoleProviderTests
{
    /// <summary>
    /// 複数ロール付与後に正しく取得できることを確認する。
    /// </summary>
    [TestMethod]
    public async Task 複数ロールを返す()
    {
        var provider = new InMemoryUserRoleProvider();
        await provider.AssignAsync("u1", UserRoleId.Predefined.Administrator);
        await provider.AssignAsync("u1", UserRoleId.Predefined.Developer);

        var roles = await provider.GetRolesAsync("u1");

        Assert.IsTrue(roles.Contains(UserRoleId.Predefined.Administrator));
        Assert.IsTrue(roles.Contains(UserRoleId.Predefined.Developer));
        Assert.AreEqual(2, roles.Count);
    }

    /// <summary>
    /// 同一ロールの重複付与が一つにまとめられることを確認する。
    /// </summary>
    [TestMethod]
    public async Task 重複付与は一つだけ()
    {
        var provider = new InMemoryUserRoleProvider();

        await provider.AssignAsync("u1", UserRoleId.Predefined.Operator);
        await provider.AssignAsync("u1", UserRoleId.Predefined.Operator);

        var roles = await provider.GetRolesAsync("u1");

        Assert.AreEqual(1, roles.Count);
        Assert.IsTrue(roles.Contains(UserRoleId.Predefined.Operator));
    }

    /// <summary>
    /// 存在しないロールを削除しても例外にならないことを確認する。
    /// </summary>
    [TestMethod]
    public async Task 存在しないロールでも例外にならない()
    {
        var provider = new InMemoryUserRoleProvider();
        await provider.AssignAsync("u1", UserRoleId.Predefined.Developer);

        await provider.RemoveAsync("u1", UserRoleId.Predefined.Administrator);
        var roles = await provider.GetRolesAsync("u1");

        Assert.IsTrue(roles.Contains(UserRoleId.Predefined.Developer));
    }

    /// <summary>
    /// ロール判定が大文字小文字を無視して行われることを確認する。
    /// </summary>
    [TestMethod]
    public async Task 大文字小文字を無視して判定する()
    {
        var provider = new InMemoryUserRoleProvider();
        await provider.AssignAsync("u1", UserRoleId.Predefined.Developer);

        var result = await provider.IsInRoleAsync("u1", "developer");

        Assert.IsTrue(result);
    }
}
