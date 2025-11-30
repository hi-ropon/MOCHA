using MOCHA.Models.Agents;
using MOCHA.Models.Auth;
using MOCHA.Services.Auth;

namespace MOCHA.Services.Agents;

/// <summary>
/// 装置エージェントの利用可否を判定し割り付けを管理するサービス
/// </summary>
internal interface IDeviceAgentAccessService
{
    /// <summary>
    /// ユーザーが利用可能な装置エージェント一覧を取得する
    /// </summary>
    /// <param name="userId">ユーザーID。</param>
    /// <param name="cancellationToken">キャンセル通知。</param>
    Task<IReadOnlyList<DeviceAgentProfile>> GetAvailableAgentsAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 割り付け済みの装置エージェント番号を取得する
    /// </summary>
    /// <param name="userId">ユーザーID。</param>
    /// <param name="cancellationToken">キャンセル通知。</param>
    Task<IReadOnlyList<string>> GetAssignmentsAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 割り付けを置き換える
    /// </summary>
    /// <param name="userId">ユーザーID。</param>
    /// <param name="agentNumbers">許可する番号一覧。</param>
    /// <param name="cancellationToken">キャンセル通知。</param>
    Task UpdateAssignmentsAsync(string userId, IEnumerable<string> agentNumbers, CancellationToken cancellationToken = default);

    /// <summary>
    /// 全装置エージェントの定義一覧を取得する
    /// </summary>
    /// <param name="cancellationToken">キャンセル通知。</param>
    Task<IReadOnlyList<DeviceAgentProfile>> ListDefinitionsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 装置エージェントの利用可否を判定し割り付けを管理するサービス
/// </summary>
internal sealed class DeviceAgentAccessService : IDeviceAgentAccessService
{
    private static readonly HashSet<string> PrivilegedRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        UserRoleId.Predefined.Administrator.Value,
        UserRoleId.Predefined.Developer.Value
    };

    private readonly IDeviceAgentRepository _agentRepository;
    private readonly IDeviceAgentPermissionRepository _permissionRepository;
    private readonly IUserRoleProvider _roleProvider;

    /// <summary>
    /// 依存を注入してサービスを初期化する
    /// </summary>
    public DeviceAgentAccessService(
        IDeviceAgentRepository agentRepository,
        IDeviceAgentPermissionRepository permissionRepository,
        IUserRoleProvider roleProvider)
    {
        _agentRepository = agentRepository;
        _permissionRepository = permissionRepository;
        _roleProvider = roleProvider;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DeviceAgentProfile>> GetAvailableAgentsAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Array.Empty<DeviceAgentProfile>();
        }

        if (await HasFullAccessAsync(userId, cancellationToken))
        {
            return await _agentRepository.GetAllAsync(cancellationToken);
        }

        var assigned = await _permissionRepository.GetAllowedAgentNumbersAsync(userId, cancellationToken);
        if (assigned.Count == 0)
        {
            return Array.Empty<DeviceAgentProfile>();
        }

        return await _agentRepository.GetByNumbersAsync(assigned, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> GetAssignmentsAsync(string userId, CancellationToken cancellationToken = default)
    {
        return _permissionRepository.GetAllowedAgentNumbersAsync(userId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAssignmentsAsync(string userId, IEnumerable<string> agentNumbers, CancellationToken cancellationToken = default)
    {
        await _permissionRepository.ReplaceAsync(userId, agentNumbers, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<DeviceAgentProfile>> ListDefinitionsAsync(CancellationToken cancellationToken = default)
    {
        return _agentRepository.GetAllAsync(cancellationToken);
    }

    private async Task<bool> HasFullAccessAsync(string userId, CancellationToken cancellationToken)
    {
        var roles = await _roleProvider.GetRolesAsync(userId, cancellationToken);
        return roles.Any(r => PrivilegedRoles.Contains(r.Value));
    }
}
