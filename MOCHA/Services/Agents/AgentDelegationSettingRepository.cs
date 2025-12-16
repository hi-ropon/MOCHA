using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using MOCHA.Data;
using MOCHA.Models.Agents;

namespace MOCHA.Services.Agents;

/// <summary>サブエージェント設定を永続化するリポジトリ</summary>
internal sealed class AgentDelegationSettingRepository : IAgentDelegationSettingRepository
{
    private readonly IDbContextFactory<ChatDbContext> _dbContextFactory;
    private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    public AgentDelegationSettingRepository(IDbContextFactory<ChatDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    private async Task<ChatDbContext> CreateDbContextAsync(CancellationToken cancellationToken)
    {
        return await _dbContextFactory.CreateDbContextAsync(cancellationToken);
    }

    public async Task<AgentDelegationSetting?> GetAsync(string userId, string agentNumber, CancellationToken cancellationToken = default)
    {
        var normalizedUser = (userId ?? string.Empty).Trim();
        var normalizedAgent = (agentNumber ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedUser) || string.IsNullOrWhiteSpace(normalizedAgent))
        {
            return null;
        }

        try
        {
            await using var db = await CreateDbContextAsync(cancellationToken);
            var entity = await db.AgentDelegationSettings
                .FirstOrDefaultAsync(x => x.UserObjectId == normalizedUser && x.AgentNumber == normalizedAgent, cancellationToken);

            if (entity is null)
            {
                return null;
            }

            var allowed = DeserializeAgents(entity.AllowedSubAgentsJson);
            return new AgentDelegationSetting(entity.AgentNumber, allowed);
        }
        catch (Exception ex) when (DatabaseErrorDetector.IsMissingTable(ex, "AgentDelegationSettings"))
        {
            await EnsureTableAsync(cancellationToken);
            return await GetAsync(normalizedUser, normalizedAgent, cancellationToken);
        }
    }

    public async Task<AgentDelegationSetting> UpsertAsync(string userId, AgentDelegationSetting setting, CancellationToken cancellationToken = default)
    {
        var normalizedUser = (userId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedUser))
        {
            throw new ArgumentException("userId must not be empty", nameof(userId));
        }

        try
        {
            await using var db = await CreateDbContextAsync(cancellationToken);
            var entity = await db.AgentDelegationSettings
                .FirstOrDefaultAsync(x => x.UserObjectId == normalizedUser && x.AgentNumber == setting.AgentNumber, cancellationToken);

            if (entity is null)
            {
                entity = new AgentDelegationSettingEntity
                {
                    UserObjectId = normalizedUser,
                    AgentNumber = setting.AgentNumber,
                    AllowedSubAgentsJson = SerializeAgents(setting.AllowedSubAgents),
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                db.AgentDelegationSettings.Add(entity);
            }
            else
            {
                entity.AllowedSubAgentsJson = SerializeAgents(setting.AllowedSubAgents);
                entity.UpdatedAt = DateTimeOffset.UtcNow;
            }

            await db.SaveChangesAsync(cancellationToken);
            return new AgentDelegationSetting(entity.AgentNumber, DeserializeAgents(entity.AllowedSubAgentsJson));
        }
        catch (Exception ex) when (DatabaseErrorDetector.IsMissingTable(ex, "AgentDelegationSettings"))
        {
            await EnsureTableAsync(cancellationToken);
            return await UpsertAsync(normalizedUser, setting, cancellationToken);
        }
    }

    private static string SerializeAgents(IReadOnlyCollection<string> agents)
    {
        return JsonSerializer.Serialize(agents ?? Array.Empty<string>(), _serializerOptions);
    }

    private static IReadOnlyCollection<string> DeserializeAgents(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<string>();
        }

        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(json, _serializerOptions);
            return list ?? new List<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }

    private async Task EnsureTableAsync(CancellationToken cancellationToken)
    {
        await using var db = await CreateDbContextAsync(cancellationToken);
        var createSql = BuildCreateSql(db);
        await db.Database.ExecuteSqlRawAsync(createSql, cancellationToken);
    }

    private static string BuildCreateSql(DbContext dbContext)
    {
        var provider = dbContext.Database.ProviderName ?? string.Empty;
        if (provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
        {
            return """
                CREATE TABLE IF NOT EXISTS "AgentDelegationSettings"(
                    "Id" INTEGER GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                    "UserObjectId" TEXT NOT NULL,
                    "AgentNumber" TEXT NOT NULL,
                    "AllowedSubAgentsJson" TEXT NOT NULL,
                    "UpdatedAt" TIMESTAMPTZ NOT NULL
                );
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_AgentDelegationSettings_UserObjectId_AgentNumber" ON "AgentDelegationSettings"("UserObjectId", "AgentNumber");
            """;
        }

        if (provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            return """
            CREATE TABLE IF NOT EXISTS AgentDelegationSettings(
                Id INTEGER NOT NULL CONSTRAINT PK_AgentDelegationSettings PRIMARY KEY AUTOINCREMENT,
                UserObjectId TEXT NOT NULL,
                AgentNumber TEXT NOT NULL,
                AllowedSubAgentsJson TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS IX_AgentDelegationSettings_UserObjectId_AgentNumber ON AgentDelegationSettings(UserObjectId, AgentNumber);
        """;
        }

        // デフォルトは ANSI で作成（念のため Postgres 互換に寄せる）
        return """
            CREATE TABLE IF NOT EXISTS "AgentDelegationSettings"(
                "Id" INTEGER GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                "UserObjectId" TEXT NOT NULL,
                "AgentNumber" TEXT NOT NULL,
                "AllowedSubAgentsJson" TEXT NOT NULL,
                "UpdatedAt" TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_AgentDelegationSettings_UserObjectId_AgentNumber" ON "AgentDelegationSettings"("UserObjectId", "AgentNumber");
        """;
    }
}
