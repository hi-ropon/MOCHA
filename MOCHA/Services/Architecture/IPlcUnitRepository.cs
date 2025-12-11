using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MOCHA.Models.Architecture;

namespace MOCHA.Services.Architecture;

/// <summary>
/// PLCユニットの永続化を抽象化するリポジトリ
/// </summary>
public interface IPlcUnitRepository
{
    /// <summary>
    /// エージェント単位で絞り込んだユニット一覧取得
    /// </summary>
    /// <param name="agentNumber">エージェント番号</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>ユニット一覧</returns>
    Task<IReadOnlyList<PlcUnit>> ListAsync(string agentNumber, CancellationToken cancellationToken = default);
    /// <summary>
    /// ユニット取得
    /// </summary>
    /// <param name="id">ユニットID</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>ユニット</returns>
    Task<PlcUnit?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>
    /// ユニット追加
    /// </summary>
    /// <param name="unit">ユニット</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>追加後ユニット</returns>
    Task<PlcUnit> AddAsync(PlcUnit unit, CancellationToken cancellationToken = default);
    /// <summary>
    /// ユニット更新
    /// </summary>
    /// <param name="unit">ユニット</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>更新後ユニット</returns>
    Task<PlcUnit> UpdateAsync(PlcUnit unit, CancellationToken cancellationToken = default);
    /// <summary>
    /// ユニット削除
    /// </summary>
    /// <param name="id">ユニットID</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>削除成功なら true</returns>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
