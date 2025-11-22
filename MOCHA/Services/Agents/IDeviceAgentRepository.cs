using MOCHA.Models.Agents;

namespace MOCHA.Services.Agents;

/// <summary>
/// 装置エージェント情報の永続化を抽象化するインターフェース。
/// </summary>
public interface IDeviceAgentRepository
{
    /// <summary>
    /// 指定ユーザーの装置エージェント一覧を取得する。
    /// </summary>
    /// <param name="userId">ユーザーID。</param>
    /// <param name="cancellationToken">キャンセル通知。</param>
    /// <returns>エージェント一覧。</returns>
    Task<IReadOnlyList<DeviceAgentProfile>> GetAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// エージェントを追加または更新する。
    /// </summary>
    /// <param name="userId">ユーザーID。</param>
    /// <param name="number">エージェント番号。</param>
    /// <param name="name">エージェント名。</param>
    /// <param name="cancellationToken">キャンセル通知。</param>
    /// <returns>保存したエージェント。</returns>
    Task<DeviceAgentProfile> UpsertAsync(string userId, string number, string name, CancellationToken cancellationToken = default);
}
