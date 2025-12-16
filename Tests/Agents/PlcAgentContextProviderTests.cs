using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Agents.Application;
using MOCHA.Models.Architecture;
using MOCHA.Services.Agents;
using MOCHA.Services.Architecture;

namespace MOCHA.Tests;

/// <summary>
/// PlcAgentContextProvider の動作を検証するテスト
/// </summary>
[TestClass]
public class PlcAgentContextProviderTests
{
    /// <summary>
    /// ユニット指定なしでゲートウェイとユニットの接続情報を返す
    /// </summary>
    [TestMethod]
    public async Task BuildAsync_ユニット指定なし_接続情報を返す()
    {
        var plcRepo = new InMemoryPlcUnitRepository();
        var gatewayRepo = new InMemoryGatewaySettingRepository();
        var provider = new PlcAgentContextProvider(plcRepo, gatewayRepo, NullLogger<PlcAgentContextProvider>.Instance);

        var gateway = GatewaySetting.Create("user-1", "A-01", new GatewaySettingDraft { Host = "10.0.0.1", Port = 8500 }, DateTimeOffset.UtcNow);
        await gatewayRepo.UpsertAsync(gateway);

        var unitA = PlcUnit.Restore(
            Guid.NewGuid(),
            "user-1",
            "A-01",
            "Unit-A",
            "Mitsubishi",
            "R04",
            "main",
            "192.168.0.10",
            5000,
            "tcp",
            null,
            null,
            null,
            Array.Empty<PlcFileUpload>(),
            null,
            Array.Empty<PlcUnitModule>(),
            Array.Empty<FunctionBlock>(),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        var unitB = PlcUnit.Restore(
            Guid.NewGuid(),
            "user-1",
            "A-01",
            "Unit-B",
            "Mitsubishi",
            "R04",
            "sub",
            "192.168.0.11",
            5001,
            "udp",
            "10.0.0.2",
            8600,
            null,
            Array.Empty<PlcFileUpload>(),
            null,
            Array.Empty<PlcUnitModule>(),
            Array.Empty<FunctionBlock>(),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        await plcRepo.AddAsync(unitA);
        await plcRepo.AddAsync(unitB);

        var context = await provider.BuildAsync("user-1", "A-01", null, CancellationToken.None);

        Assert.AreEqual("10.0.0.1", context.GatewayHost);
        Assert.AreEqual(8500, context.GatewayPort);
        Assert.AreEqual(2, context.Units.Count);
        Assert.AreEqual("192.168.0.10", context.Units[0].IpAddress);
        Assert.AreEqual("10.0.0.1", context.Units[0].GatewayHost);
        Assert.AreEqual(8500, context.Units[0].GatewayPort);
        Assert.AreEqual("10.0.0.2", context.Units[1].GatewayHost);
        Assert.AreEqual(8600, context.Units[1].GatewayPort);
    }

    /// <summary>
    /// ユニット指定時は対象ユニットのみ返す
    /// </summary>
    [TestMethod]
    public async Task BuildAsync_ユニット指定時_対象ユニットのみ返す()
    {
        var plcRepo = new InMemoryPlcUnitRepository();
        var gatewayRepo = new InMemoryGatewaySettingRepository();
        var provider = new PlcAgentContextProvider(plcRepo, gatewayRepo, NullLogger<PlcAgentContextProvider>.Instance);

        var unitA = PlcUnit.Restore(
            Guid.NewGuid(),
            "user-1",
            "A-01",
            "Unit-A",
            "Mitsubishi",
            "R04",
            "main",
            "192.168.0.10",
            5000,
            "tcp",
            null,
            null,
            null,
            Array.Empty<PlcFileUpload>(),
            null,
            Array.Empty<PlcUnitModule>(),
            Array.Empty<FunctionBlock>(),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        var unitB = PlcUnit.Restore(
            Guid.NewGuid(),
            "user-1",
            "A-01",
            "Unit-B",
            "Mitsubishi",
            "R04",
            "sub",
            "192.168.0.11",
            5001,
            "tcp",
            null,
            null,
            null,
            Array.Empty<PlcFileUpload>(),
            null,
            Array.Empty<PlcUnitModule>(),
            Array.Empty<FunctionBlock>(),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        await plcRepo.AddAsync(unitA);
        await plcRepo.AddAsync(unitB);

        var context = await provider.BuildAsync("user-1", "A-01", unitB.Id, CancellationToken.None);

        Assert.AreEqual(1, context.Units.Count);
        Assert.AreEqual(unitB.Id, context.Units[0].Id);
    }
}
