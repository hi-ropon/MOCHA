using Microsoft.Agents.CopilotStudio.Client.Discovery;

namespace MOCHA.Services.Copilot;

/// <summary>
/// Copilot Studio 接続設定（仮設定）。社内環境で上書きする想定。
/// </summary>
public sealed class CopilotStudioOptions
{
    public bool Enabled { get; set; }
    public string? SchemaName { get; set; }
    public string? EnvironmentId { get; set; }
    public string? DirectConnectUrl { get; set; }
    public PowerPlatformCloud Cloud { get; set; } = PowerPlatformCloud.Prod;
    public AgentType AgentType { get; set; } = AgentType.Published;
    public string? CustomPowerPlatformCloud { get; set; }
    public bool UseExperimentalEndpoint { get; set; }
    public bool EnableDiagnostics { get; set; }
    public string HttpClientName { get; set; } = "CopilotStudio";
    public string? AccessToken { get; set; }
}
