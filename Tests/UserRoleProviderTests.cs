using MOCHA.Models.Auth;
using MOCHA.Services.Auth;
using Xunit;

namespace MOCHA.Tests;

public class UserRoleProviderTests
{
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

    [Fact]
    public async Task RemoveAsync_存在しないロールでも例外にならない()
    {
        var provider = new InMemoryUserRoleProvider();
        await provider.AssignAsync("u1", UserRoleId.Predefined.Developer);

        await provider.RemoveAsync("u1", UserRoleId.Predefined.Administrator);
        var roles = await provider.GetRolesAsync("u1");

        Assert.Contains(UserRoleId.Predefined.Developer, roles);
    }

    [Fact]
    public async Task IsInRoleAsync_大文字小文字を無視して判定する()
    {
        var provider = new InMemoryUserRoleProvider();
        await provider.AssignAsync("u1", UserRoleId.Predefined.Developer);

        var result = await provider.IsInRoleAsync("u1", "developer");

        Assert.True(result);
    }
}
