using System;

namespace MOCHA.Services.Drawings;

/// <summary>
/// 図面を表す永続化エンティティ
/// </summary>
internal sealed class DrawingDocumentEntity
{
    /// <summary>図面ID</summary>
    public Guid Id { get; set; }
    /// <summary>ユーザーID</summary>
    public string UserId { get; set; } = string.Empty;
    /// <summary>装置エージェント番号</summary>
    public string? AgentNumber { get; set; }
    /// <summary>ファイル名</summary>
    public string FileName { get; set; } = string.Empty;
    /// <summary>コンテンツタイプ</summary>
    public string ContentType { get; set; } = string.Empty;
    /// <summary>ファイルサイズ</summary>
    public long FileSize { get; set; }
    /// <summary>説明</summary>
    public string? Description { get; set; }
    /// <summary>保存相対パス</summary>
    public string? RelativePath { get; set; }
    /// <summary>保存ルート</summary>
    public string? StorageRoot { get; set; }
    /// <summary>作成日時</summary>
    public DateTimeOffset CreatedAt { get; set; }
    /// <summary>更新日時</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
