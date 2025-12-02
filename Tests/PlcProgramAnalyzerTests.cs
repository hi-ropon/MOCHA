using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Agents.Domain.Plc;
using MOCHA.Agents.Infrastructure.Plc;

namespace MOCHA.Tests;

/// <summary>
/// プログラム解析の基本挙動を検証する
/// </summary>
[TestClass]
public class PlcProgramAnalyzerTests
{
    [TestMethod]
    public void 指定デバイス周辺行を取得できる()
    {
        var store = new PlcDataStore();
        store.SetPrograms(new[]
        {
            new ProgramFile("main", new List<string>
            {
                "0 LD X0",
                "1 AND M10",
                "2 OUT Y0"
            })
        });

        var analyzer = new PlcProgramAnalyzer(store);
        var blocks = analyzer.GetProgramBlocks("M", 10, 1);

        Assert.AreEqual(1, blocks.Count);
        StringAssert.Contains(blocks[0], "LD X0");
        StringAssert.Contains(blocks[0], "OUT Y0");
    }

    [TestMethod]
    public void 関連デバイスを抽出できる()
    {
        var store = new PlcDataStore();
        store.SetPrograms(new[]
        {
            new ProgramFile("main", new List<string>
            {
                "0 LD X0",
                "1 AND M10",
                "2 OUT Y0"
            })
        });

        var analyzer = new PlcProgramAnalyzer(store);
        var related = analyzer.GetRelatedDevices("M", 10);

        CollectionAssert.Contains((System.Collections.ICollection)related, "X0");
        CollectionAssert.Contains((System.Collections.ICollection)related, "Y0");
    }
}
