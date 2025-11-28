using MOCHA.Agents.Domain;

namespace MOCHA.Agents.Application;

/// <summary>
/// ユーザー入力を受け取り、エージェント応答を生成するポート。
/// </summary>
public interface IAgentOrchestrator
{
    Task<IAsyncEnumerable<AgentEvent>> ReplyAsync(ChatTurn userTurn, ChatContext context, CancellationToken cancellationToken = default);
}
