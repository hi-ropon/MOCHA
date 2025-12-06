using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Agents.Application;
using MOCHA.Agents.Infrastructure.Agents;

namespace MOCHA.Tests;

/// <summary>
/// AgentCatalog のエージェント登録と検索を検証するテスト
/// </summary>
[TestClass]
public class AgentCatalogTests
{
    private readonly IAgentCatalog _catalog = new AgentCatalog(new ITaskAgent[]
    {
        new PlcTaskAgent(),
        new IaiTaskAgent(),
        new OrientalTaskAgent()
    });

    /// <summary>
    /// カタログから全エージェントを取得できる確認
    /// </summary>
    [TestMethod]
    public async Task カタログ_全エージェントが取得できる()
    {
        var list = _catalog.List();
        Assert.AreEqual(3, list.Count);
        Assert.IsNotNull(_catalog.Find("plcAgent"));
        Assert.IsNotNull(_catalog.Find("iaiAgent"));
        Assert.IsNotNull(_catalog.Find("orientalAgent"));
    }

    /// <summary>
    /// 各エージェントがテンプレート応答を返す確認
    /// </summary>
    [TestMethod]
    public async Task エージェント_質問にテンプレート応答する()
    {
        var plc = _catalog.Find("plcAgent")!;
        var iai = _catalog.Find("iaiAgent")!;
        var oriental = _catalog.Find("orientalAgent")!;

        var plcResult = await plc.ExecuteAsync("デバイス値を読んで");
        var iaiResult = await iai.ExecuteAsync("IAIの設定は？");
        var orientalResult = await oriental.ExecuteAsync("AZシリーズの注意点は？");

        Assert.IsTrue(plcResult.Content.Contains("PLC Agent"));
        Assert.IsTrue(iaiResult.Content.Contains("IAI Agent"));
        Assert.IsTrue(orientalResult.Content.Contains("Oriental Agent"));
    }
}
