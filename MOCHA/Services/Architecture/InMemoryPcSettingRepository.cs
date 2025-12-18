using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MOCHA.Models.Architecture;

namespace MOCHA.Services.Architecture;

/// <summary>
/// メモリ上でPC設定を保持するリポジトリ
/// </summary>
internal sealed class InMemoryPcSettingRepository : IPcSettingRepository
{
    private readonly ConcurrentDictionary<Guid, PcSetting> _store = new();

    /// <inheritdoc />
    public Task<PcSetting> AddAsync(PcSetting setting, CancellationToken cancellationToken = default)
    {
        _store[setting.Id] = setting;
        return Task.FromResult(setting);
    }

    /// <inheritdoc />
    public Task<PcSetting> UpdateAsync(PcSetting setting, CancellationToken cancellationToken = default)
    {
        _store[setting.Id] = setting;
        return Task.FromResult(setting);
    }

    /// <inheritdoc />
    public Task<PcSetting?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(id, out var setting);
        return Task.FromResult(setting);
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_store.TryRemove(id, out _));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<PcSetting>> ListAsync(string agentNumber, CancellationToken cancellationToken = default)
    {
        var result = _store.Values
            .Where(x => x.AgentNumber == agentNumber)
            .OrderBy(x => x.CreatedAt)
            .ToList();

        return Task.FromResult<IReadOnlyList<PcSetting>>(result);
    }
}
