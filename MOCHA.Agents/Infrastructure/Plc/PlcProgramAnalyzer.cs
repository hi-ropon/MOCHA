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
    private static readonly Regex _deviceRegex = new(@"(?i)\b([dmxyct]\d+)\b", RegexOptions.Compiled);
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
                if (!ContainsDevice(program[i], token))
                {
                    continue;
                }

                var start = Math.Max(0, i - context);
                var end = Math.Min(program.Count - 1, i + context);
                var block = program.Skip(start).Take(end - start + 1);
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
                if (!ContainsDevice(line, target))
                {
                    continue;
                }

                // 対象行と前後1行をスコープに関連デバイスを抽出
                var start = Math.Max(0, i - 1);
                var end = Math.Min(program.Count - 1, i + 1);
                for (var cursor = start; cursor <= end; cursor++)
                {
                    foreach (Match m in _deviceRegex.Matches(program[cursor]))
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
        return _store.TryGetComment($"{dev}{address}", out var comment)
            ? comment ?? string.Empty
            : string.Empty;
    }

    private static bool ContainsDevice(string line, string token)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        return line.Contains(token, StringComparison.OrdinalIgnoreCase);
    }
}
