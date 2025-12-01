namespace MOCHA.Agents.Domain;

/// <summary>
/// エージェントのストリーミングイベント
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
/// AgentEvent 簡易生成ヘルパー
/// </summary>
public static class AgentEventFactory
{
    /// <summary>
    /// メッセージイベント生成
    /// </summary>
    /// <param name="conversationId">会話ID</param>
    /// <param name="text">メッセージ本文</param>
    /// <returns>生成したイベント</returns>
    public static AgentEvent Message(string conversationId, string text) =>
        new(AgentEventType.Message, conversationId, Text: text);

    /// <summary>
    /// ツール要求イベント生成
    /// </summary>
    /// <param name="conversationId">会話ID</param>
    /// <param name="call">ツール呼び出し</param>
    /// <returns>生成したイベント</returns>
    public static AgentEvent ToolRequested(string conversationId, ToolCall call) =>
        new(AgentEventType.ToolCallRequested, conversationId, ToolCall: call);

    /// <summary>
    /// ツール開始イベント生成
    /// </summary>
    /// <param name="conversationId">会話ID</param>
    /// <param name="call">ツール呼び出し</param>
    /// <returns>生成したイベント</returns>
    public static AgentEvent ToolStarted(string conversationId, ToolCall call) =>
        new(AgentEventType.ToolCallStarted, conversationId, ToolCall: call);

    /// <summary>
    /// ツール完了イベント生成
    /// </summary>
    /// <param name="conversationId">会話ID</param>
    /// <param name="result">ツール結果</param>
    /// <returns>生成したイベント</returns>
    public static AgentEvent ToolCompleted(string conversationId, ToolResult result) =>
        new(AgentEventType.ToolCallCompleted, conversationId, ToolResult: result);

    /// <summary>
    /// 進捗更新イベント生成
    /// </summary>
    /// <param name="conversationId">会話ID</param>
    /// <param name="progress">進捗率</param>
    /// <param name="text">進捗メッセージ</param>
    /// <returns>生成したイベント</returns>
    public static AgentEvent ProgressUpdated(string conversationId, double? progress, string? text = null) =>
        new(AgentEventType.ProgressUpdated, conversationId, Text: text, Progress: progress);

    /// <summary>
    /// 完了イベント生成
    /// </summary>
    /// <param name="conversationId">会話ID</param>
    /// <returns>生成したイベント</returns>
    public static AgentEvent Completed(string conversationId) =>
        new(AgentEventType.Completed, conversationId);

    /// <summary>
    /// エラーイベント生成
    /// </summary>
    /// <param name="conversationId">会話ID</param>
    /// <param name="message">エラーメッセージ</param>
    /// <returns>生成したイベント</returns>
    public static AgentEvent Error(string conversationId, string message) =>
        new(AgentEventType.Error, conversationId, Error: message);
}
