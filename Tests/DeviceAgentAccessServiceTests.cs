using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Models.Agents;
using MOCHA.Models.Auth;
using MOCHA.Services.Agents;
using MOCHA.Services.Auth;

namespace MOCHA.Tests;

/// <summary>
/// DeviceAgentAccessService の権限制御テスト
/// </summary>
[TestClass]
public class DeviceAgentAccessServiceTests
{
    /// <summary>
    /// 管理者ロールは割り付けに関わらず全エージェントを取得する
    /// </summary>
    [TestMethod]
    public async Task GetAvailableAgentsAsync_管理者ロールは全件取得する()
    {
        var agents = new[]
        {
            new DeviceAgentProfile("A-01", "ライン1", DateTimeOffset.UtcNow),
            new DeviceAgentProfile("A-02", "ライン2", DateTimeOffset.UtcNow)
        };
        var agentRepo = new InMemoryAgentRepository(agents);
        var permissionRepo = new InMemoryPermissionRepository();
        permissionRepo.Replace("user-1", new[] { "A-01" });
        var roleProvider = new InMemoryRoleProvider(new Dictionary<string, IReadOnlyCollection<UserRoleId>>
        {
            ["user-1"] = new[] { UserRoleId.Predefined.Administrator }
        });
        var service = new DeviceAgentAccessService(agentRepo, permissionRepo, roleProvider);

        var result = await service.GetAvailableAgentsAsync("user-1");

        Assert.AreEqual(2, result.Count);
        CollectionAssert.AreEquivalent(
            new[] { "A-01", "A-02" },
            result.Select(x => x.Number).ToArray());
    }

    /// <summary>
    /// Developer ロールも全エージェントを取得できる
    /// </summary>
    [TestMethod]
    public async Task GetAvailableAgentsAsync_Developerロールも全件取得する()
    {
        var agents = new[]
        {
            new DeviceAgentProfile("A-01", "ライン1", DateTimeOffset.UtcNow),
            new DeviceAgentProfile("A-02", "ライン2", DateTimeOffset.UtcNow)
        };
        var agentRepo = new InMemoryAgentRepository(agents);
        var permissionRepo = new InMemoryPermissionRepository();
        var roleProvider = new InMemoryRoleProvider(new Dictionary<string, IReadOnlyCollection<UserRoleId>>
        {
            ["user-dev"] = new[] { UserRoleId.Predefined.Developer }
        });
        var service = new DeviceAgentAccessService(agentRepo, permissionRepo, roleProvider);

        var result = await service.GetAvailableAgentsAsync("user-dev");

        Assert.AreEqual(2, result.Count);
    }

    /// <summary>
    /// 割り付けがある一般ユーザーには指定されたエージェントのみ返す
    /// </summary>
    [TestMethod]
    public async Task GetAvailableAgentsAsync_割り付けされたエージェントのみ返す()
    {
        var agents = new[]
        {
            new DeviceAgentProfile("A-01", "ライン1", DateTimeOffset.UtcNow),
            new DeviceAgentProfile("A-02", "ライン2", DateTimeOffset.UtcNow)
        };
        var agentRepo = new InMemoryAgentRepository(agents);
        var permissionRepo = new InMemoryPermissionRepository();
        permissionRepo.Replace("user-1", new[] { "A-02" });
        var roleProvider = new InMemoryRoleProvider(new Dictionary<string, IReadOnlyCollection<UserRoleId>>());
        var service = new DeviceAgentAccessService(agentRepo, permissionRepo, roleProvider);

        var result = await service.GetAvailableAgentsAsync("user-1");

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("A-02", result[0].Number);
    }

    /// <summary>
    /// 割り付け更新は既存のエントリを置き換える
    /// </summary>
    [TestMethod]
    public async Task UpdateAssignmentsAsync_割り付けを置き換える()
    {
        var agents = new[]
        {
            new DeviceAgentProfile("A-01", "ライン1", DateTimeOffset.UtcNow),
            new DeviceAgentProfile("A-02", "ライン2", DateTimeOffset.UtcNow),
            new DeviceAgentProfile("A-03", "ライン3", DateTimeOffset.UtcNow)
        };
        var agentRepo = new InMemoryAgentRepository(agents);
        var permissionRepo = new InMemoryPermissionRepository();
        permissionRepo.Replace("user-1", new[] { "A-01", "A-02" });
        var roleProvider = new InMemoryRoleProvider(new Dictionary<string, IReadOnlyCollection<UserRoleId>>());
        var service = new DeviceAgentAccessService(agentRepo, permissionRepo, roleProvider);

        await service.UpdateAssignmentsAsync("user-1", new[] { "A-03" });
        var result = await service.GetAssignmentsAsync("user-1");

        CollectionAssert.AreEquivalent(new[] { "A-03" }, (System.Collections.ICollection)result);
    }

    /// <summary>
    /// メモリ上のエージェントリポジトリ
    /// </summary>
    private sealed class InMemoryAgentRepository : IDeviceAgentRepository
    {
        private readonly List<DeviceAgentProfile> _agents;

        /// <summary>
        /// 初期エージェント集合を受け取るコンストラクター
        /// </summary>
        /// <param name="agents">初期エージェント一覧</param>
        public InMemoryAgentRepository(IEnumerable<DeviceAgentProfile> agents)
        {
            _agents = agents.ToList();
        }

        /// <summary>
        /// 全エージェント取得
        /// </summary>
        /// <param name="cancellationToken">キャンセル通知</param>
        /// <returns>エージェント一覧</returns>
        public Task<IReadOnlyList<DeviceAgentProfile>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<DeviceAgentProfile>>(_agents.ToList());
        }

        /// <summary>
        /// 番号指定でエージェント取得
        /// </summary>
        /// <param name="agentNumbers">対象番号</param>
        /// <param name="cancellationToken">キャンセル通知</param>
        /// <returns>該当エージェント一覧</returns>
        public Task<IReadOnlyList<DeviceAgentProfile>> GetByNumbersAsync(IEnumerable<string> agentNumbers, CancellationToken cancellationToken = default)
        {
            var numbers = new HashSet<string>(agentNumbers ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            var matched = _agents.Where(a => numbers.Contains(a.Number)).ToList();
            return Task.FromResult<IReadOnlyList<DeviceAgentProfile>>(matched);
        }

        /// <summary>
        /// ユーザー別エージェント取得（テスト用に全件返す）
        /// </summary>
        /// <param name="userId">ユーザーID</param>
        /// <param name="cancellationToken">キャンセル通知</param>
        /// <returns>エージェント一覧</returns>
        public Task<IReadOnlyList<DeviceAgentProfile>> GetAsync(string userId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<DeviceAgentProfile>>(_agents.ToList());
        }

        /// <summary>
        /// エージェント追加または更新
        /// </summary>
        /// <param name="userId">ユーザーID</param>
        /// <param name="number">エージェント番号</param>
        /// <param name="name">エージェント名</param>
        /// <param name="cancellationToken">キャンセル通知</param>
        /// <returns>保存後プロファイル</returns>
        public Task<DeviceAgentProfile> UpsertAsync(string userId, string number, string name, CancellationToken cancellationToken = default)
        {
            var existing = _agents.FirstOrDefault(a => a.Number == number);
            if (existing is null)
            {
                var created = new DeviceAgentProfile(number, name, DateTimeOffset.UtcNow);
                _agents.Add(created);
                return Task.FromResult(created);
            }

            existing.Name = name;
            return Task.FromResult(existing);
        }

        /// <summary>
        /// エージェント削除
        /// </summary>
        /// <param name="userId">ユーザーID</param>
        /// <param name="number">エージェント番号</param>
        /// <param name="cancellationToken">キャンセル通知</param>
        public Task DeleteAsync(string userId, string number, CancellationToken cancellationToken = default)
        {
            _agents.RemoveAll(a => a.Number == number);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// メモリ上の権限リポジトリ
    /// </summary>
    private sealed class InMemoryPermissionRepository : IDeviceAgentPermissionRepository
    {
        private readonly Dictionary<string, HashSet<string>> _map = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 割り付け済みエージェント番号取得
        /// </summary>
        /// <param name="userId">ユーザーID</param>
        /// <param name="cancellationToken">キャンセル通知</param>
        /// <returns>許可番号一覧</returns>
        public Task<IReadOnlyList<string>> GetAllowedAgentNumbersAsync(string userId, CancellationToken cancellationToken = default)
        {
            if (_map.TryGetValue(userId, out var numbers))
            {
                return Task.FromResult<IReadOnlyList<string>>(numbers.ToList());
            }

            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        /// <summary>
        /// 割り付け置き換え
        /// </summary>
        /// <param name="userId">ユーザーID</param>
        /// <param name="agentNumbers">許可番号一覧</param>
        /// <param name="cancellationToken">キャンセル通知</param>
        public Task ReplaceAsync(string userId, IEnumerable<string> agentNumbers, CancellationToken cancellationToken = default)
        {
            Replace(userId, agentNumbers);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 割り付け置き換え実行
        /// </summary>
        /// <param name="userId">ユーザーID</param>
        /// <param name="agentNumbers">許可番号一覧</param>
        public void Replace(string userId, IEnumerable<string> agentNumbers)
        {
            _map[userId] = new HashSet<string>(agentNumbers ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// メモリ上のロールプロバイダー
    /// </summary>
    private sealed class InMemoryRoleProvider : IUserRoleProvider
    {
        private readonly Dictionary<string, IReadOnlyCollection<UserRoleId>> _roles;

        /// <summary>
        /// ロール割り当てを受け取るコンストラクター
        /// </summary>
        /// <param name="roles">ユーザーごとのロール一覧</param>
        public InMemoryRoleProvider(Dictionary<string, IReadOnlyCollection<UserRoleId>> roles)
        {
            _roles = roles;
        }

        /// <summary>
        /// ロール取得
        /// </summary>
        /// <param name="userId">ユーザーID</param>
        /// <param name="cancellationToken">キャンセル通知</param>
        /// <returns>ロール一覧</returns>
        public Task<IReadOnlyCollection<UserRoleId>> GetRolesAsync(string userId, CancellationToken cancellationToken = default)
        {
            if (_roles.TryGetValue(userId, out var roles))
            {
                return Task.FromResult(roles);
            }

            return Task.FromResult<IReadOnlyCollection<UserRoleId>>(Array.Empty<UserRoleId>());
        }

        /// <summary>
        /// ロール付与は未実装
        /// </summary>
        public Task AssignAsync(string userId, UserRoleId role, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// ロール削除は未実装
        /// </summary>
        public Task RemoveAsync(string userId, UserRoleId role, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// ロール保持判定
        /// </summary>
        /// <param name="userId">ユーザーID</param>
        /// <param name="role">ロール名</param>
        /// <param name="cancellationToken">キャンセル通知</param>
        /// <returns>保持していれば true</returns>
        public Task<bool> IsInRoleAsync(string userId, string role, CancellationToken cancellationToken = default)
        {
            var hasRole = _roles.TryGetValue(userId, out var roles) && roles.Any(r => r.Value == UserRoleId.From(role).Value);
            return Task.FromResult(hasRole);
        }
    }
}
