using System;

namespace MOCHA.Services.Auth;

public class UserRoleEntity
{
    public int Id { get; set; }
    public string UserId { get; set; } = default!;
    public string Role { get; set; } = default!;
    public DateTimeOffset CreatedAt { get; set; }
}
