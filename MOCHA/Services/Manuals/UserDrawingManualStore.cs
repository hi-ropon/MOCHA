using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MOCHA.Agents.Application;
using MOCHA.Agents.Domain;
using MOCHA.Agents.Infrastructure.Manuals;
using MOCHA.Agents.Infrastructure.Options;
using MOCHA.Models.Drawings;
using MOCHA.Services.Drawings;
using Microsoft.Extensions.DependencyInjection;
using SimMetrics.Net.Metric;

namespace MOCHA.Services.Manuals;

/// <summary>
/// リソースマニュアルに加えてユーザー登録図面を検索対象に含めるストア
/// </summary>
internal sealed class UserDrawingManualStore : IManualStore
{
    private readonly FileManualStore _fileStore;
    private readonly ILogger<UserDrawingManualStore> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly JaroWinkler _fuzzyMetric = new();

    /// <summary>
    /// ファイルストアと図面リポジトリを注入して初期化
    /// </summary>
    public UserDrawingManualStore(
        IOptions<ManualStoreOptions> manualOptions,
        ILogger<UserDrawingManualStore> logger,
        ILogger<FileManualStore> fileLogger,
        IServiceScopeFactory scopeFactory)
    {
        _fileStore = new FileManualStore(manualOptions, fileLogger);
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    /// <summary>
    /// マニュアル検索（図面を含む）
    /// </summary>
    public async Task<IReadOnlyList<ManualHit>> SearchAsync(
        string agentName,
        string query,
        ManualSearchContext? context = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<ManualHit>();

        var manualHits = await _fileStore.SearchAsync(agentName, query, context, cancellationToken);
        results.AddRange(manualHits);

        var drawingHits = await SearchDrawingsAsync(query, context, cancellationToken);
        results.AddRange(drawingHits);

        return results
            .OrderByDescending(h => h.Score)
            .ThenBy(h => h.Title, StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();
    }

    /// <summary>
    /// マニュアル読取（図面はメタ情報を返す）
    /// </summary>
    public async Task<ManualContent?> ReadAsync(
        string agentName,
        string relativePath,
        int? maxBytes = null,
        ManualSearchContext? context = null,
        CancellationToken cancellationToken = default)
    {
        if (relativePath.StartsWith("drawing:", StringComparison.OrdinalIgnoreCase))
        {
            return await ReadDrawingAsync(relativePath, context, maxBytes, cancellationToken);
        }

        return await _fileStore.ReadAsync(agentName, relativePath, maxBytes, context, cancellationToken);
    }

    private async Task<IReadOnlyList<ManualHit>> SearchDrawingsAsync(
        string query,
        ManualSearchContext? context,
        CancellationToken cancellationToken)
    {
        if (context is null || string.IsNullOrWhiteSpace(context.AgentNumber))
        {
            return Array.Empty<ManualHit>();
        }

        var tokens = SplitTokens(query);
        var normalizedQuery = Normalize(query);
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return Array.Empty<ManualHit>();
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IDrawingRepository>();
            var drawings = await repository.ListAsync(context.AgentNumber, cancellationToken);
            var hits = new List<ManualHit>();

            foreach (var drawing in drawings)
            {
                var score = Score(drawing.FileName, tokens) + Score(drawing.Description ?? string.Empty, tokens);
                var fuzzy = FuzzyScore(drawing.FileName, tokens, normalizedQuery) +
                            FuzzyScore(drawing.Description ?? string.Empty, tokens, normalizedQuery) +
                            FuzzyScore(StripTimestampPrefix(drawing.FileName), tokens, normalizedQuery);
                var total = score + fuzzy;
                if (total <= 0)
                {
                    continue;
                }

                var title = string.IsNullOrWhiteSpace(drawing.Description)
                    ? drawing.FileName
                    : $"{drawing.FileName} - {drawing.Description}";
                hits.Add(new ManualHit(title, $"drawing:{drawing.Id}", total));
            }

            return hits;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "図面検索に失敗しました。");
            return Array.Empty<ManualHit>();
        }
    }

    private async Task<ManualContent?> ReadDrawingAsync(
        string relativePath,
        ManualSearchContext? context,
        int? maxBytes,
        CancellationToken cancellationToken)
    {
        if (context is null || string.IsNullOrWhiteSpace(context.AgentNumber))
        {
            return null;
        }

        if (!Guid.TryParse(relativePath["drawing:".Length..], out var id))
        {
            return null;
        }

        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IDrawingRepository>();
        var catalog = scope.ServiceProvider.GetRequiredService<DrawingCatalog>();
        var reader = scope.ServiceProvider.GetRequiredService<DrawingContentReader>();

        var drawing = await repository.GetAsync(id, cancellationToken);
        if (drawing is null || !string.Equals(drawing.AgentNumber, context.AgentNumber, StringComparison.Ordinal))
        {
            return null;
        }

        var file = await catalog.FindAsync(context.AgentNumber, id, cancellationToken);

        var builder = new StringBuilder();
        builder.AppendLine($"図面: {drawing.FileName}");
        if (!string.IsNullOrWhiteSpace(drawing.Description))
        {
            builder.AppendLine($"説明: {drawing.Description}");
        }

        builder.AppendLine($"サイズ: {drawing.FileSize} bytes");

        if (file is not null && file.Exists)
        {
            var result = await reader.ReadAsync(file, maxBytes: maxBytes, query: context.Query, cancellationToken: cancellationToken);
            if (result.Succeeded && !string.IsNullOrWhiteSpace(result.Content))
            {
                builder.AppendLine($"ヒット総数: {result.TotalHits}");
                builder.AppendLine(result.Content);
                if (result.IsTruncated && !result.Content.Contains("※読み取りを途中で打ち切りました", StringComparison.Ordinal))
                {
                    builder.AppendLine("※読み取りを途中で打ち切りました");
                }
            }
            else if (!string.IsNullOrWhiteSpace(result.Error))
            {
                builder.AppendLine(result.Error);
            }
            else if (!string.IsNullOrWhiteSpace(result.Content))
            {
                builder.AppendLine(result.Content);
            }
        }
        else
        {
            builder.AppendLine("図面ファイルが見つかりません。管理者に確認してください。");
        }

        var content = builder.ToString().TrimEnd();
        return new ManualContent(relativePath, content, content.Length);
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

    private double FuzzyScore(string text, IReadOnlyCollection<string> tokens, string normalizedQuery)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        var normalized = Normalize(text);
        var candidates = new List<double>();
        if (!string.IsNullOrWhiteSpace(normalizedQuery))
        {
            candidates.Add(_fuzzyMetric.GetSimilarity(normalized, normalizedQuery));
        }

        foreach (var token in tokens)
        {
            candidates.Add(_fuzzyMetric.GetSimilarity(normalized, token));
        }

        var best = candidates.Count == 0 ? 0 : candidates.Max();
        return best >= 0.6 ? best : 0;
    }

    private static string StripTimestampPrefix(string text)
    {
        return Regex.Replace(text, @"^\d+[_-]?", string.Empty);
    }

    private static string Normalize(string? text)
    {
        return (text ?? string.Empty)
            .ToLowerInvariant()
            .Normalize(NormalizationForm.FormKC);
    }
}
