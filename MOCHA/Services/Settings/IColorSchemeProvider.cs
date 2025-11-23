using System.Threading;
using System.Threading.Tasks;
using MOCHA.Models.Settings;

namespace MOCHA.Services.Settings;

/// <summary>
/// クライアント環境から推奨カラースキームを取得する。
/// </summary>
public interface IColorSchemeProvider
{
    Task<Theme> GetPreferredThemeAsync(CancellationToken cancellationToken = default);
}
