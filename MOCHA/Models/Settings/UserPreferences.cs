namespace MOCHA.Models.Settings;

/// <summary>
/// ユーザーの UI 設定（拡張想定）。
/// </summary>
/// <param name="Theme">テーマ。</param>
public sealed record UserPreferences(Theme Theme)
{
    public static UserPreferences DefaultLight { get; } = new(Theme.Light);
}
