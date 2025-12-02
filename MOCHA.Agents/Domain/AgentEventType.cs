namespace MOCHA.Agents.Domain;

/// <summary>
/// エージェント応答ストリームで流れるイベント種別
/// </summary>
public enum AgentEventType
{
    Message,
    ToolCallRequested,
    ToolCallStarted,
    ToolCallCompleted,
    ProgressUpdated,
    Completed,
    Error
}
