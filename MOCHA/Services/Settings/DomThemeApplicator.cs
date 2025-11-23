using System.Threading;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using MOCHA.Models.Settings;

namespace MOCHA.Services.Settings;

/// <summary>
/// HTML の data-theme 属性へテーマを適用する。
/// </summary>
public sealed class DomThemeApplicator : IThemeApplicator
{
    private readonly IJSRuntime _jsRuntime;

    public DomThemeApplicator(IJSRuntime jsRuntime)
    {
        this._jsRuntime = jsRuntime;
    }

    public Task ApplyAsync(Theme theme, CancellationToken cancellationToken = default)
    {
        var value = theme == Theme.Dark ? "dark" : "light";
        try
        {
            return _jsRuntime.InvokeVoidAsync("mochaPreferences.applyTheme", cancellationToken, value).AsTask();
        }
        catch (JSException)
        {
            return Task.CompletedTask;
        }
    }
}
