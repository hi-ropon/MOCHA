using MOCHA.Agents.Domain;

namespace MOCHA.Agents.Application;

/// <summary>
/// 複数エージェントを管理するカタログ。
/// </summary>
public interface IAgentCatalog
{
    IReadOnlyCollection<ITaskAgent> List();
    ITaskAgent? Find(string name);
}
