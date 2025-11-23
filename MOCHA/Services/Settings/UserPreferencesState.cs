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
    private readonly IUserPreferencesStore store;
    private readonly IColorSchemeProvider schemeProvider;
    private readonly IThemeApplicator themeApplicator;
    private bool loaded;

    public UserPreferencesState(
        IUserPreferencesStore store,
        IColorSchemeProvider schemeProvider,
        IThemeApplicator themeApplicator)
    {
        this.store = store;
        this.schemeProvider = schemeProvider;
        this.themeApplicator = themeApplicator;
    }

    public event Action? Changed;

    public UserPreferences Preferences { get; private set; } = UserPreferences.DefaultLight;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (loaded)
        {
            return;
        }

        var saved = await store.GetAsync(cancellationToken);
        if (saved is null)
        {
            var preferred = await schemeProvider.GetPreferredThemeAsync(cancellationToken);
            Preferences = new UserPreferences(preferred);
        }
        else
        {
            Preferences = saved;
        }

        loaded = true;
        await themeApplicator.ApplyAsync(Preferences.Theme, cancellationToken);
        Changed?.Invoke();
    }

    public async Task UpdateThemeAsync(Theme theme, CancellationToken cancellationToken = default)
    {
        Preferences = Preferences with { Theme = theme };
        await store.SaveAsync(Preferences, cancellationToken);
        await themeApplicator.ApplyAsync(theme, cancellationToken);
        Changed?.Invoke();
    }
}
