using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MOCHA.Agents.Application;
using MOCHA.Agents.Infrastructure.Clients;
using MOCHA.Agents.Infrastructure.Options;
using MOCHA.Agents.Infrastructure.Orchestration;

namespace MOCHA.Agents;

/// <summary>
/// DI 登録ヘルパー。
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddMochaAgents(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<LlmOptions>(configuration.GetSection("Llm"));
        services.AddSingleton<ILlmChatClientFactory, LlmChatClientFactory>();
        services.AddSingleton<IAgentOrchestrator, AgentFrameworkOrchestrator>();
        return services;
    }
}
