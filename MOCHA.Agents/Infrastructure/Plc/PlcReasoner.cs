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
    private static readonly Regex _deviceRegex = new(@"(?i)([dmlxyctl][0-9][0-9a-f]*)", RegexOptions.Compiled);

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
        return InferMultiple(query, Enumerable.Empty<ProgramContext>());
    }

    public string InferMultiple(string query, IEnumerable<ProgramContext> programs)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var items = new List<object>();

        void AppendDevices(IEnumerable<string> devices, string reason)
        {
            foreach (var device in devices)
            {
                if (seen.Contains(device))
                {
                    continue;
                }

                if (items.Count >= 8)
                {
                    return;
                }

                seen.Add(device);
                items.Add(new
                {
                    device,
                    priority = items.Count + 1,
                    reason
                });
            }
        }

        AppendDevices(ExtractDevices(query), "質問中のデバイス表記から抽出");

        foreach (var program in programs ?? Enumerable.Empty<ProgramContext>())
        {
            var devices = ExtractDevices(program.Lines);
            if (devices.Count == 0)
            {
                continue;
            }

            var reason = string.IsNullOrWhiteSpace(program.Name)
                ? "プログラムから抽出"
                : $"プログラム {program.Name} から抽出";
            AppendDevices(devices, reason);

            if (items.Count >= 8)
            {
                break;
            }
        }

        if (items.Count == 0)
        {
            return JsonSerializer.Serialize(new { devices = Array.Empty<object>(), message = "候補なし" });
        }

        return JsonSerializer.Serialize(new { devices = items });
    }

    private static List<string> ExtractDevices(string query) => ExtractDevices(new[] { query });

    private static List<string> ExtractDevices(IEnumerable<string> sources)
    {
        var list = new List<string>();
        foreach (var source in sources ?? Array.Empty<string>())
        {
            foreach (Match match in _deviceRegex.Matches(source ?? string.Empty))
            {
                var device = match.Groups[1].Value.ToUpperInvariant();
                if (list.Contains(device))
                {
                    continue;
                }

                list.Add(device);
                if (list.Count >= 8)
                {
                    return list;
                }
            }
        }

        return list;
    }
}

public sealed record ProgramContext(string Name, IReadOnlyList<string> Lines);
