using MOCHA.Models.Chat;

namespace MOCHA.Services.Chat;

public class ConversationHistoryState
{
    private readonly IChatRepository _repository;
    private readonly List<ConversationSummary> _summaries = new();
    private readonly object _lock = new();
    private string? _currentUserId;
    private string? _currentAgentNumber;

    public ConversationHistoryState(IChatRepository repository)
    {
        _repository = repository;
    }

    public event Action? Changed;

    public IReadOnlyList<ConversationSummary> Summaries
    {
        get
        {
            lock (_lock)
            {
                return _summaries.ToList();
            }
        }
    }

    public async Task LoadAsync(string userId, string? agentNumber = null, CancellationToken cancellationToken = default)
    {
        var items = await _repository.GetSummariesAsync(userId, agentNumber, cancellationToken);
        lock (_lock)
        {
            _currentUserId = userId;
            _currentAgentNumber = agentNumber;
            _summaries.Clear();
            _summaries.AddRange(items);
        }
        Changed?.Invoke();
    }

    public async Task UpsertAsync(string userId, string id, string title, string? agentNumber, CancellationToken cancellationToken = default)
    {
        await _repository.UpsertConversationAsync(userId, id, title, agentNumber, cancellationToken);
        lock (_lock)
        {
            if (_currentUserId != userId || _currentAgentNumber != agentNumber)
            {
                _currentUserId = userId;
                _currentAgentNumber = agentNumber;
                _summaries.Clear();
            }

            var existing = _summaries.FirstOrDefault(x => x.Id == id && x.AgentNumber == agentNumber);
            var trimmed = title.Length > 30 ? title[..30] + "…" : title;
            if (existing is null)
            {
                _summaries.Add(new ConversationSummary(id, trimmed, DateTimeOffset.UtcNow, agentNumber, userId));
            }
            else
            {
                existing.Title = trimmed;
                existing.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }
        Changed?.Invoke();
    }

    public async Task DeleteAsync(string userId, string id, string? agentNumber, CancellationToken cancellationToken = default)
    {
        await _repository.DeleteConversationAsync(userId, id, agentNumber, cancellationToken);
        lock (_lock)
        {
            if (_currentUserId != userId || _currentAgentNumber != agentNumber)
            {
                return;
            }

            _summaries.RemoveAll(x => x.Id == id);
        }
        Changed?.Invoke();
    }
}
