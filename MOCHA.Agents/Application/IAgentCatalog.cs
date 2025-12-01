using MOCHA.Agents.Domain;

namespace MOCHA.Agents.Application;

/// <summary>
/// 複数エージェントを管理するカタログ
/// </summary>
public interface IAgentCatalog
{
    /// <summary>
    /// 登録エージェント一覧取得
    /// </summary>
    /// <returns>登録済みエージェント</returns>
    IReadOnlyCollection<ITaskAgent> List();
    /// <summary>
    /// エージェント検索
    /// </summary>
    /// <param name="name">エージェント名</param>
    /// <returns>見つかったエージェント</returns>
    ITaskAgent? Find(string name);
}
