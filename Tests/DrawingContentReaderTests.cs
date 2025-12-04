using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Models.Drawings;
using MOCHA.Services.Drawings;

namespace MOCHA.Tests;

/// <summary>
/// DrawingContentReader の読取テスト
/// </summary>
[TestClass]
public class DrawingContentReaderTests
{
    /// <summary>
    /// テキストファイルを指定バイト数で読み取る
    /// </summary>
    [TestMethod]
    public async Task テキストファイルを読み取る()
    {
        var (file, path) = CreateTextFile("sample.txt", "hello world", 20);
        try
        {
            var reader = new DrawingContentReader();

            var result = await reader.ReadAsync(file, maxBytes: 5, CancellationToken.None);

            Assert.IsTrue(result.Succeeded);
            Assert.IsFalse(result.IsPreviewOnly);
            Assert.AreEqual("hello", result.Content);
            Assert.IsTrue(result.IsTruncated);
        }
        finally
        {
            TryDelete(path);
        }
    }

    /// <summary>
    /// 存在しないファイルはエラーを返す
    /// </summary>
    [TestMethod]
    public async Task 存在しないファイルは失敗する()
    {
        var document = DrawingDocument.Create(
            userId: "user",
            agentNumber: "A-01",
            fileName: "missing.txt",
            contentType: "text/plain",
            fileSize: 0,
            description: null,
            createdAt: DateTimeOffset.UtcNow,
            relativePath: null,
            storageRoot: null);

        var file = DrawingFile.Create(document, fullPath: Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.txt"), exists: false);
        var reader = new DrawingContentReader();

        var result = await reader.ReadAsync(file, maxBytes: 10, CancellationToken.None);

        Assert.IsFalse(result.Succeeded);
        StringAssert.Contains(result.Error ?? string.Empty, "見つかりません");
    }

    /// <summary>
    /// 未対応拡張子はプレビュー扱いでメッセージを返す
    /// </summary>
    [TestMethod]
    public async Task 未対応拡張子はプレビュー扱いにする()
    {
        var (file, path) = CreateTextFile("preview.pdf", "binary", 10);
        try
        {
            var reader = new DrawingContentReader();

            var result = await reader.ReadAsync(file, maxBytes: 10, CancellationToken.None);

            Assert.IsTrue(result.Succeeded);
            Assert.IsTrue(result.IsPreviewOnly);
            StringAssert.Contains(result.Content ?? string.Empty, "プレビュー");
        }
        finally
        {
            TryDelete(path);
        }
    }

    private static (DrawingFile File, string Path) CreateTextFile(string fileName, string content, long size)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var fullPath = Path.Combine(tempRoot, fileName);
        File.WriteAllText(fullPath, content);

        var document = DrawingDocument.Create(
            userId: "user",
            agentNumber: "A-01",
            fileName: fileName,
            contentType: "text/plain",
            fileSize: size,
            description: null,
            createdAt: DateTimeOffset.UtcNow,
            relativePath: fileName,
            storageRoot: tempRoot);

        var file = DrawingFile.Create(document, fullPath, exists: true);
        return (file, tempRoot);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch
        {
            // ignore
        }
    }
}
