using System.Threading;
using System.Threading.Tasks;
using MOCHA.Models.Settings;

namespace MOCHA.Services.Settings;

/// <summary>
/// テーマを実際の UI に適用する。
/// </summary>
public interface IThemeApplicator
{
    Task ApplyAsync(Theme theme, CancellationToken cancellationToken = default);
}
