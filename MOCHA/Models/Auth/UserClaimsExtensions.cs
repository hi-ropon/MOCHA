using System.Security.Claims;

namespace MOCHA.Models.Auth;

public static class UserClaimsExtensions
{
    public static string? GetUserObjectId(this ClaimsPrincipal principal)
    {
        return principal.FindFirst("oid")?.Value
               ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? principal.Identity?.Name;
    }
}
