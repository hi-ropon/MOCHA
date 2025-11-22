using System.Collections.Generic;

namespace MOCHA.Models.Auth;

/// <summary>
/// 既定管理者のユーザーIDを構成するオプション。
/// </summary>
public sealed class RoleBootstrapOptions
{
    /// <summary>
    /// 管理者ロールを付与するユーザーIDの一覧。
    /// </summary>
    public List<string> AdminUserIds { get; init; } = new();
}
