using MOCHA.Models.Chat;

namespace MOCHA.Services.Chat;

public class ConversationHistoryState
{
    private readonly IChatRepository _repository;
    private readonly List<ConversationSummary> _summaries = new();
    private readonly object _lock = new();
    private string? _currentUserId;

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

    public async Task LoadAsync(string userId, CancellationToken cancellationToken = default)
    {
        var items = await _repository.GetSummariesAsync(userId, cancellationToken);
        lock (_lock)
        {
            _currentUserId = userId;
            _summaries.Clear();
            _summaries.AddRange(items);
        }
        Changed?.Invoke();
    }

    public async Task UpsertAsync(string userId, string id, string title, CancellationToken cancellationToken = default)
    {
        await _repository.UpsertConversationAsync(userId, id, title, cancellationToken);
        lock (_lock)
        {
            if (_currentUserId != userId)
            {
                _currentUserId = userId;
                _summaries.Clear();
            }

            var existing = _summaries.FirstOrDefault(x => x.Id == id);
            var trimmed = title.Length > 30 ? title[..30] + "…" : title;
            if (existing is null)
            {
                _summaries.Add(new ConversationSummary(id, trimmed, DateTimeOffset.UtcNow));
            }
            else
            {
                existing.Title = trimmed;
                existing.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }
        Changed?.Invoke();
    }

    public async Task SeedDemoAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (Summaries.Any())
        {
            return;
        }

        await UpsertAsync(userId, "demo-1", "デモ会話 A", cancellationToken);
        await UpsertAsync(userId, "demo-2", "デモ会話 B", cancellationToken);
    }
}
