using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Models.Auth;
using MOCHA.Models.Drawings;
using MOCHA.Services.Drawings;

namespace MOCHA.Tests;

/// <summary>
/// DrawingRegistrationService の権限制御と登録・更新の検証テスト
/// </summary>
[TestClass]
public class DrawingRegistrationServiceTests
{
    /// <summary>
    /// 非管理者が図面登録に失敗する確認
    /// </summary>
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
                FileSize = 1024,
                Content = new byte[1024]
            });

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual("管理者のみ図面を登録できます", result.Error);
    }

    /// <summary>
    /// 管理者が図面を登録できる確認
    /// </summary>
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
                Description = "制御盤図",
                Content = new byte[2048]
            });

        Assert.IsTrue(result.Succeeded);
        Assert.IsNotNull(result.Document);
        Assert.AreEqual("A-02", result.Document!.AgentNumber);

        var stored = await repository.ListAsync("admin", "A-02");
        Assert.AreEqual(1, stored.Count);
        Assert.AreEqual("panel.dwg", stored[0].FileName);
    }

    /// <summary>
    /// 管理者が複数の図面をまとめて登録できる確認
    /// </summary>
    [TestMethod]
    public async Task 管理者は複数の図面を登録できる()
    {
        var repository = new InMemoryDrawingRepository();
        var service = CreateService(isAdmin: true, repository);

        var uploads = new[]
        {
            new DrawingUpload
            {
                FileName = "layout.pdf",
                ContentType = "application/pdf",
                FileSize = 1024,
                Description = "一括登録",
                Content = new byte[1024]
            },
            new DrawingUpload
            {
                FileName = "mechanical.dwg",
                ContentType = "application/octet-stream",
                FileSize = 2048,
                Description = "一括登録",
                Content = new byte[2048]
            }
        };

        var result = await service.RegisterManyAsync("admin-batch", "D-10", uploads);

        Assert.IsTrue(result.Succeeded);
        Assert.IsNotNull(result.Documents);
        Assert.AreEqual(2, result.Documents!.Count);

        var stored = await repository.ListAsync("admin-batch", "D-10");
        var fileNames = stored.Select(x => x.FileName).OrderBy(x => x).ToList();
        CollectionAssert.AreEqual(new List<string> { "layout.pdf", "mechanical.dwg" }, fileNames);
    }

    /// <summary>
    /// 図面登録時にファイルが保存される確認
    /// </summary>
    [TestMethod]
    public async Task 図面登録でファイルが保存される()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mocha-drawings-{Guid.NewGuid():N}");
        var builder = CreatePathBuilder(root, new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero));
        var repository = new InMemoryDrawingRepository();
        var service = CreateService(isAdmin: true, repository, builder);

        try
        {
            var result = await service.RegisterAsync(
                "admin-save",
                "S-01",
                new DrawingUpload
                {
                    FileName = "save.dwg",
                    ContentType = "application/octet-stream",
                    FileSize = 3,
                    Content = new byte[] { 1, 2, 3 }
                });

            Assert.IsTrue(result.Succeeded);

            var expectedDir = Path.Combine(root, "S-01");
            Assert.IsTrue(Directory.Exists(expectedDir));
            Assert.AreEqual(1, Directory.GetFiles(expectedDir).Length);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    /// <summary>
    /// 複数登録で不正なファイルを含む場合失敗する確認
    /// </summary>
    [TestMethod]
    public async Task 複数登録で不正なファイルは失敗する()
    {
        var repository = new InMemoryDrawingRepository();
        var service = CreateService(isAdmin: true, repository);

        var uploads = new[]
        {
            new DrawingUpload
            {
                FileName = "ok.pdf",
                ContentType = "application/pdf",
                FileSize = 1024,
                Content = new byte[1024]
            },
            new DrawingUpload
            {
                FileName = "zero.pdf",
                ContentType = "application/pdf",
                FileSize = 0,
                Content = Array.Empty<byte>()
            }
        };

        var result = await service.RegisterManyAsync("admin-batch2", "D-11", uploads);

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual("ファイルサイズが 0 バイトです", result.Error);

        var stored = await repository.ListAsync("admin-batch2", "D-11");
        Assert.AreEqual(0, stored.Count);
    }

    /// <summary>
    /// 非管理者が説明更新に失敗する確認
    /// </summary>
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

    /// <summary>
    /// 管理者が説明を更新できる確認
    /// </summary>
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
                Description = "初回レイアウト",
                Content = new byte[5120]
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

    /// <summary>
    /// 管理者が図面を削除できる確認
    /// </summary>
    [TestMethod]
    public async Task 管理者は図面を削除できる()
    {
        var repository = new InMemoryDrawingRepository();
        var service = CreateService(isAdmin: true, repository);
        var registered = await service.RegisterAsync(
            "admin-del",
            "D-01",
            new DrawingUpload
            {
                FileName = "delete.pdf",
                ContentType = "application/pdf",
                FileSize = 1024,
                Content = new byte[1024]
            });
        Assert.IsTrue(registered.Succeeded);
        var target = registered.Document!;

        var result = await service.DeleteAsync("admin-del", target.Id);

        Assert.IsTrue(result.Succeeded);
        var remaining = await repository.ListAsync("admin-del", "D-01");
        Assert.AreEqual(0, remaining.Count);
    }

    /// <summary>
    /// 非管理者は図面を削除できない確認
    /// </summary>
    [TestMethod]
    public async Task 非管理者は図面を削除できない()
    {
        var repository = new InMemoryDrawingRepository();
        var adminService = CreateService(isAdmin: true, repository);
        var ownerId = "owner-1";
        var registered = await adminService.RegisterAsync(
            ownerId,
            "D-02",
            new DrawingUpload
            {
                FileName = "keep.pdf",
                ContentType = "application/pdf",
                FileSize = 2048,
                Content = new byte[2048]
            });
        Assert.IsTrue(registered.Succeeded);

        var service = CreateService(isAdmin: false, repository);
        var result = await service.DeleteAsync(ownerId, registered.Document!.Id);

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual("管理者のみ図面を削除できます", result.Error);
    }

    /// <summary>
    /// テスト用サービス生成
    /// </summary>
    /// <param name="isAdmin">管理者判定</param>
    /// <param name="repository">図面リポジトリ</param>
    /// <returns>サービス</returns>
    private static DrawingRegistrationService CreateService(bool isAdmin, IDrawingRepository? repository = null, IDrawingStoragePathBuilder? pathBuilder = null)
    {
        var roleProvider = new FakeRoleProvider(isAdmin);
        return new DrawingRegistrationService(
            repository ?? new InMemoryDrawingRepository(),
            pathBuilder ?? new FakePathBuilder(),
            roleProvider,
            NullLogger<DrawingRegistrationService>.Instance);
    }

    private static DrawingStoragePathBuilder CreatePathBuilder(string rootPath, DateTimeOffset now)
    {
        var options = Options.Create(new DrawingStorageOptions
        {
            RootPath = rootPath
        });
        return new DrawingStoragePathBuilder(options, () => now);
    }

    /// <summary>
    /// 管理者判定を返すフェイクロールプロバイダー
    /// </summary>
    private sealed class FakeRoleProvider : IUserRoleProvider
    {
        private readonly bool _isAdmin;

        /// <summary>
        /// 管理者フラグ指定の初期化
        /// </summary>
        /// <param name="isAdmin">管理者フラグ</param>
        public FakeRoleProvider(bool isAdmin)
        {
            _isAdmin = isAdmin;
        }

        public Task AssignAsync(string userId, UserRoleId role, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// ロール一覧取得
        /// </summary>
        public Task<IReadOnlyCollection<UserRoleId>> GetRolesAsync(string userId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyCollection<UserRoleId>>(Array.Empty<UserRoleId>());
        }

        /// <summary>
        /// ロール保持判定
        /// </summary>
        public Task<bool> IsInRoleAsync(string userId, string role, CancellationToken cancellationToken = default)
        {
            if (string.Equals(role, UserRoleId.Predefined.Administrator.Value, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(_isAdmin);
            }

            return Task.FromResult(false);
        }

        /// <summary>
        /// ロール削除（何もしない）
        /// </summary>
        public Task RemoveAsync(string userId, UserRoleId role, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// テスト用のパスビルダー
    /// </summary>
    private sealed class FakePathBuilder : IDrawingStoragePathBuilder
    {
        public DrawingStoragePath Build(string agentNumber, string fileName)
        {
            var root = Path.Combine(Path.GetTempPath(), "mocha-drawings-tests");
            var relative = $"{agentNumber}/{fileName}";
            var directory = Path.Combine(root, agentNumber);
            var full = Path.Combine(root, relative);
            return new DrawingStoragePath(root, relative, directory, fileName, full);
        }
    }
}
