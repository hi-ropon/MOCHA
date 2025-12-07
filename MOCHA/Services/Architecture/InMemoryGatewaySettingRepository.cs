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
    private readonly ConcurrentDictionary<(string UserId, string AgentNumber), GatewaySetting> _store = new();

    public Task<GatewaySetting?> GetAsync(string userId, string agentNumber, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue((userId, agentNumber), out var value);
        return Task.FromResult(value);
    }

    public Task<GatewaySetting> UpsertAsync(GatewaySetting setting, CancellationToken cancellationToken = default)
    {
        _store[(setting.UserId, setting.AgentNumber)] = setting;
        return Task.FromResult(setting);
    }
}
