using System.ComponentModel.DataAnnotations;

namespace MOCHA.Models.Auth;

/// <summary>
/// ログイン入力値
/// </summary>
public sealed class DevLoginInput
{
    /// <summary>
    /// メールアドレス
    /// </summary>
    [Required(ErrorMessage = "メールアドレスは必須です")]
    [EmailAddress(ErrorMessage = "メールアドレスの形式が不正です")]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// パスワード
    /// </summary>
    [Required(ErrorMessage = "パスワードは必須です")]
    [MinLength(6, ErrorMessage = "パスワードは6文字以上にしてください")]
    public string Password { get; set; } = string.Empty;
}
