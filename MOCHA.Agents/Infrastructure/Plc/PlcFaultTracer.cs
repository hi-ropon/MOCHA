using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using MOCHA.Agents.Domain.Plc;

namespace MOCHA.Agents.Infrastructure.Plc;

/// <summary>
/// エラーコメント付きLコイルを追跡するトレーサー
/// </summary>
public sealed class PlcFaultTracer
{
    private static readonly string[] _errorKeywords = { "異常", "エラー", "ｴﾗｰ", "ERR", "ERROR" };
    private static readonly Regex _coilRegex = new(@"(?i)\bL[0-9a-f]+\b", RegexOptions.Compiled);
    private static readonly Regex _deviceRegex = new(@"(?i)\b([dmxyctl][0-9a-f]+)\b", RegexOptions.Compiled);
    private readonly IPlcDataStore _store;
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// 依存注入による初期化
    /// </summary>
    /// <param name="store">PLCデータストア</param>
    public PlcFaultTracer(IPlcDataStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <summary>
    /// エラーコメント付きLコイルを特定し前段接点を抽出
    /// </summary>
    /// <returns>抽出結果JSON</returns>
    public string TraceErrorCoils()
    {
        var candidates = new List<object>();

        foreach (var program in _store.Programs.Values)
        {
            for (var i = 0; i < program.Count; i++)
            {
                var line = program[i];
                var columns = Split(line);
                var instruction = ExtractInstruction(columns);
                if (!IsOutInstruction(instruction))
                {
                    continue;
                }

                var coils = ExtractCoils(line);
                foreach (var coil in coils)
                {
                    if (!HasErrorComment(coil))
                    {
                        continue;
                    }

                    var related = CollectRelatedDevices(program, i, coil);
                    candidates.Add(new
                    {
                        device = coil.ToUpperInvariant(),
                        comment = GetComment(coil),
                        instruction = instruction ?? string.Empty,
                        line = line?.Trim() ?? string.Empty,
                        relatedDevices = related
                    });
                }
            }
        }

        if (candidates.Count == 0)
        {
            return JsonSerializer.Serialize(new
            {
                status = "not_found",
                message = "エラーコメント付きのLコイルは見つかりませんでした"
            }, _serializerOptions);
        }

        return JsonSerializer.Serialize(new
        {
            status = "success",
            candidates
        }, _serializerOptions);
    }

    private bool HasErrorComment(string coil)
    {
        if (!_store.TryGetComment(coil, out var comment) || string.IsNullOrWhiteSpace(comment))
        {
            return false;
        }

        foreach (var keyword in _errorKeywords)
        {
            if (comment.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private string GetComment(string coil)
    {
        return _store.TryGetComment(coil, out var comment) && !string.IsNullOrWhiteSpace(comment)
            ? comment
            : string.Empty;
    }

    private static IReadOnlyCollection<string> ExtractCoils(string? line)
    {
        return _coilRegex.Matches(line ?? string.Empty)
            .Select(m => m.Value.ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyCollection<string> CollectRelatedDevices(IReadOnlyList<string> program, int index, string coil)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var start = Math.Max(0, index - 2);
        for (var i = start; i <= index; i++)
        {
            var line = program[i];
            foreach (Match match in _deviceRegex.Matches(line ?? string.Empty))
            {
                var device = match.Groups[1].Value;
                if (device.Equals(coil, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                set.Add(device.ToUpperInvariant());
            }
        }

        return set.ToList();
    }

    private static bool IsOutInstruction(string? instruction)
    {
        return string.Equals(instruction, "OUT", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtractInstruction(IReadOnlyList<string> columns)
    {
        if (columns is null || columns.Count < 3)
        {
            return null;
        }

        var value = columns[2];
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim().Trim('"');
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static IReadOnlyList<string> Split(string? line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return Array.Empty<string>();
        }

        var delimiter = line.Contains('\t') ? '\t' : ',';
        return line.Split(delimiter);
    }
}
