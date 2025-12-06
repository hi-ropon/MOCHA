using System;
using System.Collections.Generic;
using System.Linq;

namespace MOCHA.Models.Architecture;

/// <summary>
/// 装置ユニット構成入力ドラフト
/// </summary>
public sealed class UnitConfigurationDraft
{
    /// <summary>ユニット名</summary>
    public string Name { get; init; } = string.Empty;
    /// <summary>ユニット説明</summary>
    public string? Description { get; init; }
    /// <summary>機器一覧</summary>
    public IReadOnlyCollection<UnitDeviceDraft> Devices { get; init; } = new List<UnitDeviceDraft>();

    /// <summary>
    /// 入力値検証
    /// </summary>
    /// <returns>検証結果</returns>
    public (bool IsValid, string? Error) Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            return (false, "ユニット名を入力してください");
        }

        var trimmedName = Name.Trim();
        if (trimmedName.Length > 100)
        {
            return (false, "ユニット名は100文字以内で入力してください");
        }

        if (!string.IsNullOrWhiteSpace(Description) && Description.Trim().Length > 500)
        {
            return (false, "ユニット説明は500文字以内で入力してください");
        }

        var devices = Devices ?? Array.Empty<UnitDeviceDraft>();
        if (devices.Count > 50)
        {
            return (false, "機器は50件までにしてください");
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var device in devices)
        {
            index++;
            var validation = device.Validate();
            if (!validation.IsValid)
            {
                return (false, $"機器{index}: {validation.Error}");
            }

            var normalizedName = device.Name.Trim();
            if (!names.Add(normalizedName))
            {
                return (false, "機器名が重複しています");
            }
        }

        return (true, null);
    }
}
