using System.Collections.Generic;
using System.Linq;

namespace MOCHA.Models.Architecture;

/// <summary>
/// PLCユニット登録・更新用の入力モデル
/// </summary>
public sealed class PlcUnitDraft
{
    public string Name { get; init; } = string.Empty;
    public string? Model { get; init; }
    public string? Role { get; init; }
    public string? IpAddress { get; init; }
    public PlcFileUpload? CommentFile { get; init; }
    public PlcFileUpload? ProgramFile { get; init; }
    public IReadOnlyCollection<PlcModuleDraft> Modules { get; init; } = new List<PlcModuleDraft>();

    public (bool IsValid, string? Error) Validate(long maxFileSizeBytes)
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            return (false, "PLC名は必須です");
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
        }

        if (ProgramFile is not null)
        {
            var validation = ProgramFile.Validate(maxFileSizeBytes);
            if (!validation.IsValid)
            {
                return validation;
            }
        }

        return (true, null);
    }
}
