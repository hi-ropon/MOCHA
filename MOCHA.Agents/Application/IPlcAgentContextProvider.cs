using System;
using System.Threading;
using System.Threading.Tasks;

namespace MOCHA.Agents.Application;

/// <summary>
/// PLCエージェント用接続コンテキストの組み立て
/// </summary>
public interface IPlcAgentContextProvider
{
    /// <summary>
    /// 接続コンテキスト生成
    /// </summary>
    /// <param name="userId">ユーザーID</param>
    /// <param name="agentNumber">エージェント番号</param>
    /// <param name="plcUnitId">対象ユニットID</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>接続コンテキスト</returns>
    Task<PlcAgentContext> BuildAsync(string? userId, string? agentNumber, Guid? plcUnitId, CancellationToken cancellationToken = default);
}
