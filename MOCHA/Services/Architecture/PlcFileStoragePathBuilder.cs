using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Options;
using MOCHA.Models.Architecture;

namespace MOCHA.Services.Architecture;

/// <summary>
/// PLCファイルの保存パスを生成するビルダー
/// </summary>
internal sealed class PlcFileStoragePathBuilder : IPlcFileStoragePathBuilder
{
    private readonly PlcStorageOptions _options;
    private readonly Func<DateTimeOffset> _clock;
    private readonly HashSet<char> _invalidChars;

    /// <summary>
    /// 設定とクロックで初期化
    /// </summary>
    public PlcFileStoragePathBuilder(IOptions<PlcStorageOptions> options, Func<DateTimeOffset>? clock = null)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        if (string.IsNullOrWhiteSpace(_options.RootPath))
        {
            throw new ArgumentException("RootPath を設定してください", nameof(options));
        }

        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        _invalidChars = new HashSet<char>(Path.GetInvalidFileNameChars().Concat(Path.GetInvalidPathChars()));
    }

    /// <inheritdoc />
    public PlcFileStoragePath Build(string agentNumber, string fileName, string category)
    {
        if (string.IsNullOrWhiteSpace(agentNumber))
        {
            throw new ArgumentException("エージェント番号は必須です", nameof(agentNumber));
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("ファイル名は必須です", nameof(fileName));
        }

        var now = _clock();
        var safeAgent = Sanitize(agentNumber);
        var safeCategory = Sanitize(string.IsNullOrWhiteSpace(category) ? "files" : category);
        var safeFileName = Sanitize(fileName);
        var stampedFileName = $"{now:yyyyMMddHHmmssfff}_{safeFileName}";
        var relativePath = Path.Combine(safeAgent, safeCategory, stampedFileName);
        var directoryPath = Path.Combine(_options.RootPath, safeAgent, safeCategory);
        var fullPath = Path.Combine(_options.RootPath, relativePath);

        return new PlcFileStoragePath(
            _options.RootPath,
            relativePath,
            directoryPath,
            stampedFileName,
            fullPath);
    }

    private string Sanitize(string input)
    {
        var builder = new StringBuilder(input.Length);
        foreach (var c in input.Trim())
        {
            builder.Append(_invalidChars.Contains(c) ? '_' : c);
        }

        var result = builder.ToString();
        return string.IsNullOrWhiteSpace(result) ? "unknown" : result;
    }
}
