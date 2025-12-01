using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MOCHA.Agents.Application;
using MOCHA.Agents.Infrastructure.Agents;
using MOCHA.Agents.Infrastructure.Clients;
using MOCHA.Agents.Infrastructure.Manuals;
using MOCHA.Agents.Infrastructure.Options;
using MOCHA.Agents.Infrastructure.Orchestration;
using MOCHA.Agents.Infrastructure.Tools;

namespace MOCHA.Agents;

/// <summary>
/// DI 登録ヘルパー
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// MOCHA エージェント依存性登録拡張
    /// </summary>
    /// <param name="services">サービスコレクション</param>
    /// <param name="configuration">構成設定</param>
    /// <returns>登録済みサービスコレクション</returns>
    public static IServiceCollection AddMochaAgents(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<LlmOptions>(configuration.GetSection("Llm"));
        services.Configure<ManualStoreOptions>(configuration.GetSection("Manuals"));
        services.AddSingleton<ILlmChatClientFactory, LlmChatClientFactory>();
        services.AddSingleton<IManualStore, FileManualStore>();
        services.AddSingleton<ManualToolset>();
        services.AddSingleton<ManualAgentTool>();
        services.AddSingleton<PlcAgentTool>();
        services.AddSingleton<OrganizerToolset>();
        services.AddSingleton<IAgentOrchestrator, AgentFrameworkOrchestrator>();

        services.AddSingleton<ITaskAgent, PlcTaskAgent>();
        services.AddSingleton<ITaskAgent, IaiTaskAgent>();
        services.AddSingleton<ITaskAgent, OrientalTaskAgent>();
        services.AddSingleton<IAgentCatalog, AgentCatalog>();
        return services;
    }
}
