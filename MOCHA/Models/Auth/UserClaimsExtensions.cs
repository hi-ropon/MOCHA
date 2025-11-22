using System.Security.Claims;

namespace MOCHA.Models.Auth;

/// <summary>
/// ユーザーのクレームからID情報を取り出す拡張メソッド。
/// </summary>
public static class UserClaimsExtensions
{
    /// <summary>
    /// OID または NameIdentifier を優先的に取得し、なければ Name を返す。
    /// </summary>
    /// <param name="principal">認証済みユーザー。</param>
    /// <returns>取得したユーザーID。見つからなければ null。</returns>
    public static string? GetUserObjectId(this ClaimsPrincipal principal)
    {
        return principal.FindFirst("oid")?.Value
               ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? principal.Identity?.Name;
    }
}
