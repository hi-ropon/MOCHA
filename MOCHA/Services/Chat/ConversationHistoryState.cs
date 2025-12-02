using MOCHA.Models.Chat;

namespace MOCHA.Services.Chat;

/// <summary>
/// 会話一覧の状態保持と UI への変更通知
/// </summary>
internal sealed class ConversationHistoryState
{
    private readonly IChatRepository _repository;
    private readonly List<ConversationSummary> _summaries = new();
    private readonly object _lock = new();
    private string? _currentUserId;
    private string? _currentAgentNumber;

    /// <summary>
    /// リポジトリ注入による状態管理初期化
    /// </summary>
    /// <param name="repository">チャットリポジトリ</param>
    public ConversationHistoryState(IChatRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// 状態変更時の通知イベント
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// 現在保持している会話要約一覧取得
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
    /// 指定ユーザー・エージェントの会話一覧読み込みと状態更新
    /// </summary>
    /// <param name="userId">ユーザーID</param>
    /// <param name="agentNumber">エージェント番号</param>
    /// <param name="cancellationToken">キャンセル通知</param>
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
    /// 会話要約の追加または更新と状態同期
    /// </summary>
    /// <param name="userId">ユーザーID</param>
    /// <param name="id">会話ID</param>
    /// <param name="title">タイトル</param>
    /// <param name="agentNumber">エージェント番号</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <param name="preserveExistingTitle">既存のタイトルを優先するかどうか</param>
    public async Task UpsertAsync(string userId, string id, string title, string? agentNumber, CancellationToken cancellationToken = default, bool preserveExistingTitle = false)
    {
        var trimmed = title.Length > 30 ? title[..30] + "…" : title;
        string resolvedTitle;
        bool stateMismatch;

        lock (_lock)
        {
            stateMismatch = _currentUserId != userId || _currentAgentNumber != agentNumber;
            var existingTitle = stateMismatch
                ? null
                : _summaries.FirstOrDefault(x => x.Id == id && x.AgentNumber == agentNumber)?.Title;

            resolvedTitle = preserveExistingTitle && !string.IsNullOrWhiteSpace(existingTitle)
                ? existingTitle!
                : trimmed;
        }

        await _repository.UpsertConversationAsync(userId, id, resolvedTitle, agentNumber, cancellationToken);

        lock (_lock)
        {
            if (_currentUserId != userId || _currentAgentNumber != agentNumber)
            {
                _currentUserId = userId;
                _currentAgentNumber = agentNumber;
                _summaries.Clear();
            }

            var summary = _summaries.FirstOrDefault(x => x.Id == id && x.AgentNumber == agentNumber);
            if (summary is null)
            {
                _summaries.Add(new ConversationSummary(id, resolvedTitle, DateTimeOffset.UtcNow, agentNumber, userId));
            }
            else
            {
                summary.Title = preserveExistingTitle && !string.IsNullOrWhiteSpace(summary.Title)
                    ? summary.Title
                    : resolvedTitle;
                summary.UpdatedAt = DateTimeOffset.UtcNow;
                summary.AgentNumber ??= agentNumber;
                summary.UserId ??= userId;
            }
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// 指定会話の削除と状態一致時のキャッシュ除去
    /// </summary>
    /// <param name="userId">ユーザーID</param>
    /// <param name="id">会話ID</param>
    /// <param name="agentNumber">エージェント番号</param>
    /// <param name="cancellationToken">キャンセル通知</param>
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
