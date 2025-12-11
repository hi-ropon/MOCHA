using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using MOCHA.Agents.Domain.Plc;
using SimMetrics.Net.Metric;

namespace MOCHA.Agents.Infrastructure.Plc;

/// <summary>
/// コメント全文検索サービス
/// </summary>
public sealed class PlcCommentSearchService
{
    private static readonly Regex _deviceRegex = new(@"(?i)\b([dwmxyct]\d+)\b", RegexOptions.Compiled);
    private static readonly Regex _splitRegex = new(@"[ \t\r\n,、，。．\.\-_=+!?！？:：;；()（）""'「」『』［］\\/\[\]{}<>]+", RegexOptions.Compiled);
    private readonly IPlcDataStore _store;
    private readonly JaroWinkler _fuzzyMetric = new();

    public PlcCommentSearchService(IPlcDataStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <summary>
    /// 質問文からコメントファイルを横断検索
    /// </summary>
    public IReadOnlyList<CommentSearchResult> Search(string question, int maxResults = 5)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return Array.Empty<CommentSearchResult>();
        }

        maxResults = Math.Max(1, Math.Min(maxResults, 20));
        var normalizedQuestion = Normalize(question);
        var tokens = ExtractTokens(question);
        if (tokens.Count == 0 && normalizedQuestion.Length > 0)
        {
            tokens.Add(new Token(question.Trim(), normalizedQuestion, IsAscii(question)));
        }

        var deviceTokens = ExtractDeviceTokens(question);
        var results = new List<CommentSearchResult>();

        foreach (var kv in _store.Comments)
        {
            var device = kv.Key?.Trim() ?? string.Empty;
            var comment = kv.Value ?? string.Empty;
            if (string.IsNullOrWhiteSpace(device) && string.IsNullOrWhiteSpace(comment))
            {
                continue;
            }

            var normalizedComment = Normalize(comment);
            if (normalizedComment.Length == 0)
            {
                continue;
            }

            var normalizedDevice = Normalize(device);
            var score = 0.0;
            var matched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (normalizedQuestion.Length > 0 && normalizedComment.Contains(normalizedQuestion, StringComparison.Ordinal))
            {
                score += Math.Max(2, tokens.Count);
                matched.Add(question.Trim());
            }

            for (var i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];
                if (token.Normalized.Length == 0)
                {
                    continue;
                }

                if (normalizedComment.Contains(token.Normalized, StringComparison.Ordinal))
                {
                    var priority = Math.Max(1, tokens.Count - i);
                    var baseScore = token.IsAscii ? 1.0 : 1.5;
                    score += baseScore + priority * 0.5;
                    matched.Add(token.Original);
                }
            }

            var fuzzy = ComputeFuzzyScore(normalizedComment, normalizedQuestion, question, tokens);
            if (fuzzy.Score > 0)
            {
                score += 2.5 * fuzzy.Score;
                if (!string.IsNullOrWhiteSpace(fuzzy.Term))
                {
                    matched.Add(fuzzy.Term);
                }
            }

            foreach (var deviceToken in deviceTokens)
            {
                if (normalizedDevice.Length > 0 && normalizedDevice.Equals(deviceToken.Normalized, StringComparison.Ordinal))
                {
                    score += 5.0;
                    matched.Add(deviceToken.Original);
                }
            }

            if (score <= 0)
            {
                continue;
            }

            results.Add(new CommentSearchResult(device, comment, score, matched.ToList()));
        }

        return results
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.Device, StringComparer.OrdinalIgnoreCase)
            .Take(maxResults)
            .ToList();
    }

    private static List<Token> ExtractTokens(string text)
    {
        var tokens = new List<Token>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return tokens;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var part in _splitRegex.Split(text))
        {
            var trimmed = part?.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                continue;
            }

            var normalized = Normalize(trimmed);
            if (normalized.Length == 0)
            {
                continue;
            }

            var isAscii = IsAscii(trimmed);
            if (isAscii && normalized.Length < 2)
            {
                continue;
            }

            if (seen.Add(normalized))
            {
                tokens.Add(new Token(trimmed, normalized, isAscii));
            }

            if (!isAscii)
            {
                foreach (var segment in SplitJapaneseSegments(trimmed))
                {
                    var segmentNormalized = Normalize(segment);
                    if (segmentNormalized.Length < 2 || !seen.Add(segmentNormalized))
                    {
                        continue;
                    }

                    tokens.Add(new Token(segment, segmentNormalized, false));
                }
            }
        }

        return tokens;
    }

    private static List<Token> ExtractDeviceTokens(string question)
    {
        var tokens = new List<Token>();
        if (string.IsNullOrWhiteSpace(question))
        {
            return tokens;
        }

        foreach (Match match in _deviceRegex.Matches(question))
        {
            var value = match.Groups[1].Value;
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            tokens.Add(new Token(value, Normalize(value), true));
        }

        return tokens;
    }

    private static string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalized = text.Normalize(NormalizationForm.FormKC);
        return normalized.ToUpperInvariant();
    }

    private static bool IsAscii(string text)
    {
        foreach (var ch in text)
        {
            if (ch > sbyte.MaxValue)
            {
                return false;
            }
        }

        return true;
    }

    private static IEnumerable<string> SplitJapaneseSegments(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        var sb = new StringBuilder();
        foreach (var ch in text)
        {
            if (IsHiragana(ch))
            {
                if (sb.Length > 0)
                {
                    yield return sb.ToString();
                    sb.Clear();
                }

                continue;
            }

            sb.Append(ch);
        }

        if (sb.Length > 0)
        {
            yield return sb.ToString();
        }
    }

    private FuzzyMatch ComputeFuzzyScore(string normalizedComment, string normalizedQuestion, string question, IReadOnlyList<Token> tokens)
    {
        if (string.IsNullOrWhiteSpace(normalizedComment))
        {
            return default;
        }

        var bestScore = 0.0;
        string? bestTerm = null;

        void Consider(string candidate, string? term)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return;
            }

            var score = _fuzzyMetric.GetSimilarity(normalizedComment, candidate);
            if (score > bestScore)
            {
                bestScore = score;
                bestTerm = term;
            }
        }

        if (!string.IsNullOrWhiteSpace(normalizedQuestion))
        {
            Consider(normalizedQuestion, question.Trim());
        }

        foreach (var token in tokens)
        {
            if (token.Normalized.Length == 0)
            {
                continue;
            }

            Consider(token.Normalized, token.Original);
        }

        return bestScore >= 0.75 ? new FuzzyMatch(bestScore, bestTerm) : default;
    }

    private static bool IsHiragana(char ch) => ch >= 0x3040 && ch <= 0x309F;

    private readonly record struct Token(string Original, string Normalized, bool IsAscii);
    private readonly record struct FuzzyMatch(double Score, string? Term);
}
