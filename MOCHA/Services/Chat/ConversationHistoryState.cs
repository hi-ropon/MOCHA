using MOCHA.Models.Chat;

namespace MOCHA.Services.Chat;

/// <summary>
/// 会話一覧の状態を保持し、UI に変更を通知する。
/// </summary>
internal sealed class ConversationHistoryState
{
    private readonly IChatRepository _repository;
    private readonly List<ConversationSummary> _summaries = new();
    private readonly object _lock = new();
    private string? _currentUserId;
    private string? _currentAgentNumber;

    /// <summary>
    /// リポジトリを注入して状態管理を初期化する。
    /// </summary>
    /// <param name="repository">チャットリポジトリ。</param>
    public ConversationHistoryState(IChatRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// 状態変更時に通知するイベント。
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// 現在保持している会話要約一覧を取得する。
    /// </summary>
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

    /// <summary>
    /// 指定ユーザー・エージェントの会話一覧を読み込み、状態を更新する。
    /// </summary>
    /// <param name="userId">ユーザーID。</param>
    /// <param name="agentNumber">エージェント番号。</param>
    /// <param name="cancellationToken">キャンセル通知。</param>
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

    /// <summary>
    /// 会話要約を追加または更新し、状態を同期する。
    /// </summary>
    /// <param name="userId">ユーザーID。</param>
    /// <param name="id">会話ID。</param>
    /// <param name="title">タイトル。</param>
    /// <param name="agentNumber">エージェント番号。</param>
    /// <param name="cancellationToken">キャンセル通知。</param>
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

    /// <summary>
    /// 指定会話を削除し、状態が一致する場合はキャッシュからも除去する。
    /// </summary>
    /// <param name="userId">ユーザーID。</param>
    /// <param name="id">会話ID。</param>
    /// <param name="agentNumber">エージェント番号。</param>
    /// <param name="cancellationToken">キャンセル通知。</param>
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
