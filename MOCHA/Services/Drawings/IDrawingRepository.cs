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
    Task<IReadOnlyList<DrawingDocument>> ListAsync(string userId, string? agentNumber, CancellationToken cancellationToken = default);
    Task<DrawingDocument?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<DrawingDocument> AddAsync(DrawingDocument document, CancellationToken cancellationToken = default);
    Task<DrawingDocument> UpdateAsync(DrawingDocument document, CancellationToken cancellationToken = default);
}
