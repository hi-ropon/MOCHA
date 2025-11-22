using System.Collections.Generic;

namespace MOCHA.Models.Auth;

public sealed class RoleBootstrapOptions
{
    public List<string> AdminUserIds { get; init; } = new();
}
