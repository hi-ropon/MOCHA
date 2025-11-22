namespace MOCHA.Models.Auth;

/// <summary>
/// 開発用のフェイク認証設定。
/// </summary>
public sealed class FakeAuthOptions
{
    /// <summary>
    /// フェイク認証を有効にするかどうか。
    /// </summary>
    public bool Enabled { get; set; }
    /// <summary>
    /// フェイク認証時に使用するユーザーID。
    /// </summary>
    public string UserId { get; set; } = "dev-user";
    /// <summary>
    /// フェイク認証時の表示名。
    /// </summary>
    public string Name { get; set; } = "Developer";
}
