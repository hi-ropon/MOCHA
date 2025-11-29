using MOCHA.Agents.Domain;

namespace MOCHA.Agents.Application;

/// <summary>
/// マニュアル検索・読取のポート。
/// </summary>
public interface IManualStore
{
    Task<IReadOnlyList<ManualHit>> SearchAsync(string agentName, string query, CancellationToken cancellationToken = default);

    Task<ManualContent?> ReadAsync(string agentName, string relativePath, int? maxBytes = null, CancellationToken cancellationToken = default);
}
