namespace MOCHA.Agents.Domain;

/// <summary>
/// エージェントのストリーミングイベント。
/// </summary>
public sealed record AgentEvent(
    AgentEventType Type,
    string ConversationId,
    string? Text = null,
    ToolCall? ToolCall = null,
    ToolResult? ToolResult = null,
    double? Progress = null,
    string? Error = null);

/// <summary>
/// AgentEvent の簡易生成ヘルパー。
/// </summary>
public static class AgentEventFactory
{
    public static AgentEvent Message(string conversationId, string text) =>
        new(AgentEventType.Message, conversationId, Text: text);

    public static AgentEvent ToolRequested(string conversationId, ToolCall call) =>
        new(AgentEventType.ToolCallRequested, conversationId, ToolCall: call);

    public static AgentEvent ToolStarted(string conversationId, ToolCall call) =>
        new(AgentEventType.ToolCallStarted, conversationId, ToolCall: call);

    public static AgentEvent ToolCompleted(string conversationId, ToolResult result) =>
        new(AgentEventType.ToolCallCompleted, conversationId, ToolResult: result);

    public static AgentEvent ProgressUpdated(string conversationId, double? progress, string? text = null) =>
        new(AgentEventType.ProgressUpdated, conversationId, Text: text, Progress: progress);

    public static AgentEvent Completed(string conversationId) =>
        new(AgentEventType.Completed, conversationId);

    public static AgentEvent Error(string conversationId, string message) =>
        new(AgentEventType.Error, conversationId, Error: message);
}
