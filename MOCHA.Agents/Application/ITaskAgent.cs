using MOCHA.Agents.Domain;

namespace MOCHA.Agents.Application;

/// <summary>
/// 個別エージェントの共通インターフェース
/// </summary>
public interface ITaskAgent
{
    /// <summary>エージェント名</summary>
    string Name { get; }
    /// <summary>エージェントの説明</summary>
    string Description { get; }

    /// <summary>
    /// 質問に対するエージェント実行
    /// </summary>
    /// <param name="question">問い合わせ内容</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>エージェント実行結果</returns>
    Task<AgentResult> ExecuteAsync(string question, CancellationToken cancellationToken = default);
}
