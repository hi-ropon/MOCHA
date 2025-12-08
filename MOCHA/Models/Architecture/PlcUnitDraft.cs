using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MOCHA.Models.Architecture;

/// <summary>
/// PLCユニット登録・更新用の入力モデル
/// </summary>
public sealed class PlcUnitDraft
{
    /// <summary>サポートするメーカー一覧</summary>
    public static readonly IReadOnlyList<string> SupportedManufacturers = new[] { "三菱電機", "KEYENCE" };
    /// <summary>ユニット名</summary>
    public string Name { get; init; } = string.Empty;
    /// <summary>メーカー</summary>
    public string Manufacturer { get; init; } = string.Empty;
    /// <summary>機種</summary>
    public string? Model { get; init; }
    /// <summary>役割</summary>
    public string? Role { get; init; }
    /// <summary>IP アドレス</summary>
    public string? IpAddress { get; init; }
    /// <summary>ポート番号</summary>
    public int? Port { get; init; }
    /// <summary>ゲートウェイIPアドレス</summary>
    public string? GatewayHost { get; init; }
    /// <summary>ゲートウェイポート番号</summary>
    public int? GatewayPort { get; init; }
    /// <summary>コメントファイル</summary>
    public PlcFileUpload? CommentFile { get; init; }
    /// <summary>プログラムファイル群</summary>
    public IReadOnlyCollection<PlcFileUpload> ProgramFiles { get; init; } = new List<PlcFileUpload>();
    /// <summary>モジュールドラフト</summary>
    public IReadOnlyCollection<PlcModuleDraft> Modules { get; init; } = new List<PlcModuleDraft>();
    /// <summary>プログラム構成説明</summary>
    public string? ProgramDescription { get; init; }
    /// <summary>説明文字数上限</summary>
    public const int ProgramDescriptionMaxLength = 300;

    /// <summary>
    /// 入力値のバリデーション
    /// </summary>
    /// <param name="maxFileSizeBytes">最大ファイルサイズ</param>
    /// <returns>検証結果</returns>
    public (bool IsValid, string? Error) Validate(long maxFileSizeBytes)
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            return (false, "PLC名は必須です");
        }

        if (string.IsNullOrWhiteSpace(Manufacturer))
        {
            return (false, "メーカーを選択してください");
        }

        if (!SupportedManufacturers.Contains(Manufacturer.Trim(), StringComparer.Ordinal))
        {
            return (false, "メーカーは「三菱電機」「KEYENCE」から選択してください");
        }

        if (Port is not null && (Port <= 0 || Port > 65535))
        {
            return (false, "ポート番号は1-65535で入力してください");
        }

        if (Modules.Any(m => string.IsNullOrWhiteSpace(m.Name)))
        {
            return (false, "モジュール名は必須です");
        }

        if (CommentFile is not null)
        {
            var normalizedComment = new PlcFileUpload
            {
                Id = CommentFile.Id,
                FileName = CommentFile.FileName,
                ContentType = CommentFile.ContentType,
                FileSize = GetSize(CommentFile),
                DisplayName = CommentFile.DisplayName,
                RelativePath = CommentFile.RelativePath,
                StorageRoot = CommentFile.StorageRoot
            };

            var validation = normalizedComment.Validate(maxFileSizeBytes);
            if (!validation.IsValid)
            {
                return validation;
            }

            if (!IsCsvFile(CommentFile.FileName))
            {
                return (false, "コメントファイルはCSVファイルを選択してください");
            }
        }

        var programFiles = ProgramFiles ?? Array.Empty<PlcFileUpload>();
        foreach (var programFile in programFiles)
        {
            var normalizedProgram = new PlcFileUpload
            {
                Id = programFile.Id,
                FileName = programFile.FileName,
                ContentType = programFile.ContentType,
                FileSize = GetSize(programFile),
                DisplayName = programFile.DisplayName,
                RelativePath = programFile.RelativePath,
                StorageRoot = programFile.StorageRoot
            };

            var validation = normalizedProgram.Validate(maxFileSizeBytes);
            if (!validation.IsValid)
            {
                return validation;
            }

            if (!IsCsvFile(programFile.FileName))
            {
                return (false, "プログラムファイルはCSVファイルを選択してください");
            }
        }

        if (!string.IsNullOrWhiteSpace(ProgramDescription) && ProgramDescription.Trim().Length > ProgramDescriptionMaxLength)
        {
            return (false, $"プログラム構成の説明は{ProgramDescriptionMaxLength}文字以内で入力してください");
        }

        return (true, null);
    }

    private static bool IsCsvFile(string fileName)
    {
        return string.Equals(Path.GetExtension(fileName), ".csv", System.StringComparison.OrdinalIgnoreCase);
    }

    private static long GetSize(PlcFileUpload file)
    {
        return file.Content?.LongLength ?? file.FileSize;
    }
}
