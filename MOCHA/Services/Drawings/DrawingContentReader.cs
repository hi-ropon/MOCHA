using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MOCHA.Models.Drawings;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace MOCHA.Services.Drawings;

/// <summary>
/// 図面ファイルの読取サービス
/// </summary>
public sealed class DrawingContentReader
{
    private const int _defaultMaxBytes = 10_000_000;
    private const int _defaultMaxPages = 200;
    private const int _defaultSnippetRadius = 120;
    private const int _maxSnippets = 3;
    private readonly ILogger<DrawingContentReader> _logger;

    /// <summary>
    /// ロガーを受け取って初期化
    /// </summary>
    public DrawingContentReader(ILogger<DrawingContentReader>? logger = null)
    {
        _logger = logger ?? NullLogger<DrawingContentReader>.Instance;
    }

    /// <summary>
    /// 図面ファイルを読み取る
    /// </summary>
    /// <param name="file">図面ファイル参照</param>
    /// <param name="maxBytes">最大読取バイト数</param>
    /// <param name="query">検索クエリ</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>読取結果</returns>
    public async Task<DrawingContentResult> ReadAsync(DrawingFile file, int? maxBytes = null, string? query = null, CancellationToken cancellationToken = default)
    {
        if (file is null)
        {
            throw new ArgumentNullException(nameof(file));
        }

        if (!file.Exists || string.IsNullOrWhiteSpace(file.FullPath))
        {
            return DrawingContentResult.Fail("図面ファイルが見つかりません");
        }

        var limit = maxBytes.GetValueOrDefault(_defaultMaxBytes);
        var ext = file.Extension?.ToLowerInvariant() ?? string.Empty;
        var tokens = NormalizeTokens(query);
        _logger.LogInformation("図面読取開始: {Path} ext={Ext} limit={Limit} tokens={TokenCount}", file.FullPath, ext, limit, tokens.Count);

        if (IsPlainText(ext))
        {
            return await ReadTextAsync(file, limit, tokens, cancellationToken);
        }

        if (ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return ReadPdf(file, limit, tokens);
        }

        var fallback = $"この形式のプレビューは未対応です: {ext}";
        return DrawingContentResult.Preview(fallback, file.FullPath);
    }

    private static bool IsPlainText(string extension)
    {
        return extension is ".txt" or ".log" or ".md" or ".csv";
    }

    private static async Task<DrawingContentResult> ReadTextAsync(DrawingFile file, int maxBytes, IReadOnlyList<string> tokens, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(file.FullPath!);
        var bufferLength = (int)Math.Min(maxBytes, Math.Max(1, stream.Length));
        var buffer = new byte[bufferLength];
        var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
        var content = Encoding.UTF8.GetString(buffer, 0, read);
        var truncated = stream.Length > read;
        var normalized = Normalize(content);
        var hits = CountHits(normalized, tokens);
        var matches = BuildSingleSnippet(content, tokens, hits);
        return DrawingContentResult.Success(content, file.FullPath, read, truncated, hits, matches);
    }

    private DrawingContentResult ReadPdf(DrawingFile file, int maxBytes, IReadOnlyList<string> tokens)
    {
        if (tokens.Count == 0)
        {
            return ReadPdfFullText(file, maxBytes);
        }

        try
        {
            using var document = PdfDocument.Open(file.FullPath!);
            var matches = new List<DrawingContentMatch>();
            var totalHits = 0;
            var bytesRead = 0;
            var truncated = false;
            string? firstNonEmptyText = null;
            var emptyPages = 0;

            var pageCount = Math.Min(document.NumberOfPages, _defaultMaxPages);
            for (var i = 1; i <= pageCount; i++)
            {
                var page = document.GetPage(i);
                var text = ExtractPageText(page);
                if (string.IsNullOrWhiteSpace(text))
                {
                    emptyPages++;
                }
                if (firstNonEmptyText is null && !string.IsNullOrWhiteSpace(text))
                {
                    firstNonEmptyText = text;
                }
                var normalized = Normalize(text);
                var pageHits = CountHits(normalized, tokens);
                if (pageHits > 0)
                {
                    totalHits += pageHits;
                    var snippet = BuildSnippet(text, normalized, tokens);
                    matches.Add(new DrawingContentMatch
                    {
                        PageNumber = i,
                        Score = pageHits,
                        Snippet = snippet
                    });
                }

                bytesRead += Encoding.UTF8.GetByteCount(text);
                if (bytesRead >= maxBytes)
                {
                    truncated = true;
                    break;
                }
            }

            var topMatches = matches
                .OrderByDescending(m => m.Score)
                .ThenBy(m => m.PageNumber)
                .Take(_maxSnippets)
                .ToList();

            if (topMatches.Count == 0 && !string.IsNullOrWhiteSpace(firstNonEmptyText))
            {
                var snippet = BuildSnippet(firstNonEmptyText, Normalize(firstNonEmptyText), tokens);
                topMatches.Add(new DrawingContentMatch
                {
                    PageNumber = 1,
                    Score = totalHits,
                    Snippet = snippet
                });
            }

            if (totalHits == 0)
            {
                _logger.LogInformation("PDFヒットなし。全文モードにフォールバックします: {Path}", file.FullPath);
                return ReadPdfFullText(file, maxBytes);
            }

            var content = BuildContentSummary(topMatches, totalHits, truncated);
            if (string.IsNullOrWhiteSpace(content))
            {
                content = "PDFからテキストを抽出できませんでした";
            }

            _logger.LogInformation("PDF読取完了: {Path} pages={Pages} hits={Hits} bytes={Bytes} truncated={Truncated} emptyPages={EmptyPages}", file.FullPath, pageCount, totalHits, bytesRead, truncated, emptyPages);
            return DrawingContentResult.Success(content, file.FullPath, bytesRead, truncated, totalHits, topMatches);
        }
        catch (Exception ex)
        {
            var message = $"PDF読取に失敗しました: {ex.Message}";
            return DrawingContentResult.Preview(message, file.FullPath);
        }
    }

    private DrawingContentResult ReadPdfFullText(DrawingFile file, int maxBytes)
    {
        try
        {
            using var document = PdfDocument.Open(file.FullPath!);
            var sb = new StringBuilder();
            var bytesRead = 0;
            var truncated = false;
            var pageCount = Math.Min(document.NumberOfPages, _defaultMaxPages);
            var nonEmptyPages = 0;

            for (var i = 1; i <= pageCount; i++)
            {
                if (bytesRead >= maxBytes)
                {
                    truncated = true;
                    break;
                }

                var page = document.GetPage(i);
                var text = ExtractPageText(page);
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }
                nonEmptyPages++;

                var byteLen = Encoding.UTF8.GetByteCount(text);
                if (bytesRead + byteLen > maxBytes)
                {
                    truncated = true;
                    break;
                }

                sb.AppendLine($"[p{i}]");
                sb.AppendLine(text.Trim());
                bytesRead += byteLen;
            }

            var content = sb.ToString().TrimEnd();
            if (string.IsNullOrWhiteSpace(content))
            {
                content = "PDFからテキストを抽出できませんでした";
            }

            _logger.LogInformation("PDF全文読取完了: {Path} pages={Pages} bytes={Bytes} truncated={Truncated} nonEmptyPages={NonEmptyPages}", file.FullPath, pageCount, bytesRead, truncated, nonEmptyPages);
            return DrawingContentResult.Success(content, file.FullPath, bytesRead, truncated, totalHits: 0, matches: Array.Empty<DrawingContentMatch>());
        }
        catch (Exception ex)
        {
            var message = $"PDF読取に失敗しました: {ex.Message}";
            return DrawingContentResult.Preview(message, file.FullPath);
        }
    }

    private static string BuildContentSummary(IReadOnlyList<DrawingContentMatch> matches, int totalHits, bool truncated)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"ヒット件数: {totalHits}");
        if (matches.Count == 0)
        {
            sb.AppendLine("ヒットしたページはありませんでした");
        }
        else
        {
            foreach (var match in matches)
            {
                sb.AppendLine($"p{match.PageNumber} (ヒット {match.Score})");
                sb.AppendLine(match.Snippet);
            }
        }

        if (truncated)
        {
            sb.Append("※読み取りを途中で打ち切りました");
        }

        return sb.ToString().TrimEnd();
    }

    private static IReadOnlyList<DrawingContentMatch> BuildSingleSnippet(string content, IReadOnlyList<string> tokens, int hits)
    {
        if (hits == 0 || tokens.Count == 0)
        {
            return Array.Empty<DrawingContentMatch>();
        }

        var normalized = Normalize(content);
        var snippet = BuildSnippet(content, normalized, tokens);
        return new[]
        {
            new DrawingContentMatch
            {
                PageNumber = 1,
                Score = hits,
                Snippet = snippet
            }
        };
    }

    private static string BuildSnippet(string rawText, string normalized, IReadOnlyList<string> tokens)
    {
        if (tokens.Count == 0)
        {
            return TrimSnippet(rawText, 0, _defaultSnippetRadius);
        }

        var positions = tokens
            .Select(t => normalized.IndexOf(t, StringComparison.OrdinalIgnoreCase))
            .Where(pos => pos >= 0)
            .ToList();
        var index = positions.Count == 0 ? 0 : positions.Min();
        return TrimSnippet(rawText, index, _defaultSnippetRadius);
    }

    private static string TrimSnippet(string text, int index, int radius)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var start = Math.Max(0, index - radius);
        var length = Math.Min(text.Length - start, radius * 2);
        var snippet = text.Substring(start, length);

        if (start > 0)
        {
            snippet = "…" + snippet;
        }

        if (start + length < text.Length)
        {
            snippet += "…";
        }

        return snippet.Trim();
    }

    private static int CountHits(string text, IReadOnlyList<string> tokens)
    {
        if (tokens.Count == 0 || string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        var count = 0;
        foreach (var token in tokens)
        {
            var index = 0;
            var normalized = text;
            while ((index = normalized.IndexOf(token, index, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                count++;
                index += token.Length;
            }
        }

        return count;
    }

    private static List<string> NormalizeTokens(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new List<string>();
        }

        var normalized = Normalize(query);
        return normalized
            .Split(new[] { ' ', '\n', '\r', '\t', ',', '、', '。', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 1)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string Normalize(string text)
    {
        return text
            .Normalize(NormalizationForm.FormKC)
            .ToLowerInvariant();
    }

    private static string ExtractPageText(Page page)
    {
        var text = ContentOrderTextExtractor.GetText(page);
        if (!string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        text = page.Text;
        if (!string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        var letters = page.Letters;
        if (letters is null || letters.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var letter in letters)
        {
            builder.Append(letter.Value);
        }

        return builder.ToString();
    }
}
