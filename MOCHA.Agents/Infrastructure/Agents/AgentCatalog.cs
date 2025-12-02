using MOCHA.Agents.Application;

namespace MOCHA.Agents.Infrastructure.Agents;

/// <summary>
/// シンプルなエージェントカタログ実装
/// </summary>
public sealed class AgentCatalog : IAgentCatalog
{
    private readonly IReadOnlyDictionary<string, ITaskAgent> _agents;

    /// <summary>
    /// エージェント集合による初期化
    /// </summary>
    /// <param name="agents">登録エージェント一覧</param>
    public AgentCatalog(IEnumerable<ITaskAgent> agents)
    {
        _agents = agents.ToDictionary(a => a.Name, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// エージェント検索
    /// </summary>
    /// <param name="name">エージェント名</param>
    /// <returns>見つかったエージェント</returns>
    public ITaskAgent? Find(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return _agents.TryGetValue(name, out var agent) ? agent : null;
    }

    /// <summary>
    /// エージェント一覧取得
    /// </summary>
    /// <returns>登録エージェント一覧</returns>
    public IReadOnlyCollection<ITaskAgent> List() => _agents.Values.ToList();
}
