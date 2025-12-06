using System;
using System.IO;

namespace MOCHA.Models.Architecture;

/// <summary>
/// ファンクションブロック登録・更新用ドラフト
/// </summary>
public sealed class FunctionBlockDraft
{
    /// <summary>ファンクションブロック名</summary>
    public string Name { get; init; } = string.Empty;
    /// <summary>ラベル定義CSV</summary>
    public PlcFileUpload? LabelFile { get; init; }
    /// <summary>プログラムCSV</summary>
    public PlcFileUpload? ProgramFile { get; init; }

    /// <summary>
    /// 入力のバリデーション
    /// </summary>
    /// <param name="maxSizeBytes">最大ファイルサイズ</param>
    /// <returns>検証結果</returns>
    public (bool IsValid, string? Error) Validate(long maxSizeBytes)
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            return (false, "ファンクションブロック名は必須です");
        }

        if (LabelFile is null)
        {
            return (false, "ラベルファイルを選択してください");
        }

        var labelValidation = ValidateFile(LabelFile, maxSizeBytes);
        if (!labelValidation.IsValid)
        {
            return labelValidation;
        }

        if (ProgramFile is null)
        {
            return (false, "プログラムファイルを選択してください");
        }

        var programValidation = ValidateFile(ProgramFile, maxSizeBytes);
        if (!programValidation.IsValid)
        {
            return programValidation;
        }

        return (true, null);
    }

    private static (bool IsValid, string? Error) ValidateFile(PlcFileUpload file, long maxSizeBytes)
    {
        var normalized = new PlcFileUpload
        {
            Id = file.Id,
            FileName = file.FileName,
            ContentType = file.ContentType,
            FileSize = file.Content?.LongLength ?? file.FileSize,
            DisplayName = file.DisplayName,
            RelativePath = file.RelativePath,
            StorageRoot = file.StorageRoot,
            Content = file.Content
        };

        var validation = normalized.Validate(maxSizeBytes);
        if (!validation.IsValid)
        {
            return validation;
        }

        if (!string.Equals(Path.GetExtension(file.FileName), ".csv", StringComparison.OrdinalIgnoreCase))
        {
            return (false, "CSVファイルをアップロードしてください");
        }

        if (file.Content is null || file.Content.LongLength == 0)
        {
            return (false, "ファイル内容が空です");
        }

        return (true, null);
    }
}
