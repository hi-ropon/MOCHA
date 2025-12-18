using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MOCHA.Data;
using MOCHA.Models.Architecture;

namespace MOCHA.Services.Architecture;

/// <summary>
/// ゲートウェイ設定の永続化リポジトリ
/// </summary>
internal sealed class GatewaySettingRepository : IGatewaySettingRepository
{
    private readonly IChatDbContext _dbContext;

    public GatewaySettingRepository(IChatDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<GatewaySetting?> GetAsync(string agentNumber, CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken);
        var normalizedAgent = agentNumber.Trim();

        try
        {
            var entity = _dbContext.GatewaySettings
                .Where(x => x.AgentNumber == normalizedAgent)
                .AsEnumerable()
                .OrderByDescending(x => x.UpdatedAt)
                .FirstOrDefault();

            return entity is null ? null : ToModel(entity);
        }
        catch (Exception ex) when (DatabaseErrorDetector.IsMissingTable(ex, "GatewaySettings"))
        {
            await EnsureTableAsync(cancellationToken);
            return null;
        }
    }

    public async Task<GatewaySetting> UpsertAsync(GatewaySetting setting, CancellationToken cancellationToken = default)
    {
        await EnsureTableAsync(cancellationToken);

        var entity = await _dbContext.GatewaySettings
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(x => x.AgentNumber == setting.AgentNumber, cancellationToken);

        if (entity is null)
        {
            entity = ToEntity(setting);
            await _dbContext.GatewaySettings.AddAsync(entity, cancellationToken);
        }
        else
        {
            entity.Host = setting.Host;
            entity.Port = setting.Port;
            entity.UpdatedAt = setting.UpdatedAt;
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (DatabaseErrorDetector.IsMissingTable(ex, "GatewaySettings"))
        {
            await EnsureTableAsync(cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return ToModel(entity);
    }

    private GatewaySettingEntity ToEntity(GatewaySetting setting)
    {
        return new GatewaySettingEntity
        {
            Id = setting.Id,
            UserId = setting.UserId,
            AgentNumber = setting.AgentNumber,
            Host = setting.Host,
            Port = setting.Port,
            UpdatedAt = setting.UpdatedAt
        };
    }

    private GatewaySetting ToModel(GatewaySettingEntity entity)
    {
        return GatewaySetting.Restore(entity.Id, entity.UserId, entity.AgentNumber, entity.Host, entity.Port, entity.UpdatedAt);
    }

    private async Task EnsureTableAsync(CancellationToken cancellationToken)
    {
        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        const string createSql = """
            CREATE TABLE IF NOT EXISTS GatewaySettings(
                Id TEXT NOT NULL CONSTRAINT PK_GatewaySettings PRIMARY KEY,
                UserId TEXT NOT NULL,
                AgentNumber TEXT NOT NULL,
                Host TEXT NOT NULL,
                Port INTEGER NOT NULL,
                UpdatedAt TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_GatewaySettings_AgentNumber ON GatewaySettings(AgentNumber);
            CREATE INDEX IF NOT EXISTS IX_GatewaySettings_UserId_AgentNumber ON GatewaySettings(UserId, AgentNumber);
        """;

        await _dbContext.Database.ExecuteSqlRawAsync(createSql, cancellationToken);
    }

}
