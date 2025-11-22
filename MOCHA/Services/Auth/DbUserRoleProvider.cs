using Microsoft.EntityFrameworkCore;
using MOCHA.Data;
using MOCHA.Models.Auth;

namespace MOCHA.Services.Auth;

public sealed class DbUserRoleProvider : IUserRoleProvider
{
    private readonly ChatDbContext _db;

    public DbUserRoleProvider(ChatDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyCollection<UserRoleId>> GetRolesAsync(string userId, CancellationToken cancellationToken = default)
    {
        var roles = await _db.UserRoles
            .Where(r => r.UserId == userId)
            .Select(r => r.Role)
            .ToListAsync(cancellationToken);

        return roles.Select(UserRoleId.From).ToArray();
    }

    public async Task AssignAsync(string userId, UserRoleId role, CancellationToken cancellationToken = default)
    {
        var normalized = role.Value;
        var exists = await _db.UserRoles.AnyAsync(
            r => r.UserId == userId && r.Role == normalized,
            cancellationToken);

        if (exists)
        {
            return;
        }

        _db.UserRoles.Add(new UserRoleEntity
        {
            UserId = userId,
            Role = normalized,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(string userId, UserRoleId role, CancellationToken cancellationToken = default)
    {
        var normalized = role.Value;
        var entities = await _db.UserRoles
            .Where(r => r.UserId == userId && r.Role == normalized)
            .ToListAsync(cancellationToken);

        if (entities.Count == 0)
        {
            return;
        }

        _db.UserRoles.RemoveRange(entities);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> IsInRoleAsync(string userId, string role, CancellationToken cancellationToken = default)
    {
        var normalized = UserRoleId.From(role).Value;
        return await _db.UserRoles.AnyAsync(
            r => r.UserId == userId && r.Role == normalized,
            cancellationToken);
    }
}
