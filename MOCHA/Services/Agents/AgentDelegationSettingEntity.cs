using System;

namespace MOCHA.Services.Agents;

/// <summary>サブエージェント設定の永続化エンティティ</summary>
internal sealed class AgentDelegationSettingEntity
{
    /// <summary>主キー</summary>
    public int Id { get; set; }

    /// <summary>ユーザーID</summary>
    public string UserObjectId { get; set; } = default!;

    /// <summary>装置エージェント番号</summary>
    public string AgentNumber { get; set; } = string.Empty;

    /// <summary>許可サブエージェント(JSON)</summary>
    public string AllowedSubAgentsJson { get; set; } = "[]";

    /// <summary>更新日時</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
