namespace MOCHA.Agents.Infrastructure.Options;

/// <summary>
/// LLM 接続設定
/// </summary>
public sealed class LlmOptions
{
    public ProviderKind Provider { get; set; } = ProviderKind.OpenAI;
    public string? Endpoint { get; set; }
    public string? ApiKey { get; set; }
    public string? ModelOrDeployment { get; set; }
    public string? Instructions { get; set; }
    public string? AgentName { get; set; }
    public string? AgentDescription { get; set; }
}
