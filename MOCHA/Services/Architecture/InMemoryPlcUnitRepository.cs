using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MOCHA.Models.Architecture;

namespace MOCHA.Services.Architecture;

/// <summary>
/// メモリ上でPLCユニットを保持する開発用リポジトリ
/// </summary>
internal sealed class InMemoryPlcUnitRepository : IPlcUnitRepository
{
    private readonly ConcurrentDictionary<Guid, PlcUnit> _store = new();

    public Task<PlcUnit> AddAsync(PlcUnit unit, CancellationToken cancellationToken = default)
    {
        _store[unit.Id] = unit;
        return Task.FromResult(unit);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_store.TryRemove(id, out _));
    }

    public Task<PlcUnit?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(id, out var unit);
        return Task.FromResult(unit);
    }

    public Task<IReadOnlyList<PlcUnit>> ListAsync(string userId, string agentNumber, CancellationToken cancellationToken = default)
    {
        var result = _store.Values
            .Where(u => u.UserId == userId && u.AgentNumber == agentNumber)
            .OrderBy(u => u.CreatedAt)
            .ToList();

        return Task.FromResult<IReadOnlyList<PlcUnit>>(result);
    }

    public Task<PlcUnit> UpdateAsync(PlcUnit unit, CancellationToken cancellationToken = default)
    {
        _store[unit.Id] = unit;
        return Task.FromResult(unit);
    }
}
