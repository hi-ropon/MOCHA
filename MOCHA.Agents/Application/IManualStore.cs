using MOCHA.Agents.Domain;

namespace MOCHA.Agents.Application;

/// <summary>
/// マニュアル検索・読取のポート
/// </summary>
public interface IManualStore
{
    /// <summary>
    /// マニュアル検索
    /// </summary>
    /// <param name="agentName">エージェント名</param>
    /// <param name="query">検索クエリ</param>
    /// <param name="context">ユーザーや装置エージェントのコンテキスト</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>検索結果</returns>
    Task<IReadOnlyList<ManualHit>> SearchAsync(
        string agentName,
        string query,
        ManualSearchContext? context = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// マニュアル内容読取
    /// </summary>
    /// <param name="agentName">エージェント名</param>
    /// <param name="relativePath">マニュアルの相対パス</param>
    /// <param name="maxBytes">読み取り上限バイト数</param>
    /// <param name="context">ユーザーや装置エージェントのコンテキスト</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>読み取った内容</returns>
    Task<ManualContent?> ReadAsync(
        string agentName,
        string relativePath,
        int? maxBytes = null,
        ManualSearchContext? context = null,
        CancellationToken cancellationToken = default);
}
