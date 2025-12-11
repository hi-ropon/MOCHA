using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Agents.Domain.Plc;
using MOCHA.Agents.Infrastructure.Plc;

namespace MOCHA.Tests;

/// <summary>
/// 異常コメント付きLコイルのトレース挙動を検証する
/// </summary>
[TestClass]
public class PlcFaultTracerTests
{
    [TestMethod]
    public void TraceErrorCoils_異常コメント付きLコイルを返す()
    {
        var store = new PlcDataStore(new TabularProgramParser());
        store.SetComments(new Dictionary<string, string>
        {
            ["L100"] = "過負荷異常",
            ["L200"] = "ステータスERR"
        });
        store.SetPrograms(new[]
        {
            new ProgramFile("main", new List<string>
            {
                "\"0\"\t\"\"\t\"OUT\"\t\"L100\"\t\"X0\"\t\"M10\"",
                "\"1\"\t\"\"\t\"AND\"\t\"X1\"",
                "\"2\"\t\"\"\t\"OUT\"\t\"L200\"\t\"X2\""
            })
        });

        var tracer = new PlcFaultTracer(store);

        var json = tracer.TraceErrorCoils();

        StringAssert.Contains(json, "L100");
        StringAssert.Contains(json, "L200");
        StringAssert.Contains(json, "過負荷異常");
        StringAssert.Contains(json, "ステータスERR");
        StringAssert.Contains(json, "X0");
        StringAssert.Contains(json, "X1");
    }

    [TestMethod]
    public void TraceErrorCoils_キーワードを含まないコメントは除外する()
    {
        var store = new PlcDataStore(new TabularProgramParser());
        store.SetComments(new Dictionary<string, string>
        {
            ["L300"] = "正常完了"
        });
        store.SetPrograms(new[]
        {
            new ProgramFile("main", new List<string> { "\"0\"\t\"\"\t\"OUT\"\t\"L300\"\t\"X3\"" })
        });

        var tracer = new PlcFaultTracer(store);

        var json = tracer.TraceErrorCoils();

        StringAssert.Contains(json, "not_found");
        Assert.IsFalse(json.Contains("L300"));
    }
}
