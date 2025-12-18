using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MOCHA.Models.Drawings;

namespace MOCHA.Services.Drawings;

/// <summary>
/// メモリ上で図面を保持する開発用リポジトリ
/// </summary>
internal sealed class InMemoryDrawingRepository : IDrawingRepository
{
    private readonly ConcurrentDictionary<Guid, DrawingDocument> _store = new();

    /// <summary>
    /// 図面追加
    /// </summary>
    /// <param name="document">追加図面</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>追加後図面</returns>
    public Task<DrawingDocument> AddAsync(DrawingDocument document, CancellationToken cancellationToken = default)
    {
        _store[document.Id] = document;
        return Task.FromResult(document);
    }

    /// <summary>
    /// 図面取得
    /// </summary>
    /// <param name="id">図面ID</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>取得した図面</returns>
    public Task<DrawingDocument?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(id, out var document);
        return Task.FromResult(document);
    }

    /// <summary>
    /// 図面一覧取得
    /// </summary>
    /// <param name="userId">ユーザーID</param>
    /// <param name="agentNumber">エージェント番号</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>図面一覧</returns>
    public Task<IReadOnlyList<DrawingDocument>> ListAsync(string? agentNumber, CancellationToken cancellationToken = default)
    {
        var documents = _store.Values
            .Where(x => x.AgentNumber == agentNumber)
            .OrderByDescending(x => x.UpdatedAt)
            .ToList();

        return Task.FromResult<IReadOnlyList<DrawingDocument>>(documents);
    }

    /// <summary>
    /// 図面更新
    /// </summary>
    /// <param name="document">更新図面</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>更新後図面</returns>
    public Task<DrawingDocument> UpdateAsync(DrawingDocument document, CancellationToken cancellationToken = default)
    {
        _store[document.Id] = document;
        return Task.FromResult(document);
    }

    /// <summary>
    /// 図面削除
    /// </summary>
    /// <param name="id">図面ID</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>削除成否</returns>
    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_store.TryRemove(id, out _));
    }
}
