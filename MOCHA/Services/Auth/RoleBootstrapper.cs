using Microsoft.Extensions.Options;
using MOCHA.Models.Auth;

namespace MOCHA.Services.Auth;

public sealed class RoleBootstrapper
{
    private readonly IUserRoleProvider _roleProvider;
    private readonly RoleBootstrapOptions _options;

    public RoleBootstrapper(IUserRoleProvider roleProvider, IOptions<RoleBootstrapOptions> options)
    {
        _roleProvider = roleProvider;
        _options = options.Value;
    }

    public async Task EnsureAdminRolesAsync(CancellationToken cancellationToken = default)
    {
        if (_options.AdminUserIds is null || _options.AdminUserIds.Count == 0)
        {
            return;
        }

        foreach (var userId in _options.AdminUserIds)
        {
            await _roleProvider.AssignAsync(userId, UserRoleId.Predefined.Administrator, cancellationToken);
        }
    }
}
