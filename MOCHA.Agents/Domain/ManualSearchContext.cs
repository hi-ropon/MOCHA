namespace MOCHA.Agents.Domain;

/// <summary>
/// マニュアル検索・読取時のコンテキスト
/// </summary>
public sealed record ManualSearchContext(string? UserId, string? AgentNumber, string? Query = null);
