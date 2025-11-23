using System.Threading;
using System.Threading.Tasks;
using MOCHA.Models.Settings;

namespace MOCHA.Services.Settings;

/// <summary>
/// ユーザー設定の取得・保存を担うストア。
/// </summary>
public interface IUserPreferencesStore
{
    Task<UserPreferences?> GetAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(UserPreferences preferences, CancellationToken cancellationToken = default);
}
