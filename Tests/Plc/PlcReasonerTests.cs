using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Agents.Infrastructure.Plc;

namespace MOCHA.Tests;

/// <summary>
/// デバイス推定ロジックの挙動を検証する
/// </summary>
[TestClass]
public class PlcReasonerTests
{
    [TestMethod]
    public void 単一推定_デバイスを抽出する()
    {
        var reasoner = new PlcReasoner();
        var json = reasoner.InferSingle("D100とM10を確認したい");
        StringAssert.Contains(json, "D100");
    }

    [TestMethod]
    public void 複数推定_複数デバイスを返す()
    {
        var reasoner = new PlcReasoner();
        var json = reasoner.InferMultiple("X0とY1とM10を確認したい");

        StringAssert.Contains(json, "X0");
        StringAssert.Contains(json, "Y1");
        StringAssert.Contains(json, "M10");
    }

    [TestMethod]
    public void 複数推定_Lコイルも抽出する()
    {
        var reasoner = new PlcReasoner();
        var json = reasoner.InferMultiple("異常はL100かL200で出ます");

        StringAssert.Contains(json, "L100");
        StringAssert.Contains(json, "L200");
    }
}
