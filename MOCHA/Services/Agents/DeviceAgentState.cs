using System.Linq;
using MOCHA.Models.Agents;

namespace MOCHA.Services.Agents;

/// <summary>
/// 装置エージェントの選択状態と一覧を管理する
/// </summary>
internal sealed class DeviceAgentState
{
    private readonly IDeviceAgentRepository _repository;
    private readonly IDeviceAgentAccessService _accessService;
    private readonly List<DeviceAgentProfile> _agents = new();
    private readonly object _lock = new();
    private string? _currentUserId;

    /// <summary>
    /// リポジトリ注入による状態管理初期化
    /// </summary>
    /// <param name="repository">装置エージェントリポジトリ</param>
    /// <param name="accessService">装置エージェント権限サービス</param>
    public DeviceAgentState(IDeviceAgentRepository repository, IDeviceAgentAccessService accessService)
    {
        _repository = repository;
        _accessService = accessService;
    }

    /// <summary>
    /// 状態変更の通知イベント
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// 現在保持している装置エージェント一覧
    /// </summary>
    public IReadOnlyList<DeviceAgentProfile> Agents
    {
        get
        {
            lock (_lock)
            {
                return _agents.ToList();
            }
        }
    }

    /// <summary>
    /// 選択中のエージェント番号
    /// </summary>
    public string? SelectedAgentNumber { get; private set; }

    /// <summary>
    /// ユーザーのエージェント一覧読み込みと状態更新
    /// </summary>
    /// <param name="userId">ユーザーID</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    public async Task LoadAsync(string userId, CancellationToken cancellationToken = default)
    {
        var items = await _accessService.GetAvailableAgentsAsync(userId, cancellationToken);
        lock (_lock)
        {
            _currentUserId = userId;
            _agents.Clear();
            _agents.AddRange(items);
            SelectedAgentNumber ??= items.FirstOrDefault()?.Number;
        }
        Changed?.Invoke();
    }

    /// <summary>
    /// エージェントの追加または更新と選択状態の更新
    /// </summary>
    /// <param name="userId">ユーザーID</param>
    /// <param name="number">エージェント番号</param>
    /// <param name="name">エージェント名</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>保存後のエージェント</returns>
    public async Task<DeviceAgentProfile> AddOrUpdateAsync(string userId, string number, string name, CancellationToken cancellationToken = default)
    {
        var agent = await _repository.UpsertAsync(userId, number, name, cancellationToken);
        lock (_lock)
        {
            if (_currentUserId != userId)
            {
                _currentUserId = userId;
                _agents.Clear();
            }

            var existing = _agents.FirstOrDefault(a => a.Number == number);
            if (existing is null)
            {
                _agents.Add(agent);
            }
            else
            {
                existing.Name = agent.Name;
            }

            SelectedAgentNumber = number;
        }
        Changed?.Invoke();
        return agent;
    }

    /// <summary>
    /// エージェント削除と選択状態の更新
    /// </summary>
    /// <param name="userId">ユーザーID</param>
    /// <param name="number">エージェント番号</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    public async Task RemoveAsync(string userId, string number, CancellationToken cancellationToken = default)
    {
        await _repository.DeleteAsync(userId, number, cancellationToken);
        lock (_lock)
        {
            if (_currentUserId != userId)
            {
                _currentUserId = userId;
                _agents.Clear();
                SelectedAgentNumber = null;
                Changed?.Invoke();
                return;
            }

            var removed = _agents.RemoveAll(a => a.Number == number) > 0;
            if (!removed)
            {
                return;
            }

            if (SelectedAgentNumber == number)
            {
                SelectedAgentNumber = _agents.FirstOrDefault()?.Number;
            }
        }
        Changed?.Invoke();
    }

    /// <summary>
    /// 指定番号のエージェント選択（存在しない番号は無視）
    /// </summary>
    /// <param name="number">エージェント番号</param>
    public void Select(string? number)
    {
        lock (_lock)
        {
            if (number is not null && _agents.All(a => a.Number != number))
            {
                return;
            }

            if (SelectedAgentNumber == number)
            {
                return;
            }

            SelectedAgentNumber = number;
        }
        Changed?.Invoke();
    }
}
