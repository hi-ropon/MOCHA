using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MOCHA.Models.Auth;

namespace MOCHA.Services.Auth;

/// <summary>
/// メモリ上でユーザーロールを管理するシンプルなプロバイダー。
/// </summary>
public sealed class InMemoryUserRoleProvider : IUserRoleProvider
{
    private readonly ConcurrentDictionary<string, HashSet<UserRoleId>> _roles = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// ユーザーに付与されたロールを取得する。
    /// </summary>
    /// <param name="userId">ユーザーID。</param>
    /// <param name="cancellationToken">キャンセル通知。</param>
    /// <returns>ロールの一覧。</returns>
    public Task<IReadOnlyCollection<UserRoleId>> GetRolesAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (!_roles.TryGetValue(userId, out var set))
        {
            return Task.FromResult<IReadOnlyCollection<UserRoleId>>(Array.Empty<UserRoleId>());
        }

        lock (set)
        {
            return Task.FromResult<IReadOnlyCollection<UserRoleId>>(set.ToArray());
        }
    }

    /// <summary>
    /// ユーザーにロールを付与する。
    /// </summary>
    /// <param name="userId">ユーザーID。</param>
    /// <param name="role">付与するロール。</param>
    /// <param name="cancellationToken">キャンセル通知。</param>
    /// <returns>完了タスク。</returns>
    public Task AssignAsync(string userId, UserRoleId role, CancellationToken cancellationToken = default)
    {
        var set = _roles.GetOrAdd(userId, _ => new HashSet<UserRoleId>());
        lock (set)
        {
            set.Add(role);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// ユーザーからロールを削除する。
    /// </summary>
    /// <param name="userId">ユーザーID。</param>
    /// <param name="role">削除するロール。</param>
    /// <param name="cancellationToken">キャンセル通知。</param>
    /// <returns>完了タスク。</returns>
    public Task RemoveAsync(string userId, UserRoleId role, CancellationToken cancellationToken = default)
    {
        if (_roles.TryGetValue(userId, out var set))
        {
            lock (set)
            {
                set.Remove(role);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// ユーザーが指定ロールを保持しているか判定する。
    /// </summary>
    /// <param name="userId">ユーザーID。</param>
    /// <param name="role">確認するロール名。</param>
    /// <param name="cancellationToken">キャンセル通知。</param>
    /// <returns>保持していれば true。</returns>
    public Task<bool> IsInRoleAsync(string userId, string role, CancellationToken cancellationToken = default)
    {
        var roleId = UserRoleId.From(role);

        if (!_roles.TryGetValue(userId, out var set))
        {
            return Task.FromResult(false);
        }

        lock (set)
        {
            return Task.FromResult(set.Contains(roleId));
        }
    }
}
