using MOCHA.Models.Agents;

namespace MOCHA.Services.Agents;

/// <summary>
/// 装置エージェント情報の永続化を抽象化するインターフェース。
/// </summary>
internal interface IDeviceAgentRepository
{
    /// <summary>
    /// 指定ユーザーの装置エージェント一覧を取得する
    /// </summary>
    /// <param name="userId">ユーザーID。</param>
    /// <param name="cancellationToken">キャンセル通知。</param>
    /// <returns>エージェント一覧。</returns>
    Task<IReadOnlyList<DeviceAgentProfile>> GetAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 全ユーザーが登録した装置エージェント一覧を取得する
    /// </summary>
    /// <param name="cancellationToken">キャンセル通知。</param>
    /// <returns>全エージェント一覧。</returns>
    Task<IReadOnlyList<DeviceAgentProfile>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 指定番号の装置エージェントをまとめて取得する
    /// </summary>
    /// <param name="agentNumbers">取得対象番号。</param>
    /// <param name="cancellationToken">キャンセル通知。</param>
    /// <returns>該当エージェント一覧。</returns>
    Task<IReadOnlyList<DeviceAgentProfile>> GetByNumbersAsync(IEnumerable<string> agentNumbers, CancellationToken cancellationToken = default);

    /// <summary>
    /// エージェントを追加または更新する。
    /// </summary>
    /// <param name="userId">ユーザーID。</param>
    /// <param name="number">エージェント番号。</param>
    /// <param name="name">エージェント名。</param>
    /// <param name="cancellationToken">キャンセル通知。</param>
    /// <returns>保存したエージェント。</returns>
    Task<DeviceAgentProfile> UpsertAsync(string userId, string number, string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// 指定したユーザーのエージェントを削除する。存在しない場合は何もしない。
    /// </summary>
    /// <param name="userId">ユーザーID。</param>
    /// <param name="number">エージェント番号。</param>
    /// <param name="cancellationToken">キャンセル通知。</param>
    Task DeleteAsync(string userId, string number, CancellationToken cancellationToken = default);
}
