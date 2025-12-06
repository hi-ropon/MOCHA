using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Agents.Application;
using MOCHA.Models.Architecture;
using MOCHA.Models.Drawings;
using MOCHA.Services.Agents;
using MOCHA.Services.Architecture;
using MOCHA.Services.Drawings;

namespace MOCHA.Tests;

/// <summary>
/// OrganizerContextProvider のコンテキスト組み立てを検証するテスト
/// </summary>
[TestClass]
public class OrganizerContextProviderTests
{
    /// <summary>
    /// PC設定がある場合にアーキテクチャに含める確認
    /// </summary>
    [TestMethod]
    public async Task BuildAsync_PC設定がある場合_PC情報を含む()
    {
        var pcRepo = new InMemoryPcSettingRepository();
        var plcRepo = new InMemoryPlcUnitRepository();
        var drawingCatalog = new DrawingCatalog(new FakeDrawingRepository(), Options.Create(new DrawingStorageOptions()));
        var provider = new OrganizerContextProvider(pcRepo, plcRepo, drawingCatalog, NullLogger<OrganizerContextProvider>.Instance);

        var pcSetting = PcSetting.Create(
            "user-1",
            "A-01",
            new PcSettingDraft
            {
                Os = "Windows 11",
                Role = "HMI",
                RepositoryUrls = new List<string> { "https://example.com/repo.git" }
            },
            DateTimeOffset.UtcNow);
        await pcRepo.AddAsync(pcSetting);

        var context = await provider.BuildAsync("user-1", "A-01", CancellationToken.None);

        StringAssert.Contains(context.Architecture, "PC: Windows 11");
        StringAssert.Contains(context.Architecture, "repos:https://example.com/repo.git");
    }

    private sealed class FakeDrawingRepository : IDrawingRepository
    {
        public Task<DrawingDocument> AddAsync(DrawingDocument document, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(document);
        }

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public Task<DrawingDocument?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<DrawingDocument?>(null);
        }

        public Task<IReadOnlyList<DrawingDocument>> ListAsync(string userId, string? agentNumber, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<DrawingDocument>>(new List<DrawingDocument>());
        }

        public Task<DrawingDocument> UpdateAsync(DrawingDocument document, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(document);
        }

        public Task<DrawingDocument?> UpdateDescriptionAsync(Guid id, string description, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<DrawingDocument?>(null);
        }
    }
}
