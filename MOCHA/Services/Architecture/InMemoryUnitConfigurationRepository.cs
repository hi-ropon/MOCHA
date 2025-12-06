using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MOCHA.Models.Architecture;

namespace MOCHA.Services.Architecture;

/// <summary>
/// メモリ保持のユニット構成リポジトリ
/// </summary>
internal sealed class InMemoryUnitConfigurationRepository : IUnitConfigurationRepository
{
    private readonly ConcurrentDictionary<Guid, UnitConfiguration> _store = new();

    /// <inheritdoc />
    public Task<UnitConfiguration> AddAsync(UnitConfiguration unit, CancellationToken cancellationToken = default)
    {
        _store[unit.Id] = unit;
        return Task.FromResult(unit);
    }

    /// <inheritdoc />
    public Task<UnitConfiguration> UpdateAsync(UnitConfiguration unit, CancellationToken cancellationToken = default)
    {
        _store[unit.Id] = unit;
        return Task.FromResult(unit);
    }

    /// <inheritdoc />
    public Task<UnitConfiguration?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(id, out var unit);
        return Task.FromResult(unit);
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_store.TryRemove(id, out _));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<UnitConfiguration>> ListAsync(string userId, string agentNumber, CancellationToken cancellationToken = default)
    {
        var list = _store.Values
            .Where(x => x.UserId == userId && x.AgentNumber == agentNumber)
            .OrderBy(x => x.CreatedAt)
            .ToList();

        return Task.FromResult<IReadOnlyList<UnitConfiguration>>(list);
    }
}
