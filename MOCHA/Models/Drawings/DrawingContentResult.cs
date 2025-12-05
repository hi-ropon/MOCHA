namespace MOCHA.Models.Drawings;

/// <summary>
/// 図面読取の結果
/// </summary>
public sealed class DrawingContentResult
{
    private DrawingContentResult(
        bool succeeded,
        bool isPreviewOnly,
        bool isTruncated,
        string? error,
        string? content,
        string? fullPath,
        long bytesRead,
        int totalHits,
        IReadOnlyList<DrawingContentMatch> matches)
    {
        Succeeded = succeeded;
        IsPreviewOnly = isPreviewOnly;
        IsTruncated = isTruncated;
        Error = error;
        Content = content;
        FullPath = fullPath;
        BytesRead = bytesRead;
        TotalHits = totalHits;
        Matches = matches;
    }

    /// <summary>成功フラグ</summary>
    public bool Succeeded { get; }
    /// <summary>プレビューのみか</summary>
    public bool IsPreviewOnly { get; }
    /// <summary>読み取りが途中で切り捨てられたか</summary>
    public bool IsTruncated { get; }
    /// <summary>エラー</summary>
    public string? Error { get; }
    /// <summary>内容</summary>
    public string? Content { get; }
    /// <summary>ファイルパス</summary>
    public string? FullPath { get; }
    /// <summary>読み取ったバイト数</summary>
    public long BytesRead { get; }
    /// <summary>ヒット総数</summary>
    public int TotalHits { get; }
    /// <summary>ページ別ヒット情報</summary>
    public IReadOnlyList<DrawingContentMatch> Matches { get; }

    /// <summary>
    /// 成功結果生成
    /// </summary>
    /// <param name="content">読取内容</param>
    /// <param name="fullPath">ファイルパス</param>
    /// <param name="bytesRead">読み取ったバイト数</param>
    /// <param name="isTruncated">切り捨てフラグ</param>
    /// <param name="totalHits">ヒット総数</param>
    /// <param name="matches">マッチ情報</param>
    /// <returns>結果</returns>
    public static DrawingContentResult Success(
        string content,
        string? fullPath,
        long bytesRead,
        bool isTruncated,
        int totalHits,
        IReadOnlyList<DrawingContentMatch>? matches = null)
    {
        return new DrawingContentResult(true, false, isTruncated, null, content, fullPath, bytesRead, totalHits, matches ?? Array.Empty<DrawingContentMatch>());
    }

    /// <summary>
    /// プレビュー結果生成
    /// </summary>
    /// <param name="message">表示用メッセージ</param>
    /// <param name="fullPath">ファイルパス</param>
    /// <returns>結果</returns>
    public static DrawingContentResult Preview(string message, string? fullPath)
    {
        return new DrawingContentResult(true, true, false, null, message, fullPath, 0, 0, Array.Empty<DrawingContentMatch>());
    }

    /// <summary>
    /// 失敗結果生成
    /// </summary>
    /// <param name="error">エラー内容</param>
    /// <returns>結果</returns>
    public static DrawingContentResult Fail(string error)
    {
        return new DrawingContentResult(false, false, false, error, null, null, 0, 0, Array.Empty<DrawingContentMatch>());
    }
}
