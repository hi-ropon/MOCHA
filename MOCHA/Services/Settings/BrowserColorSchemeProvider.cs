using System.Threading;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using MOCHA.Models.Settings;

namespace MOCHA.Services.Settings;

/// <summary>
/// ブラウザの prefers-color-scheme を取得するプロバイダ
/// </summary>
public sealed class BrowserColorSchemeProvider : IColorSchemeProvider
{
    private readonly IJSRuntime _jsRuntime;

    /// <summary>
    /// JS ランタイム注入による初期化
    /// </summary>
    /// <param name="jsRuntime">JS ランタイム</param>
    public BrowserColorSchemeProvider(IJSRuntime jsRuntime)
    {
        this._jsRuntime = jsRuntime;
    }

    /// <summary>
    /// ブラウザから推奨テーマを取得
    /// </summary>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>取得したテーマ</returns>
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
