using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using MOCHA.Models.Auth;

namespace MOCHA.Services.Auth;

/// <summary>
/// 開発用に固定ユーザーで認証済みとするフェイクハンドラー。
/// </summary>
public sealed class FakeAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly FakeAuthOptions _options;

    /// <summary>
    /// フェイク認証で使用するスキーム名。
    /// </summary>
    public const string scheme = "Fake";

    /// <summary>
    /// フェイク認証のオプションを受け取り初期化する。
    /// </summary>
    /// <param name="schemeOptions">スキーム設定。</param>
    /// <param name="logger">ロガー。</param>
    /// <param name="encoder">URL エンコーダー。</param>
    /// <param name="clock">システムクロック。</param>
    /// <param name="fakeOptions">フェイク認証設定。</param>
    public FakeAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> schemeOptions,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock,
        IOptions<FakeAuthOptions> fakeOptions)
        : base(schemeOptions, logger, encoder, clock)
    {
        _options = fakeOptions.Value;
    }

    /// <summary>
    /// フェイク認証を行い、設定が無効なら認証なしとして扱う。
    /// </summary>
    /// <returns>認証結果。</returns>
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!_options.Enabled)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new[]
        {
            new Claim("oid", _options.UserId),
            new Claim(ClaimTypes.NameIdentifier, _options.UserId),
            new Claim(ClaimTypes.Name, _options.Name),
        };
        var identity = new ClaimsIdentity(claims, scheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, scheme);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
