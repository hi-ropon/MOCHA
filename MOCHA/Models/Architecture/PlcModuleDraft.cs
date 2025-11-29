namespace MOCHA.Models.Architecture;

/// <summary>
/// モジュール登録用の入力値
/// </summary>
public sealed class PlcModuleDraft
{
    public string Name { get; init; } = string.Empty;
    public string? Specification { get; init; }
}
