namespace MOCHA.Models.Drawings;

/// <summary>
/// 図面読取のマッチ情報
/// </summary>
public sealed class DrawingContentMatch
{
    /// <summary>ページ番号</summary>
    public int PageNumber { get; init; }

    /// <summary>ヒットスコア</summary>
    public int Score { get; init; }

    /// <summary>抜粋</summary>
    public string Snippet { get; init; } = string.Empty;
}
