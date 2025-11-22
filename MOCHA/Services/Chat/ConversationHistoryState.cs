using MOCHA.Models.Chat;

namespace MOCHA.Services.Chat;

/// <summary>
/// 会話履歴の簡易ステート。現在はインメモリで保持し、UI間で共有する。
/// 実際の永続化層（SQLiteなど）と置き換え可能。
/// </summary>
public class ConversationHistoryState
{
    private readonly List<ConversationSummary> _summaries = new();
    private readonly object _lock = new();

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

    public void Upsert(string id, string title)
    {
        lock (_lock)
        {
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
    }

    public void SeedDemo()
    {
        Upsert("demo-1", "デモ会話 A");
        Upsert("demo-2", "デモ会話 B");
    }
}
