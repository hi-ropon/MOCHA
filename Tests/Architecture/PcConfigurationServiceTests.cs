using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Models.Architecture;
using MOCHA.Services.Architecture;

namespace MOCHA.Tests;

/// <summary>
/// PcConfigurationService の動作検証
/// </summary>
[TestClass]
public class PcConfigurationServiceTests
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
            new PcSettingDraft
            {
                Os = "Windows 11",
                Role = "HMI",
                RepositoryUrls = new List<string> { "https://example.com/repo1", "https://example.com/repo2" }
            });

        Assert.IsTrue(result.Succeeded);
        Assert.IsNotNull(result.Setting);
        Assert.AreEqual(2, result.Setting!.RepositoryUrls.Count);

        var list = await service.ListAsync("user-1", "A-01");
        Assert.AreEqual(1, list.Count);
    }

    /// <summary>
    /// OS未入力なら失敗することを確認
    /// </summary>
    [TestMethod]
    public async Task AddAsync_OS未入力_失敗する()
    {
        var service = CreateService();
        var result = await service.AddAsync("user-2", "A-02", new PcSettingDraft());

        Assert.IsFalse(result.Succeeded);
        StringAssert.Contains(result.Error!, "OS");
    }

    /// <summary>
    /// 不正URLを弾くことを確認
    /// </summary>
    [TestMethod]
    public async Task AddAsync_不正URL_失敗する()
    {
        var service = CreateService();
        var result = await service.AddAsync(
            "user-3",
            "A-03",
            new PcSettingDraft
            {
                Os = "Ubuntu",
                RepositoryUrls = new List<string> { "ftp://invalid.example.com" }
            });

        Assert.IsFalse(result.Succeeded);
        StringAssert.Contains(result.Error!, "URL");
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
            new PcSettingDraft
            {
                Os = "Ubuntu",
                Role = "制御",
                RepositoryUrls = new List<string> { "https://git.example.com/repo-a.git" }
            });
        Assert.IsTrue(created.Succeeded);
        var id = created.Setting!.Id;

        var updated = await service.UpdateAsync(
            "user-4",
            "A-04",
            id,
            new PcSettingDraft
            {
                Os = "Debian",
                Role = "監視",
                RepositoryUrls = new List<string> { "https://git.example.com/repo-b.git" }
            });

        Assert.IsTrue(updated.Succeeded);
        Assert.AreEqual("Debian", updated.Setting!.Os);
        Assert.AreEqual("監視", updated.Setting.Role);
        Assert.AreEqual(1, updated.Setting.RepositoryUrls.Count);
        Assert.AreEqual("https://git.example.com/repo-b.git", updated.Setting.RepositoryUrls.First());
    }

    /// <summary>
    /// 削除で一覧から消えることを確認
    /// </summary>
    [TestMethod]
    public async Task DeleteAsync_既存削除_一覧から消える()
    {
        var service = CreateService();
        var added = await service.AddAsync(
            "user-5",
            "A-05",
            new PcSettingDraft
            {
                Os = "Rocky Linux"
            });
        Assert.IsTrue(added.Succeeded);

        var deleted = await service.DeleteAsync("user-5", "A-05", added.Setting!.Id);
        Assert.IsTrue(deleted);

        var list = await service.ListAsync("user-5", "A-05");
        Assert.AreEqual(0, list.Count);
    }

    /// <summary>
    /// 複数URLを正規化して保持できることを確認
    /// </summary>
    [TestMethod]
    public async Task AddAsync_重複URLを含む_正規化されたURLが保持される()
    {
        var service = CreateService();
        var result = await service.AddAsync(
            "user-6",
            "A-06",
            new PcSettingDraft
            {
                Os = "Windows 10",
                RepositoryUrls = new List<string> { " https://example.com/repo ", "https://example.com/repo", "https://example.com/repo2" }
            });

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(2, result.Setting!.RepositoryUrls.Count);
        Assert.IsTrue(result.Setting.RepositoryUrls.Contains("https://example.com/repo"));
        Assert.IsTrue(result.Setting.RepositoryUrls.Contains("https://example.com/repo2"));
    }

    private static PcConfigurationService CreateService()
    {
        return new PcConfigurationService(
            new InMemoryPcSettingRepository(),
            NullLogger<PcConfigurationService>.Instance);
    }
}
