using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MOCHA.Data;
using MOCHA.Models.Architecture;

namespace MOCHA.Services.Architecture;

/// <summary>
/// PC設定をDBに永続化するリポジトリ
/// </summary>
internal sealed class PcSettingRepository : IPcSettingRepository
{
    private readonly IChatDbContext _dbContext;
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// DbContext 受け取りによる初期化
    /// </summary>
    /// <param name="dbContext">チャット用 DbContext</param>
    public PcSettingRepository(IChatDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<PcSetting> AddAsync(PcSetting setting, CancellationToken cancellationToken = default)
    {
        await EnsureTableIfMissingAsync(cancellationToken);
        var entity = ToEntity(setting);
        await _dbContext.PcSettings.AddAsync(entity, cancellationToken);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ToModel(entity);
        }
        catch (DbUpdateException ex) when (IsMissingTable(ex))
        {
            await EnsureTableIfMissingAsync(cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ToModel(entity);
        }
        catch (SqliteException ex) when (IsMissingTable(ex))
        {
            await EnsureTableIfMissingAsync(cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ToModel(entity);
        }
    }

    /// <inheritdoc />
    public async Task<PcSetting> UpdateAsync(PcSetting setting, CancellationToken cancellationToken = default)
    {
        await EnsureTableIfMissingAsync(cancellationToken);
        PcSettingEntity? entity = null;
        try
        {
            entity = await _dbContext.PcSettings.FirstOrDefaultAsync(x => x.Id == setting.Id, cancellationToken);
            if (entity is null)
            {
                entity = ToEntity(setting);
                _dbContext.PcSettings.Add(entity);
            }
            else
            {
                entity.UserId = setting.UserId;
                entity.AgentNumber = setting.AgentNumber;
                entity.Os = setting.Os;
                entity.Role = setting.Role;
                entity.RepositoryUrlsJson = SerializeUrls(setting.RepositoryUrls);
                entity.CreatedAt = setting.CreatedAt;
                entity.UpdatedAt = setting.UpdatedAt;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return ToModel(entity);
        }
        catch (DbUpdateException ex) when (IsMissingTable(ex))
        {
            await EnsureTableIfMissingAsync(cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ToModel(entity!);
        }
        catch (SqliteException ex) when (IsMissingTable(ex))
        {
            await EnsureTableIfMissingAsync(cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ToModel(entity!);
        }
    }

    /// <inheritdoc />
    public async Task<PcSetting?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureTableIfMissingAsync(cancellationToken);
        try
        {
            var entity = await _dbContext.PcSettings.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            return entity is null ? null : ToModel(entity);
        }
        catch (SqliteException ex) when (IsMissingTable(ex))
        {
            await EnsureTableIfMissingAsync(cancellationToken);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureTableIfMissingAsync(cancellationToken);
        try
        {
            var entity = await _dbContext.PcSettings.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (entity is null)
            {
                return false;
            }

            _dbContext.PcSettings.Remove(entity);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex) when (IsMissingTable(ex))
        {
            await EnsureTableIfMissingAsync(cancellationToken);
            return false;
        }
        catch (SqliteException ex) when (IsMissingTable(ex))
        {
            await EnsureTableIfMissingAsync(cancellationToken);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PcSetting>> ListAsync(string userId, string agentNumber, CancellationToken cancellationToken = default)
    {
        await EnsureTableIfMissingAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(agentNumber))
        {
            return Array.Empty<PcSetting>();
        }

        var normalizedUserId = userId.Trim();
        var normalizedAgent = agentNumber.Trim();

        try
        {
            var list = await _dbContext.PcSettings
                .Where(x => x.UserId == normalizedUserId && x.AgentNumber == normalizedAgent)
                .ToListAsync(cancellationToken);

            return list
                .OrderBy(x => x.CreatedAt)
                .Select(ToModel)
                .ToList();
        }
        catch (SqliteException ex) when (IsMissingTable(ex))
        {
            await EnsureTableIfMissingAsync(cancellationToken);
            return Array.Empty<PcSetting>();
        }
    }

    private PcSettingEntity ToEntity(PcSetting setting)
    {
        return new PcSettingEntity
        {
            Id = setting.Id,
            UserId = setting.UserId,
            AgentNumber = setting.AgentNumber,
            Os = setting.Os,
            Role = setting.Role,
            RepositoryUrlsJson = SerializeUrls(setting.RepositoryUrls),
            CreatedAt = setting.CreatedAt,
            UpdatedAt = setting.UpdatedAt
        };
    }

    private PcSetting ToModel(PcSettingEntity entity)
    {
        var urls = DeserializeUrls(entity.RepositoryUrlsJson);
        return PcSetting.Restore(
            entity.Id,
            entity.UserId,
            entity.AgentNumber,
            entity.Os,
            entity.Role,
            urls,
            entity.CreatedAt,
            entity.UpdatedAt);
    }

    private string SerializeUrls(IReadOnlyCollection<string> urls)
    {
        return JsonSerializer.Serialize(urls ?? Array.Empty<string>(), _serializerOptions);
    }

    private IReadOnlyCollection<string> DeserializeUrls(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<string>();
        }

        var urls = JsonSerializer.Deserialize<IReadOnlyCollection<string>>(json, _serializerOptions);
        return urls ?? Array.Empty<string>();
    }

    private async Task EnsureTableIfMissingAsync(CancellationToken cancellationToken)
    {
        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        const string createSql = """
            CREATE TABLE IF NOT EXISTS PcSettings(
                Id TEXT NOT NULL CONSTRAINT PK_PcSettings PRIMARY KEY,
                UserId TEXT NOT NULL,
                AgentNumber TEXT NOT NULL,
                Os TEXT NOT NULL,
                Role TEXT NULL,
                RepositoryUrlsJson TEXT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_PcSettings_UserId_AgentNumber_CreatedAt ON PcSettings(UserId, AgentNumber, CreatedAt);
        """;

        await _dbContext.Database.ExecuteSqlRawAsync(createSql, cancellationToken);
    }

    private static bool IsMissingTable(Exception exception)
    {
        if (exception is SqliteException sqliteEx)
        {
            return sqliteEx.SqliteErrorCode == 1
                   && sqliteEx.Message.Contains("PcSettings", StringComparison.OrdinalIgnoreCase);
        }

        if (exception is DbUpdateException updateEx && updateEx.InnerException is SqliteException inner)
        {
            return inner.SqliteErrorCode == 1
                   && inner.Message.Contains("PcSettings", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}
