using Microsoft.AspNetCore.Mvc;
using MOCHA.Models.Auth;
using MOCHA.Services.Auth;

namespace MOCHA.Controllers;

/// <summary>
/// ユーザーのロール付与・削除を行う API コントローラー
/// </summary>
[ApiController]
[Route("api/roles")]
public sealed class RolesController : ControllerBase
{
    private readonly IUserRoleProvider _roleProvider;

    /// <summary>
    /// ロールプロバイダー注入によるコントローラー初期化
    /// </summary>
    /// <param name="roleProvider">ロールプロバイダー</param>
    public RolesController(IUserRoleProvider roleProvider)
    {
        _roleProvider = roleProvider;
    }

    /// <summary>
    /// ユーザーのロール一覧取得
    /// </summary>
    /// <param name="userId">対象ユーザーID</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>ロール名のコレクション</returns>
    [HttpGet("{userId}")]
    public async Task<ActionResult<IEnumerable<string>>> GetRoles(string userId, CancellationToken cancellationToken)
    {
        if (!await RequireAdminAsync(cancellationToken))
        {
            return Forbid();
        }

        var roles = await _roleProvider.GetRolesAsync(userId, cancellationToken);
        return Ok(roles.Select(r => r.Value));
    }

    /// <summary>
    /// ユーザーへのロール付与
    /// </summary>
    /// <param name="request">変更リクエスト</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>処理結果</returns>
    [HttpPost("assign")]
    public async Task<IActionResult> Assign([FromBody] RoleChangeRequest request, CancellationToken cancellationToken)
    {
        if (!await RequireAdminAsync(cancellationToken))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.UserId) || string.IsNullOrWhiteSpace(request.Role))
        {
            return BadRequest("UserId と Role は必須です。");
        }

        await _roleProvider.AssignAsync(request.UserId, UserRoleId.From(request.Role), cancellationToken);
        return Ok();
    }

    /// <summary>
    /// ユーザーからのロール削除
    /// </summary>
    /// <param name="userId">対象ユーザーID</param>
    /// <param name="role">削除するロール</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>処理結果</returns>
    [HttpDelete("{userId}/{role}")]
    public async Task<IActionResult> Remove(string userId, string role, CancellationToken cancellationToken)
    {
        if (!await RequireAdminAsync(cancellationToken))
        {
            return Forbid();
        }

        await _roleProvider.RemoveAsync(userId, UserRoleId.From(role), cancellationToken);
        return Ok();
    }

    /// <summary>
    /// 呼び出しユーザーの管理者ロール判定
    /// </summary>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>管理者であれば true</returns>
    private async Task<bool> RequireAdminAsync(CancellationToken cancellationToken)
    {
        var userId = User.GetUserObjectId();
        if (userId is null)
        {
            return false;
        }

        return await _roleProvider.IsInRoleAsync(userId, UserRoleId.Predefined.Administrator.Value, cancellationToken);
    }
}

/// <summary>
/// ロール付与・削除用のリクエストボディ
/// </summary>
public sealed class RoleChangeRequest
{
    /// <summary>対象ユーザーID</summary>
    public string UserId { get; set; } = string.Empty;
    /// <summary>付与/削除するロール名</summary>
    public string Role { get; set; } = string.Empty;
}
