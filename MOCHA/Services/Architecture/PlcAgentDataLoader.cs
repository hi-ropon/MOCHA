using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MOCHA.Agents.Application;
using MOCHA.Agents.Domain.Plc;
using MOCHA.Models.Architecture;

namespace MOCHA.Services.Architecture;

/// <summary>
/// PLCエージェント用にコメント・プログラムをストアへ流し込むローダー
/// </summary>
internal sealed class PlcAgentDataLoader : IPlcDataLoader
{
    private readonly IPlcUnitRepository _repository;
    private readonly IPlcDataStore _store;
    private readonly ILogger<PlcAgentDataLoader> _logger;

    /// <summary>
    /// 依存関係を受け取って初期化
    /// </summary>
    public PlcAgentDataLoader(
        IPlcUnitRepository repository,
        IPlcDataStore store,
        ILogger<PlcAgentDataLoader> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task LoadAsync(string? userId, string? agentNumber, CancellationToken cancellationToken = default)
    {
        _store.Clear();

        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(agentNumber))
        {
            return;
        }

        var units = await _repository.ListAsync(userId, agentNumber, cancellationToken);
        if (units.Count == 0)
        {
            return;
        }

        var comments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var programs = new List<ProgramFile>();

        foreach (var unit in units)
        {
            if (unit.CommentFile is not null)
            {
                await TryLoadCommentsAsync(unit.CommentFile, comments, cancellationToken);
            }

            foreach (var program in unit.ProgramFiles ?? Array.Empty<PlcFileUpload>())
            {
                await TryLoadProgramAsync(program, programs, cancellationToken);
            }
        }

        _store.SetComments(comments);
        _store.SetPrograms(programs);
    }

    private async Task TryLoadCommentsAsync(PlcFileUpload file, IDictionary<string, string> comments, CancellationToken cancellationToken)
    {
        var path = BuildFullPath(file);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (!File.Exists(path))
        {
            _logger.LogInformation("コメントファイルが見つかりませんでした: {Path}", path);
            return;
        }

        try
        {
            using var reader = new StreamReader(path, Encoding.UTF8);
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

                if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var parts = Split(line);
            if (parts.Length < 2)
            {
                continue;
            }

            var device = TrimQuotes(parts[0].Trim());
            if (string.IsNullOrWhiteSpace(device))
            {
                continue;
            }

            var comment = TrimQuotes(parts[1].Trim());
            comments[device] = comment;
        }
    }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "コメントファイル読取に失敗しました: {Path}", path);
        }
    }

    private async Task TryLoadProgramAsync(PlcFileUpload file, IList<ProgramFile> programs, CancellationToken cancellationToken)
    {
        var path = BuildFullPath(file);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (!File.Exists(path))
        {
            _logger.LogInformation("プログラムファイルが見つかりませんでした: {Path}", path);
            return;
        }

        try
        {
            var lines = await File.ReadAllLinesAsync(path, cancellationToken);
            programs.Add(new ProgramFile(Path.GetFileName(path), lines));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "プログラムファイル読取に失敗しました: {Path}", path);
        }
    }

    private static string? BuildFullPath(PlcFileUpload file)
    {
        if (string.IsNullOrWhiteSpace(file.StorageRoot) || string.IsNullOrWhiteSpace(file.RelativePath))
        {
            return null;
        }

        var root = file.StorageRoot;
        if (!Path.IsPathRooted(root))
        {
            root = Path.GetFullPath(root);
        }

        return Path.Combine(root, file.RelativePath);
    }

    private static string[] Split(string line)
    {
        var delimiter = line.Contains('\t') ? '\t' : ',';
        return line.Split(delimiter);
    }

    private static string TrimQuotes(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value.Trim().Trim('"');
    }
}
