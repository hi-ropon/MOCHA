using System;

namespace MOCHA.Models.Drawings;

/// <summary>
/// 図面登録時の入力値を表す値オブジェクト
/// </summary>
public sealed class DrawingUpload
{
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = "application/octet-stream";
    public long FileSize { get; init; }
    public string? Description { get; init; }

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

        return (true, null);
    }
}
