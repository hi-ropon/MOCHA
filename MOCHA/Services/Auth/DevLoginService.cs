using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using MOCHA.Models.Auth;

namespace MOCHA.Services.Auth;

/// <summary>
/// 開発用ログインで使用するプリンシパル生成サービス
/// </summary>
public interface IDevLoginService
{
    /// <summary>
    /// 入力をもとにプリンシパルを作成する
    /// </summary>
    /// <param name="email">メールアドレス</param>
    /// <param name="displayName">表示名</param>
    /// <returns>作成したプリンシパル</returns>
    ClaimsPrincipal CreatePrincipal(string email, string displayName);

    /// <summary>
    /// 認証プロパティを構成する
    /// </summary>
    /// <param name="lifetime">有効期間</param>
    /// <returns>認証プロパティ</returns>
    AuthenticationProperties CreateProperties(TimeSpan lifetime);
}

/// <summary>
/// 開発用の簡易ログイン生成実装
/// </summary>
internal sealed class DevLoginService : IDevLoginService
{
    /// <inheritdoc />
    public ClaimsPrincipal CreatePrincipal(string email, string displayName)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("メールアドレスは必須です", nameof(email));
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var normalizedDisplayName = string.IsNullOrWhiteSpace(displayName) ? normalizedEmail : displayName.Trim();

        var claims = new List<Claim>
        {
            new("oid", normalizedEmail),
            new(ClaimTypes.NameIdentifier, normalizedEmail),
            new(ClaimTypes.Name, normalizedDisplayName),
            new(ClaimTypes.Email, normalizedEmail)
        };

        var identity = new ClaimsIdentity(claims, DevAuthDefaults.scheme);
        var principal = new ClaimsPrincipal(identity);
        return principal;
    }

    /// <inheritdoc />
    public AuthenticationProperties CreateProperties(TimeSpan lifetime)
    {
        return new AuthenticationProperties
        {
            IsPersistent = true,
            AllowRefresh = true,
            ExpiresUtc = DateTimeOffset.UtcNow.Add(lifetime)
        };
    }
}
