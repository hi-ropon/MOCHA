using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MOCHA.Agents.Domain.Plc;

namespace MOCHA.Agents.Infrastructure.Plc;

/// <summary>
/// コメントとプログラムを保持するインメモリストア
/// </summary>
public sealed class PlcDataStore : IPlcDataStore
{
    private readonly ConcurrentDictionary<string, string> _comments = new();
    private readonly ConcurrentDictionary<string, IReadOnlyList<string>> _programs = new();

    /// <inheritdoc />
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Programs => _programs;

    /// <inheritdoc />
    public void Clear()
    {
        _comments.Clear();
        _programs.Clear();
    }

    /// <inheritdoc />
    public void SetComments(IDictionary<string, string> comments)
    {
        _comments.Clear();
        foreach (var kv in comments)
        {
            var key = kv.Key?.Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            _comments[key] = kv.Value?.Trim() ?? string.Empty;
        }
    }

    /// <inheritdoc />
    public void SetPrograms(IEnumerable<ProgramFile> programs)
    {
        _programs.Clear();
        foreach (var program in programs)
        {
            if (string.IsNullOrWhiteSpace(program.Name))
            {
                continue;
            }

            _programs[program.Name] = program.Lines.ToList();
        }
    }

    /// <inheritdoc />
    public bool TryGetComment(string device, out string? comment)
    {
        if (string.IsNullOrWhiteSpace(device))
        {
            comment = null;
            return false;
        }

        return _comments.TryGetValue(device.Trim(), out comment);
    }

    /// <summary>
    /// CSVファイルからコメントを読み込む（ヘッダー対応）
    /// </summary>
    public async Task LoadCommentsAsync(string path, CancellationToken cancellationToken = default)
    {
        using var stream = File.OpenRead(path);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        var map = new Dictionary<string, string>();
        string? line;
        var isFirst = true;
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (isFirst)
            {
                isFirst = false;
                if (line.Contains("device", StringComparison.OrdinalIgnoreCase) &&
                    line.Contains("comment", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }

            var parts = SplitCsvLine(line);
            if (parts.Length < 2)
            {
                continue;
            }

            var device = parts[0].Trim();
            var comment = parts[1].Trim();
            if (!string.IsNullOrWhiteSpace(device))
            {
                map[device] = comment;
            }
        }

        SetComments(map);
    }

    /// <summary>
    /// プログラムファイル群を読み込む
    /// </summary>
    public async Task LoadProgramsAsync(IEnumerable<string> paths, CancellationToken cancellationToken = default)
    {
        var programs = new List<ProgramFile>();
        foreach (var path in paths ?? Enumerable.Empty<string>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(path))
            {
                continue;
            }

            var lines = (await File.ReadAllLinesAsync(path, cancellationToken)).ToList();
            programs.Add(new ProgramFile(Path.GetFileName(path), lines));
        }

        SetPrograms(programs);
    }

    private static string[] SplitCsvLine(string line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return Array.Empty<string>();
        }

        var delimiter = line.Contains('\t') ? '\t' : ',';
        return line.Split(delimiter);
    }
}
