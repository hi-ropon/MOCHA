using System.Threading;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using MOCHA.Models.Settings;

namespace MOCHA.Services.Settings;

/// <summary>
/// HTML の data-theme 属性へのテーマ適用器
/// </summary>
public sealed class DomThemeApplicator : IThemeApplicator
{
    private readonly IJSRuntime _jsRuntime;

    /// <summary>
    /// JS ランタイム注入による初期化
    /// </summary>
    /// <param name="jsRuntime">JS ランタイム</param>
    public DomThemeApplicator(IJSRuntime jsRuntime)
    {
        this._jsRuntime = jsRuntime;
    }

    /// <summary>
    /// テーマ適用処理
    /// </summary>
    /// <param name="theme">適用するテーマ</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>適用タスク</returns>
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
