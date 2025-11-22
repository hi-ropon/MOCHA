using Microsoft.Agents.CopilotStudio.Client.Discovery;

namespace MOCHA.Services.Copilot;

/// <summary>
/// Copilot Studio 接続設定（仮設定）。社内環境で上書きする想定。
/// </summary>
public sealed class CopilotStudioOptions
{
    /// <summary>
    /// Copilot Studio 連携を有効にするかどうか。
    /// </summary>
    public bool Enabled { get; set; }
    /// <summary>
    /// Copilot Studio のスキーマ名。
    /// </summary>
    public string? SchemaName { get; set; }
    /// <summary>
    /// 環境ID。
    /// </summary>
    public string? EnvironmentId { get; set; }
    /// <summary>
    /// 直接接続URL（必要に応じて指定）。
    /// </summary>
    public string? DirectConnectUrl { get; set; }
    /// <summary>
    /// 接続先クラウド。
    /// </summary>
    public PowerPlatformCloud Cloud { get; set; } = PowerPlatformCloud.Prod;
    /// <summary>
    /// 接続するエージェント種別（公開/編集）。
    /// </summary>
    public AgentType AgentType { get; set; } = AgentType.Published;
    /// <summary>
    /// カスタムクラウド名（必要に応じて指定）。
    /// </summary>
    public string? CustomPowerPlatformCloud { get; set; }
    /// <summary>
    /// 実験的エンドポイントの利用可否。
    /// </summary>
    public bool UseExperimentalEndpoint { get; set; }
    /// <summary>
    /// 診断情報を有効にするか。
    /// </summary>
    public bool EnableDiagnostics { get; set; }
    /// <summary>
    /// 利用する HTTP クライアント名。
    /// </summary>
    public string HttpClientName { get; set; } = "CopilotStudio";
    /// <summary>
    /// アクセストークン（固定値が必要な場合のみ）。
    /// </summary>
    public string? AccessToken { get; set; }
}
