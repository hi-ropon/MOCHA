using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MOCHA.Models.Architecture;

namespace MOCHA.Services.Architecture;

/// <summary>
/// メモリ上でPLCユニットを保持する開発用リポジトリ
/// </summary>
internal sealed class InMemoryPlcUnitRepository : IPlcUnitRepository
{
    private readonly ConcurrentDictionary<Guid, PlcUnit> _store = new();

    /// <summary>
    /// ユニット追加
    /// </summary>
    /// <param name="unit">ユニット</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>追加後ユニット</returns>
    public Task<PlcUnit> AddAsync(PlcUnit unit, CancellationToken cancellationToken = default)
    {
        _store[unit.Id] = unit;
        return Task.FromResult(unit);
    }

    /// <summary>
    /// ユニット削除
    /// </summary>
    /// <param name="id">ユニットID</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>削除成功なら true</returns>
    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_store.TryRemove(id, out _));
    }

    /// <summary>
    /// ユニット取得
    /// </summary>
    /// <param name="id">ユニットID</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>ユニット</returns>
    public Task<PlcUnit?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(id, out var unit);
        return Task.FromResult(unit);
    }

    /// <summary>
    /// エージェント単位で絞り込んだユニット一覧取得
    /// </summary>
    /// <param name="agentNumber">エージェント番号</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>ユニット一覧</returns>
    public Task<IReadOnlyList<PlcUnit>> ListAsync(string agentNumber, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(agentNumber))
        {
            return Task.FromResult<IReadOnlyList<PlcUnit>>(Array.Empty<PlcUnit>());
        }

        var normalizedAgent = agentNumber.Trim();
        var result = _store.Values
            .Where(u => string.Equals(u.AgentNumber, normalizedAgent, StringComparison.Ordinal))
            .OrderBy(u => u.CreatedAt)
            .ToList();

        return Task.FromResult<IReadOnlyList<PlcUnit>>(result);
    }

    /// <summary>
    /// ユニット更新
    /// </summary>
    /// <param name="unit">ユニット</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>更新後ユニット</returns>
    public Task<PlcUnit> UpdateAsync(PlcUnit unit, CancellationToken cancellationToken = default)
    {
        _store[unit.Id] = unit;
        return Task.FromResult(unit);
    }
}
