using MOCHA.Agents.Domain;

namespace MOCHA.Agents.Application;

/// <summary>
/// ユーザー入力を受け取りエージェント応答を生成するポート
/// </summary>
public interface IAgentOrchestrator
{
    /// <summary>
    /// エージェント応答生成
    /// </summary>
    /// <param name="userTurn">ユーザーターン</param>
    /// <param name="context">チャットコンテキスト</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>ストリーミングイベント列</returns>
    Task<IAsyncEnumerable<AgentEvent>> ReplyAsync(ChatTurn userTurn, ChatContext context, CancellationToken cancellationToken = default);
}
