using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Models.Drawings;
using MOCHA.Services.Drawings;

namespace MOCHA.Tests;

/// <summary>
/// DrawingCatalog のパス解決と権限チェックのテスト
/// </summary>
[TestClass]
public class DrawingCatalogTests
{
    /// <summary>
    /// 相対ルートと相対パスを結合して存在確認する
    /// </summary>
    [TestMethod]
    public async Task 相対ルートと相対パスでファイルを解決する()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var relative = Path.Combine("A-01", "layout.txt");
            var fullPath = Path.Combine(tempRoot, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            await File.WriteAllTextAsync(fullPath, "hello", CancellationToken.None);

            var document = DrawingDocument.Create(
                userId: "user-1",
                agentNumber: "A-01",
                fileName: "layout.txt",
                contentType: "text/plain",
                fileSize: 5,
                description: "test",
                createdAt: DateTimeOffset.UtcNow,
                relativePath: relative,
                storageRoot: null);

            var catalog = CreateCatalog(tempRoot, new[] { document });

            var result = await catalog.FindAsync("user-1", "A-01", document.Id, CancellationToken.None);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Exists);
            Assert.AreEqual(fullPath, result.FullPath);
            Assert.AreEqual(".txt", result.Extension);
        }
        finally
        {
            try
            {
                Directory.Delete(tempRoot, recursive: true);
            }
            catch
            {
                // ignore
            }
        }
    }

    /// <summary>
    /// エージェントが一致しない場合は null を返す
    /// </summary>
    [TestMethod]
    public async Task エージェント不一致なら見つからない()
    {
        var document = DrawingDocument.Create(
            userId: "user-1",
            agentNumber: "A-01",
            fileName: "layout.txt",
            contentType: "text/plain",
            fileSize: 5,
            description: null,
            createdAt: DateTimeOffset.UtcNow,
            relativePath: "A-01/layout.txt",
            storageRoot: null);

        var catalog = CreateCatalog(Path.Combine(Path.GetTempPath(), "dummy"), new[] { document });

        var result = await catalog.FindAsync("user-1", "B-99", document.Id, CancellationToken.None);

        Assert.IsNull(result);
    }

    /// <summary>
    /// RelativePath が空の場合は Exists を false として返す
    /// </summary>
    [TestMethod]
    public async Task 相対パスなしは存在しない扱いにする()
    {
        var document = DrawingDocument.Create(
            userId: "user-1",
            agentNumber: "A-01",
            fileName: "layout.txt",
            contentType: "text/plain",
            fileSize: 5,
            description: null,
            createdAt: DateTimeOffset.UtcNow,
            relativePath: null,
            storageRoot: null);

        var catalog = CreateCatalog(Path.Combine(Path.GetTempPath(), "dummy"), new[] { document });

        var result = await catalog.FindAsync("user-1", "A-01", document.Id, CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.IsFalse(result.Exists);
        Assert.IsNull(result.FullPath);
    }

    private static DrawingCatalog CreateCatalog(string rootPath, IEnumerable<DrawingDocument> documents)
    {
        var options = Options.Create(new DrawingStorageOptions
        {
            RootPath = rootPath
        });

        var repository = new FakeDrawingRepository(documents);
        return new DrawingCatalog(repository, options);
    }

    private sealed class FakeDrawingRepository : IDrawingRepository
    {
        private readonly Dictionary<Guid, DrawingDocument> _documents = new();

        public FakeDrawingRepository(IEnumerable<DrawingDocument> documents)
        {
            foreach (var document in documents)
            {
                _documents[document.Id] = document;
            }
        }

        public Task<DrawingDocument> AddAsync(DrawingDocument document, CancellationToken cancellationToken = default)
        {
            _documents[document.Id] = document;
            return Task.FromResult(document);
        }

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_documents.Remove(id));
        }

        public Task<DrawingDocument?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        {
            _documents.TryGetValue(id, out var document);
            return Task.FromResult(document);
        }

        public Task<IReadOnlyList<DrawingDocument>> ListAsync(string userId, string? agentNumber, CancellationToken cancellationToken = default)
        {
            var list = new List<DrawingDocument>();
            foreach (var document in _documents.Values)
            {
                list.Add(document);
            }

            return Task.FromResult<IReadOnlyList<DrawingDocument>>(list);
        }

        public Task<DrawingDocument> UpdateAsync(DrawingDocument document, CancellationToken cancellationToken = default)
        {
            _documents[document.Id] = document;
            return Task.FromResult(document);
        }
    }
}
