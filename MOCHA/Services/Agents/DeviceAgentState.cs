using System.Linq;
using MOCHA.Models.Agents;

namespace MOCHA.Services.Agents;

public class DeviceAgentState
{
    private readonly IDeviceAgentRepository _repository;
    private readonly List<DeviceAgentProfile> _agents = new();
    private readonly object _lock = new();
    private string? _currentUserId;

    public DeviceAgentState(IDeviceAgentRepository repository)
    {
        _repository = repository;
    }

    public event Action? Changed;

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

    public string? SelectedAgentNumber { get; private set; }

    public async Task LoadAsync(string userId, CancellationToken cancellationToken = default)
    {
        var items = await _repository.GetAsync(userId, cancellationToken);
        lock (_lock)
        {
            _currentUserId = userId;
            _agents.Clear();
            _agents.AddRange(items);
            SelectedAgentNumber ??= items.FirstOrDefault()?.Number;
        }
        Changed?.Invoke();
    }

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
