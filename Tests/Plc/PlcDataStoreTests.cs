using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Agents.Infrastructure.Plc;
using MOCHA.Agents.Domain.Plc;

namespace MOCHA.Tests;

/// <summary>
/// PLCデータストアのロード機能を検証する
/// </summary>
[TestClass]
public class PlcDataStoreTests
{
    /// <summary>
    /// コメントCSVを読み込んでデバイスコメントを取得できることを確認
    /// </summary>
    [TestMethod]
    public async Task コメントCSVを読み込む_コメントを取得できる()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "device,comment\nD100,ポンプ起動\nM10,警報", CancellationToken.None);

            var store = new PlcDataStore();
            await store.LoadCommentsAsync(path, CancellationToken.None);

            Assert.IsTrue(store.TryGetComment("D100", out var comment));
            Assert.AreEqual("ポンプ起動", comment);
            Assert.IsTrue(store.TryGetComment("M10", out var comment2));
            Assert.AreEqual("警報", comment2);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// プログラムファイルを読み込み行を保持することを確認
    /// </summary>
    [TestMethod]
    public async Task プログラムを読み込む_行を保持する()
    {
        var programPath = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(programPath, "0\tLD X0\n1\tAND M10\n2\tOUT Y0", CancellationToken.None);

            var store = new PlcDataStore();
            await store.LoadProgramsAsync(new[] { programPath }, CancellationToken.None);

            Assert.IsTrue(store.Programs.TryGetValue(Path.GetFileName(programPath), out var lines));
            Assert.AreEqual(3, lines.Count);
            StringAssert.Contains(lines[1], "AND M10");
        }
        finally
        {
            File.Delete(programPath);
        }
    }

    /// <summary>
    /// ファンクションブロックを設定し取得できることを確認
    /// </summary>
    [TestMethod]
    public void ファンクションブロックを設定_取得できる()
    {
        var store = new PlcDataStore();
        var fb = new FunctionBlockData("Start", "Start", "device,comment\nX0,開始", "line,instruction\n0000,LD X0");
        store.SetFunctionBlocks(new[] { fb });

        Assert.IsTrue(store.TryGetFunctionBlock("Start", out var block));
        Assert.IsNotNull(block);
        Assert.AreEqual("Start", block!.Name);
        Assert.AreEqual(1, store.FunctionBlocks.Count);
    }
}
