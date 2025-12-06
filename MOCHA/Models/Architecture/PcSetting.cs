using System;
using System.Collections.Generic;
using System.Linq;

namespace MOCHA.Models.Architecture;

/// <summary>
/// PC設定を表す集約
/// </summary>
public sealed class PcSetting
{
    private PcSetting(
        Guid id,
        string userId,
        string agentNumber,
        string os,
        string? role,
        IReadOnlyCollection<string> repositoryUrls,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        UserId = userId;
        AgentNumber = agentNumber;
        Os = os;
        Role = role;
        RepositoryUrls = repositoryUrls;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    /// <summary>設定ID</summary>
    public Guid Id { get; }
    /// <summary>ユーザーID</summary>
    public string UserId { get; }
    /// <summary>エージェント番号</summary>
    public string AgentNumber { get; }
    /// <summary>OS</summary>
    public string Os { get; }
    /// <summary>役割</summary>
    public string? Role { get; }
    /// <summary>リポジトリURL一覧</summary>
    public IReadOnlyCollection<string> RepositoryUrls { get; }
    /// <summary>作成日時</summary>
    public DateTimeOffset CreatedAt { get; }
    /// <summary>更新日時</summary>
    public DateTimeOffset UpdatedAt { get; }

    /// <summary>
    /// 新規設定生成
    /// </summary>
    /// <param name="userId">ユーザーID</param>
    /// <param name="agentNumber">エージェント番号</param>
    /// <param name="draft">入力値</param>
    /// <param name="createdAt">作成日時</param>
    /// <returns>生成した設定</returns>
    public static PcSetting Create(string userId, string agentNumber, PcSettingDraft draft, DateTimeOffset? createdAt = null)
    {
        var timestamp = createdAt ?? DateTimeOffset.UtcNow;
        return new PcSetting(
            Guid.NewGuid(),
            NormalizeRequired(userId),
            NormalizeRequired(agentNumber),
            NormalizeRequired(draft.Os),
            NormalizeNullable(draft.Role),
            NormalizeUrls(draft.RepositoryUrls),
            timestamp,
            timestamp);
    }

    /// <summary>
    /// 永続化情報から復元
    /// </summary>
    /// <param name="id">設定ID</param>
    /// <param name="userId">ユーザーID</param>
    /// <param name="agentNumber">エージェント番号</param>
    /// <param name="os">OS</param>
    /// <param name="role">役割</param>
    /// <param name="repositoryUrls">リポジトリURL一覧</param>
    /// <param name="createdAt">作成日時</param>
    /// <param name="updatedAt">更新日時</param>
    /// <returns>復元した設定</returns>
    public static PcSetting Restore(
        Guid id,
        string userId,
        string agentNumber,
        string os,
        string? role,
        IReadOnlyCollection<string> repositoryUrls,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        return new PcSetting(
            id,
            NormalizeRequired(userId),
            NormalizeRequired(agentNumber),
            NormalizeRequired(os),
            NormalizeNullable(role),
            NormalizeUrls(repositoryUrls),
            createdAt,
            updatedAt);
    }

    /// <summary>
    /// ドラフトから更新
    /// </summary>
    /// <param name="draft">更新内容</param>
    /// <returns>更新後設定</returns>
    public PcSetting Update(PcSettingDraft draft)
    {
        return new PcSetting(
            Id,
            UserId,
            AgentNumber,
            NormalizeRequired(draft.Os),
            NormalizeNullable(draft.Role),
            NormalizeUrls(draft.RepositoryUrls),
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

    private static string? NormalizeNullable(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static IReadOnlyCollection<string> NormalizeUrls(IReadOnlyCollection<string>? urls)
    {
        var normalized = urls ?? Array.Empty<string>();
        var result = new List<string>();

        foreach (var url in normalized)
        {
            var text = url?.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            result.Add(text);
        }

        return result
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
