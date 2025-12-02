using System;

namespace MOCHA.Models.Architecture;

/// <summary>
/// PLCユニットに紐づくファイルのアップロード情報
/// </summary>
public sealed class PlcFileUpload
{
    /// <summary>ファイルID</summary>
    public Guid Id { get; init; } = Guid.NewGuid();
    /// <summary>ファイル名</summary>
    public string FileName { get; init; } = string.Empty;
    /// <summary>コンテンツタイプ</summary>
    public string ContentType { get; init; } = "application/octet-stream";
    /// <summary>ファイルサイズ</summary>
    public long FileSize { get; init; }
    /// <summary>表示名</summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// 入力値のバリデーション
    /// </summary>
    /// <param name="maxSizeBytes">許容最大サイズ</param>
    /// <returns>検証結果</returns>
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
