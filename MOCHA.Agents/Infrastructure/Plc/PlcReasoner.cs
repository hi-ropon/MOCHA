using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MOCHA.Agents.Infrastructure.Plc;

/// <summary>
/// 質問からデバイス候補を抽出する簡易推定
/// </summary>
public sealed class PlcReasoner
{
    // 日本語文中でもヒットさせるため単純な前後チェックにする
    private static readonly Regex _deviceRegex = new(@"(?i)([dmxyct][0-9a-f]+)", RegexOptions.Compiled);

    public string InferSingle(string query)
    {
        var tokens = ExtractDevices(query);
        if (tokens.Count == 0)
        {
            return "デバイスを推定できませんでした。例: D100 や M10 のように記載してください。";
        }

        return JsonSerializer.Serialize(new { device = tokens[0], reason = "質問中のデバイス表記から抽出" });
    }

    public string InferMultiple(string query)
    {
        var tokens = ExtractDevices(query);
        var items = tokens.Select((d, i) => new
        {
            device = d,
            priority = i + 1,
            reason = "質問中のデバイス表記から抽出"
        }).ToList();

        if (items.Count == 0)
        {
            return JsonSerializer.Serialize(new { devices = Array.Empty<object>(), message = "候補なし" });
        }

        return JsonSerializer.Serialize(new { devices = items });
    }

    private static List<string> ExtractDevices(string query)
    {
        return _deviceRegex.Matches(query ?? string.Empty)
            .Select(m => m.Groups[1].Value.ToUpperInvariant())
            .Distinct()
            .Take(8)
            .ToList();
    }
}
