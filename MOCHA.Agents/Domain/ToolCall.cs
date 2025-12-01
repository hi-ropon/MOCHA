namespace MOCHA.Agents.Domain;

/// <summary>
/// モデルが要求したツール呼び出し
/// </summary>
public sealed record ToolCall(string Name, string ArgumentsJson);
