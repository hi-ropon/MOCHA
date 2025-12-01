using System;

namespace MOCHA.Services.Auth;

/// <summary>
/// ユーザーに割り当てたロールの永続化エンティティ
/// </summary>
internal sealed class UserRoleEntity
{
    /// <summary>
    /// 主キー
    /// </summary>
    public int Id { get; set; }
    /// <summary>
    /// ユーザーID
    /// </summary>
    public string UserId { get; set; } = default!;
    /// <summary>
    /// 付与したロール名
    /// </summary>
    public string Role { get; set; } = default!;
    /// <summary>
    /// 付与日時
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
}
