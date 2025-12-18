using System.Threading;
using System.Threading.Tasks;
using MOCHA.Models.Architecture;

namespace MOCHA.Services.Architecture;

/// <summary>
/// ゲートウェイ設定リポジトリ
/// </summary>
public interface IGatewaySettingRepository
{
    /// <summary>取得</summary>
    Task<GatewaySetting?> GetAsync(string agentNumber, CancellationToken cancellationToken = default);

    /// <summary>追加・更新</summary>
    Task<GatewaySetting> UpsertAsync(GatewaySetting setting, CancellationToken cancellationToken = default);
}
