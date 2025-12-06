using System.Threading;
using System.Threading.Tasks;
using MOCHA.Agents.Application;

namespace MOCHA.Agents.Infrastructure.Orchestration;

/// <summary>
/// コンテキストを提供しない既定プロバイダー
/// </summary>
public sealed class NullOrganizerContextProvider : IOrganizerContextProvider
{
    /// <inheritdoc />
    public Task<OrganizerContext> BuildAsync(string? userId, string? agentNumber, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(OrganizerContext.Empty);
    }
}
