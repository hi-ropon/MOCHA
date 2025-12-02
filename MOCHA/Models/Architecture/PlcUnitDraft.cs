using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MOCHA.Models.Architecture;

/// <summary>
/// PLCユニット登録・更新用の入力モデル
/// </summary>
public sealed class PlcUnitDraft
{
    /// <summary>ユニット名</summary>
    public string Name { get; init; } = string.Empty;
    /// <summary>機種</summary>
    public string? Model { get; init; }
    /// <summary>役割</summary>
    public string? Role { get; init; }
    /// <summary>IP アドレス</summary>
    public string? IpAddress { get; init; }
    /// <summary>ポート番号</summary>
    public int? Port { get; init; }
    /// <summary>コメントファイル</summary>
    public PlcFileUpload? CommentFile { get; init; }
    /// <summary>プログラムファイル</summary>
    public PlcFileUpload? ProgramFile { get; init; }
    /// <summary>モジュールドラフト</summary>
    public IReadOnlyCollection<PlcModuleDraft> Modules { get; init; } = new List<PlcModuleDraft>();

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
            var validation = CommentFile.Validate(maxFileSizeBytes);
            if (!validation.IsValid)
            {
                return validation;
            }

            if (!IsCsvFile(CommentFile.FileName))
            {
                return (false, "コメントファイルはCSVファイルを選択してください");
            }
        }

        if (ProgramFile is not null)
        {
            var validation = ProgramFile.Validate(maxFileSizeBytes);
            if (!validation.IsValid)
            {
                return validation;
            }

            if (!IsCsvFile(ProgramFile.FileName))
            {
                return (false, "プログラムファイルはCSVファイルを選択してください");
            }
        }

        return (true, null);
    }

    private static bool IsCsvFile(string fileName)
    {
        return string.Equals(Path.GetExtension(fileName), ".csv", System.StringComparison.OrdinalIgnoreCase);
    }
}
