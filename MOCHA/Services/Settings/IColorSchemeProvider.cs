using System.Threading;
using System.Threading.Tasks;
using MOCHA.Models.Settings;

namespace MOCHA.Services.Settings;

/// <summary>
/// クライアント環境から推奨カラースキームを取得するプロバイダー
/// </summary>
public interface IColorSchemeProvider
{
    /// <summary>
    /// 推奨テーマ取得
    /// </summary>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>推奨テーマ</returns>
    Task<Theme> GetPreferredThemeAsync(CancellationToken cancellationToken = default);
}
