using MOCHA.Models.Auth;
using MOCHA.Services.Auth;
using Microsoft.Extensions.Options;
using Xunit;

namespace MOCHA.Tests;

public class RoleBootstrapperTests
{
    [Fact]
    public async Task EnsureAdminRolesAsync_管理者が付与される()
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

    [Fact]
    public async Task EnsureAdminRolesAsync_設定が空なら何もしない()
    {
        var provider = new InMemoryUserRoleProvider();
        var options = Options.Create(new RoleBootstrapOptions());
        var bootstrapper = new RoleBootstrapper(provider, options);

        await bootstrapper.EnsureAdminRolesAsync();

        var roles = await provider.GetRolesAsync("u1");
        Assert.Empty(roles);
    }
}
