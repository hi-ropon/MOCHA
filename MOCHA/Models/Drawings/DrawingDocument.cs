using System;

namespace MOCHA.Models.Drawings;

/// <summary>
/// 装置図面のメタデータを保持するドメインモデル
/// </summary>
public sealed class DrawingDocument
{
    public DrawingDocument(
        Guid id,
        string userId,
        string? agentNumber,
        string fileName,
        string contentType,
        long fileSize,
        string? description,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
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
    }

    public Guid Id { get; }
    public string UserId { get; }
    public string? AgentNumber { get; }
    public string FileName { get; }
    public string ContentType { get; }
    public long FileSize { get; }
    public string? Description { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; }

    public static DrawingDocument Create(
        string userId,
        string? agentNumber,
        string fileName,
        string contentType,
        long fileSize,
        string? description,
        DateTimeOffset? createdAt = null)
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
            now);
    }

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
            updatedAt ?? DateTimeOffset.UtcNow);
    }
}
