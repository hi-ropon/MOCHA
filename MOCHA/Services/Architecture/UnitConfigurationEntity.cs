using System;

namespace MOCHA.Services.Architecture;

/// <summary>
/// 装置ユニット構成永続化エンティティ
/// </summary>
internal sealed class UnitConfigurationEntity
{
    /// <summary>ユニットID</summary>
    public Guid Id { get; set; }
    /// <summary>ユーザーID</summary>
    public string UserId { get; set; } = string.Empty;
    /// <summary>エージェント番号</summary>
    public string AgentNumber { get; set; } = string.Empty;
    /// <summary>ユニット名</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>ユニット説明</summary>
    public string? Description { get; set; }
    /// <summary>機器JSON</summary>
    public string? DevicesJson { get; set; }
    /// <summary>作成日時</summary>
    public DateTimeOffset CreatedAt { get; set; }
    /// <summary>更新日時</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
