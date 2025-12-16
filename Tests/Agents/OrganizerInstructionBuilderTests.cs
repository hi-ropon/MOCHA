using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Agents.Application;
using MOCHA.Agents.Infrastructure.Orchestration;

namespace MOCHA.Tests;

/// <summary>
/// OrganizerInstructionBuilder の置換動作を検証するテスト
/// </summary>
[TestClass]
public class OrganizerInstructionBuilderTests
{
    /// <summary>
    /// コンテキストなしの場合に情報なしが埋め込まれる
    /// </summary>
    [TestMethod]
    public async Task BuildAsync_コンテキストなし_情報なしを埋め込む()
    {
        var builder = new OrganizerInstructionBuilder(new FakeContextProvider());

        var result = await builder.BuildAsync(OrganizerInstructions.Template, null, null, plcOnline: true);

        StringAssert.Contains(result, "アーキテクチャ設定: 情報なし");
        StringAssert.Contains(result, "図面情報: 情報なし");
        StringAssert.Contains(result, "実機読み取りが許可されている");
        StringAssert.Contains(result, "サブエージェント呼び出しは全て許可");
        Assert.IsFalse(result.Contains("{{architecture_context}}", StringComparison.Ordinal));
        Assert.IsFalse(result.Contains("{{drawing_context}}", StringComparison.Ordinal));
        Assert.IsFalse(result.Contains("{{plc_reading_status}}", StringComparison.Ordinal));
        Assert.IsFalse(result.Contains("{{subagent_policy}}", StringComparison.Ordinal));
    }

    /// <summary>
    /// コンテキストを埋め込める
    /// </summary>
    [TestMethod]
    public async Task BuildAsync_コンテキストあり_差し込みできる()
    {
        var provider = new FakeContextProvider(new OrganizerContext("ユニット: A-01", "drawing:1 テスト図面"));
        var builder = new OrganizerInstructionBuilder(provider);

        var result = await builder.BuildAsync(OrganizerInstructions.Template, "user-1", "A-01", plcOnline: true);

        StringAssert.Contains(result, "ユニット: A-01");
        StringAssert.Contains(result, "drawing:1 テスト図面");
    }

    /// <summary>
    /// PLC読み取り設定が反映される
    /// </summary>
    [TestMethod]
    public async Task BuildAsync_PlcOffline_説明が切り替わる()
    {
        var builder = new OrganizerInstructionBuilder(new FakeContextProvider());

        var result = await builder.BuildAsync(OrganizerInstructions.Template, null, null, plcOnline: false);

        StringAssert.Contains(result, "実機読み取りはユーザー設定で無効");
        StringAssert.DoesNotMatch(result, new System.Text.RegularExpressions.Regex("\\{\\{plc_reading_status\\}\\}"));
    }

    /// <summary>
    /// サブエージェント制限がプロンプトに反映される
    /// </summary>
    [TestMethod]
    public async Task BuildAsync_サブエージェント制限あり_ポリシーを埋め込む()
    {
        var builder = new OrganizerInstructionBuilder(new FakeContextProvider());

        var result = await builder.BuildAsync(
            OrganizerInstructions.Template,
            null,
            null,
            plcOnline: true,
            allowedSubAgents: new[] { "plcAgent", "drawingAgent" });

        StringAssert.Contains(result, "許可: PLCエージェント, 図面エージェント");
        StringAssert.Contains(result, "禁止: IAIエージェント, Orientalエージェント");
    }

    private sealed class FakeContextProvider : IOrganizerContextProvider
    {
        private readonly OrganizerContext _context;

        public FakeContextProvider(OrganizerContext? context = null)
        {
            _context = context ?? OrganizerContext.Empty;
        }

        public Task<OrganizerContext> BuildAsync(string? userId, string? agentNumber, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_context);
        }
    }
}
