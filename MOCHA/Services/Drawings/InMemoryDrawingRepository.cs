using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MOCHA.Models.Drawings;

namespace MOCHA.Services.Drawings;

/// <summary>
/// メモリ上で図面を保持する開発用リポジトリ
/// </summary>
internal sealed class InMemoryDrawingRepository : IDrawingRepository
{
    private readonly ConcurrentDictionary<Guid, DrawingDocument> _store = new();

    public Task<DrawingDocument> AddAsync(DrawingDocument document, CancellationToken cancellationToken = default)
    {
        _store[document.Id] = document;
        return Task.FromResult(document);
    }

    public Task<DrawingDocument?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(id, out var document);
        return Task.FromResult(document);
    }

    public Task<IReadOnlyList<DrawingDocument>> ListAsync(string userId, string? agentNumber, CancellationToken cancellationToken = default)
    {
        var documents = _store.Values
            .Where(x => x.UserId == userId && x.AgentNumber == agentNumber)
            .OrderByDescending(x => x.UpdatedAt)
            .ToList();

        return Task.FromResult<IReadOnlyList<DrawingDocument>>(documents);
    }

    public Task<DrawingDocument> UpdateAsync(DrawingDocument document, CancellationToken cancellationToken = default)
    {
        _store[document.Id] = document;
        return Task.FromResult(document);
    }
}
