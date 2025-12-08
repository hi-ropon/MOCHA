using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Agents.Application;
using MOCHA.Models.Architecture;
using MOCHA.Models.Drawings;
using MOCHA.Services.Agents;
using MOCHA.Services.Architecture;
using MOCHA.Services.Drawings;

namespace MOCHA.Tests;

/// <summary>
/// OrganizerContextProvider のコンテキスト組み立てを検証するテスト
/// </summary>
[TestClass]
public class OrganizerContextProviderTests
{
    /// <summary>
    /// PC設定がある場合にアーキテクチャに含める確認
    /// </summary>
    [TestMethod]
    public async Task BuildAsync_PC設定がある場合_PC情報を含む()
    {
        var pcRepo = new InMemoryPcSettingRepository();
        var plcRepo = new InMemoryPlcUnitRepository();
        var gatewayRepo = new InMemoryGatewaySettingRepository();
        var unitConfigRepo = new InMemoryUnitConfigurationRepository();
        var drawingCatalog = new DrawingCatalog(new FakeDrawingRepository(), Options.Create(new DrawingStorageOptions()));
        var provider = new OrganizerContextProvider(pcRepo, plcRepo, gatewayRepo, unitConfigRepo, drawingCatalog, NullLogger<OrganizerContextProvider>.Instance);

        var pcSetting = PcSetting.Create(
            "user-1",
            "A-01",
            new PcSettingDraft
            {
                Os = "Windows 11",
                Role = "HMI",
                RepositoryUrls = new List<string> { "https://example.com/repo.git" }
            },
            DateTimeOffset.UtcNow);
        await pcRepo.AddAsync(pcSetting);

        var context = await provider.BuildAsync("user-1", "A-01", CancellationToken.None);

        StringAssert.Contains(context.Architecture, "PC: Windows 11");
        StringAssert.Contains(context.Architecture, "repos:https://example.com/repo.git");
    }

    /// <summary>
    /// 装置ユニットがある場合に全件の構成機器を含める
    /// </summary>
    [TestMethod]
    public async Task BuildAsync_装置ユニットがある場合_構成機器を全て含める()
    {
        var pcRepo = new InMemoryPcSettingRepository();
        var plcRepo = new InMemoryPlcUnitRepository();
        var gatewayRepo = new InMemoryGatewaySettingRepository();
        var unitConfigRepo = new InMemoryUnitConfigurationRepository();
        var drawingCatalog = new DrawingCatalog(new FakeDrawingRepository(), Options.Create(new DrawingStorageOptions()));
        var provider = new OrganizerContextProvider(pcRepo, plcRepo, gatewayRepo, unitConfigRepo, drawingCatalog, NullLogger<OrganizerContextProvider>.Instance);

        var draft = new UnitConfigurationDraft
        {
            Name = "ラインA",
            Description = "搬送ライン",
            Devices = new List<UnitDeviceDraft>
            {
                new() { Name = "CPU", Model = "R04", Maker = "Mitsubishi", Description = "主制御" },
                new() { Name = "I/O", Model = "RX41C4", Maker = "Mitsubishi" }
            }
        };
        var unit = UnitConfiguration.Create("user-1", "A-01", draft, DateTimeOffset.UtcNow);
        await unitConfigRepo.AddAsync(unit);

        var context = await provider.BuildAsync("user-1", "A-01", CancellationToken.None);

        StringAssert.Contains(context.Architecture, "装置ユニット: ラインA desc:搬送ライン");
        StringAssert.Contains(context.Architecture, "CPU(R04/Mitsubishi) desc:主制御");
        StringAssert.Contains(context.Architecture, "I/O(RX41C4/Mitsubishi)");
    }

    /// <summary>
    /// PLCユニットのモジュールとFBを全件含める
    /// </summary>
    [TestMethod]
    public async Task BuildAsync_PLCユニットがある場合_モジュールとFBを全件含める()
    {
        var pcRepo = new InMemoryPcSettingRepository();
        var plcRepo = new InMemoryPlcUnitRepository();
        var gatewayRepo = new InMemoryGatewaySettingRepository();
        var unitConfigRepo = new InMemoryUnitConfigurationRepository();
        var drawingCatalog = new DrawingCatalog(new FakeDrawingRepository(), Options.Create(new DrawingStorageOptions()));
        var provider = new OrganizerContextProvider(pcRepo, plcRepo, gatewayRepo, unitConfigRepo, drawingCatalog, NullLogger<OrganizerContextProvider>.Instance);

        var modules = Enumerable.Range(1, 5)
            .Select(i => new PlcUnitModule(Guid.NewGuid(), $"M{i}", $"Spec{i}"))
            .ToList();
        var blocks = Enumerable.Range(1, 5)
            .Select(i => FunctionBlock.Create(
                $"FB{i}",
                $"fb{i}",
                new PlcFileUpload { FileName = $"label{i}.txt", FileSize = 1 },
                new PlcFileUpload { FileName = $"prog{i}.txt", FileSize = 1 }))
            .ToList();

        var unit = PlcUnit.Restore(
            Guid.NewGuid(),
            "user-1",
            "A-01",
            "Unit-A",
            "Mitsubishi",
            "R04",
            "main",
            "192.168.0.10",
            5000,
            null,
            null,
            null,
            Array.Empty<PlcFileUpload>(),
            modules,
            blocks,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        await plcRepo.AddAsync(unit);

        var context = await provider.BuildAsync("user-1", "A-01", CancellationToken.None);

        StringAssert.Contains(context.Architecture, "モジュール: M1(Spec1)");
        StringAssert.Contains(context.Architecture, "M5(Spec5)");
        StringAssert.Contains(context.Architecture, "FB: FB1(safe:fb1)");
        StringAssert.Contains(context.Architecture, "FB5(safe:fb5)");
        Assert.IsFalse(context.Architecture.Contains("…他"));
    }

    private sealed class FakeDrawingRepository : IDrawingRepository
    {
        public Task<DrawingDocument> AddAsync(DrawingDocument document, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(document);
        }

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public Task<DrawingDocument?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<DrawingDocument?>(null);
        }

        public Task<IReadOnlyList<DrawingDocument>> ListAsync(string userId, string? agentNumber, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<DrawingDocument>>(new List<DrawingDocument>());
        }

        public Task<DrawingDocument> UpdateAsync(DrawingDocument document, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(document);
        }

        public Task<DrawingDocument?> UpdateDescriptionAsync(Guid id, string description, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<DrawingDocument?>(null);
        }
    }
}
