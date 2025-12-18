using Microsoft.EntityFrameworkCore;
using MOCHA.Data;

namespace MOCHA.Services.Auth;

internal sealed class DevUserLookupStrategy : IUserLookupStrategy
{
    private readonly IDbContextFactory<ChatDbContext> _dbFactory;

    public DevUserLookupStrategy(IDbContextFactory<ChatDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<UserLookupResult?> LookupAsync(string normalizedIdentifier, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.DevUsers.SingleOrDefaultAsync(
            u => u.Email == normalizedIdentifier,
            cancellationToken);

        if (entity is null)
        {
            return null;
        }

        var displayName = string.IsNullOrWhiteSpace(entity.DisplayName)
            ? entity.Email
            : entity.DisplayName.Trim();

        return new UserLookupResult(entity.Email, displayName);
    }
}
