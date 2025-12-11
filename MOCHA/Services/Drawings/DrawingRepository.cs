using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MOCHA.Data;
using MOCHA.Models.Drawings;

namespace MOCHA.Services.Drawings;

/// <summary>
/// 図面をDBに永続化するリポジトリ
/// </summary>
internal sealed class DrawingRepository : IDrawingRepository
{
    private readonly IChatDbContext _dbContext;

    /// <summary>
    /// DbContext を受け取って初期化する
    /// </summary>
    /// <param name="dbContext">チャット用 DbContext</param>
    public DrawingRepository(IChatDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<DrawingDocument> AddAsync(DrawingDocument document, CancellationToken cancellationToken = default)
    {
        await EnsureTableIfMissingAsync(cancellationToken);
        var entity = ToEntity(document);
        await _dbContext.Drawings.AddAsync(entity, cancellationToken);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ToModel(entity);
        }
        catch (Exception ex) when (DatabaseErrorDetector.IsMissingTable(ex, "Drawings"))
        {
            await EnsureTableIfMissingAsync(cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ToModel(entity);
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureTableIfMissingAsync(cancellationToken);
        try
        {
            var entity = await _dbContext.Drawings.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (entity is null)
            {
                return false;
            }

            _dbContext.Drawings.Remove(entity);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (Exception ex) when (DatabaseErrorDetector.IsMissingTable(ex, "Drawings"))
        {
            await EnsureTableIfMissingAsync(cancellationToken);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<DrawingDocument?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureTableIfMissingAsync(cancellationToken);
        try
        {
            var entity = await _dbContext.Drawings.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            return entity is null ? null : ToModel(entity);
        }
        catch (Exception ex) when (DatabaseErrorDetector.IsMissingTable(ex, "Drawings"))
        {
            await EnsureTableIfMissingAsync(cancellationToken);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DrawingDocument>> ListAsync(string userId, string? agentNumber, CancellationToken cancellationToken = default)
    {
        await EnsureTableIfMissingAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Array.Empty<DrawingDocument>();
        }

        var trimmedUserId = userId.Trim();
        var trimmedAgent = string.IsNullOrWhiteSpace(agentNumber) ? null : agentNumber.Trim();

        try
        {
            var query = _dbContext.Drawings
                .Where(x => x.UserId == trimmedUserId);

            if (!string.IsNullOrWhiteSpace(trimmedAgent))
            {
                query = query.Where(x => x.AgentNumber == trimmedAgent);
            }

            var list = await query.ToListAsync(cancellationToken);
            return list
                .OrderBy(x => x.CreatedAt)
                .Select(ToModel)
                .ToList();
        }
        catch (Exception ex) when (DatabaseErrorDetector.IsMissingTable(ex, "Drawings"))
        {
            await EnsureTableIfMissingAsync(cancellationToken);
            return Array.Empty<DrawingDocument>();
        }
    }

    /// <inheritdoc />
    public async Task<DrawingDocument> UpdateAsync(DrawingDocument document, CancellationToken cancellationToken = default)
    {
        await EnsureTableIfMissingAsync(cancellationToken);
        DrawingDocumentEntity? entity = null;
        try
        {
            entity = await _dbContext.Drawings.FirstOrDefaultAsync(x => x.Id == document.Id, cancellationToken);
            if (entity is null)
            {
                entity = ToEntity(document);
                _dbContext.Drawings.Add(entity);
            }
            else
            {
                entity.Description = document.Description;
                entity.ContentType = document.ContentType;
                entity.FileName = document.FileName;
                entity.FileSize = document.FileSize;
                entity.AgentNumber = document.AgentNumber;
                entity.UserId = document.UserId;
                entity.RelativePath = document.RelativePath;
                entity.StorageRoot = document.StorageRoot;
                entity.UpdatedAt = document.UpdatedAt;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return ToModel(entity);
        }
        catch (Exception ex) when (DatabaseErrorDetector.IsMissingTable(ex, "Drawings"))
        {
            await EnsureTableIfMissingAsync(cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ToModel(entity);
        }
    }

    private DrawingDocumentEntity ToEntity(DrawingDocument document)
    {
        return new DrawingDocumentEntity
        {
            Id = document.Id,
            UserId = document.UserId,
            AgentNumber = document.AgentNumber,
            FileName = document.FileName,
            ContentType = document.ContentType,
            FileSize = document.FileSize,
            Description = document.Description,
            RelativePath = document.RelativePath,
            StorageRoot = document.StorageRoot,
            CreatedAt = document.CreatedAt,
            UpdatedAt = document.UpdatedAt
        };
    }

    private DrawingDocument ToModel(DrawingDocumentEntity entity)
    {
        return new DrawingDocument(
            entity.Id,
            entity.UserId,
            entity.AgentNumber,
            entity.FileName,
            entity.ContentType,
            entity.FileSize,
            entity.Description,
            entity.CreatedAt,
            entity.UpdatedAt,
            entity.RelativePath,
            entity.StorageRoot);
    }

    private async Task EnsureTableIfMissingAsync(CancellationToken cancellationToken)
    {
        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        const string createSql = """
            CREATE TABLE IF NOT EXISTS Drawings(
                Id TEXT NOT NULL CONSTRAINT PK_Drawings PRIMARY KEY,
                UserId TEXT NOT NULL,
                AgentNumber TEXT NULL,
                FileName TEXT NOT NULL,
                ContentType TEXT NOT NULL,
                FileSize INTEGER NOT NULL,
                Description TEXT NULL,
                RelativePath TEXT NULL,
                StorageRoot TEXT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_Drawings_UserId_AgentNumber_CreatedAt ON Drawings(UserId, AgentNumber, CreatedAt);
        """;

        await _dbContext.Database.ExecuteSqlRawAsync(createSql, cancellationToken);
    }

}
