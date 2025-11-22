using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using MOCHA.Models.Auth;

namespace MOCHA.Services.Auth;

public sealed class FakeAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly FakeAuthOptions _options;

    public const string Scheme = "Fake";

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
        var identity = new ClaimsIdentity(claims, Scheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
