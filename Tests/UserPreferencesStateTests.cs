using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Models.Settings;
using MOCHA.Services.Settings;

namespace MOCHA.Tests;

/// <summary>
/// ユーザー設定状態（テーマ切替）のふるまいを検証するテスト。
/// </summary>
[TestClass]
public class UserPreferencesStateTests
{
    /// <summary>
    /// ストアに保存がなければ OS のテーマ設定を初期値として採用する。
    /// </summary>
    [TestMethod]
    public async Task 初回ロードでローカル未設定ならOSテーマを反映する()
    {
        var store = new FakeStore();
        var provider = new FakeSchemeProvider(Theme.Dark);
        var applicator = new FakeApplicator();
        var state = new UserPreferencesState(store, provider, applicator);

        await state.LoadAsync();

        Assert.AreEqual(Theme.Dark, state.Preferences.Theme);
        Assert.AreEqual(Theme.Dark, applicator.LastAppliedTheme);
    }

    /// <summary>
    /// 保存済みテーマがあればそれを優先的に読み込む。
    /// </summary>
    [TestMethod]
    public async Task 保存済みテーマをロードで再現する()
    {
        var store = new FakeStore(new UserPreferences(Theme.Light));
        var provider = new FakeSchemeProvider(Theme.Dark);
        var applicator = new FakeApplicator();
        var state = new UserPreferencesState(store, provider, applicator);

        await state.LoadAsync();

        Assert.AreEqual(Theme.Light, state.Preferences.Theme);
        Assert.AreEqual(Theme.Light, applicator.LastAppliedTheme);
    }

    /// <summary>
    /// テーマ更新時にストアへ保存され、Changed イベントが発火する。
    /// </summary>
    [TestMethod]
    public async Task テーマ更新で保存と通知が行われる()
    {
        var store = new FakeStore();
        var provider = new FakeSchemeProvider(Theme.Light);
        var applicator = new FakeApplicator();
        var state = new UserPreferencesState(store, provider, applicator);
        var changed = false;
        state.Changed += () => changed = true;
        await state.LoadAsync();

        await state.UpdateThemeAsync(Theme.Dark);

        Assert.AreEqual(Theme.Dark, state.Preferences.Theme);
        Assert.AreEqual(Theme.Dark, store.SavedTheme);
        Assert.AreEqual(Theme.Dark, applicator.LastAppliedTheme);
        Assert.IsTrue(changed);
    }

    private sealed class FakeStore : IUserPreferencesStore
    {
        private UserPreferences? _preferences;

        public FakeStore(UserPreferences? preferences = null)
        {
            _preferences = preferences;
        }

        public Theme? SavedTheme { get; private set; }

        public Task<UserPreferences?> GetAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_preferences);
        }

        public Task SaveAsync(UserPreferences prefs, CancellationToken cancellationToken = default)
        {
            _preferences = prefs;
            SavedTheme = prefs.Theme;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSchemeProvider : IColorSchemeProvider
    {
        private readonly Theme _preferred;

        public FakeSchemeProvider(Theme preferred)
        {
            _preferred = preferred;
        }

        public Task<Theme> GetPreferredThemeAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_preferred);
        }
    }

    private sealed class FakeApplicator : IThemeApplicator
    {
        public Theme? LastAppliedTheme { get; private set; }

        public Task ApplyAsync(Theme theme, CancellationToken cancellationToken = default)
        {
            LastAppliedTheme = theme;
            return Task.CompletedTask;
        }
    }
}
