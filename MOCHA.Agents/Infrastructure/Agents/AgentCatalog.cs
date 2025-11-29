using MOCHA.Agents.Application;

namespace MOCHA.Agents.Infrastructure.Agents;

/// <summary>
/// シンプルなエージェントカタログ実装。
/// </summary>
public sealed class AgentCatalog : IAgentCatalog
{
    private readonly IReadOnlyDictionary<string, ITaskAgent> _agents;

    public AgentCatalog(IEnumerable<ITaskAgent> agents)
    {
        _agents = agents.ToDictionary(a => a.Name, StringComparer.OrdinalIgnoreCase);
    }

    public ITaskAgent? Find(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return _agents.TryGetValue(name, out var agent) ? agent : null;
    }

    public IReadOnlyCollection<ITaskAgent> List() => _agents.Values.ToList();
}
