using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MOCHA.Models.Auth;

namespace MOCHA.Services.Auth;

public sealed class InMemoryUserRoleProvider : IUserRoleProvider
{
    private readonly ConcurrentDictionary<string, HashSet<UserRoleId>> _roles = new(StringComparer.OrdinalIgnoreCase);

    public Task<IReadOnlyCollection<UserRoleId>> GetRolesAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (!_roles.TryGetValue(userId, out var set))
        {
            return Task.FromResult<IReadOnlyCollection<UserRoleId>>(Array.Empty<UserRoleId>());
        }

        lock (set)
        {
            return Task.FromResult<IReadOnlyCollection<UserRoleId>>(set.ToArray());
        }
    }

    public Task AssignAsync(string userId, UserRoleId role, CancellationToken cancellationToken = default)
    {
        var set = _roles.GetOrAdd(userId, _ => new HashSet<UserRoleId>());
        lock (set)
        {
            set.Add(role);
        }

        return Task.CompletedTask;
    }

    public Task RemoveAsync(string userId, UserRoleId role, CancellationToken cancellationToken = default)
    {
        if (_roles.TryGetValue(userId, out var set))
        {
            lock (set)
            {
                set.Remove(role);
            }
        }

        return Task.CompletedTask;
    }

    public Task<bool> IsInRoleAsync(string userId, string role, CancellationToken cancellationToken = default)
    {
        var roleId = UserRoleId.From(role);

        if (!_roles.TryGetValue(userId, out var set))
        {
            return Task.FromResult(false);
        }

        lock (set)
        {
            return Task.FromResult(set.Contains(roleId));
        }
    }
}
