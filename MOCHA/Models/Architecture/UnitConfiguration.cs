using System;
using System.Collections.Generic;
using System.Linq;

namespace MOCHA.Models.Architecture;

/// <summary>
/// 装置ユニット構成を表す集約
/// </summary>
public sealed class UnitConfiguration
{
    private UnitConfiguration(
        Guid id,
        string userId,
        string agentNumber,
        string name,
        string? description,
        IReadOnlyCollection<UnitDevice> devices,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        UserId = userId;
        AgentNumber = agentNumber;
        Name = name;
        Description = description;
        Devices = devices;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    /// <summary>ユニットID</summary>
    public Guid Id { get; }
    /// <summary>ユーザーID</summary>
    public string UserId { get; }
    /// <summary>エージェント番号</summary>
    public string AgentNumber { get; }
    /// <summary>ユニット名</summary>
    public string Name { get; }
    /// <summary>ユニット説明</summary>
    public string? Description { get; }
    /// <summary>機器一覧</summary>
    public IReadOnlyCollection<UnitDevice> Devices { get; }
    /// <summary>作成日時</summary>
    public DateTimeOffset CreatedAt { get; }
    /// <summary>更新日時</summary>
    public DateTimeOffset UpdatedAt { get; }

    /// <summary>
    /// 新規ユニット生成
    /// </summary>
    /// <param name="userId">ユーザーID</param>
    /// <param name="agentNumber">エージェント番号</param>
    /// <param name="draft">入力ドラフト</param>
    /// <param name="createdAt">作成日時</param>
    /// <returns>ユニット</returns>
    public static UnitConfiguration Create(string userId, string agentNumber, UnitConfigurationDraft draft, DateTimeOffset? createdAt = null)
    {
        var timestamp = createdAt ?? DateTimeOffset.UtcNow;
        return new UnitConfiguration(
            Guid.NewGuid(),
            NormalizeRequired(userId),
            NormalizeRequired(agentNumber),
            NormalizeRequired(draft.Name),
            NormalizeOptional(draft.Description),
            NormalizeDevices(draft.Devices),
            timestamp,
            timestamp);
    }

    /// <summary>
    /// 永続化情報から復元
    /// </summary>
    /// <param name="id">ユニットID</param>
    /// <param name="userId">ユーザーID</param>
    /// <param name="agentNumber">エージェント番号</param>
    /// <param name="name">ユニット名</param>
    /// <param name="description">ユニット説明</param>
    /// <param name="devices">機器一覧</param>
    /// <param name="createdAt">作成日時</param>
    /// <param name="updatedAt">更新日時</param>
    /// <returns>ユニット</returns>
    public static UnitConfiguration Restore(
        Guid id,
        string userId,
        string agentNumber,
        string name,
        string? description,
        IReadOnlyCollection<UnitDevice> devices,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        return new UnitConfiguration(
            id,
            NormalizeRequired(userId),
            NormalizeRequired(agentNumber),
            NormalizeRequired(name),
            NormalizeOptional(description),
            devices ?? Array.Empty<UnitDevice>(),
            createdAt,
            updatedAt);
    }

    /// <summary>
    /// ドラフトで更新
    /// </summary>
    /// <param name="draft">更新内容</param>
    /// <returns>更新後ユニット</returns>
    public UnitConfiguration Update(UnitConfigurationDraft draft)
    {
        return new UnitConfiguration(
            Id,
            UserId,
            AgentNumber,
            NormalizeRequired(draft.Name),
            NormalizeOptional(draft.Description),
            NormalizeDevices(draft.Devices),
            CreatedAt,
            DateTimeOffset.UtcNow);
    }

    private static string NormalizeRequired(string value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("値が空です", nameof(value));
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static IReadOnlyCollection<UnitDevice> NormalizeDevices(IReadOnlyCollection<UnitDeviceDraft>? drafts)
    {
        var devices = drafts ?? Array.Empty<UnitDeviceDraft>();
        var order = 0;
        return devices.Select(draft => UnitDevice.FromDraft(draft, ++order)).ToList();
    }
}
