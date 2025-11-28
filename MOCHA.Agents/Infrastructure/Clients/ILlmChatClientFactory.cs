using Microsoft.Extensions.AI;

namespace MOCHA.Agents.Infrastructure.Clients;

/// <summary>
/// IChatClient をプロバイダーごとに生成するファクトリ。
/// </summary>
public interface ILlmChatClientFactory
{
    IChatClient Create();
}
