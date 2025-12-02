namespace MOCHA.Agents.Domain;

/// <summary>
/// モデルが要求したツール実行結果
/// </summary>
public sealed record ToolResult(string Name, string PayloadJson, bool Success, string? Error = null, double? LatencyMilliseconds = null);
