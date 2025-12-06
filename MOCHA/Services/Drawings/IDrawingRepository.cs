using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MOCHA.Models.Drawings;

namespace MOCHA.Services.Drawings;

/// <summary>
/// 図面の永続化を抽象化するリポジトリ
/// </summary>
public interface IDrawingRepository
{
    /// <summary>
    /// 図面一覧取得
    /// </summary>
    /// <param name="userId">ユーザーID</param>
    /// <param name="agentNumber">エージェント番号</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>図面一覧</returns>
    Task<IReadOnlyList<DrawingDocument>> ListAsync(string userId, string? agentNumber, CancellationToken cancellationToken = default);
    /// <summary>
    /// 図面取得
    /// </summary>
    /// <param name="id">図面ID</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>図面</returns>
    Task<DrawingDocument?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>
    /// 図面追加
    /// </summary>
    /// <param name="document">図面ドキュメント</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>保存した図面</returns>
    Task<DrawingDocument> AddAsync(DrawingDocument document, CancellationToken cancellationToken = default);
    /// <summary>
    /// 図面更新
    /// </summary>
    /// <param name="document">図面ドキュメント</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>更新後図面</returns>
    Task<DrawingDocument> UpdateAsync(DrawingDocument document, CancellationToken cancellationToken = default);
    /// <summary>
    /// 図面削除
    /// </summary>
    /// <param name="id">図面ID</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>削除成否</returns>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
