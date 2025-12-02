namespace MOCHA.Models.Architecture;

/// <summary>
/// モジュール登録用の入力値
/// </summary>
public sealed class PlcModuleDraft
{
    /// <summary>モジュール名</summary>
    public string Name { get; init; } = string.Empty;
    /// <summary>仕様</summary>
    public string? Specification { get; init; }
}
