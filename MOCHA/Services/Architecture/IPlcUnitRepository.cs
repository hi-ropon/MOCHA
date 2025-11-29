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
    Task<IReadOnlyList<PlcUnit>> ListAsync(string userId, string agentNumber, CancellationToken cancellationToken = default);
    Task<PlcUnit?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PlcUnit> AddAsync(PlcUnit unit, CancellationToken cancellationToken = default);
    Task<PlcUnit> UpdateAsync(PlcUnit unit, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
