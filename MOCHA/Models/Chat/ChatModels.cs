using System.Collections.Generic;

namespace MOCHA.Models.Chat;

/// <summary>
/// チャットで扱う発話の役割
/// </summary>
public enum ChatRole
{
    User,
    Assistant,
    System,
    Tool
}

/// <summary>
/// 役割と本文を持つ単一メッセージ
/// </summary>
public record ChatMessage(ChatRole Role, string Content);

/// <summary>
/// 会話IDと複数のメッセージをまとめたターン
/// </summary>
public record ChatTurn(string? ConversationId, IReadOnlyList<ChatMessage> Messages);

/// <summary>
/// Agent からのアクション要求
/// </summary>
/// <param name="ActionName">アクション名</param>
/// <param name="ConversationId">対象の会話ID</param>
/// <param name="Payload">アクションに付随するペイロード</param>
public record AgentActionRequest(
    string ActionName,
    string ConversationId,
    IReadOnlyDictionary<string, object?> Payload
);

/// <summary>
/// Agent へのアクション実行結果
/// </summary>
/// <param name="ActionName">アクション名</param>
/// <param name="ConversationId">対象の会話ID</param>
/// <param name="Success">成功フラグ</param>
/// <param name="Payload">返却するペイロード</param>
/// <param name="Error">エラー内容（失敗時）</param>
public record AgentActionResult(
    string ActionName,
    string ConversationId,
    bool Success,
    IReadOnlyDictionary<string, object?> Payload,
    string? Error = null
);

/// <summary>
/// ストリームで流れるイベント種別
/// </summary>
public enum ChatStreamEventType
{
    Message,
    ActionRequest,
    ToolResult,
    Completed,
    Error
}

/// <summary>
/// Agent とのやり取りで使用するストリームイベント
/// </summary>
/// <param name="Type">イベント種別</param>
/// <param name="Message">チャットメッセージ</param>
/// <param name="ActionRequest">ツール実行要求</param>
/// <param name="ActionResult">ツール実行結果</param>
/// <param name="Error">エラー内容</param>
public record ChatStreamEvent(
    ChatStreamEventType Type,
    ChatMessage? Message = null,
    AgentActionRequest? ActionRequest = null,
    AgentActionResult? ActionResult = null,
    string? Error = null
)
{
    /// <summary>
    /// メッセージイベント生成
    /// </summary>
    /// <param name="message">発話内容</param>
    /// <returns>メッセージイベント</returns>
    public static ChatStreamEvent FromMessage(ChatMessage message) =>
        new(ChatStreamEventType.Message, Message: message);

    /// <summary>
    /// 完了イベント生成
    /// </summary>
    /// <param name="conversationId">対象会話ID</param>
    /// <returns>完了イベント</returns>
    public static ChatStreamEvent Completed(string? conversationId = null) =>
        new(ChatStreamEventType.Completed);

    /// <summary>
    /// エラーイベント生成
    /// </summary>
    /// <param name="error">エラーメッセージ</param>
    /// <returns>エラーイベント</returns>
    public static ChatStreamEvent Fail(string error) =>
        new(ChatStreamEventType.Error, Error: error);
}

/// <summary>
/// ユーザーのIDと表示名をまとめた情報
/// </summary>
public record UserContext(string UserId, string DisplayName);
