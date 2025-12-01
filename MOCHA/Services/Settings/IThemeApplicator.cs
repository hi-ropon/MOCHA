using System.Threading;
using System.Threading.Tasks;
using MOCHA.Models.Settings;

namespace MOCHA.Services.Settings;

/// <summary>
/// テーマを実際の UI に適用するコンポーネント
/// </summary>
public interface IThemeApplicator
{
    /// <summary>
    /// テーマ適用
    /// </summary>
    /// <param name="theme">適用するテーマ</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    Task ApplyAsync(Theme theme, CancellationToken cancellationToken = default);
}
