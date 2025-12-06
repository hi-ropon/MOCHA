using System;

namespace MOCHA.Services.Architecture;

/// <summary>
/// PC設定永続化エンティティ
/// </summary>
internal sealed class PcSettingEntity
{
    /// <summary>設定ID</summary>
    public Guid Id { get; set; }
    /// <summary>ユーザーID</summary>
    public string UserId { get; set; } = string.Empty;
    /// <summary>エージェント番号</summary>
    public string AgentNumber { get; set; } = string.Empty;
    /// <summary>OS</summary>
    public string Os { get; set; } = string.Empty;
    /// <summary>役割</summary>
    public string? Role { get; set; }
    /// <summary>リポジトリURL JSON</summary>
    public string? RepositoryUrlsJson { get; set; }
    /// <summary>作成日時</summary>
    public DateTimeOffset CreatedAt { get; set; }
    /// <summary>更新日時</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
