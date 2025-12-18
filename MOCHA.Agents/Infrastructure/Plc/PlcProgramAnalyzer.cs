using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using MOCHA.Agents.Domain.Plc;

namespace MOCHA.Agents.Infrastructure.Plc;

/// <summary>
/// プログラム検索と関連デバイス抽出を担当
/// </summary>
public sealed class PlcProgramAnalyzer
{
    private static readonly Regex _deviceRegex = new(@"(?i)\b([dwmxyct]\d+)\b", RegexOptions.Compiled);
    private readonly IPlcDataStore _store;

    public PlcProgramAnalyzer(IPlcDataStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <summary>
    /// 指定デバイスを含む周辺行を返す
    /// </summary>
    public IReadOnlyList<string> GetProgramBlocks(string dev, int address, int context = 30)
    {
        var token = $"{dev}{address}";
        var results = new List<string>();
        foreach (var program in _store.Programs.Values)
        {
            for (var i = 0; i < program.Count; i++)
            {
                if (!ContainsDevice(program[i].Raw, token))
                {
                    continue;
                }

                var start = Math.Max(0, i - context);
                var end = Math.Min(program.Count - 1, i + context);
                var block = program
                    .Skip(start)
                    .Take(end - start + 1)
                    .Select(FormatProgramLine);
                results.Add(string.Join(Environment.NewLine, block));
            }
        }

        return results;
    }

    /// <summary>
    /// 指定デバイスと同じ行に登場するデバイスを列挙
    /// </summary>
    public IReadOnlyList<string> GetRelatedDevices(string dev, int address)
    {
        var target = $"{dev}{address}".ToLowerInvariant();
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var program in _store.Programs.Values)
        {
            for (var i = 0; i < program.Count; i++)
            {
                var line = program[i];
                if (!ContainsDevice(line.Raw, target))
                {
                    continue;
                }

                // 対象行と前後1行をスコープに関連デバイスを抽出
                var start = Math.Max(0, i - 1);
                var end = Math.Min(program.Count - 1, i + 1);
                for (var cursor = start; cursor <= end; cursor++)
                {
                    foreach (Match m in _deviceRegex.Matches(program[cursor].Raw))
                    {
                        var value = m.Groups[1].Value;
                        if (!string.Equals(value, target, StringComparison.OrdinalIgnoreCase))
                        {
                            set.Add(value);
                        }
                    }
                }
            }
        }

        return set.OrderBy(x => x).ToList();
    }

    /// <summary>
    /// コメント取得
    /// </summary>
    public string GetComment(string dev, int address)
    {
        var key = $"{dev}{address}";
        if (_store.TryGetComment(key, out var comment))
        {
            return comment ?? string.Empty;
        }

        // タイマはコメント登録がT表記の場合があるためフォールバック
        if (string.Equals(dev, "TS", StringComparison.OrdinalIgnoreCase)
            && _store.TryGetComment($"T{address}", out comment))
        {
            return comment ?? string.Empty;
        }

        return string.Empty;
    }

    /// <summary>
    /// デバイスの使用箇所からデータ型を推定
    /// </summary>
    /// <param name="device">デバイス種別</param>
    /// <param name="address">アドレス</param>
    /// <returns>推定したデータ型</returns>
    public DeviceDataType InferDeviceDataType(string device, int address)
    {
        if (!IsWordDevice(device))
        {
            return DeviceDataType.Unknown;
        }

        var token = $"{device}{address}";
        foreach (var program in _store.Programs.Values)
        {
            var currentInstruction = string.Empty;
            foreach (var line in program)
            {
                var columns = line.Columns;
                var instruction = ExtractInstruction(columns);
                if (!string.IsNullOrWhiteSpace(instruction))
                {
                    currentInstruction = instruction;
                }

                if (!ContainsDevice(columns, token))
                {
                    continue;
                }

                var classified = Classify(currentInstruction, columns);
                if (classified != DeviceDataType.Unknown)
                {
                    return classified;
                }
            }
        }

        return DeviceDataType.Unknown;
    }

    private static DeviceDataType Classify(string instruction, IReadOnlyList<string> columns)
    {
        var normalizedInstruction = instruction?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedInstruction))
        {
            var upper = normalizedInstruction.ToUpperInvariant();
            if (IsFloatInstruction(upper))
            {
                return DeviceDataType.Float;
            }

            if (IsDoubleWordInstruction(upper))
            {
                return DeviceDataType.DoubleWord;
            }
        }

        foreach (var column in columns ?? Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(column))
            {
                continue;
            }

            var upper = column.Trim().Trim('"').ToUpperInvariant();
            if (upper.Contains("REAL", StringComparison.OrdinalIgnoreCase) ||
                upper.Contains("FLOAT", StringComparison.OrdinalIgnoreCase))
            {
                return DeviceDataType.Float;
            }

            if (upper.Contains("DWORD", StringComparison.OrdinalIgnoreCase) ||
                upper.Contains("DINT", StringComparison.OrdinalIgnoreCase))
            {
                return DeviceDataType.DoubleWord;
            }
        }

        return DeviceDataType.Unknown;
    }

    private static bool IsFloatInstruction(string instruction)
    {
        if (instruction.StartsWith("E", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return instruction.Contains("FLT", StringComparison.OrdinalIgnoreCase) ||
               instruction.Contains("REAL", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDoubleWordInstruction(string instruction)
    {
        if (instruction.StartsWith("DI", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (instruction.StartsWith("D", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return instruction.Contains("DINT", StringComparison.OrdinalIgnoreCase) ||
               instruction.Contains("DWORD", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsWordDevice(string device)
    {
        return string.Equals(device, "D", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(device, "W", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsDevice(IReadOnlyList<string> columns, string token)
    {
        var pattern = new Regex($@"\b{Regex.Escape(token)}\b", RegexOptions.IgnoreCase);
        foreach (var column in columns ?? Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(column))
            {
                continue;
            }

            if (pattern.IsMatch(column))
            {
                return true;
            }
        }

        return false;
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

    private static bool ContainsDevice(string line, string token)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        return line.Contains(token, StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatProgramLine(ProgramLine line)
    {
        var columns = line.Columns ?? Array.Empty<string>();
        var normalized = columns
            .Select(c => c?.Trim().Trim('"'))
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .ToList();

        if (normalized.Count == 0)
        {
            return line.Raw ?? string.Empty;
        }

        return string.Join(' ', normalized);
    }
}
