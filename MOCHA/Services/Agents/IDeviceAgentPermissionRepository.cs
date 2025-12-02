namespace MOCHA.Services.Agents;

/// <summary>
/// 装置エージェント利用許可の永続化を抽象化するリポジトリ
/// </summary>
internal interface IDeviceAgentPermissionRepository
{
    /// <summary>
    /// 指定ユーザーに割り付けられた装置エージェント番号取得
    /// </summary>
    /// <param name="userId">ユーザーID</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>割り付けられた番号一覧</returns>
    Task<IReadOnlyList<string>> GetAllowedAgentNumbersAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 指定ユーザーの割り付け置き換え
    /// </summary>
    /// <param name="userId">ユーザーID</param>
    /// <param name="agentNumbers">許可する番号一覧</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    Task ReplaceAsync(string userId, IEnumerable<string> agentNumbers, CancellationToken cancellationToken = default);
}
