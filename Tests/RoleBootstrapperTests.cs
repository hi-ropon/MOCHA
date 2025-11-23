using MOCHA.Models.Auth;
using MOCHA.Services.Auth;
using Microsoft.Extensions.Options;
using Xunit;

namespace MOCHA.Tests;

/// <summary>
/// RoleBootstrapper の管理者ロール付与を検証するテスト。
/// </summary>
public class RoleBootstrapperTests
{
    /// <summary>
    /// 設定されたユーザーに管理者ロールが付与されることを確認する。
    /// </summary>
    [Fact]
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
        Assert.Contains(UserRoleId.Predefined.Administrator, roles);
    }

    /// <summary>
    /// 設定が空の場合は何も変更しないことを確認する。
    /// </summary>
    [Fact]
    public async Task 設定が空なら何もしない()
    {
        var provider = new InMemoryUserRoleProvider();
        var options = Options.Create(new RoleBootstrapOptions());
        var bootstrapper = new RoleBootstrapper(provider, options);

        await bootstrapper.EnsureAdminRolesAsync();

        var roles = await provider.GetRolesAsync("u1");
        Assert.Empty(roles);
    }
}
