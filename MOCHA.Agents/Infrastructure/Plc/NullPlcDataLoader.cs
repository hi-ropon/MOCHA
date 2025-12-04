using System.Threading;
using System.Threading.Tasks;
using MOCHA.Agents.Application;

namespace MOCHA.Agents.Infrastructure.Plc;

/// <summary>
/// 何も読み込まない PLC データローダー
/// </summary>
public sealed class NullPlcDataLoader : IPlcDataLoader
{
    /// <inheritdoc />
    public Task LoadAsync(string? userId, string? agentNumber, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
