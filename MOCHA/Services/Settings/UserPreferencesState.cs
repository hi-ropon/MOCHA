using System;
using System.Threading;
using System.Threading.Tasks;
using MOCHA.Models.Settings;

namespace MOCHA.Services.Settings;

/// <summary>
/// ユーザー設定の状態を管理し、UI へ通知する。
/// </summary>
public sealed class UserPreferencesState
{
    private readonly IUserPreferencesStore _store;
    private readonly IColorSchemeProvider _schemeProvider;
    private readonly IThemeApplicator _themeApplicator;
    private bool _loaded;

    public UserPreferencesState(
        IUserPreferencesStore store,
        IColorSchemeProvider schemeProvider,
        IThemeApplicator themeApplicator)
    {
        _store = store;
        _schemeProvider = schemeProvider;
        _themeApplicator = themeApplicator;
    }

    public event Action? Changed;

    public UserPreferences Preferences { get; private set; } = UserPreferences.DefaultLight;

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

    public async Task UpdateThemeAsync(Theme theme, CancellationToken cancellationToken = default)
    {
        Preferences = Preferences with { Theme = theme };
        await _store.SaveAsync(Preferences, cancellationToken);
        await _themeApplicator.ApplyAsync(theme, cancellationToken);
        Changed?.Invoke();
    }
}
