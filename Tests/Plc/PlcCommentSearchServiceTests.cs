using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Agents.Infrastructure.Plc;

namespace MOCHA.Tests;

/// <summary>
/// コメント検索サービスの挙動を検証する
/// </summary>
[TestClass]
public class PlcCommentSearchServiceTests
{
    [TestMethod]
    public void Search_全角質問と半角コメントを突き合わせる()
    {
        var store = new PlcDataStore(new TabularProgramParser());
        store.SetComments(new Dictionary<string, string>
        {
            ["M10"] = "ﾘﾐｯﾄｽｲｯﾁ異常あり",
            ["D20"] = "ポンプ圧力"
        });

        var sut = new PlcCommentSearchService(store);

        var results = sut.Search("リミットスイッチが効かない", 5);

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("M10", results[0].Device);
    }

    [TestMethod]
    public void Search_デバイス指定を優先する()
    {
        var store = new PlcDataStore(new TabularProgramParser());
        store.SetComments(new Dictionary<string, string>
        {
            ["D100"] = "メインモータ回転数",
            ["D200"] = "予備コメント"
        });

        var sut = new PlcCommentSearchService(store);

        var results = sut.Search("D100 のモータが止まった", 5);

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("D100", results[0].Device);
    }

    [TestMethod]
    public void Search_スコア降順で複数結果を返す()
    {
        var store = new PlcDataStore(new TabularProgramParser());
        store.SetComments(new Dictionary<string, string>
        {
            ["X10"] = "原点リミットエラー",
            ["X20"] = "非常停止回路",
            ["M0"] = "サービス用コメント"
        });

        var sut = new PlcCommentSearchService(store);

        var results = sut.Search("非常停止が入り原点リミットエラーも出る", 2);

        Assert.AreEqual(2, results.Count);
        Assert.AreEqual("X20", results[0].Device);
        Assert.AreEqual("X10", results[1].Device);
    }

    [TestMethod]
    public void Search_あいまい一致で結果を返す()
    {
        var store = new PlcDataStore(new TabularProgramParser());
        store.SetComments(new Dictionary<string, string>
        {
            ["D30"] = "PRESSURE SENSOR FEEDBACK"
        });

        var sut = new PlcCommentSearchService(store);

        var results = sut.Search("presure senser feedback が乱れる", 3);

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("D30", results[0].Device);
    }
}
