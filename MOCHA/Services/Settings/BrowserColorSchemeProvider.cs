using System.Threading;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using MOCHA.Models.Settings;

namespace MOCHA.Services.Settings;

/// <summary>
/// ブラウザの prefers-color-scheme を取得するプロバイダ。
/// </summary>
public sealed class BrowserColorSchemeProvider : IColorSchemeProvider
{
    private readonly IJSRuntime _jsRuntime;

    public BrowserColorSchemeProvider(IJSRuntime jsRuntime)
    {
        this._jsRuntime = jsRuntime;
    }

    public async Task<Theme> GetPreferredThemeAsync(CancellationToken cancellationToken = default)
    {
        string? scheme;
        try
        {
            scheme = await _jsRuntime.InvokeAsync<string>(
                "mochaPreferences.getPreferredColorScheme",
                cancellationToken);
        }
        catch (JSException)
        {
            return Theme.Light;
        }

        return scheme?.ToLowerInvariant() == "dark" ? Theme.Dark : Theme.Light;
    }
}
