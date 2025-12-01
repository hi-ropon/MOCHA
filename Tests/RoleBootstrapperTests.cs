using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Options;
using MOCHA.Models.Auth;
using MOCHA.Services.Auth;

namespace MOCHA.Tests;

/// <summary>
/// RoleBootstrapper の管理者ロール付与検証テスト
/// </summary>
[TestClass]
public class RoleBootstrapperTests
{
    /// <summary>
    /// 設定されたユーザーへの管理者ロール付与確認
    /// </summary>
    [TestMethod]
    public async Task 管理者が付与される()
    {
        var provider = new InMemoryUserRoleProvider();
        var options = Options.Create(new RoleBootstrapOptions
        {
            AdminUserIds = { "u1" }
        });
        var bootstrapper = new RoleBootstrapper(provider, options);

        await bootstrapper.EnsureAdminRolesAsync();

        var roles = await provider.GetRolesAsync("u1");
        Assert.IsTrue(roles.Contains(UserRoleId.Predefined.Administrator));
    }

    /// <summary>
    /// 設定空時の無変更確認
    /// </summary>
    [TestMethod]
    public async Task 設定が空なら何もしない()
    {
        var provider = new InMemoryUserRoleProvider();
        var options = Options.Create(new RoleBootstrapOptions());
        var bootstrapper = new RoleBootstrapper(provider, options);

        await bootstrapper.EnsureAdminRolesAsync();

        var roles = await provider.GetRolesAsync("u1");
        Assert.AreEqual(0, roles.Count);
    }
}
