using System.Collections;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Agents.Infrastructure.Plc;

namespace MOCHA.Tests;

/// <summary>
/// タブ区切りプログラムパーサの挙動を検証する
/// </summary>
[TestClass]
public class TabularProgramParserTests
{
    [TestMethod]
    public void タブ区切りを解析_引用符を除去する()
    {
        var parser = new TabularProgramParser();

        var line = "\"0\"\t\"\"\t\"LD\"\t\"X30\"";
        var parsed = parser.Parse(line);

        CollectionAssert.AreEqual(new[] { "0", string.Empty, "LD", "X30" }, (ICollection)parsed.Columns);
        Assert.AreEqual(line, parsed.Raw);
    }

    [TestMethod]
    public void タブ連続時_空列を保持する()
    {
        var parser = new TabularProgramParser();

        var line = "\"13\"\t\"\"\t\"INV\"\t\"\"\t\"\"\t\"\"";
        var parsed = parser.Parse(line);

        Assert.AreEqual(6, parsed.Columns.Count);
        Assert.AreEqual("INV", parsed.Columns[2]);
        Assert.AreEqual(string.Empty, parsed.Columns[3]);
    }

    [TestMethod]
    public void ダブルクォートを含む列_エスケープを復元する()
    {
        var parser = new TabularProgramParser();

        var line = "\"0\"\t\"\"\t\"MOV\"\t\"\"\"NAME\"\"\"";
        var parsed = parser.Parse(line);

        Assert.AreEqual("\"NAME\"", parsed.Columns[3]);
    }
}
