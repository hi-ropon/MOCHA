using Microsoft.Extensions.AI;

namespace MOCHA.Agents.Infrastructure.Clients;

/// <summary>
/// IChatClient をプロバイダーごとに生成するファクトリ
/// </summary>
public interface ILlmChatClientFactory
{
    /// <summary>
    /// チャットクライアント生成
    /// </summary>
    /// <returns>生成したクライアント</returns>
    IChatClient Create();
}
