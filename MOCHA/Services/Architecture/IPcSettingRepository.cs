using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MOCHA.Models.Architecture;

namespace MOCHA.Services.Architecture;

/// <summary>
/// PC設定永続化リポジトリ
/// </summary>
public interface IPcSettingRepository
{
    /// <summary>
    /// 設定追加
    /// </summary>
    /// <param name="setting">設定</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>追加後設定</returns>
    Task<PcSetting> AddAsync(PcSetting setting, CancellationToken cancellationToken = default);

    /// <summary>
    /// 設定更新
    /// </summary>
    /// <param name="setting">設定</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>更新後設定</returns>
    Task<PcSetting> UpdateAsync(PcSetting setting, CancellationToken cancellationToken = default);

    /// <summary>
    /// 設定取得
    /// </summary>
    /// <param name="id">設定ID</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>設定</returns>
    Task<PcSetting?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 設定削除
    /// </summary>
    /// <param name="id">設定ID</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>削除成功なら true</returns>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// エージェントの設定一覧取得
    /// </summary>
    /// <param name="agentNumber">エージェント番号</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>設定一覧</returns>
    Task<IReadOnlyList<PcSetting>> ListAsync(string agentNumber, CancellationToken cancellationToken = default);
}
