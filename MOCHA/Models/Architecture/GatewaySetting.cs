using System;

namespace MOCHA.Models.Architecture;

/// <summary>
/// ゲートウェイ設定
/// </summary>
public sealed class GatewaySetting
{
    private GatewaySetting(Guid id, string userId, string agentNumber, string host, int port, DateTimeOffset updatedAt)
    {
        Id = id;
        UserId = userId;
        AgentNumber = agentNumber;
        Host = host;
        Port = port;
        UpdatedAt = updatedAt;
    }

    /// <summary>設定ID</summary>
    public Guid Id { get; }
    /// <summary>ユーザーID</summary>
    public string UserId { get; }
    /// <summary>エージェント番号</summary>
    public string AgentNumber { get; }
    /// <summary>ゲートウェイIP</summary>
    public string Host { get; }
    /// <summary>ゲートウェイポート</summary>
    public int Port { get; }
    /// <summary>更新日時</summary>
    public DateTimeOffset UpdatedAt { get; }

    /// <summary>
    /// 新規作成
    /// </summary>
    public static GatewaySetting Create(string userId, string agentNumber, GatewaySettingDraft draft, DateTimeOffset? now = null)
    {
        var timestamp = now ?? DateTimeOffset.UtcNow;
        return new GatewaySetting(
            Guid.NewGuid(),
            Normalize(userId),
            Normalize(agentNumber),
            Normalize(draft.Host),
            draft.Port ?? 0,
            timestamp);
    }

    /// <summary>
    /// 更新
    /// </summary>
    public GatewaySetting Update(GatewaySettingDraft draft)
    {
        return new GatewaySetting(
            Id,
            UserId,
            AgentNumber,
            Normalize(draft.Host),
            draft.Port ?? 0,
            DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// 復元
    /// </summary>
    public static GatewaySetting Restore(Guid id, string userId, string agentNumber, string host, int port, DateTimeOffset updatedAt)
    {
        return new GatewaySetting(id, Normalize(userId), Normalize(agentNumber), Normalize(host), port, updatedAt);
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
