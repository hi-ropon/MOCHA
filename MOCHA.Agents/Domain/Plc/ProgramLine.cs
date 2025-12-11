using System.Collections.Generic;

namespace MOCHA.Agents.Domain.Plc;

/// <summary>
/// PLCプログラムの1行
/// </summary>
public sealed record ProgramLine
{
    /// <summary>
    /// 行テキスト
    /// </summary>
    public string Raw { get; }

    /// <summary>
    /// タブ/カンマ区切りの列
    /// </summary>
    public IReadOnlyList<string> Columns { get; }

    /// <summary>
    /// 行内容の指定による初期化
    /// </summary>
    /// <param name="raw">生テキスト</param>
    /// <param name="columns">分解済み列</param>
    public ProgramLine(string? raw, IReadOnlyList<string>? columns)
    {
        Raw = raw ?? string.Empty;
        Columns = columns ?? System.Array.Empty<string>();
    }
}
