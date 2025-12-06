using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MOCHA.Models.Architecture;

namespace MOCHA.Services.Architecture;

/// <summary>
/// 装置ユニット構成リポジトリ
/// </summary>
public interface IUnitConfigurationRepository
{
    /// <summary>
    /// ユニット追加
    /// </summary>
    /// <param name="unit">ユニット</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>追加後ユニット</returns>
    Task<UnitConfiguration> AddAsync(UnitConfiguration unit, CancellationToken cancellationToken = default);

    /// <summary>
    /// ユニット更新
    /// </summary>
    /// <param name="unit">ユニット</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>更新後ユニット</returns>
    Task<UnitConfiguration> UpdateAsync(UnitConfiguration unit, CancellationToken cancellationToken = default);

    /// <summary>
    /// ユニット取得
    /// </summary>
    /// <param name="id">ユニットID</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>ユニット</returns>
    Task<UnitConfiguration?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// ユニット削除
    /// </summary>
    /// <param name="id">ユニットID</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>削除可否</returns>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// ユニット一覧取得
    /// </summary>
    /// <param name="userId">ユーザーID</param>
    /// <param name="agentNumber">エージェント番号</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>ユニット一覧</returns>
    Task<IReadOnlyList<UnitConfiguration>> ListAsync(string userId, string agentNumber, CancellationToken cancellationToken = default);
}
