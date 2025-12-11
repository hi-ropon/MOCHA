using System;
using System.Threading;
using System.Threading.Tasks;
using MOCHA.Agents.Application;

namespace MOCHA.Agents.Infrastructure.Orchestration;

/// <summary>
/// PLCエージェント用コンテキストを返さないプロバイダー
/// </summary>
public sealed class NullPlcAgentContextProvider : IPlcAgentContextProvider
{
    /// <inheritdoc />
    public Task<PlcAgentContext> BuildAsync(string? userId, string? agentNumber, Guid? plcUnitId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(PlcAgentContext.Empty);
    }
}
