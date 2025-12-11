using System.Collections.Generic;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Agents.Domain.Plc;
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

    [TestMethod]
    public void 複数推定_プログラム内容からデバイスを抽出する()
    {
        var reasoner = new PlcReasoner();
        var program = new ProgramContext("ProgPou.csv", ParseLines(
            "\"0\"\t\"\"\t\"LD\"\t\"X30\"",
            "\"1\"\t\"\"\t\"AND\"\t\"M180\"",
            "\"2\"\t\"\"\t\"OUT\"\t\"Y1000\""));

        var json = reasoner.InferMultiple("ProgPou.csv を確認して", new[] { program });

        using var doc = JsonDocument.Parse(json);
        var devices = ExtractDevices(doc);
        CollectionAssert.Contains(devices, "X30");
        CollectionAssert.Contains(devices, "M180");
        CollectionAssert.Contains(devices, "Y1000");
    }

    [TestMethod]
    public void 複数推定_質問中のデバイスを優先する()
    {
        var reasoner = new PlcReasoner();
        var program = new ProgramContext("ProgPou.csv", ParseLines("\"0\"\t\"\"\t\"LD\"\t\"X30\""));

        var json = reasoner.InferMultiple("D10 を確認しながら ProgPou.csv も参照", new[] { program });

        using var doc = JsonDocument.Parse(json);
        var devices = ExtractDevices(doc);
        Assert.AreEqual("D10", devices[0]);
        CollectionAssert.Contains(devices, "X30");
    }

    [TestMethod]
    public void 複数推定_デバイス表記が無い場合は候補なしを返す()
    {
        var reasoner = new PlcReasoner();

        var json = reasoner.InferMultiple("ME を確認したい");

        using var doc = JsonDocument.Parse(json);
        var message = doc.RootElement.GetProperty("message").GetString();
        Assert.AreEqual("候補なし", message);
    }

    private static List<string> ExtractDevices(JsonDocument doc)
    {
        var list = new List<string>();
        foreach (var element in doc.RootElement.GetProperty("devices").EnumerateArray())
        {
            var device = element.GetProperty("device").GetString();
            if (!string.IsNullOrEmpty(device))
            {
                list.Add(device);
            }
        }

        return list;
    }

    private static IReadOnlyList<ProgramLine> ParseLines(params string[] lines)
    {
        var parser = new TabularProgramParser();
        var list = new List<ProgramLine>();
        foreach (var line in lines)
        {
            list.Add(parser.Parse(line));
        }

        return list;
    }
}
