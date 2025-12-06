using System;

namespace MOCHA.Models.Drawings;

/// <summary>
/// 図面登録時の入力値を表す値オブジェクト
/// </summary>
public sealed class DrawingUpload
{
    /// <summary>ファイル名</summary>
    public string FileName { get; init; } = string.Empty;
    /// <summary>コンテンツタイプ</summary>
    public string ContentType { get; init; } = "application/octet-stream";
    /// <summary>ファイルサイズ</summary>
    public long FileSize { get; init; }
    /// <summary>説明</summary>
    public string? Description { get; init; }
    /// <summary>ファイル内容</summary>
    public byte[]? Content { get; init; }

    /// <summary>
    /// 入力値のバリデーション
    /// </summary>
    /// <param name="maxFileSizeBytes">許容最大サイズ</param>
    /// <returns>結果とエラーメッセージ</returns>
    public (bool IsValid, string? Error) Validate(long maxFileSizeBytes)
    {
        if (string.IsNullOrWhiteSpace(FileName))
        {
            return (false, "ファイル名は必須です");
        }

        if (FileSize <= 0)
        {
            return (false, "ファイルサイズが 0 バイトです");
        }

        if (FileSize > maxFileSizeBytes)
        {
            var megaBytes = Math.Round(maxFileSizeBytes / 1024d / 1024d, 1);
            return (false, $"ファイルサイズは {megaBytes}MB 以下にしてください");
        }

        if (Content is null || Content.LongLength == 0)
        {
            return (false, "図面ファイルを選択してください");
        }

        return (true, null);
    }
}
