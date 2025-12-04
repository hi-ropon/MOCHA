using System;
using System.IO;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Models.Drawings;
using MOCHA.Services.Drawings;

namespace MOCHA.Tests;

/// <summary>
/// DrawingStoragePathBuilder のパス構築テスト
/// </summary>
[TestClass]
public class DrawingStoragePathBuilderTests
{
    /// <summary>
    /// エージェントと日付から階層化された相対パスを構築する
    /// </summary>
    [TestMethod]
    public void エージェント番号と日付でパスを構築する()
    {
        var builder = CreateBuilder("C:\\DrawingStorage", new DateTimeOffset(2024, 5, 1, 12, 34, 56, TimeSpan.Zero));

        var path = builder.Build("A-01", "layout.pdf");

        var expectedRelative = Path.Combine("A-01", "2024", "05", "01", "20240501123456000_layout.pdf");
        Assert.AreEqual(expectedRelative, path.RelativePath);
        Assert.AreEqual("C:\\DrawingStorage", path.RootPath);
        Assert.AreEqual(Path.Combine("C:\\DrawingStorage", expectedRelative), path.FullPath);
        Assert.AreEqual(Path.Combine("C:\\DrawingStorage", "A-01", "2024", "05", "01"), path.DirectoryPath);
        Assert.AreEqual("20240501123456000_layout.pdf", path.FileName);
    }

    /// <summary>
    /// 無効文字を置換して安全なパスを返す
    /// </summary>
    [TestMethod]
    public void 無効文字を置換する()
    {
        var builder = CreateBuilder("C:\\DrawingStorage", new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero));

        var path = builder.Build("A:01", "lay?out.pdf");

        StringAssert.Contains(path.RelativePath, "A_01");
        StringAssert.Contains(path.FileName, "lay_out.pdf");
        Assert.IsFalse(path.RelativePath.Contains(":", StringComparison.Ordinal));
        Assert.IsFalse(path.RelativePath.Contains("?", StringComparison.Ordinal));
    }

    private static DrawingStoragePathBuilder CreateBuilder(string rootPath, DateTimeOffset now)
    {
        var options = Options.Create(new DrawingStorageOptions
        {
            RootPath = rootPath
        });
        return new DrawingStoragePathBuilder(options, () => now);
    }
}
