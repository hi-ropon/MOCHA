using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Agents.Application;
using MOCHA.Agents.Domain;
using MOCHA.Agents.Domain.Plc;
using MOCHA.Agents.Infrastructure.Plc;
using MOCHA.Agents.Infrastructure.Tools;

namespace MOCHA.Tests;

/// <summary>
/// PlcToolset のツール構成を検証するテスト
/// </summary>
[TestClass]
public class PlcToolsetTests
{
    /// <summary>
    /// オンライン状態では読み取りツールを含める
    /// </summary>
    [TestMethod]
    public void GetTools_オンラインでは読み取りツールを含む()
    {
        var toolset = CreateToolset();

        var tools = toolset.GetTools(includeGatewayReads: true);

        Assert.IsTrue(tools.Any(t => string.Equals(t.Name, "read_plc_values", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(tools.Any(t => string.Equals(t.Name, "read_multiple_plc_values", StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// オフライン状態では読み取りツールを除外する
    /// </summary>
    [TestMethod]
    public void GetTools_オフラインでは読み取りツールを除外する()
    {
        var toolset = CreateToolset();

        var tools = toolset.GetTools(includeGatewayReads: false);

        Assert.IsFalse(tools.Any(t => string.Equals(t.Name, "read_plc_values", StringComparison.OrdinalIgnoreCase)));
        Assert.IsFalse(tools.Any(t => string.Equals(t.Name, "read_multiple_plc_values", StringComparison.OrdinalIgnoreCase)));
    }

    private static PlcToolset CreateToolset()
    {
        var store = new PlcDataStore();
        var gateway = new DummyGateway();
        var analyzer = new PlcProgramAnalyzer(store);
        var reasoner = new PlcReasoner();
        var manuals = new PlcManualService(new DummyManualStore());
        return new PlcToolset(store, gateway, analyzer, reasoner, manuals, NullLogger<PlcToolset>.Instance);
    }

    private sealed class DummyGateway : IPlcGatewayClient
    {
        public Task<DeviceReadResult> ReadAsync(DeviceReadRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new DeviceReadResult(request.Spec, Array.Empty<int>(), true, null));
        }

        public Task<BatchReadResult> ReadBatchAsync(BatchReadRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new BatchReadResult(Array.Empty<DeviceReadResult>(), null));
        }
    }

    private sealed class DummyManualStore : IManualStore
    {
        public Task<ManualContent?> ReadAsync(string agentName, string relativePath, int? maxBytes = null, ManualSearchContext? context = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ManualContent?>(new ManualContent(relativePath, "dummy", 1));
        }

        public Task<IReadOnlyList<ManualHit>> SearchAsync(string agentName, string query, ManualSearchContext? context = null, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ManualHit> hits = new List<ManualHit> { new(query, "path", 1.0) };
            return Task.FromResult(hits);
        }
    }
}
