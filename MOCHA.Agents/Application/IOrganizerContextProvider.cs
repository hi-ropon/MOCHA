using System.Threading;
using System.Threading.Tasks;

namespace MOCHA.Agents.Application;

/// <summary>
/// Organizer 用コンテキストの組み立てを行うプロバイダー
/// </summary>
public interface IOrganizerContextProvider
{
    /// <summary>
    /// ユーザーとエージェントに紐づくコンテキストを構築
    /// </summary>
    /// <param name="userId">ユーザーID</param>
    /// <param name="agentNumber">装置エージェント番号</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>コンテキスト</returns>
    Task<OrganizerContext> BuildAsync(string? userId, string? agentNumber, CancellationToken cancellationToken = default);
}
