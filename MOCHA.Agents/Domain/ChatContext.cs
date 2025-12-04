using System;

namespace MOCHA.Agents.Domain;

/// <summary>
/// 会話単位のコンテキスト情報
/// </summary>
public sealed record ChatContext(string ConversationId, IReadOnlyList<ChatTurn> History)
{
    /// <summary>装置エージェント番号</summary>
    public string? AgentNumber { get; init; }

    /// <summary>ユーザーID</summary>
    public string? UserId { get; init; }

    /// <summary>
    /// 履歴なしのチャットコンテキスト生成
    /// </summary>
    /// <param name="conversationId">会話ID</param>
    /// <returns>空のコンテキスト</returns>
    public static ChatContext Empty(string? conversationId = null) =>
        new(conversationId ?? Guid.NewGuid().ToString("N"), Array.Empty<ChatTurn>());
}
