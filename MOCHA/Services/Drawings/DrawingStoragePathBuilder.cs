using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Options;
using MOCHA.Models.Drawings;

namespace MOCHA.Services.Drawings;

/// <summary>
/// ローカルフォルダ向けの図面保存パスビルダー
/// </summary>
internal sealed class DrawingStoragePathBuilder : IDrawingStoragePathBuilder
{
    private readonly DrawingStorageOptions _options;
    private readonly Func<DateTimeOffset> _clock;
    private readonly HashSet<char> _invalidChars;

    /// <summary>
    /// ルートとクロックを受け取って初期化する
    /// </summary>
    /// <param name="options">ストレージ設定</param>
    /// <param name="clock">時刻プロバイダー</param>
    public DrawingStoragePathBuilder(
        IOptions<DrawingStorageOptions> options,
        Func<DateTimeOffset>? clock = null)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        if (string.IsNullOrWhiteSpace(_options.RootPath))
        {
            throw new ArgumentException("RootPath を設定してください", nameof(options));
        }

        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        _invalidChars = new HashSet<char>(Path.GetInvalidFileNameChars()
            .Concat(Path.GetInvalidPathChars()));
    }

    /// <inheritdoc />
    public DrawingStoragePath Build(string agentNumber, string fileName)
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
        var safeFileName = Sanitize(fileName);
        var stampedFileName = $"{now:yyyyMMddHHmmssfff}_{safeFileName}";
        var relativePath = Path.Combine(safeAgent, stampedFileName);
        var directoryPath = Path.Combine(_options.RootPath, safeAgent);
        var fullPath = Path.Combine(_options.RootPath, relativePath);

        return new DrawingStoragePath(
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
