using System.Text.Json.Serialization;

namespace MOCHA.Agents.Infrastructure.Tools;

/// <summary>
/// PLCエージェント呼び出し用オプション
/// </summary>
public sealed class PlcAgentOptions
{
    /// <summary>ゲートウェイオプションJSON</summary>
    [JsonPropertyName("gatewayOptions")]
    public string? GatewayOptionsJson { get; init; }

    /// <summary>ユニットID（Guid文字列）</summary>
    [JsonPropertyName("plcUnitId")]
    public string? PlcUnitId { get; init; }

    /// <summary>ユニット名</summary>
    [JsonPropertyName("plcUnitName")]
    public string? PlcUnitName { get; init; }

    /// <summary>ファンクションブロックツールを有効化</summary>
    [JsonPropertyName("enableFunctionBlocks")]
    public bool EnableFunctionBlocks { get; init; } = true;

    /// <summary>備考/ヒント</summary>
    [JsonPropertyName("note")]
    public string? Note { get; init; }
}
