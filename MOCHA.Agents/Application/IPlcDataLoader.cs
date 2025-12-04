using System.Threading;
using System.Threading.Tasks;

namespace MOCHA.Agents.Application;

/// <summary>
/// PLC用データストアをコンテキストに合わせて準備するローダー
/// </summary>
public interface IPlcDataLoader
{
    /// <summary>
    /// 対象ユーザーとエージェント番号でストアをロード
    /// </summary>
    /// <param name="userId">ユーザーID</param>
    /// <param name="agentNumber">エージェント番号</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    Task LoadAsync(string? userId, string? agentNumber, CancellationToken cancellationToken = default);
}
