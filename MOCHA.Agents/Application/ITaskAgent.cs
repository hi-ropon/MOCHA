using MOCHA.Agents.Domain;

namespace MOCHA.Agents.Application;

/// <summary>
/// 個別エージェントの共通インターフェース。
/// </summary>
public interface ITaskAgent
{
    string Name { get; }
    string Description { get; }

    Task<AgentResult> ExecuteAsync(string question, CancellationToken cancellationToken = default);
}
