using Microsoft.EntityFrameworkCore;
using MOCHA.Data;
using MOCHA.Models.Agents;
using Microsoft.Data.Sqlite;

namespace MOCHA.Services.Agents;

public class DeviceAgentRepository : IDeviceAgentRepository
{
    private readonly IChatDbContext _dbContext;

    public DeviceAgentRepository(IChatDbContext dbContext)
    {
        _dbContext = dbContext;
    }

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
