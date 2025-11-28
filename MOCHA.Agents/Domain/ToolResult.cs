namespace MOCHA.Agents.Domain;

/// <summary>
/// モデルが要求したツール実行の結果。
/// </summary>
public sealed record ToolResult(string Name, string PayloadJson, bool Success, string? Error = null, double? LatencyMilliseconds = null);
