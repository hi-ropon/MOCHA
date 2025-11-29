using System;

namespace MOCHA.Models.Architecture;

/// <summary>
/// PLCユニットに紐づくファイルのアップロード情報
/// </summary>
public sealed class PlcFileUpload
{
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = "application/octet-stream";
    public long FileSize { get; init; }

    public (bool IsValid, string? Error) Validate(long maxSizeBytes)
    {
        if (string.IsNullOrWhiteSpace(FileName))
        {
            return (false, "ファイル名は必須です");
        }

        if (FileSize <= 0)
        {
            return (false, "ファイルサイズが 0 バイトです");
        }

        if (FileSize > maxSizeBytes)
        {
            var megaBytes = Math.Round(maxSizeBytes / 1024d / 1024d, 1);
            return (false, $"ファイルサイズは {megaBytes}MB 以下にしてください");
        }

        return (true, null);
    }
}
