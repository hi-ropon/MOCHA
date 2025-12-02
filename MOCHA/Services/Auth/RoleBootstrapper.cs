using Microsoft.Extensions.Options;
using MOCHA.Models.Auth;

namespace MOCHA.Services.Auth;

/// <summary>
/// 設定されたユーザーに管理者ロールを付与する初期化処理
/// </summary>
internal sealed class RoleBootstrapper
{
    private readonly IUserRoleProvider _roleProvider;
    private readonly RoleBootstrapOptions _options;

    /// <summary>
    /// ロールプロバイダーとオプションを受け取り初期化する
    /// </summary>
    /// <param name="roleProvider">ロールプロバイダー</param>
    /// <param name="options">ブートストラップ設定</param>
    public RoleBootstrapper(IUserRoleProvider roleProvider, IOptions<RoleBootstrapOptions> options)
    {
        _roleProvider = roleProvider;
        _options = options.Value;
    }

    /// <summary>
    /// 設定されたユーザー全員への管理者ロール付与
    /// </summary>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>非同期タスク</returns>
    public async Task EnsureAdminRolesAsync(CancellationToken cancellationToken = default)
    {
        if (_options.AdminUserIds is null || _options.AdminUserIds.Count == 0)
        {
            return;
        }

        foreach (var userId in _options.AdminUserIds)
        {
            await _roleProvider.AssignAsync(userId, UserRoleId.Predefined.Administrator, cancellationToken);
        }
    }
}
