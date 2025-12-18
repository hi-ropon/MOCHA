using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Agents.Domain;
using MOCHA.Agents.Infrastructure.Manuals;
using MOCHA.Agents.Infrastructure.Options;
using MOCHA.Models.Drawings;
using MOCHA.Services.Drawings;
using MOCHA.Services.Manuals;

namespace MOCHA.Tests;

/// <summary>
/// UserDrawingManualStore の図面連携を検証するテスト
/// </summary>
[TestClass]
public class UserDrawingManualStoreTests
{
    /// <summary>
    /// 図面検索でキーワード一致がヒットすることを確認
    /// </summary>
    [TestMethod]
    public async Task 図面検索_キーワード一致でヒットする()
    {
        var repo = new FakeDrawingRepository();
        var drawing = DrawingDocument.Create("user-1", "A-01", "layout.dwg", "application/octet-stream", 1234, "メインレイアウト");
        repo.Seed(drawing);

        var store = CreateStore(repo);
        var context = new ManualSearchContext("user-1", "A-01");

        var hits = await store.SearchAsync("iaiAgent", "layout", context);
        var drawingHit = hits.FirstOrDefault(hit => hit.RelativePath.StartsWith("drawing:", StringComparison.OrdinalIgnoreCase));

        Assert.IsNotNull(drawingHit);
        StringAssert.StartsWith(drawingHit!.RelativePath, "drawing:");
    }

    /// <summary>
    /// 図面読み取りでメタ情報を返すことを確認
    /// </summary>
    [TestMethod]
    public async Task 図面読取_メタ情報を返す()
    {
        var repo = new FakeDrawingRepository();
        var drawing = DrawingDocument.Create("user-1", "A-01", "layout.dwg", "application/octet-stream", 2048, "レイアウト図面");
        repo.Seed(drawing);

        var store = CreateStore(repo);
        var context = new ManualSearchContext("user-1", "A-01");
        var relativePath = $"drawing:{drawing.Id}";

        var content = await store.ReadAsync("iaiAgent", relativePath, context: context);

        Assert.IsNotNull(content);
        StringAssert.Contains(content.Content, "layout.dwg");
        StringAssert.Contains(content.Content, "2048");
    }

    /// <summary>
    /// ファジー一致で図面がヒットする
    /// </summary>
    [TestMethod]
    public async Task 図面検索_ファジー一致でヒットする()
    {
        var repo = new FakeDrawingRepository();
        var drawing = DrawingDocument.Create("user-1", "A-01", "20251204140357219_RAGの落とし穴.pdf", "application/pdf", 2559290, "RAGの落とし穴");
        repo.Seed(drawing);

        var store = CreateStore(repo);
        var context = new ManualSearchContext("user-1", "A-01");

        var hits = await store.SearchAsync("drawingAgent", "図面登録したRAGの落とし穴.pdf", context);

        Assert.AreEqual(1, hits.Count);
        StringAssert.StartsWith(hits[0].RelativePath, "drawing:");
    }

    private static UserDrawingManualStore CreateStore(IDrawingRepository repository)
    {
        var options = Options.Create(new ManualStoreOptions
        {
            BasePath = "../../../../MOCHA.Agents/Resources"
        });

        var services = new ServiceCollection();
        services.AddSingleton(repository);
        services.AddSingleton(Options.Create(new DrawingStorageOptions()));
        services.AddSingleton<DrawingCatalog>();
        services.AddSingleton<DrawingContentReader>();

        var provider = services.BuildServiceProvider();

        return new UserDrawingManualStore(
            options,
            NullLogger<UserDrawingManualStore>.Instance,
            NullLogger<FileManualStore>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>());
    }

    private sealed class FakeDrawingRepository : IDrawingRepository
    {
        private readonly Dictionary<Guid, DrawingDocument> _store = new();

        public Task<DrawingDocument> AddAsync(DrawingDocument document, CancellationToken cancellationToken = default)
        {
            _store[document.Id] = document;
            return Task.FromResult(document);
        }

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_store.Remove(id));
        }

        public Task<DrawingDocument?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        {
            _store.TryGetValue(id, out var doc);
            return Task.FromResult(doc);
        }

        public Task<IReadOnlyList<DrawingDocument>> ListAsync(string? agentNumber, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<DrawingDocument> list = new List<DrawingDocument>(_store.Values
                .Where(d => agentNumber is null || d.AgentNumber == agentNumber));
            return Task.FromResult(list);
        }

        public Task<DrawingDocument> UpdateAsync(DrawingDocument document, CancellationToken cancellationToken = default)
        {
            _store[document.Id] = document;
            return Task.FromResult(document);
        }

        public void Seed(DrawingDocument doc)
        {
            _store[doc.Id] = doc;
        }
    }
}
