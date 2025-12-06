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
/// 装置ユニット構成のDBリポジトリ
/// </summary>
internal sealed class UnitConfigurationRepository : IUnitConfigurationRepository
{
    private readonly IChatDbContext _dbContext;
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// 依存注入による初期化
    /// </summary>
    /// <param name="dbContext">DbContext</param>
    public UnitConfigurationRepository(IChatDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<UnitConfiguration> AddAsync(UnitConfiguration unit, CancellationToken cancellationToken = default)
    {
        await EnsureTableIfMissingAsync(cancellationToken);
        var entity = ToEntity(unit);
        await _dbContext.UnitConfigurations.AddAsync(entity, cancellationToken);

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
    public async Task<UnitConfiguration> UpdateAsync(UnitConfiguration unit, CancellationToken cancellationToken = default)
    {
        await EnsureTableIfMissingAsync(cancellationToken);
        UnitConfigurationEntity? entity = null;
        try
        {
            entity = await _dbContext.UnitConfigurations.FirstOrDefaultAsync(x => x.Id == unit.Id, cancellationToken);
            if (entity is null)
            {
                entity = ToEntity(unit);
                _dbContext.UnitConfigurations.Add(entity);
            }
            else
            {
                entity.UserId = unit.UserId;
                entity.AgentNumber = unit.AgentNumber;
                entity.Name = unit.Name;
                entity.Description = unit.Description;
                entity.DevicesJson = SerializeDevices(unit.Devices);
                entity.CreatedAt = unit.CreatedAt;
                entity.UpdatedAt = unit.UpdatedAt;
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
    public async Task<UnitConfiguration?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureTableIfMissingAsync(cancellationToken);
        try
        {
            var entity = await _dbContext.UnitConfigurations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
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
            var entity = await _dbContext.UnitConfigurations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (entity is null)
            {
                return false;
            }

            _dbContext.UnitConfigurations.Remove(entity);
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
    public async Task<IReadOnlyList<UnitConfiguration>> ListAsync(string userId, string agentNumber, CancellationToken cancellationToken = default)
    {
        await EnsureTableIfMissingAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(agentNumber))
        {
            return Array.Empty<UnitConfiguration>();
        }

        var normalizedUserId = userId.Trim();
        var normalizedAgent = agentNumber.Trim();

        try
        {
            var list = await _dbContext.UnitConfigurations
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
            return Array.Empty<UnitConfiguration>();
        }
    }

    private UnitConfigurationEntity ToEntity(UnitConfiguration unit)
    {
        return new UnitConfigurationEntity
        {
            Id = unit.Id,
            UserId = unit.UserId,
            AgentNumber = unit.AgentNumber,
            Name = unit.Name,
            Description = unit.Description,
            DevicesJson = SerializeDevices(unit.Devices),
            CreatedAt = unit.CreatedAt,
            UpdatedAt = unit.UpdatedAt
        };
    }

    private UnitConfiguration ToModel(UnitConfigurationEntity entity)
    {
        var devices = DeserializeDevices(entity.DevicesJson);
        return UnitConfiguration.Restore(
            entity.Id,
            entity.UserId,
            entity.AgentNumber,
            entity.Name,
            entity.Description,
            devices,
            entity.CreatedAt,
            entity.UpdatedAt);
    }

    private string SerializeDevices(IReadOnlyCollection<UnitDevice> devices)
    {
        var payload = devices?
            .Select(d => new UnitDeviceData
            {
                Id = d.Id,
                Name = d.Name,
                Model = d.Model,
                Maker = d.Maker,
                Description = d.Description,
                Order = d.Order
            })
            .ToList() ?? new List<UnitDeviceData>();

        return JsonSerializer.Serialize(payload, _serializerOptions);
    }

    private IReadOnlyCollection<UnitDevice> DeserializeDevices(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<UnitDevice>();
        }

        var devices = JsonSerializer.Deserialize<IReadOnlyCollection<UnitDeviceData>>(json, _serializerOptions);
        if (devices is null)
        {
            return Array.Empty<UnitDevice>();
        }

        return devices
            .OrderBy(d => d.Order)
            .ThenBy(d => d.Name)
            .Select(d => UnitDevice.Restore(d.Id, d.Name, d.Model, d.Maker, d.Description, d.Order))
            .ToList();
    }

    private async Task EnsureTableIfMissingAsync(CancellationToken cancellationToken)
    {
        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        const string createSql = """
            CREATE TABLE IF NOT EXISTS UnitConfigurations(
                Id TEXT NOT NULL CONSTRAINT PK_UnitConfigurations PRIMARY KEY,
                UserId TEXT NOT NULL,
                AgentNumber TEXT NOT NULL,
                Name TEXT NOT NULL,
                Description TEXT NULL,
                DevicesJson TEXT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_UnitConfigurations_UserId_AgentNumber_CreatedAt ON UnitConfigurations(UserId, AgentNumber, CreatedAt);
        """;

        await _dbContext.Database.ExecuteSqlRawAsync(createSql, cancellationToken);
    }

    private static bool IsMissingTable(Exception exception)
    {
        if (exception is SqliteException sqliteEx)
        {
            return sqliteEx.SqliteErrorCode == 1
                   && sqliteEx.Message.Contains("UnitConfigurations", StringComparison.OrdinalIgnoreCase);
        }

        if (exception is DbUpdateException updateEx && updateEx.InnerException is SqliteException inner)
        {
            return inner.SqliteErrorCode == 1
                   && inner.Message.Contains("UnitConfigurations", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private sealed class UnitDeviceData
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Model { get; set; }
        public string? Maker { get; set; }
        public string? Description { get; set; }
        public int Order { get; set; }
    }
}
