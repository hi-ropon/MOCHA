using System.ComponentModel.DataAnnotations;

namespace MOCHA.Services.Auth;

/// <summary>
/// 開発用ユーザーエンティティ
/// </summary>
public sealed class DevUserEntity
{
    /// <summary>
    /// 識別子
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// メールアドレス
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// 表示名
    /// </summary>
    [MaxLength(200)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// パスワードハッシュ
    /// </summary>
    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// 作成日時
    /// </summary>
    public DateTime CreatedAt { get; set; }
}
