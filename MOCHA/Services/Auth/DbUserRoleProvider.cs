using Microsoft.EntityFrameworkCore;
using MOCHA.Data;
using MOCHA.Models.Auth;

namespace MOCHA.Services.Auth;

/// <summary>
/// データベースにロール割り当てを保存するプロバイダー
/// </summary>
internal sealed class DbUserRoleProvider : IUserRoleProvider
{
    private readonly ChatDbContext _db;

    /// <summary>
    /// DbContext を受け取りプロバイダーを初期化する
    /// </summary>
    /// <param name="db">チャット用 DbContext</param>
    public DbUserRoleProvider(ChatDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// ユーザーに付与されているロール取得
    /// </summary>
    /// <param name="userId">ユーザーID</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>ロール一覧</returns>
    public async Task<IReadOnlyCollection<UserRoleId>> GetRolesAsync(string userId, CancellationToken cancellationToken = default)
    {
        var roles = await _db.UserRoles
            .Where(r => r.UserId == userId)
            .Select(r => r.Role)
            .ToListAsync(cancellationToken);

        return roles.Select(UserRoleId.From).ToArray();
    }

    /// <summary>
    /// ユーザーへのロール追加（既存の場合は無視）
    /// </summary>
    /// <param name="userId">ユーザーID</param>
    /// <param name="role">付与するロール</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    public async Task AssignAsync(string userId, UserRoleId role, CancellationToken cancellationToken = default)
    {
        var normalized = role.Value;
        var exists = await _db.UserRoles.AnyAsync(
            r => r.UserId == userId && r.Role == normalized,
            cancellationToken);

        if (exists)
        {
            return;
        }

        _db.UserRoles.Add(new UserRoleEntity
        {
            UserId = userId,
            Role = normalized,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// ユーザーから指定ロールを削除する
    /// </summary>
    /// <param name="userId">ユーザーID</param>
    /// <param name="role">削除するロール</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    public async Task RemoveAsync(string userId, UserRoleId role, CancellationToken cancellationToken = default)
    {
        var normalized = role.Value;
        var entities = await _db.UserRoles
            .Where(r => r.UserId == userId && r.Role == normalized)
            .ToListAsync(cancellationToken);

        if (entities.Count == 0)
        {
            return;
        }

        _db.UserRoles.RemoveRange(entities);
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// ユーザーが指定ロールを保持しているか確認する
    /// </summary>
    /// <param name="userId">ユーザーID</param>
    /// <param name="role">ロール名</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>保持していれば true</returns>
    public async Task<bool> IsInRoleAsync(string userId, string role, CancellationToken cancellationToken = default)
    {
        var normalized = UserRoleId.From(role).Value;
        return await _db.UserRoles.AnyAsync(
            r => r.UserId == userId && r.Role == normalized,
            cancellationToken);
    }
}
