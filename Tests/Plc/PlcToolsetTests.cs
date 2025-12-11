using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

    /// <summary>
    /// コンテキストヒントに接続設定を含める
    /// </summary>
    [TestMethod]
    public void BuildContextHint_接続設定を含める()
    {
        var toolset = CreateToolset();
        var connection = new PlcAgentContext(
            "10.0.0.1",
            8500,
            new[]
            {
                new PlcAgentUnit(Guid.NewGuid(), "Unit-A", "192.168.0.10", 5000, "10.0.0.1", 8500),
                new PlcAgentUnit(Guid.NewGuid(), "Unit-B", "192.168.0.11", 5001, "10.0.0.2", 8600)
            });

        var hint = toolset.BuildContextHint(
            gatewayOptionsJson: "{\"ip\":\"10.0.0.3\"}",
            plcUnitId: null,
            plcUnitName: null,
            enableFunctionBlocks: true,
            note: "note",
            plcOnline: true,
            connectionContext: connection);

        StringAssert.Contains(hint, "デフォルトゲートウェイ: 10.0.0.1:8500");
        StringAssert.Contains(hint, "ユニット: Unit-A ip=192.168.0.10 port=5000 gw=10.0.0.1:8500");
        StringAssert.Contains(hint, "ユニット: Unit-B ip=192.168.0.11 port=5001 gw=10.0.0.2:8600");
        StringAssert.Contains(hint, "ゲートウェイオプション");
        StringAssert.Contains(hint, "補足: note");
    }

    /// <summary>
    /// DMOVで使われているDレジスタは2ワードでゲートウェイに投げる
    /// </summary>
    [TestMethod]
    public async Task ReadValuesAsync_DMOV使用時_2ワードを指定する()
    {
        var store = CreateStore();
        store.SetPrograms(new[]
        {
            new ProgramFile("main", new List<string> { "\"0\"\t\"\"\t\"DMOV\"\t\"D500\"" })
        });

        var gateway = new CaptureGateway();
        var analyzer = new PlcProgramAnalyzer(store);
        var search = new PlcCommentSearchService(store);
        var reasoner = new PlcReasoner();
        var faultTracer = new PlcFaultTracer(store);
        var manuals = new PlcManualService(new DummyManualStore());
        var toolset = new PlcToolset(store, gateway, analyzer, search, reasoner, faultTracer, manuals, NullLogger<PlcToolset>.Instance);
        var method = typeof(PlcToolset).GetMethod("ReadValuesAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method);

        var task = (Task<string>)method!.Invoke(toolset, new object?[] { "D500", string.Empty, 0, 0, null, CancellationToken.None })!;
        await task;

        Assert.AreEqual("D500:2", gateway.LastRequest?.Spec);
    }

    /// <summary>
    /// 浮動小数演算で使われているDレジスタも2ワードでバッチ読み取りする
    /// </summary>
    [TestMethod]
    public async Task ReadMultipleValuesAsync_EMOV使用時_2ワードを指定する()
    {
        var store = CreateStore();
        store.SetPrograms(new[]
        {
            new ProgramFile("main", new List<string> { "\"0\"\t\"\"\t\"EMOV\"\t\"D600\"" })
        });

        var gateway = new CaptureGateway();
        var analyzer = new PlcProgramAnalyzer(store);
        var search = new PlcCommentSearchService(store);
        var reasoner = new PlcReasoner();
        var faultTracer = new PlcFaultTracer(store);
        var manuals = new PlcManualService(new DummyManualStore());
        var toolset = new PlcToolset(store, gateway, analyzer, search, reasoner, faultTracer, manuals, NullLogger<PlcToolset>.Instance);
        var method = typeof(PlcToolset).GetMethod("ReadMultipleValuesAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method);

        var task = (Task<string>)method!.Invoke(toolset, new object?[] { new List<string> { "D600" }, null, CancellationToken.None })!;
        await task;

        CollectionAssert.Contains((System.Collections.ICollection)gateway.LastBatchSpecs!, "D600:2");
    }

    /// <summary>
    /// プログラム名を含む質問ではプログラム内容からデバイスを抽出する
    /// </summary>
    [TestMethod]
    public async Task InferDevicesAsync_プログラム名指定時はプログラム内容を使う()
    {
        var store = CreateStore();
        store.SetPrograms(new[]
        {
            new ProgramFile("ProgPou.csv", new List<string> { "\"0\"\t\"\"\t\"LD\"\t\"X30\"" })
        });

        var gateway = new DummyGateway();
        var analyzer = new PlcProgramAnalyzer(store);
        var search = new PlcCommentSearchService(store);
        var reasoner = new PlcReasoner();
        var faultTracer = new PlcFaultTracer(store);
        var manuals = new PlcManualService(new DummyManualStore());
        var toolset = new PlcToolset(store, gateway, analyzer, search, reasoner, faultTracer, manuals, NullLogger<PlcToolset>.Instance);
        var method = typeof(PlcToolset).GetMethod("InferDevicesAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method);

        var task = (Task<string>)method!.Invoke(toolset, new object?[] { "ProgPou.csv を確認したい", CancellationToken.None })!;
        var json = await task;

        StringAssert.Contains(json, "X30");
    }

    /// <summary>
    /// コメント検索ツールで質問文のキーワードから候補を返す
    /// </summary>
    [TestMethod]
    public async Task SearchCommentsAsync_質問キーワードから候補を返す()
    {
        var store = CreateStore();
        store.SetComments(new Dictionary<string, string>
        {
            ["M0"] = "ﾘﾐｯﾄｽｲｯﾁ異常",
            ["D10"] = "圧力計"
        });

        var gateway = new DummyGateway();
        var analyzer = new PlcProgramAnalyzer(store);
        var search = new PlcCommentSearchService(store);
        var reasoner = new PlcReasoner();
        var faultTracer = new PlcFaultTracer(store);
        var manuals = new PlcManualService(new DummyManualStore());
        var toolset = new PlcToolset(store, gateway, analyzer, search, reasoner, faultTracer, manuals, NullLogger<PlcToolset>.Instance);
        var method = typeof(PlcToolset).GetMethod("SearchCommentsAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method);

        var task = (Task<string>)method!.Invoke(toolset, new object?[] { "リミットスイッチが効かない", CancellationToken.None })!;
        var json = await task;

        StringAssert.Contains(json, "\"matchCount\":1");
        StringAssert.Contains(json, "M0");
    }

    private static PlcToolset CreateToolset()
    {
        var store = CreateStore();
        var gateway = new DummyGateway();
        var analyzer = new PlcProgramAnalyzer(store);
        var search = new PlcCommentSearchService(store);
        var reasoner = new PlcReasoner();
        var faultTracer = new PlcFaultTracer(store);
        var manuals = new PlcManualService(new DummyManualStore());
        return new PlcToolset(store, gateway, analyzer, search, reasoner, faultTracer, manuals, NullLogger<PlcToolset>.Instance);
    }

    private static PlcDataStore CreateStore() => new(new TabularProgramParser());

    private sealed class CaptureGateway : IPlcGatewayClient
    {
        public DeviceReadRequest? LastRequest { get; private set; }

        public IReadOnlyList<string>? LastBatchSpecs { get; private set; }

        public Task<DeviceReadResult> ReadAsync(DeviceReadRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(new DeviceReadResult(request.Spec, Array.Empty<int>(), true, null));
        }

        public Task<BatchReadResult> ReadBatchAsync(BatchReadRequest request, CancellationToken cancellationToken = default)
        {
            LastBatchSpecs = request.Specs;
            return Task.FromResult(new BatchReadResult(Array.Empty<DeviceReadResult>(), null));
        }
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
