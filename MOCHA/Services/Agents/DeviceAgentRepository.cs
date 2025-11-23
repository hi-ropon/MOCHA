using Microsoft.EntityFrameworkCore;
using MOCHA.Data;
using MOCHA.Models.Agents;
using Microsoft.Data.Sqlite;

namespace MOCHA.Services.Agents;

/// <summary>
/// 装置エージェントの永続化を行うリポジトリ。
/// </summary>
internal sealed class DeviceAgentRepository : IDeviceAgentRepository
{
    private readonly IChatDbContext _dbContext;

    /// <summary>
    /// DbContext を受け取り、リポジトリを初期化する。
    /// </summary>
    /// <param name="dbContext">チャット用 DbContext。</param>
    public DeviceAgentRepository(IChatDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// 指定ユーザーの装置エージェント一覧を取得する。テーブルが無ければ自動作成する。
    /// </summary>
    /// <param name="userId">ユーザーID。</param>
    /// <param name="cancellationToken">キャンセル通知。</param>
    /// <returns>エージェント一覧。</returns>
    public async Task<IReadOnlyList<DeviceAgentProfile>> GetAsync(string userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var list = await _dbContext.DeviceAgents
                .Where(x => x.UserObjectId == userId)
                .Select(x => new DeviceAgentProfile(x.Number, x.Name, x.CreatedAt))
                .ToListAsync(cancellationToken);

            return list
                .OrderBy(x => x.CreatedAt)
                .ToList();
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 1 && ex.Message.Contains("DeviceAgents", StringComparison.OrdinalIgnoreCase))
        {
            await EnsureTableAsync(cancellationToken);
            var list = await _dbContext.DeviceAgents
                .Where(x => x.UserObjectId == userId)
                .Select(x => new DeviceAgentProfile(x.Number, x.Name, x.CreatedAt))
                .ToListAsync(cancellationToken);
            return list
                .OrderBy(x => x.CreatedAt)
                .ToList();
        }
    }

    /// <summary>
    /// 装置エージェントを追加または更新する。テーブルが無い場合は作成後にリトライする。
    /// </summary>
    /// <param name="userId">ユーザーID。</param>
    /// <param name="number">エージェント番号。</param>
    /// <param name="name">エージェント名。</param>
    /// <param name="cancellationToken">キャンセル通知。</param>
    /// <returns>保存されたプロファイル。</returns>
    public async Task<DeviceAgentProfile> UpsertAsync(string userId, string number, string name, CancellationToken cancellationToken = default)
    {
        try
        {
            var existing = await _dbContext.DeviceAgents
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
                _dbContext.DeviceAgents.Add(existing);
            }
            else
            {
                existing.Name = name;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return new DeviceAgentProfile(existing.Number, existing.Name, existing.CreatedAt);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 1 && ex.Message.Contains("DeviceAgents", StringComparison.OrdinalIgnoreCase))
        {
            await EnsureTableAsync(cancellationToken);
            return await UpsertAsync(userId, number, name, cancellationToken);
        }
    }

    /// <summary>
    /// 指定されたエージェントを削除する。存在しない場合は何もしない。
    /// </summary>
    /// <param name="userId">ユーザーID。</param>
    /// <param name="number">エージェント番号。</param>
    /// <param name="cancellationToken">キャンセル通知。</param>
    public async Task DeleteAsync(string userId, string number, CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = await _dbContext.DeviceAgents
                .FirstOrDefaultAsync(x => x.UserObjectId == userId && x.Number == number, cancellationToken);

            if (entity is null)
            {
                return;
            }

            _dbContext.DeviceAgents.Remove(entity);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 1 && ex.Message.Contains("DeviceAgents", StringComparison.OrdinalIgnoreCase))
        {
            await EnsureTableAsync(cancellationToken);
        }
    }

    /// <summary>
    /// データベースに DeviceAgents テーブルとユニークインデックスを作成する。
    /// </summary>
    /// <param name="cancellationToken">キャンセル通知。</param>
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

        await _dbContext.Database.ExecuteSqlRawAsync(createSql, cancellationToken);
    }
}
