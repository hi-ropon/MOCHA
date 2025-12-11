using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Agents.Application;
using MOCHA.Agents.Infrastructure.Manuals;
using MOCHA.Agents.Infrastructure.Options;

namespace MOCHA.Tests;

/// <summary>
/// FileManualStore の検索・読取を検証するテスト
/// </summary>
[TestClass]
public class ManualStoreTests
{
    /// <summary>
    /// IAI インデックスをキーワード検索できる確認
    /// </summary>
    [TestMethod]
    public async Task IAIインデックスをキーワード検索できる()
    {
        var options = Options.Create(new ManualStoreOptions
        {
            BasePath = "../../../../MOCHA.Agents/Resources", // bin/Debug/net10 からリポジトリルート配下を指す
            AgentFolders = new() { ["iaiAgent"] = "IAI" },
            MaxReadBytes = 2000
        });

        var store = new FileManualStore(options, NullLogger<FileManualStore>.Instance);

        var hits = await store.SearchAsync("iaiAgent", "RCON", default);

        Assert.IsTrue(hits.Count > 0);
    }

    /// <summary>
    /// IAI マニュアルを相対パスで読み出せる確認
    /// </summary>
    [TestMethod]
    public async Task IAIマニュアルを相対パスで読み出せる()
    {
        var options = Options.Create(new ManualStoreOptions
        {
            BasePath = "../../../../MOCHA.Agents/Resources",
            AgentFolders = new() { ["iaiAgent"] = "IAI" },
            MaxReadBytes = 500
        });

        var store = new FileManualStore(options, NullLogger<FileManualStore>.Instance);

        var content = await store.ReadAsync("iaiAgent", "01_RCON/index.md", 500, default);

        Assert.IsNotNull(content);
        Assert.IsTrue(content.Content.Length > 0);
    }

    /// <summary>
    /// 半角コメントインデックスを全角クエリで検索できる確認
    /// </summary>
    [TestMethod]
    public async Task 半角コメントインデックスを全角クエリで検索できる()
    {
        var options = Options.Create(new ManualStoreOptions
        {
            BasePath = "../../../TestData/Manuals",
            AgentFolders = new() { ["plcAgent"] = "Plc" }
        });

        var store = new FileManualStore(options, NullLogger<FileManualStore>.Instance);

        var hits = await store.SearchAsync("plcAgent", "インターロック", default);

        Assert.IsTrue(hits.Count > 0);
    }

    /// <summary>
    /// PLC目次検索で対応ページパスが返却される確認
    /// </summary>
    [TestMethod]
    public async Task PLC目次検索_インターロック_ページファイル返却()
    {
        var options = Options.Create(new ManualStoreOptions
        {
            BasePath = "../../../TestData/Manuals",
            AgentFolders = new() { ["plcAgent"] = "Plc" }
        });

        var store = new FileManualStore(options, NullLogger<FileManualStore>.Instance);

        var hits = await store.SearchAsync("plcAgent", "インターロック", default);

        Assert.IsTrue(hits.Any(h => h.RelativePath.EndsWith("page10.txt", StringComparison.OrdinalIgnoreCase)));
    }
}
