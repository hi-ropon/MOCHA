using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Agents.Application;
using MOCHA.Agents.Infrastructure.Manuals;
using MOCHA.Agents.Infrastructure.Options;

namespace MOCHA.Tests;

[TestClass]
public class ManualStoreTests
{
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
}
