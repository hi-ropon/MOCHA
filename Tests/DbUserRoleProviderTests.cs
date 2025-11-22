using Microsoft.EntityFrameworkCore;
using MOCHA.Data;
using MOCHA.Models.Auth;
using MOCHA.Services.Auth;
using Xunit;

namespace MOCHA.Tests;

public class DbUserRoleProviderTests
{
    private static ChatDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ChatDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new ChatDbContext(options);
    }

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
