using System;
using System.Collections.Generic;
using System.Linq;

namespace MOCHA.Agents.Domain;

/// <summary>
/// エージェント委譲ポリシー
/// </summary>
public sealed class AgentDelegationPolicy
{
    private readonly Dictionary<string, HashSet<string>> _edges;

    /// <summary>許可する呼び出し深さ</summary>
    public int MaxDepth { get; }

    /// <summary>
    /// ポリシー生成
    /// </summary>
    /// <param name="options">委譲設定</param>
    public AgentDelegationPolicy(AgentDelegationOptions options)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        MaxDepth = Math.Max(1, options.MaxDepth);
        _edges = Normalize(options.AllowedEdges ?? AgentDelegationOptions.CreateDefaultEdges());
    }

    /// <summary>
    /// 呼び出し可否の評価
    /// </summary>
    /// <param name="caller">呼び出し元</param>
    /// <param name="callee">呼び出し先</param>
    /// <param name="currentDepth">現在の深さ</param>
    /// <param name="reason">拒否理由</param>
    /// <returns>許可可否</returns>
    public bool CanInvoke(string caller, string callee, int currentDepth, out string? reason)
    {
        var normalizedCaller = NormalizeName(caller);
        var normalizedCallee = NormalizeName(callee);
        var nextDepth = currentDepth + 1;

        if (nextDepth > MaxDepth)
        {
            reason = $"呼び出し深さ上限({MaxDepth})を超えています";
            return false;
        }

        if (string.Equals(normalizedCaller, normalizedCallee, StringComparison.OrdinalIgnoreCase))
        {
            reason = "同一エージェントの再呼び出しは許可されていません";
            return false;
        }

        if (_edges.TryGetValue(normalizedCaller, out var allowed) && allowed.Contains(normalizedCallee))
        {
            reason = null;
            return true;
        }

        reason = $"{normalizedCaller} から {normalizedCallee} への委譲は許可されていません";
        return false;
    }

    /// <summary>
    /// 呼び出し可能な先を取得
    /// </summary>
    /// <param name="caller">呼び出し元</param>
    /// <returns>許可済み呼び出し先</returns>
    public IReadOnlyCollection<string> GetAllowedCallees(string caller)
    {
        var normalized = NormalizeName(caller);
        return _edges.TryGetValue(normalized, out var allowed)
            ? allowed.ToArray()
            : Array.Empty<string>();
    }

    private static Dictionary<string, HashSet<string>> Normalize(Dictionary<string, string[]> edges)
    {
        var normalized = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var kvp in edges)
        {
            var caller = NormalizeName(kvp.Key);
            if (string.IsNullOrWhiteSpace(caller))
            {
                continue;
            }

            if (!normalized.TryGetValue(caller, out var set))
            {
                set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                normalized[caller] = set;
            }

            foreach (var callee in kvp.Value ?? Array.Empty<string>())
            {
                var target = NormalizeName(callee);
                if (!string.IsNullOrWhiteSpace(target) && !string.Equals(target, caller, StringComparison.OrdinalIgnoreCase))
                {
                    set.Add(target);
                }
            }
        }

        return normalized;
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "organizer";
        }

        return name.Trim();
    }
}
