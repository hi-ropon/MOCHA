using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using MOCHA.Models.Architecture;

namespace MOCHA.Services.Architecture;

/// <summary>
/// ゲートウェイ設定のメモリ実装
/// </summary>
internal sealed class InMemoryGatewaySettingRepository : IGatewaySettingRepository
{
    private readonly ConcurrentDictionary<string, GatewaySetting> _store = new(StringComparer.OrdinalIgnoreCase);

    public Task<GatewaySetting?> GetAsync(string agentNumber, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(agentNumber, out var value);
        return Task.FromResult(value);
    }

    public Task<GatewaySetting> UpsertAsync(GatewaySetting setting, CancellationToken cancellationToken = default)
    {
        _store[setting.AgentNumber] = setting;
        return Task.FromResult(setting);
    }
}
