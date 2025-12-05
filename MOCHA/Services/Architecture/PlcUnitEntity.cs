using System;

namespace MOCHA.Services.Architecture;

/// <summary>
/// PLCユニット永続化用エンティティ
/// </summary>
internal sealed class PlcUnitEntity
{
    /// <summary>ユニットID</summary>
    public Guid Id { get; set; }
    /// <summary>ユーザーID</summary>
    public string UserId { get; set; } = string.Empty;
    /// <summary>エージェント番号</summary>
    public string AgentNumber { get; set; } = string.Empty;
    /// <summary>ユニット名</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>メーカー</summary>
    public string? Manufacturer { get; set; }
    /// <summary>機種</summary>
    public string? Model { get; set; }
    /// <summary>役割</summary>
    public string? Role { get; set; }
    /// <summary>IPアドレス</summary>
    public string? IpAddress { get; set; }
    /// <summary>ポート番号</summary>
    public int? Port { get; set; }
    /// <summary>コメントファイルJSON</summary>
    public string? CommentFileJson { get; set; }
    /// <summary>プログラムファイルJSON</summary>
    public string? ProgramFilesJson { get; set; }
    /// <summary>モジュールJSON</summary>
    public string? ModulesJson { get; set; }
    /// <summary>ファンクションブロックJSON</summary>
    public string? FunctionBlocksJson { get; set; }
    /// <summary>作成日時</summary>
    public DateTimeOffset CreatedAt { get; set; }
    /// <summary>更新日時</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
