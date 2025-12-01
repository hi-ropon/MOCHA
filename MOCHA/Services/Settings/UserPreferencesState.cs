using System;
using System.Threading;
using System.Threading.Tasks;
using MOCHA.Models.Settings;

namespace MOCHA.Services.Settings;

/// <summary>
/// ユーザー設定状態管理と UI への通知
/// </summary>
public sealed class UserPreferencesState
{
    private readonly IUserPreferencesStore _store;
    private readonly IColorSchemeProvider _schemeProvider;
    private readonly IThemeApplicator _themeApplicator;
    private bool _loaded;

    /// <summary>
    /// 依存関係注入による初期化
    /// </summary>
    /// <param name="store">プリファレンスストア</param>
    /// <param name="schemeProvider">カラースキームプロバイダー</param>
    /// <param name="themeApplicator">テーマ適用器</param>
    public UserPreferencesState(
        IUserPreferencesStore store,
        IColorSchemeProvider schemeProvider,
        IThemeApplicator themeApplicator)
    {
        _store = store;
        _schemeProvider = schemeProvider;
        _themeApplicator = themeApplicator;
    }

    /// <summary>状態変更通知イベント</summary>
    public event Action? Changed;

    /// <summary>ユーザープリファレンス</summary>
    public UserPreferences Preferences { get; private set; } = UserPreferences.DefaultLight;

    /// <summary>
    /// プリファレンス読み込みと適用
    /// </summary>
    /// <param name="cancellationToken">キャンセル通知</param>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (_loaded)
        {
            return;
        }

        var saved = await _store.GetAsync(cancellationToken);
        if (saved is null)
        {
            var preferred = await _schemeProvider.GetPreferredThemeAsync(cancellationToken);
            Preferences = new UserPreferences(preferred);
        }
        else
        {
            Preferences = saved;
        }

        _loaded = true;
        await _themeApplicator.ApplyAsync(Preferences.Theme, cancellationToken);
        Changed?.Invoke();
    }

    /// <summary>
    /// テーマ更新と永続化・適用
    /// </summary>
    /// <param name="theme">更新後テーマ</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    public async Task UpdateThemeAsync(Theme theme, CancellationToken cancellationToken = default)
    {
        Preferences = Preferences with { Theme = theme };
        await _store.SaveAsync(Preferences, cancellationToken);
        await _themeApplicator.ApplyAsync(theme, cancellationToken);
        Changed?.Invoke();
    }
}
