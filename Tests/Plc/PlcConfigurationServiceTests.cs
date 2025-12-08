using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Models.Architecture;
using MOCHA.Services.Architecture;

namespace MOCHA.Tests;

/// <summary>
/// PlcConfigurationService の登録・更新・削除動作を検証するテスト
/// </summary>
[TestClass]
public class PlcConfigurationServiceTests
{
    /// <summary>
    /// 複数台のPLCを追加できる確認
    /// </summary>
    [TestMethod]
    public async Task 複数台のPLCを追加できる()
    {
        var service = CreateService();
        var draft = new PlcUnitDraft
        {
            Name = "PLC-1",
            Manufacturer = "三菱電機",
            Model = "Q03UDV",
            Role = "制御",
            GatewayHost = "127.0.0.1",
            GatewayPort = 8000,
            Modules = new List<PlcModuleDraft>
            {
                new() { Name = "入力", Specification = "16点" }
            }
        };

        var first = await service.AddAsync("user-1", "A-01", draft);
        var second = await service.AddAsync("user-1", "A-01", new PlcUnitDraft
        {
            Name = "PLC-2",
            Manufacturer = "KEYENCE",
            Model = "Q03UDV",
            Role = "制御",
            GatewayHost = "127.0.0.1",
            GatewayPort = 8000,
            Modules = new List<PlcModuleDraft>
            {
                new() { Name = "入力", Specification = "16点" }
            }
        });

        Assert.IsTrue(first.Succeeded);
        Assert.IsTrue(second.Succeeded);

        var list = await service.ListAsync("user-1", "A-01");
        Assert.AreEqual(2, list.Count);
    }

    /// <summary>
    /// コメントファイルとプログラムファイルを上書きできる確認
    /// </summary>
    [TestMethod]
    public async Task コメントとプログラムファイルを上書きできる()
    {
        var service = CreateService();
        var initial = await service.AddAsync(
            "user-2",
            "B-02",
            new PlcUnitDraft
            {
                Name = "PLC-3",
                Manufacturer = "三菱電機",
                GatewayHost = "127.0.0.1",
                GatewayPort = 8000,
                CommentFile = new PlcFileUpload { FileName = "comment_v1.csv", FileSize = 1024, Content = new byte[1024] },
                ProgramFiles = new List<PlcFileUpload>
                {
                    new() { FileName = "program_v1.csv", FileSize = 2048, Content = new byte[2048] }
                }
            });
        Assert.IsTrue(initial.Succeeded);
        var unitId = initial.Unit!.Id;

        var updated = await service.UpdateAsync(
            "user-2",
            "B-02",
            unitId,
            new PlcUnitDraft
            {
                Name = "PLC-3",
                Manufacturer = "三菱電機",
                GatewayHost = "127.0.0.1",
                GatewayPort = 8000,
                CommentFile = new PlcFileUpload { FileName = "comment_v2.csv", FileSize = 3072, Content = new byte[3072] },
                ProgramFiles = new List<PlcFileUpload>
                {
                    new() { FileName = "program_v2.csv", FileSize = 4096, Content = new byte[4096] }
                }
            });

        Assert.IsTrue(updated.Succeeded);
        Assert.AreEqual("comment_v2.csv", updated.Unit!.CommentFile!.FileName);
        Assert.AreEqual(1, updated.Unit.ProgramFiles.Count);
        Assert.AreEqual("program_v2.csv", updated.Unit.ProgramFiles.First().FileName);
    }

    /// <summary>
    /// 削除で一覧から消える確認
    /// </summary>
    [TestMethod]
    public async Task 削除すると一覧から消える()
    {
        var service = CreateService();
        var added = await service.AddAsync(
            "user-3",
            "C-03",
            new PlcUnitDraft
            {
                Name = "PLC-4",
                Manufacturer = "KEYENCE",
                GatewayHost = "127.0.0.1",
                GatewayPort = 8000
            });
        Assert.IsTrue(added.Succeeded);

        var deleted = await service.DeleteAsync("user-3", "C-03", added.Unit!.Id);
        Assert.IsTrue(deleted);

        var list = await service.ListAsync("user-3", "C-03");
        Assert.AreEqual(0, list.Count);
    }

    /// <summary>
    /// ポート番号を保存できる確認
    /// </summary>
    [TestMethod]
    public async Task ポート番号を保存できる()
    {
        var service = CreateService();
        var result = await service.AddAsync(
            "user-4",
            "D-04",
            new PlcUnitDraft
            {
                Name = "PLC-5",
                Manufacturer = "三菱電機",
                IpAddress = "192.168.0.20",
                Port = 5000,
                GatewayHost = "127.0.0.1",
                GatewayPort = 8000
            });

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(5000, result.Unit!.Port);
        Assert.AreEqual("192.168.0.20", result.Unit.IpAddress);
    }

    /// <summary>
    /// プログラムファイルを複数保持できる確認
    /// </summary>
    [TestMethod]
    public async Task プログラムファイルを複数保持できる()
    {
        var service = CreateService();
        var result = await service.AddAsync(
            "user-6",
            "F-06",
            new PlcUnitDraft
            {
                Name = "PLC-7",
                Manufacturer = "KEYENCE",
                GatewayHost = "127.0.0.1",
                GatewayPort = 8000,
                ProgramFiles = new List<PlcFileUpload>
                {
                    new() { FileName = "logic_a.csv", FileSize = 1024, DisplayName = "ロジックA", Content = new byte[1024] },
                    new() { FileName = "logic_b.csv", FileSize = 2048, Content = new byte[2048] }
                }
            });

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(2, result.Unit!.ProgramFiles.Count);
        Assert.AreEqual("ロジックA", result.Unit.ProgramFiles.First().DisplayName);
    }

    /// <summary>
    /// プログラムファイルが保存される確認
    /// </summary>
    [TestMethod]
    public async Task プログラムファイルが保存される()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mocha-plc-save-{Guid.NewGuid():N}");
        var options = Options.Create(new PlcStorageOptions { RootPath = root });
        var service = new PlcConfigurationService(
            new InMemoryPlcUnitRepository(),
            new PlcFileStoragePathBuilder(options),
            NullLogger<PlcConfigurationService>.Instance);

        try
        {
            var result = await service.AddAsync(
                "user-7",
                "G-07",
                new PlcUnitDraft
                {
                    Name = "PLC-8",
                    Manufacturer = "三菱電機",
                    GatewayHost = "127.0.0.1",
                    GatewayPort = 8000,
                    ProgramFiles = new List<PlcFileUpload>
                    {
                        new() { FileName = "logic.csv", FileSize = 3, Content = new byte[] { 1, 2, 3 } }
                    }
                });

            Assert.IsTrue(result.Succeeded);

            var expectedDir = Directory.EnumerateDirectories(root, "G-07", SearchOption.AllDirectories).FirstOrDefault();
            Assert.IsNotNull(expectedDir);
            var files = Directory.GetFiles(expectedDir, "*.csv", SearchOption.AllDirectories);
            Assert.AreEqual(1, files.Length);
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
    /// プログラム構成説明を保存・更新できる
    /// </summary>
    [TestMethod]
    public async Task プログラム構成説明を保存し更新できる()
    {
        var service = CreateService();
        var added = await service.AddAsync(
            "user-10",
            "I-10",
            new PlcUnitDraft
            {
                Name = "PLC-10",
                Manufacturer = "三菱電機",
                GatewayHost = "127.0.0.1",
                GatewayPort = 8000,
                ProgramDescription = "メインプログラムと安全系を分離"
            });

        Assert.IsTrue(added.Succeeded);
        Assert.AreEqual("メインプログラムと安全系を分離", added.Unit!.ProgramDescription);

        var updated = await service.UpdateAsync(
            "user-10",
            "I-10",
            added.Unit.Id,
            new PlcUnitDraft
            {
                Name = "PLC-10",
                Manufacturer = "三菱電機",
                GatewayHost = "127.0.0.1",
                GatewayPort = 8000,
                ProgramDescription = "安全系:FB_SAFETY 呼出し順を先頭に移動"
            });

        Assert.IsTrue(updated.Succeeded);
        Assert.AreEqual("安全系:FB_SAFETY 呼出し順を先頭に移動", updated.Unit!.ProgramDescription);
    }

    /// <summary>
    /// プログラム構成説明が長すぎる場合は保存に失敗する
    /// </summary>
    [TestMethod]
    public async Task プログラム構成説明が長すぎる場合_保存に失敗する()
    {
        var service = CreateService();
        var tooLong = new string('a', PlcUnitDraft.ProgramDescriptionMaxLength + 1);

        var result = await service.AddAsync(
            "user-11",
            "J-11",
            new PlcUnitDraft
            {
                Name = "PLC-11",
                Manufacturer = "KEYENCE",
                GatewayHost = "127.0.0.1",
                GatewayPort = 8000,
                ProgramDescription = tooLong
            });

        Assert.IsFalse(result.Succeeded);
        StringAssert.Contains(result.Error!, "300文字以内");
    }

    /// <summary>
    /// CSV以外のファイルは拒否する確認
    /// </summary>
    [TestMethod]
    public async Task Csv以外のファイルは拒否する()
    {
        var service = CreateService();
        var result = await service.AddAsync(
            "user-5",
            "E-05",
            new PlcUnitDraft
            {
                Name = "PLC-6",
                Manufacturer = "KEYENCE",
                GatewayHost = "127.0.0.1",
                GatewayPort = 8000,
                CommentFile = new PlcFileUpload { FileName = "memo.txt", FileSize = 512 },
                ProgramFiles = new List<PlcFileUpload>
                {
                    new() { FileName = "logic.bin", FileSize = 1024 }
                }
            });

        Assert.IsFalse(result.Succeeded);
        StringAssert.Contains(result.Error!, "CSV");
    }

    /// <summary>
    /// メーカー未選択の場合は保存しない確認
    /// </summary>
    [TestMethod]
    public async Task メーカー未選択なら保存に失敗する()
    {
        var service = CreateService();
        var result = await service.AddAsync(
            "user-8",
            "H-08",
            new PlcUnitDraft
            {
                Name = "PLC-9",
                GatewayHost = "127.0.0.1",
                GatewayPort = 8000
            });

        Assert.IsFalse(result.Succeeded);
        StringAssert.Contains(result.Error!, "メーカー");
    }

    /// <summary>
    /// テスト用サービス生成
    /// </summary>
    /// <returns>構成サービス</returns>
    private static PlcConfigurationService CreateService()
    {
        var options = Options.Create(new PlcStorageOptions
        {
            RootPath = Path.Combine(Path.GetTempPath(), $"mocha-plc-{Guid.NewGuid():N}")
        });
        return new PlcConfigurationService(
            new InMemoryPlcUnitRepository(),
            new PlcFileStoragePathBuilder(options),
            NullLogger<PlcConfigurationService>.Instance);
    }
}
