using System;
using System.Collections.Generic;

namespace MOCHA.Services.Agents;

/// <summary>
/// PLC接続オン/オフ状態を保持する
/// </summary>
internal sealed class PlcConnectionState
{
    private readonly Dictionary<string, bool> _states = new(StringComparer.OrdinalIgnoreCase);
    private string? _currentAgent;

    /// <summary>状態変更通知</summary>
    public event Action? Changed;

    /// <summary>
    /// アクティブな装置エージェントを設定
    /// </summary>
    /// <param name="agentNumber">エージェント番号</param>
    public void SetAgent(string? agentNumber)
    {
        var normalized = Normalize(agentNumber);
        var changed = !string.Equals(_currentAgent, normalized, StringComparison.OrdinalIgnoreCase);
        _currentAgent = normalized;
        if (normalized is not null && !_states.ContainsKey(normalized))
        {
            _states[normalized] = true;
            changed = true;
        }

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// 指定エージェントの接続状態を取得
    /// </summary>
    /// <param name="agentNumber">エージェント番号（未指定時は現在の選択）</param>
    /// <returns>オンラインなら true</returns>
    public bool GetOnline(string? agentNumber = null)
    {
        var key = Normalize(agentNumber ?? _currentAgent);
        if (key is null)
        {
            return true;
        }

        return _states.TryGetValue(key, out var value) ? value : true;
    }

    /// <summary>
    /// 接続状態の更新
    /// </summary>
    /// <param name="agentNumber">エージェント番号（未指定時は現在の選択）</param>
    /// <param name="isOnline">オンライン可否</param>
    public void SetOnline(string? agentNumber, bool isOnline)
    {
        var key = Normalize(agentNumber ?? _currentAgent);
        if (key is null)
        {
            return;
        }

        var current = _states.TryGetValue(key, out var existing) ? existing : true;
        if (current == isOnline)
        {
            _currentAgent = key;
            return;
        }

        _states[key] = isOnline;
        _currentAgent = key;
        Changed?.Invoke();
    }

    private static string? Normalize(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
