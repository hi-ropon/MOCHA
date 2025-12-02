using System.Threading;
using System.Threading.Tasks;
using MOCHA.Models.Settings;

namespace MOCHA.Services.Settings;

/// <summary>
/// ユーザー設定の取得・保存を担うストア
/// </summary>
public interface IUserPreferencesStore
{
    /// <summary>
    /// 保存済みユーザー設定取得
    /// </summary>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>取得したプリファレンス</returns>
    Task<UserPreferences?> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// ユーザー設定保存
    /// </summary>
    /// <param name="preferences">保存するプリファレンス</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    Task SaveAsync(UserPreferences preferences, CancellationToken cancellationToken = default);
}
