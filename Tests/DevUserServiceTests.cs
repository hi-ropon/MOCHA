using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Data;
using MOCHA.Models.Auth;
using MOCHA.Services.Auth;

namespace MOCHA.Tests;

/// <summary>
/// DevUserService のサインアップと検証を確認するテスト
/// </summary>
[TestClass]
public class DevUserServiceTests
{
    private static ChatDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ChatDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new ChatDbContext(options);
    }

    /// <summary>
    /// 新規登録できることを確認する
    /// </summary>
    [TestMethod]
    public async Task 新規登録できる()
    {
        await using var db = CreateContext(nameof(新規登録できる));
        var service = new DevUserService(db, new PasswordHasher<DevUserEntity>());

        var user = await service.SignUpAsync(new DevSignUpInput
        {
            Email = "user@example.com",
            Password = "Passw0rd!"
        });

        Assert.AreEqual("user@example.com", user.Email);
        Assert.AreEqual("user@example.com", user.DisplayName);
        Assert.IsFalse(string.IsNullOrWhiteSpace(user.PasswordHash));
    }

    /// <summary>
    /// 重複メールは例外となることを確認する
    /// </summary>
    [TestMethod]
    public async Task 重複メールは例外()
    {
        await using var db = CreateContext(nameof(重複メールは例外));
        var service = new DevUserService(db, new PasswordHasher<DevUserEntity>());

        await service.SignUpAsync(new DevSignUpInput
        {
            Email = "user@example.com",
            Password = "Passw0rd!"
        });

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
        {
            await service.SignUpAsync(new DevSignUpInput
            {
                Email = "user@example.com",
                Password = "Passw0rd!"
            });
        });
    }

    /// <summary>
    /// 検証成功でユーザーが返ることを確認する
    /// </summary>
    [TestMethod]
    public async Task 検証成功でユーザーを返す()
    {
        await using var db = CreateContext(nameof(検証成功でユーザーを返す));
        var service = new DevUserService(db, new PasswordHasher<DevUserEntity>());

        await service.SignUpAsync(new DevSignUpInput
        {
            Email = "user@example.com",
            Password = "Passw0rd!"
        });

        var result = await service.ValidateAsync("user@example.com", "Passw0rd!");

        Assert.IsNotNull(result);
        Assert.AreEqual("user@example.com", result.Email);
    }

    /// <summary>
    /// パスワード不一致は null を返すことを確認する
    /// </summary>
    [TestMethod]
    public async Task パスワード不一致はnull()
    {
        await using var db = CreateContext(nameof(パスワード不一致はnull));
        var service = new DevUserService(db, new PasswordHasher<DevUserEntity>());

        await service.SignUpAsync(new DevSignUpInput
        {
            Email = "user@example.com",
            Password = "Passw0rd!"
        });

        var result = await service.ValidateAsync("user@example.com", "WrongPass");

        Assert.IsNull(result);
    }
}
