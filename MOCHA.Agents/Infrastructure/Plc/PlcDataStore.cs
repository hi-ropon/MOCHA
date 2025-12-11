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
    private readonly ITabularProgramParser _programParser;
    private readonly ConcurrentDictionary<string, string> _comments = new();
    private readonly ConcurrentDictionary<string, IReadOnlyList<ProgramLine>> _programs = new();
    private readonly ConcurrentDictionary<string, FunctionBlockData> _functionBlocks = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// パーサ注入による初期化
    /// </summary>
    /// <param name="programParser">プログラム行パーサ</param>
    public PlcDataStore(ITabularProgramParser programParser)
    {
        _programParser = programParser ?? throw new ArgumentNullException(nameof(programParser));
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, IReadOnlyList<ProgramLine>> Programs => _programs;
    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> Comments => _comments;
    /// <inheritdoc />
    public IReadOnlyCollection<FunctionBlockData> FunctionBlocks => _functionBlocks.Values.ToList();

    /// <inheritdoc />
    public void Clear()
    {
        _comments.Clear();
        _programs.Clear();
        _functionBlocks.Clear();
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

            var parsedLines = new List<ProgramLine>();
            foreach (var line in program.Lines ?? Array.Empty<string>())
            {
                parsedLines.Add(_programParser.Parse(line));
            }

            _programs[program.Name] = parsedLines;
        }
    }

    /// <inheritdoc />
    public void SetFunctionBlocks(IEnumerable<FunctionBlockData> blocks)
    {
        _functionBlocks.Clear();
        foreach (var block in blocks ?? Enumerable.Empty<FunctionBlockData>())
        {
            if (string.IsNullOrWhiteSpace(block.Name))
            {
                continue;
            }

            _functionBlocks[block.Name.Trim()] = block;
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

    /// <inheritdoc />
    public bool TryGetFunctionBlock(string name, out FunctionBlockData? block)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            block = null;
            return false;
        }

        return _functionBlocks.TryGetValue(name.Trim(), out block);
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
