using System.Threading;
using System.Threading.Tasks;

namespace MOCHA.Factories;

/// <summary>
/// アプリ起動時のデータベース初期化を担うインターフェース。
/// </summary>
public interface IDatabaseInitializer
{
    /// <summary>
    /// スキーマの作成やマイグレーションを実行する。
    /// </summary>
    /// <param name="cancellationToken">キャンセル通知。</param>
    /// <returns>非同期タスク。</returns>
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
