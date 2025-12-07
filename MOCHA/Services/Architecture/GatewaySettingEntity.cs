using System;

namespace MOCHA.Services.Architecture;

/// <summary>
/// ゲートウェイ設定エンティティ
/// </summary>
internal sealed class GatewaySettingEntity
{
    /// <summary>設定ID</summary>
    public Guid Id { get; set; }
    /// <summary>ユーザーID</summary>
    public string UserId { get; set; } = string.Empty;
    /// <summary>エージェント番号</summary>
    public string AgentNumber { get; set; } = string.Empty;
    /// <summary>ゲートウェイIP</summary>
    public string Host { get; set; } = string.Empty;
    /// <summary>ゲートウェイポート</summary>
    public int Port { get; set; }
    /// <summary>更新日時</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
