using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MOCHA.Agents.Application;
using MOCHA.Agents.Domain;
using MOCHA.Agents.Infrastructure.Options;

namespace MOCHA.Agents.Infrastructure.Manuals;

/// <summary>
/// ファイルベースのマニュアル検索・読取。
/// </summary>
public sealed class FileManualStore : IManualStore
{
    private readonly ManualStoreOptions _options;
    private readonly ILogger<FileManualStore> _logger;

    public FileManualStore(IOptions<ManualStoreOptions> options, ILogger<FileManualStore> logger)
    {
        _options = options.Value ?? new ManualStoreOptions();
        _logger = logger;
    }

    public async Task<IReadOnlyList<ManualHit>> SearchAsync(string agentName, string query, CancellationToken cancellationToken = default)
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
            var relative = Path.GetRelativePath(root, indexFile);
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
                var trimmed = line.Trim();
                if (!trimmed.StartsWith("-"))
                {
                    continue;
                }

                var score = Score(trimmed, tokens);
                if (score <= 0)
                {
                    continue;
                }

                var title = ExtractTitle(trimmed);
                var combinedScore = score + Score(relative, tokens);
                hits.Add(new ManualHit(title, relative, combinedScore));
            }
        }

        return hits
            .OrderByDescending(h => h.Score)
            .ThenBy(h => h.Title, StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();
    }

    public async Task<ManualContent?> ReadAsync(string agentName, string relativePath, int? maxBytes = null, CancellationToken cancellationToken = default)
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

    private string? ResolveAgentRoot(string agentName)
    {
        var normalized = NormalizeAgentName(agentName);
        if (!_options.AgentFolders.TryGetValue(normalized, out var folder))
        {
            return null;
        }

        return Path.Combine(AppContext.BaseDirectory, _options.BasePath, folder, "docs");
    }

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

    private static List<string> SplitTokens(string query)
    {
        return Regex.Matches(query ?? string.Empty, @"[\p{L}\p{N}\-_\.]+")
            .Select(m => m.Value.ToLowerInvariant())
            .Where(s => s.Length > 1)
            .Distinct()
            .ToList();
    }

    private static double Score(string text, IReadOnlyCollection<string> tokens)
    {
        var lowered = text.ToLowerInvariant();
        var score = 0.0;
        foreach (var token in tokens)
        {
            if (lowered.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                score += 1;
            }
        }
        return score;
    }

    private static string ExtractTitle(string line)
    {
        var trimmed = line.TrimStart('-', ' ');
        var colon = trimmed.IndexOf(':');
        return colon > 0 ? trimmed[..colon].Trim() : trimmed;
    }
}
