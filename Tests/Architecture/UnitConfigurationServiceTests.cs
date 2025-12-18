using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Models.Auth;
using MOCHA.Models.Architecture;
using MOCHA.Services.Architecture;

namespace MOCHA.Tests;

/// <summary>
/// UnitConfigurationService の動作検証
/// </summary>
[TestClass]
public class UnitConfigurationServiceTests
{
    /// <summary>
    /// 正常追加で一覧に含まれることを確認
    /// </summary>
    [TestMethod]
    public async Task AddAsync_正常入力_一覧に追加される()
    {
        var service = CreateService();

        var result = await service.AddAsync(
            "user-1",
            "A-01",
            new UnitConfigurationDraft
            {
                Name = "ユニット1",
                Description = "第一ライン",
                Devices = new List<UnitDeviceDraft>
                {
                    new() { Name = "搬送機", Model = "C-100", Maker = "Contoso", Description = "搬送" },
                    new() { Name = "検査機", Model = "V-200", Maker = "Fabrikam", Description = "検査" }
                }
            });

        Assert.IsTrue(result.Succeeded);
        Assert.IsNotNull(result.Unit);
        Assert.AreEqual(2, result.Unit!.Devices.Count);

        var list = await service.ListAsync("user-1", "A-01");
        Assert.AreEqual(1, list.Count);
    }

    /// <summary>
    /// ユニット名未入力なら失敗することを確認
    /// </summary>
    [TestMethod]
    public async Task AddAsync_ユニット名未入力_失敗する()
    {
        var service = CreateService();

        var result = await service.AddAsync(
            "user-2",
            "A-02",
            new UnitConfigurationDraft
            {
                Name = " ",
                Devices = new List<UnitDeviceDraft>
                {
                    new() { Name = "搬送機" }
                }
            });

        Assert.IsFalse(result.Succeeded);
        StringAssert.Contains(result.Error!, "ユニット名");
    }

    /// <summary>
    /// 機器名未入力なら失敗することを確認
    /// </summary>
    [TestMethod]
    public async Task AddAsync_機器名未入力_失敗する()
    {
        var service = CreateService();

        var result = await service.AddAsync(
            "user-3",
            "A-03",
            new UnitConfigurationDraft
            {
                Name = "ユニット3",
                Devices = new List<UnitDeviceDraft>
                {
                    new()
                }
            });

        Assert.IsFalse(result.Succeeded);
        StringAssert.Contains(result.Error!, "機器名");
    }

    /// <summary>
    /// 既存設定を更新できることを確認
    /// </summary>
    [TestMethod]
    public async Task UpdateAsync_既存更新_値が上書きされる()
    {
        var service = CreateService();
        var created = await service.AddAsync(
            "user-4",
            "A-04",
            new UnitConfigurationDraft
            {
                Name = "ユニット4",
                Description = "旧説明",
                Devices = new List<UnitDeviceDraft> { new() { Name = "旧機器" } }
            });

        Assert.IsTrue(created.Succeeded);
        var id = created.Unit!.Id;

        var updated = await service.UpdateAsync(
            "user-4",
            "A-04",
            id,
            new UnitConfigurationDraft
            {
                Name = "ユニット4-更新",
                Description = "新説明",
                Devices = new List<UnitDeviceDraft>
                {
                    new() { Name = "新機器1", Model = "M-1" },
                    new() { Name = "新機器2", Maker = "Fabrikam" }
                }
            });

        Assert.IsTrue(updated.Succeeded);
        Assert.AreEqual("ユニット4-更新", updated.Unit!.Name);
        Assert.AreEqual("新説明", updated.Unit.Description);
        Assert.AreEqual(2, updated.Unit.Devices.Count);
        Assert.IsTrue(updated.Unit.Devices.Any(d => d.Name == "新機器1"));
    }

    /// <summary>
    /// 削除で一覧から消えることを確認
    /// </summary>
    [TestMethod]
    public async Task DeleteAsync_既存削除_一覧から消える()
    {
        var service = CreateService();
        var created = await service.AddAsync(
            "user-5",
            "A-05",
            new UnitConfigurationDraft
            {
                Name = "ユニット5",
                Devices = new List<UnitDeviceDraft> { new() { Name = "搬送機" } }
            });

        Assert.IsTrue(created.Succeeded);

        var deleted = await service.DeleteAsync("user-5", "A-05", created.Unit!.Id);
        Assert.IsTrue(deleted);

        var list = await service.ListAsync("user-5", "A-05");
        Assert.AreEqual(0, list.Count);
    }

    private static UnitConfigurationService CreateService()
    {
        return new UnitConfigurationService(
            new InMemoryUnitConfigurationRepository(),
            new AlwaysAllowRoleProvider(),
            NullLogger<UnitConfigurationService>.Instance);
    }

    private sealed class AlwaysAllowRoleProvider : IUserRoleProvider
    {
        public Task AssignAsync(string userId, UserRoleId role, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyCollection<UserRoleId>> GetRolesAsync(string userId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<UserRoleId>>(Array.Empty<UserRoleId>());
        public Task<bool> IsInRoleAsync(string userId, string role, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task RemoveAsync(string userId, UserRoleId role, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
