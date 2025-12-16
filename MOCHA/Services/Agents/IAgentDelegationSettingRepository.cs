using MOCHA.Models.Agents;

namespace MOCHA.Services.Agents;

/// <summary>サブエージェント設定の永続化を行うリポジトリ</summary>
public interface IAgentDelegationSettingRepository
{
    /// <summary>設定取得</summary>
    Task<AgentDelegationSetting?> GetAsync(string userId, string agentNumber, CancellationToken cancellationToken = default);

    /// <summary>設定の追加または更新</summary>
    Task<AgentDelegationSetting> UpsertAsync(string userId, AgentDelegationSetting setting, CancellationToken cancellationToken = default);
}
