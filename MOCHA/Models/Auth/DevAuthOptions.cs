namespace MOCHA.Models.Auth;

/// <summary>
/// 開発用クッキー認証の設定値
/// </summary>
public sealed class DevAuthOptions
{
    /// <summary>
    /// 開発用認証を有効にするかどうか
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// ログインページのパス
    /// </summary>
    public string LoginPath { get; set; } = "/login";

    /// <summary>
    /// ログアウトエンドポイントのパス
    /// </summary>
    public string LogoutPath { get; set; } = "/logout";

    /// <summary>
    /// アクセス拒否ページのパス
    /// </summary>
    public string AccessDeniedPath { get; set; } = "/denied";

    /// <summary>
    /// クッキー名
    /// </summary>
    public string CookieName { get; set; } = "mocha.dev.auth";

    /// <summary>
    /// 認証クッキーの有効期間（時）
    /// </summary>
    public int ExpireHours { get; set; } = 8;
}
