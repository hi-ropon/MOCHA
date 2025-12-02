namespace MOCHA.Agents.Domain;

/// <summary>
/// エージェントからの応答
/// </summary>
public sealed record AgentReply(
    string ConversationId,
    string? Text,
    IReadOnlyList<ToolCall> ToolCalls,
    IReadOnlyList<string> Citations);
