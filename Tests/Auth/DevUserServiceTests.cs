using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
    /// <summary>
    /// インメモリ DbContext 生成
    /// </summary>
    /// <param name="dbName">DB 名</param>
    /// <returns>生成したコンテキスト</returns>
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
        var service = new DevUserService(db, new PasswordHasher<DevUserEntity>(), new FakeUserRoleProvider());

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
        var service = new DevUserService(db, new PasswordHasher<DevUserEntity>(), new FakeUserRoleProvider());

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
        var service = new DevUserService(db, new PasswordHasher<DevUserEntity>(), new FakeUserRoleProvider());

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
        var service = new DevUserService(db, new PasswordHasher<DevUserEntity>(), new FakeUserRoleProvider());

        await service.SignUpAsync(new DevSignUpInput
        {
            Email = "user@example.com",
            Password = "Passw0rd!"
        });

        var result = await service.ValidateAsync("user@example.com", "WrongPass");

        Assert.IsNull(result);
    }

    /// <summary>
    /// サインアップ時に operator ロールを付与することを確認する
    /// </summary>
    [TestMethod]
    public async Task サインアップ時にOperatorロール付与()
    {
        await using var db = CreateContext(nameof(サインアップ時にOperatorロール付与));
        var roleProvider = new FakeUserRoleProvider();
        var service = new DevUserService(db, new PasswordHasher<DevUserEntity>(), roleProvider);

        var user = await service.SignUpAsync(new DevSignUpInput
        {
            Email = "operator@example.com",
            Password = "Passw0rd!"
        });

        Assert.AreEqual("operator@example.com", user.Email);
        CollectionAssert.AreEquivalent(
            new[] { UserRoleId.Predefined.Operator },
            roleProvider.GetAssignedRoles("operator@example.com").ToArray());
    }

    private sealed class FakeUserRoleProvider : IUserRoleProvider
    {
        private readonly Dictionary<string, HashSet<UserRoleId>> _roles = new(StringComparer.OrdinalIgnoreCase);

        public Task<IReadOnlyCollection<UserRoleId>> GetRolesAsync(string userId, CancellationToken cancellationToken = default)
        {
            if (!_roles.TryGetValue(userId, out var roles))
            {
                return Task.FromResult<IReadOnlyCollection<UserRoleId>>(Array.Empty<UserRoleId>());
            }

            return Task.FromResult<IReadOnlyCollection<UserRoleId>>(roles.ToArray());
        }

        public Task AssignAsync(string userId, UserRoleId role, CancellationToken cancellationToken = default)
        {
            if (!_roles.TryGetValue(userId, out var roles))
            {
                roles = new HashSet<UserRoleId>();
                _roles[userId] = roles;
            }

            roles.Add(role);
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string userId, UserRoleId role, CancellationToken cancellationToken = default)
        {
            if (_roles.TryGetValue(userId, out var roles))
            {
                roles.Remove(role);
            }

            return Task.CompletedTask;
        }

        public Task<bool> IsInRoleAsync(string userId, string role, CancellationToken cancellationToken = default)
        {
            if (!_roles.TryGetValue(userId, out var roles))
            {
                return Task.FromResult(false);
            }

            return Task.FromResult(roles.Contains(UserRoleId.From(role)));
        }

        public IEnumerable<UserRoleId> GetAssignedRoles(string userId)
        {
            return _roles.TryGetValue(userId, out var roles)
                ? roles.AsEnumerable()
                : Array.Empty<UserRoleId>();
        }
    }
}
