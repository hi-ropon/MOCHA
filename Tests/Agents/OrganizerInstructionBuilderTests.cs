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

        var result = await builder.BuildAsync(OrganizerInstructions.Template, null, null);

        StringAssert.Contains(result, "アーキテクチャ設定: 情報なし");
        StringAssert.Contains(result, "図面情報: 情報なし");
        Assert.IsFalse(result.Contains("{{architecture_context}}", StringComparison.Ordinal));
        Assert.IsFalse(result.Contains("{{drawing_context}}", StringComparison.Ordinal));
    }

    /// <summary>
    /// コンテキストを埋め込める
    /// </summary>
    [TestMethod]
    public async Task BuildAsync_コンテキストあり_差し込みできる()
    {
        var provider = new FakeContextProvider(new OrganizerContext("ユニット: A-01", "drawing:1 テスト図面"));
        var builder = new OrganizerInstructionBuilder(provider);

        var result = await builder.BuildAsync(OrganizerInstructions.Template, "user-1", "A-01");

        StringAssert.Contains(result, "ユニット: A-01");
        StringAssert.Contains(result, "drawing:1 テスト図面");
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
