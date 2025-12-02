using System.Threading;
using System.Threading.Tasks;

namespace MOCHA.Factories;

/// <summary>
/// アプリ起動時のデータベース初期化インターフェース
/// </summary>
public interface IDatabaseInitializer
{
    /// <summary>
    /// スキーマ作成やマイグレーション実行
    /// </summary>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>非同期タスク</returns>
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
