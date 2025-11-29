using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Models.Auth;
using MOCHA.Models.Drawings;
using MOCHA.Services.Drawings;

namespace MOCHA.Tests;

[TestClass]
public class DrawingRegistrationServiceTests
{
    [TestMethod]
    public async Task 非管理者は図面登録に失敗する()
    {
        var service = CreateService(isAdmin: false);

        var result = await service.RegisterAsync(
            "user-1",
            "A-01",
            new DrawingUpload
            {
                FileName = "layout.pdf",
                ContentType = "application/pdf",
                FileSize = 1024
            });

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual("管理者のみ図面を登録できます", result.Error);
    }

    [TestMethod]
    public async Task 管理者は図面を登録できる()
    {
        var repository = new InMemoryDrawingRepository();
        var service = CreateService(isAdmin: true, repository);

        var result = await service.RegisterAsync(
            "admin",
            "A-02",
            new DrawingUpload
            {
                FileName = "panel.dwg",
                ContentType = "application/octet-stream",
                FileSize = 2048,
                Description = "制御盤図"
            });

        Assert.IsTrue(result.Succeeded);
        Assert.IsNotNull(result.Document);
        Assert.AreEqual("A-02", result.Document!.AgentNumber);

        var stored = await repository.ListAsync("admin", "A-02");
        Assert.AreEqual(1, stored.Count);
        Assert.AreEqual("panel.dwg", stored[0].FileName);
    }

    [TestMethod]
    public async Task 非管理者は説明更新に失敗する()
    {
        var repository = new InMemoryDrawingRepository();
        var existing = DrawingDocument.Create(
            "user-2",
            "B-01",
            "assembly.pdf",
            "application/pdf",
            4096,
            "初回版");
        await repository.AddAsync(existing);

        var service = CreateService(isAdmin: false, repository);
        var result = await service.UpdateDescriptionAsync("user-2", existing.Id, "修正案");

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual("管理者のみ図面を編集できます", result.Error);
    }

    [TestMethod]
    public async Task 管理者は説明を更新できる()
    {
        var service = CreateService(isAdmin: true);

        var registered = await service.RegisterAsync(
            "admin-2",
            "C-03",
            new DrawingUpload
            {
                FileName = "line.png",
                ContentType = "image/png",
                FileSize = 5120,
                Description = "初回レイアウト"
            });
        Assert.IsTrue(registered.Succeeded);
        var original = registered.Document!;

        await Task.Delay(5);
        var updated = await service.UpdateDescriptionAsync("admin-2", original.Id, "承認済みレイアウト");

        Assert.IsTrue(updated.Succeeded);
        Assert.IsNotNull(updated.Document);
        Assert.AreEqual("承認済みレイアウト", updated.Document!.Description);
        Assert.IsTrue(updated.Document.UpdatedAt > original.UpdatedAt);
    }

    private static DrawingRegistrationService CreateService(bool isAdmin, IDrawingRepository? repository = null)
    {
        var roleProvider = new FakeRoleProvider(isAdmin);
        return new DrawingRegistrationService(
            repository ?? new InMemoryDrawingRepository(),
            roleProvider,
            NullLogger<DrawingRegistrationService>.Instance);
    }

    private sealed class FakeRoleProvider : IUserRoleProvider
    {
        private readonly bool _isAdmin;

        public FakeRoleProvider(bool isAdmin)
        {
            _isAdmin = isAdmin;
        }

        public Task AssignAsync(string userId, UserRoleId role, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<UserRoleId>> GetRolesAsync(string userId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyCollection<UserRoleId>>(Array.Empty<UserRoleId>());
        }

        public Task<bool> IsInRoleAsync(string userId, string role, CancellationToken cancellationToken = default)
        {
            if (string.Equals(role, UserRoleId.Predefined.Administrator.Value, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(_isAdmin);
            }

            return Task.FromResult(false);
        }

        public Task RemoveAsync(string userId, UserRoleId role, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
