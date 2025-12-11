using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MOCHA.Agents.Application;
using MOCHA.Agents.Domain;
using MOCHA.Agents.Infrastructure.Options;

namespace MOCHA.Agents.Infrastructure.Manuals;

/// <summary>
/// ファイルベースのマニュアル検索・読取ストア
/// </summary>
public sealed class FileManualStore : IManualStore
{
    private readonly ManualStoreOptions _options;
    private readonly ILogger<FileManualStore> _logger;
    private static readonly Regex _bulletLineRegex = new(@"^[\-\*\+]\s*(.+)", RegexOptions.Compiled);
    private static readonly Regex _pageNumberRegex = new(@"p(?:age)?\.?\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// オプションとロガー注入による初期化
    /// </summary>
    /// <param name="options">マニュアルストア設定</param>
    /// <param name="logger">ロガー</param>
    public FileManualStore(IOptions<ManualStoreOptions> options, ILogger<FileManualStore> logger)
    {
        _options = options.Value ?? new ManualStoreOptions();
        _logger = logger;
    }

    /// <summary>
    /// マニュアルインデックス検索
    /// </summary>
    /// <param name="agentName">エージェント名</param>
    /// <param name="query">検索クエリ</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>検索ヒット</returns>
    public async Task<IReadOnlyList<ManualHit>> SearchAsync(
        string agentName,
        string query,
        ManualSearchContext? context = null,
        CancellationToken cancellationToken = default)
    {
        var root = ResolveAgentRoot(agentName);
        if (root is null || !Directory.Exists(root))
        {
            return Array.Empty<ManualHit>();
        }

        var tokens = SplitTokens(query);
        if (tokens.Count == 0)
        {
            return Array.Empty<ManualHit>();
        }

        var hits = new List<ManualHit>();
        foreach (var indexFile in Directory.EnumerateFiles(root, "index.*", SearchOption.AllDirectories))
        {
            var relativeIndex = Path.GetRelativePath(root, indexFile);
            var indexDirectory = Path.GetDirectoryName(indexFile) ?? root;
            var pageMap = BuildPageMap(root, indexDirectory);
            string content;
            try
            {
                content = await File.ReadAllTextAsync(indexFile, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "インデックス読み込みに失敗しました: {Path}", indexFile);
                continue;
            }

            foreach (var line in content.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var entry = ParseIndexEntry(line);
                if (entry is null)
                {
                    continue;
                }

                var score = Score(entry.Text, tokens);
                if (score <= 0)
                {
                    continue;
                }

                var title = ExtractTitle(entry.Text);
                var relativePath = entry.PageNumber is int pageNumber && pageMap.TryGetValue(pageNumber, out var pagePath)
                    ? pagePath
                    : relativeIndex;
                var combinedScore = score + Score(relativePath, tokens);
                hits.Add(new ManualHit(title, relativePath, combinedScore));
            }
        }

        return hits
            .OrderByDescending(h => h.Score)
            .ThenBy(h => h.Title, StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();
    }

    /// <summary>
    /// マニュアル内容の読み取り
    /// </summary>
    /// <param name="agentName">エージェント名</param>
    /// <param name="relativePath">マニュアル相対パス</param>
    /// <param name="maxBytes">読み取り上限バイト数</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>読み取ったマニュアル</returns>
    public async Task<ManualContent?> ReadAsync(
        string agentName,
        string relativePath,
        int? maxBytes = null,
        ManualSearchContext? context = null,
        CancellationToken cancellationToken = default)
    {
        var root = ResolveAgentRoot(agentName);
        if (root is null || !Directory.Exists(root))
        {
            return null;
        }

        var combined = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!combined.StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!File.Exists(combined))
        {
            return null;
        }

        try
        {
            var limit = maxBytes ?? _options.MaxReadBytes;
            string content;
            if (limit is not null)
            {
                using var fs = File.OpenRead(combined);
                using var ms = new MemoryStream();
                await fs.CopyToAsync(ms, limit.Value, cancellationToken);
                var buffer = ms.ToArray();
                if (buffer.Length > limit.Value)
                {
                    content = Encoding.UTF8.GetString(buffer, 0, limit.Value);
                }
                else
                {
                    content = Encoding.UTF8.GetString(buffer);
                }
            }
            else
            {
                content = await File.ReadAllTextAsync(combined, cancellationToken);
            }

            var rel = Path.GetRelativePath(root, combined);
            return new ManualContent(rel, content, content.Length);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "マニュアル読取に失敗しました: {Path}", combined);
            return null;
        }
    }

    /// <summary>
    /// エージェントごとのルートパス解決
    /// </summary>
    /// <param name="agentName">エージェント名</param>
    /// <returns>ルートパス</returns>
    private string? ResolveAgentRoot(string agentName)
    {
        var normalized = NormalizeAgentName(agentName);
        if (!_options.AgentFolders.TryGetValue(normalized, out var folder))
        {
            return null;
        }

        return Path.Combine(AppContext.BaseDirectory, _options.BasePath, folder, "docs");
    }

    /// <summary>
    /// エージェント名正規化
    /// </summary>
    /// <param name="agentName">入力エージェント名</param>
    /// <returns>正規化済みエージェント名</returns>
    private static string NormalizeAgentName(string agentName)
    {
        if (string.IsNullOrWhiteSpace(agentName))
        {
            return agentName;
        }

        // "IAI/Oriental/PLC" のような複合指定の場合は先頭要素を使用
        var first = agentName.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        var token = (first ?? agentName).Trim();
        if (token.EndsWith("Agent", StringComparison.OrdinalIgnoreCase))
        {
            return token;
        }

        var lowered = token.ToLowerInvariant();
        return lowered switch
        {
            "iai" => "iaiAgent",
            "oriental" => "orientalAgent",
            "plc" => "plcAgent",
            _ => token
        };
    }

    /// <summary>
    /// クエリ文字列のトークン分割
    /// </summary>
    /// <param name="query">検索クエリ</param>
    /// <returns>分割トークン</returns>
    private static List<string> SplitTokens(string query)
    {
        return Regex.Matches(query ?? string.Empty, @"[\p{L}\p{N}\-_\.]+")
            .Select(m => NormalizeForSearch(m.Value))
            .Where(s => s.Length > 1)
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// トークン一致によるスコア計算
    /// </summary>
    /// <param name="text">対象テキスト</param>
    /// <param name="tokens">検索トークン</param>
    /// <returns>スコア</returns>
    private static double Score(string text, IReadOnlyCollection<string> tokens)
    {
        if (tokens.Count == 0)
        {
            return 0;
        }

        var normalized = NormalizeForSearch(text);
        var entryTokens = SplitTokens(normalized);
        double score = 0;
        foreach (var token in tokens)
        {
            if (string.IsNullOrEmpty(token))
            {
                continue;
            }

            if (normalized.Contains(token, StringComparison.Ordinal))
            {
                score += 1;
                continue;
            }

            var bestFuzzy = entryTokens
                .Select(entryToken => GetFuzzyScore(entryToken, token))
                .DefaultIfEmpty(0.0)
                .Max();

            if (bestFuzzy >= 0.7)
            {
                score += 0.6;
            }
            else if (bestFuzzy > 0)
            {
                score += bestFuzzy * 0.5;
            }
        }

        return score;
    }

    /// <summary>
    /// 行からタイトル部分を抽出
    /// </summary>
    /// <param name="line">インデックス行</param>
    /// <returns>抽出タイトル</returns>
    private static string ExtractTitle(string line)
    {
        var trimmed = line.Trim();
        while (trimmed.StartsWith("#", StringComparison.Ordinal))
        {
            trimmed = trimmed[1..].TrimStart();
        }

        trimmed = trimmed.TrimStart('-', ' ');
        var colon = trimmed.IndexOf(':');
        trimmed = colon > 0 ? trimmed[..colon].Trim() : trimmed;

        var pageMatch = _pageNumberRegex.Match(trimmed);
        if (pageMatch.Success)
        {
            trimmed = trimmed[..pageMatch.Index].TrimEnd(' ', '(', ')', ':', '-');
        }

        return string.IsNullOrEmpty(trimmed) ? line.Trim() : trimmed;
    }

    /// <summary>
    /// 検索用に文字列を正規化
    /// </summary>
    /// <param name="text">対象テキスト</param>
    /// <returns>正規化済み文字列</returns>
    private static string NormalizeForSearch(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return text
            .Normalize(NormalizationForm.FormKC)
            .ToLowerInvariant();
    }

    private static IndexEntry? ParseIndexEntry(string line)
    {
        var trimmed = line.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        var match = _bulletLineRegex.Match(trimmed);
        if (!match.Success)
        {
            return null;
        }

        var text = match.Groups[1].Value.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var pageNumber = TryExtractPageNumber(text, out var page) ? page : (int?)null;
        return new IndexEntry(text, pageNumber);
    }

    private static Dictionary<int, string> BuildPageMap(string root, string indexDirectory)
    {
        var map = new Dictionary<int, string>();
        foreach (var file in Directory.EnumerateFiles(indexDirectory, "*page*.txt", SearchOption.AllDirectories))
        {
            if (!TryExtractPageNumber(Path.GetFileName(file), out var pageNumber))
            {
                continue;
            }

            if (map.ContainsKey(pageNumber))
            {
                continue;
            }

            var relative = Path.GetRelativePath(root, file);
            map[pageNumber] = relative;
        }

        return map;
    }

    private static bool TryExtractPageNumber(string text, out int page)
    {
        var match = _pageNumberRegex.Match(text);
        if (!match.Success)
        {
            page = 0;
            return false;
        }

        return int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out page);
    }

    private static double GetFuzzyScore(string source, string target)
    {
        if (string.Equals(source, target, StringComparison.Ordinal))
        {
            return 1;
        }

        if (source.Length == 0 || target.Length == 0)
        {
            return 0;
        }

        var max = Math.Max(source.Length, target.Length);
        if (max == 0)
        {
            return 0;
        }

        var distance = ComputeLevenshteinDistance(source, target);
        var similarity = 1.0 - (double)distance / max;
        return similarity > 0 ? similarity : 0;
    }

    private static int ComputeLevenshteinDistance(string source, string target)
    {
        var sourceLength = source.Length;
        var targetLength = target.Length;

        if (sourceLength == 0)
        {
            return targetLength;
        }

        if (targetLength == 0)
        {
            return sourceLength;
        }

        var previous = new int[targetLength + 1];
        for (var j = 0; j <= targetLength; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= sourceLength; i++)
        {
            var current = new int[targetLength + 1];
            current[0] = i;
            var sourceChar = source[i - 1];
            for (var j = 1; j <= targetLength; j++)
            {
                var cost = sourceChar == target[j - 1] ? 0 : 1;
                var insertion = current[j - 1] + 1;
                var deletion = previous[j] + 1;
                var substitution = previous[j - 1] + cost;
                current[j] = Math.Min(Math.Min(insertion, deletion), substitution);
            }

            previous = current;
        }

        return previous[targetLength];
    }

    private sealed record IndexEntry(string Text, int? PageNumber);
}
