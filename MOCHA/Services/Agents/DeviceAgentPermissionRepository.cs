using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MOCHA.Data;

namespace MOCHA.Services.Agents;

/// <summary>
/// 装置エージェント利用許可の永続化を行うリポジトリ
/// </summary>
internal sealed class DeviceAgentPermissionRepository : IDeviceAgentPermissionRepository
{
    private readonly IDbContextFactory<ChatDbContext> _dbContextFactory;

    /// <summary>
    /// DbContext ファクトリ注入によるリポジトリ初期化
    /// </summary>
    /// <param name="dbContextFactory">チャット用 DbContext ファクトリ</param>
    public DeviceAgentPermissionRepository(IDbContextFactory<ChatDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    /// <summary>
    /// 指定ユーザーの利用許可取得（テーブルが無い場合は作成後リトライ）
    /// </summary>
    /// <param name="userId">ユーザーID</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    public async Task<IReadOnlyList<string>> GetAllowedAgentNumbersAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Array.Empty<string>();
        }

        try
        {
            await using var db = await CreateDbContextAsync(cancellationToken);
            var list = await db.DeviceAgentPermissions
                .Where(x => x.UserObjectId == userId)
                .Select(x => x.AgentNumber)
                .ToListAsync(cancellationToken);

            return list.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }
        catch (Exception ex) when (DatabaseErrorDetector.IsMissingTable(ex, "DeviceAgentPermissions"))
        {
            await EnsureTableAsync(cancellationToken);
            return await GetAllowedAgentNumbersAsync(userId, cancellationToken);
        }
    }

    /// <summary>
    /// 指定ユーザーの利用許可置き換え（テーブルが無い場合は作成後リトライ）
    /// </summary>
    /// <param name="userId">ユーザーID</param>
    /// <param name="agentNumbers">許可する番号一覧</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    public async Task ReplaceAsync(string userId, IEnumerable<string> agentNumbers, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        var normalized = new HashSet<string>(
            (agentNumbers ?? Array.Empty<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim()),
            StringComparer.OrdinalIgnoreCase);

        try
        {
            await using var db = await CreateDbContextAsync(cancellationToken);
            var existing = await db.DeviceAgentPermissions
                .Where(x => x.UserObjectId == userId)
                .ToListAsync(cancellationToken);

            var existingSet = new HashSet<string>(existing.Select(x => x.AgentNumber), StringComparer.OrdinalIgnoreCase);
            var toRemove = existing.Where(x => !normalized.Contains(x.AgentNumber)).ToList();
            if (toRemove.Count > 0)
            {
                db.DeviceAgentPermissions.RemoveRange(toRemove);
            }

            foreach (var number in normalized)
            {
                if (existingSet.Contains(number))
                {
                    continue;
                }

                db.DeviceAgentPermissions.Add(new DeviceAgentPermissionEntity
                {
                    UserObjectId = userId,
                    AgentNumber = number,
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }

            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (DatabaseErrorDetector.IsMissingTable(ex, "DeviceAgentPermissions"))
        {
            await EnsureTableAsync(cancellationToken);
            await ReplaceAsync(userId, normalized, cancellationToken);
        }
    }

    /// <summary>
    /// DeviceAgentPermissions テーブルとユニークインデックス作成
    /// </summary>
    /// <param name="cancellationToken">キャンセル通知</param>
    private async Task EnsureTableAsync(CancellationToken cancellationToken)
    {
        const string createSql = """
            CREATE TABLE IF NOT EXISTS DeviceAgentPermissions(
                Id INTEGER NOT NULL CONSTRAINT PK_DeviceAgentPermissions PRIMARY KEY AUTOINCREMENT,
                UserObjectId TEXT NOT NULL,
                AgentNumber TEXT NOT NULL,
                CreatedAt TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS IX_DeviceAgentPermissions_UserObjectId_AgentNumber ON DeviceAgentPermissions(UserObjectId, AgentNumber);
        """;

        await using var db = await CreateDbContextAsync(cancellationToken);
        await db.Database.ExecuteSqlRawAsync(createSql, cancellationToken);
    }

    private Task<ChatDbContext> CreateDbContextAsync(CancellationToken cancellationToken)
    {
        return _dbContextFactory.CreateDbContextAsync(cancellationToken);
    }
}
