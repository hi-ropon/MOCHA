using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Models.Settings;
using MOCHA.Services.Settings;

namespace MOCHA.Tests;

/// <summary>
/// ユーザー設定状態（テーマ切替）のふるまい検証テスト
/// </summary>
[TestClass]
public class UserPreferencesStateTests
{
    /// <summary>
    /// 未保存時の OS テーマ初期値採用確認
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
    /// 保存済みテーマ優先読み込み確認
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
    /// テーマ更新時の保存と通知確認
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

        /// <summary>
        /// テスト用ユーザープリファレンスストアフェイク
        /// </summary>
        /// <param name="preferences">事前設定済みプリファレンス</param>
        public FakeStore(UserPreferences? preferences = null)
        {
            _preferences = preferences;
        }

        public Theme? SavedTheme { get; private set; }

        /// <summary>
        /// 保存済みプリファレンス取得
        /// </summary>
        /// <param name="cancellationToken">キャンセル通知</param>
        /// <returns>保存済みプリファレンス</returns>
        public Task<UserPreferences?> GetAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_preferences);
        }

        /// <summary>
        /// プリファレンス保存
        /// </summary>
        /// <param name="prefs">保存対象プリファレンス</param>
        /// <param name="cancellationToken">キャンセル通知</param>
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

        /// <summary>
        /// テスト用カラースキームプロバイダフェイク
        /// </summary>
        /// <param name="preferred">既定テーマ</param>
        public FakeSchemeProvider(Theme preferred)
        {
            _preferred = preferred;
        }

        /// <summary>
        /// 優先テーマ取得
        /// </summary>
        /// <param name="cancellationToken">キャンセル通知</param>
        /// <returns>優先テーマ</returns>
        public Task<Theme> GetPreferredThemeAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_preferred);
        }
    }

    private sealed class FakeApplicator : IThemeApplicator
    {
        public Theme? LastAppliedTheme { get; private set; }

        /// <summary>
        /// テーマ適用
        /// </summary>
        /// <param name="theme">適用対象テーマ</param>
        /// <param name="cancellationToken">キャンセル通知</param>
        public Task ApplyAsync(Theme theme, CancellationToken cancellationToken = default)
        {
            LastAppliedTheme = theme;
            return Task.CompletedTask;
        }
    }
}
