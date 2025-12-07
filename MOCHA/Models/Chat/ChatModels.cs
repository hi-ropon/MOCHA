using System;
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
/// チャット添付画像
/// </summary>
/// <param name="Id">添付ID</param>
/// <param name="FileName">ファイル名</param>
/// <param name="ContentType">コンテンツタイプ</param>
/// <param name="Size">バイトサイズ</param>
/// <param name="SmallBase64">小サイズプレビュー(Base64)</param>
/// <param name="MediumBase64">中サイズプレビュー(Base64)</param>
/// <param name="CreatedAt">作成日時</param>
public record ImageAttachment(
    string Id,
    string FileName,
    string ContentType,
    long Size,
    string SmallBase64,
    string MediumBase64,
    DateTimeOffset CreatedAt);

/// <summary>
/// 役割と本文と添付を持つ単一メッセージ
/// </summary>
public record ChatMessage(ChatRole Role, string Content, IReadOnlyList<ImageAttachment>? Attachments = null)
{
    public IReadOnlyList<ImageAttachment> Attachments { get; init; } = Attachments ?? Array.Empty<ImageAttachment>();

    public ChatMessage(ChatRole role, string content) : this(role, content, null)
    {
    }
}

/// <summary>
/// 会話IDと複数のメッセージをまとめたターン
/// </summary>
public record ChatTurn(string? ConversationId, IReadOnlyList<ChatMessage> Messages)
{
    /// <summary>装置エージェント番号</summary>
    public string? AgentNumber { get; init; }

    /// <summary>ユーザーID</summary>
    public string? UserId { get; init; }
}

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
