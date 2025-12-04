using System;

namespace MOCHA.Models.Drawings;

/// <summary>
/// 装置図面のメタデータを保持するドメインモデル
/// </summary>
public sealed class DrawingDocument
{
    /// <summary>
    /// 図面ドキュメント初期化
    /// </summary>
    /// <param name="id">図面ID</param>
    /// <param name="userId">ユーザーID</param>
    /// <param name="agentNumber">エージェント番号</param>
    /// <param name="fileName">ファイル名</param>
    /// <param name="contentType">コンテンツタイプ</param>
    /// <param name="fileSize">ファイルサイズ</param>
    /// <param name="description">説明</param>
    /// <param name="createdAt">作成日時</param>
    /// <param name="updatedAt">更新日時</param>
    /// <param name="relativePath">ストレージ内の相対パス</param>
    /// <param name="storageRoot">保存ルート</param>
    public DrawingDocument(
        Guid id,
        string userId,
        string? agentNumber,
        string fileName,
        string contentType,
        long fileSize,
        string? description,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        string? relativePath = null,
        string? storageRoot = null)
    {
        Id = id;
        UserId = userId;
        AgentNumber = agentNumber;
        FileName = fileName;
        ContentType = contentType;
        FileSize = fileSize;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        RelativePath = Normalize(relativePath);
        StorageRoot = Normalize(storageRoot);
    }

    /// <summary>図面ID</summary>
    public Guid Id { get; }
    /// <summary>所有ユーザーID</summary>
    public string UserId { get; }
    /// <summary>装置エージェント番号</summary>
    public string? AgentNumber { get; }
    /// <summary>ファイル名</summary>
    public string FileName { get; }
    /// <summary>コンテンツタイプ</summary>
    public string ContentType { get; }
    /// <summary>ファイルサイズ</summary>
    public long FileSize { get; }
    /// <summary>説明</summary>
    public string? Description { get; }
    /// <summary>作成日時</summary>
    public DateTimeOffset CreatedAt { get; }
    /// <summary>更新日時</summary>
    public DateTimeOffset UpdatedAt { get; }
    /// <summary>ストレージ内相対パス</summary>
    public string? RelativePath { get; }
    /// <summary>保存ルート</summary>
    public string? StorageRoot { get; }

    /// <summary>
    /// 図面ドキュメント生成
    /// </summary>
    /// <param name="userId">ユーザーID</param>
    /// <param name="agentNumber">エージェント番号</param>
    /// <param name="fileName">ファイル名</param>
    /// <param name="contentType">コンテンツタイプ</param>
    /// <param name="fileSize">ファイルサイズ</param>
    /// <param name="description">説明</param>
    /// <param name="createdAt">作成日時</param>
    /// <param name="relativePath">ストレージ内相対パス</param>
    /// <param name="storageRoot">保存ルート</param>
    /// <returns>生成した図面</returns>
    public static DrawingDocument Create(
        string userId,
        string? agentNumber,
        string fileName,
        string contentType,
        long fileSize,
        string? description,
        DateTimeOffset? createdAt = null,
        string? relativePath = null,
        string? storageRoot = null)
    {
        var now = createdAt ?? DateTimeOffset.UtcNow;
        return new DrawingDocument(
            Guid.NewGuid(),
            userId,
            agentNumber,
            fileName,
            contentType,
            fileSize,
            description,
            now,
            now,
            relativePath,
            storageRoot);
    }

    /// <summary>
    /// 説明を更新した新しいドキュメントを返す
    /// </summary>
    /// <param name="description">説明</param>
    /// <param name="updatedAt">更新日時</param>
    /// <returns>更新後ドキュメント</returns>
    public DrawingDocument WithDescription(string? description, DateTimeOffset? updatedAt = null)
    {
        return new DrawingDocument(
            Id,
            UserId,
            AgentNumber,
            FileName,
            ContentType,
            FileSize,
            description,
            CreatedAt,
            updatedAt ?? DateTimeOffset.UtcNow,
            RelativePath,
            StorageRoot);
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
