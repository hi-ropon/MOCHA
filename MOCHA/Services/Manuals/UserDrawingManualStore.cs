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

namespace MOCHA.Services.Manuals;

/// <summary>
/// リソースマニュアルに加えてユーザー登録図面を検索対象に含めるストア
/// </summary>
internal sealed class UserDrawingManualStore : IManualStore
{
    private readonly FileManualStore _fileStore;
    private readonly ILogger<UserDrawingManualStore> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

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
            return await ReadDrawingAsync(relativePath, context, cancellationToken);
        }

        return await _fileStore.ReadAsync(agentName, relativePath, maxBytes, context, cancellationToken);
    }

    private async Task<IReadOnlyList<ManualHit>> SearchDrawingsAsync(
        string query,
        ManualSearchContext? context,
        CancellationToken cancellationToken)
    {
        if (context is null || string.IsNullOrWhiteSpace(context.UserId) || string.IsNullOrWhiteSpace(context.AgentNumber))
        {
            return Array.Empty<ManualHit>();
        }

        var tokens = SplitTokens(query);
        if (tokens.Count == 0)
        {
            return Array.Empty<ManualHit>();
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IDrawingRepository>();
            var drawings = await repository.ListAsync(context.UserId, context.AgentNumber, cancellationToken);
            var hits = new List<ManualHit>();

            foreach (var drawing in drawings)
            {
                var score = Score(drawing.FileName, tokens) + Score(drawing.Description ?? string.Empty, tokens);
                if (score <= 0)
                {
                    continue;
                }

                var title = string.IsNullOrWhiteSpace(drawing.Description)
                    ? drawing.FileName
                    : $"{drawing.FileName} - {drawing.Description}";
                hits.Add(new ManualHit(title, $"drawing:{drawing.Id}", score));
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
        CancellationToken cancellationToken)
    {
        if (context is null || string.IsNullOrWhiteSpace(context.UserId) || string.IsNullOrWhiteSpace(context.AgentNumber))
        {
            return null;
        }

        if (!Guid.TryParse(relativePath["drawing:".Length..], out var id))
        {
            return null;
        }

        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IDrawingRepository>();
        var drawing = await repository.GetAsync(id, cancellationToken);
        if (drawing is null || !string.Equals(drawing.UserId, context.UserId, StringComparison.Ordinal) ||
            !string.Equals(drawing.AgentNumber, context.AgentNumber, StringComparison.Ordinal))
        {
            return null;
        }

        var builder = new StringBuilder();
        builder.AppendLine($"図面: {drawing.FileName}");
        if (!string.IsNullOrWhiteSpace(drawing.Description))
        {
            builder.AppendLine($"説明: {drawing.Description}");
        }

        builder.AppendLine($"サイズ: {drawing.FileSize} bytes");
        builder.AppendLine("ファイル本体のプレビューはチャットからは参照できません。必要に応じて管理者に確認してください。");

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
}
