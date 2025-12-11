using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using MOCHA.Data;
using MOCHA.Models.Agents;

namespace MOCHA.Services.Agents;

/// <summary>
/// 装置エージェントの永続化を行うリポジトリ
/// </summary>
internal sealed class DeviceAgentRepository : IDeviceAgentRepository
{
    private readonly IDbContextFactory<ChatDbContext> _dbContextFactory;

    /// <summary>
    /// DbContext ファクトリを受け取りリポジトリを初期化する
    /// </summary>
    /// <param name="dbContextFactory">チャット用 DbContext ファクトリ</param>
    public DeviceAgentRepository(IDbContextFactory<ChatDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    private async Task<ChatDbContext> CreateDbContextAsync(CancellationToken cancellationToken)
    {
        return await _dbContextFactory.CreateDbContextAsync(cancellationToken);
    }

    /// <summary>
    /// 指定ユーザーの装置エージェント一覧取得（テーブルが無ければ自動作成）
    /// </summary>
    /// <param name="userId">ユーザーID</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>エージェント一覧</returns>
    public async Task<IReadOnlyList<DeviceAgentProfile>> GetAsync(string userId, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = await CreateDbContextAsync(cancellationToken);
            var list = await db.DeviceAgents
                .Where(x => x.UserObjectId == userId)
                .Select(x => new DeviceAgentProfile(x.Number, x.Name, x.CreatedAt))
                .ToListAsync(cancellationToken);

            return list
                .OrderBy(x => x.CreatedAt)
                .ToList();
        }
        catch (Exception ex) when (DatabaseErrorDetector.IsMissingTable(ex, "DeviceAgents"))
        {
            await EnsureTableAsync(cancellationToken);
            return await GetAsync(userId, cancellationToken);
        }
    }

    /// <summary>
    /// 全ユーザーが登録した装置エージェント一覧取得（テーブルが無い場合は作成後リトライ）
    /// </summary>
    /// <param name="cancellationToken">キャンセル通知</param>
    public async Task<IReadOnlyList<DeviceAgentProfile>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = await CreateDbContextAsync(cancellationToken);
            var list = await db.DeviceAgents
                .Select(x => new DeviceAgentProfile(x.Number, x.Name, x.CreatedAt))
                .ToListAsync(cancellationToken);

            return list
                .OrderBy(x => x.CreatedAt)
                .DistinctBy(x => x.Number, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex) when (DatabaseErrorDetector.IsMissingTable(ex, "DeviceAgents"))
        {
            await EnsureTableAsync(cancellationToken);
            return await GetAllAsync(cancellationToken);
        }
    }

    /// <summary>
    /// 指定番号の装置エージェント取得（テーブルが無い場合は作成後リトライ）
    /// </summary>
    /// <param name="agentNumbers">対象番号</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    public async Task<IReadOnlyList<DeviceAgentProfile>> GetByNumbersAsync(IEnumerable<string> agentNumbers, CancellationToken cancellationToken = default)
    {
        var normalized = new HashSet<string>(
            (agentNumbers ?? Array.Empty<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim()),
            StringComparer.OrdinalIgnoreCase);

        if (normalized.Count == 0)
        {
            return Array.Empty<DeviceAgentProfile>();
        }

        try
        {
            await using var db = await CreateDbContextAsync(cancellationToken);
            var list = await db.DeviceAgents
                .Where(x => normalized.Contains(x.Number))
                .Select(x => new DeviceAgentProfile(x.Number, x.Name, x.CreatedAt))
                .ToListAsync(cancellationToken);

            return list
                .OrderBy(x => x.CreatedAt)
                .DistinctBy(x => x.Number, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex) when (DatabaseErrorDetector.IsMissingTable(ex, "DeviceAgents"))
        {
            await EnsureTableAsync(cancellationToken);
            return await GetByNumbersAsync(normalized, cancellationToken);
        }
    }

    /// <summary>
    /// 装置エージェントの追加または更新（テーブルが無い場合は作成後にリトライ）
    /// </summary>
    /// <param name="userId">ユーザーID</param>
    /// <param name="number">エージェント番号</param>
    /// <param name="name">エージェント名</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>保存されたプロファイル</returns>
    public async Task<DeviceAgentProfile> UpsertAsync(string userId, string number, string name, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = await CreateDbContextAsync(cancellationToken);
            var existing = await db.DeviceAgents
                .FirstOrDefaultAsync(x => x.UserObjectId == userId && x.Number == number, cancellationToken);

            if (existing is null)
            {
                existing = new DeviceAgentEntity
                {
                    UserObjectId = userId,
                    Number = number,
                    Name = name,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                db.DeviceAgents.Add(existing);
            }
            else
            {
                existing.Name = name;
            }

            await db.SaveChangesAsync(cancellationToken);
            return new DeviceAgentProfile(existing.Number, existing.Name, existing.CreatedAt);
        }
        catch (Exception ex) when (DatabaseErrorDetector.IsMissingTable(ex, "DeviceAgents"))
        {
            await EnsureTableAsync(cancellationToken);
            return await UpsertAsync(userId, number, name, cancellationToken);
        }
    }

    /// <summary>
    /// 指定されたエージェント削除（存在しない場合は何もしない）
    /// </summary>
    /// <param name="userId">ユーザーID</param>
    /// <param name="number">エージェント番号</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    public async Task DeleteAsync(string userId, string number, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = await CreateDbContextAsync(cancellationToken);
            var entity = await db.DeviceAgents
                .FirstOrDefaultAsync(x => x.UserObjectId == userId && x.Number == number, cancellationToken);

            if (entity is null)
            {
                return;
            }

            db.DeviceAgents.Remove(entity);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (DatabaseErrorDetector.IsMissingTable(ex, "DeviceAgents"))
        {
            await EnsureTableAsync(cancellationToken);
        }
    }

    /// <summary>
    /// データベースに DeviceAgents テーブルとユニークインデックスを作成する
    /// </summary>
    /// <param name="cancellationToken">キャンセル通知</param>
    private async Task EnsureTableAsync(CancellationToken cancellationToken)
    {
        const string createSql = """
            CREATE TABLE IF NOT EXISTS DeviceAgents(
                Id INTEGER NOT NULL CONSTRAINT PK_DeviceAgents PRIMARY KEY AUTOINCREMENT,
                UserObjectId TEXT NOT NULL,
                Number TEXT NOT NULL,
                Name TEXT NOT NULL,
                CreatedAt TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS IX_DeviceAgents_UserObjectId_Number ON DeviceAgents(UserObjectId, Number);
        """;

        await using var db = await CreateDbContextAsync(cancellationToken);
        await db.Database.ExecuteSqlRawAsync(createSql, cancellationToken);
    }
}
