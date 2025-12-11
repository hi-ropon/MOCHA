namespace MOCHA.Agents.Domain.Plc;

/// <summary>
/// コメント検索結果
/// </summary>
public sealed class CommentSearchResult
{
    public CommentSearchResult(string device, string comment, double score, IReadOnlyCollection<string> matchedTerms)
    {
        Device = device;
        Comment = comment;
        Score = score;
        MatchedTerms = matchedTerms;
    }

    /// <summary>デバイス</summary>
    public string Device { get; }

    /// <summary>コメント</summary>
    public string Comment { get; }

    /// <summary>一致スコア</summary>
    public double Score { get; }

    /// <summary>一致したキーワード</summary>
    public IReadOnlyCollection<string> MatchedTerms { get; }
}
