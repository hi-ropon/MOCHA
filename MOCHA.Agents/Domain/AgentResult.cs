namespace MOCHA.Agents.Domain;

/// <summary>
/// エージェントが返す結果
/// </summary>
public sealed record AgentResult(string AgentName, string Content);
