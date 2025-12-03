using System;
using System.Collections.Generic;
using System.Linq;
using MOCHA.Models.Agents;

namespace MOCHA.Services.Agents;

/// <summary>装置エージェント設定モーダルの選択状態を管理する</summary>
internal sealed class AgentSettingsSelection
{
    /// <summary>モーダル内で選択されたエージェント番号</summary>
    public string? SelectedAgentNumber { get; private set; }

    /// <summary>現在の一覧を元に選択状態を初期化する</summary>
    /// <param name="agents">選択可能なエージェント一覧</param>
    /// <param name="preferredNumber">優先したいエージェント番号</param>
    /// <returns>選択状態が変わったか</returns>
    public bool Reset(IEnumerable<DeviceAgentProfile> agents, string? preferredNumber)
    {
        return ApplySelection(agents, preferredNumber, allowFallback: true);
    }

    /// <summary>指定されたエージェント番号を選択する</summary>
    /// <param name="agents">選択可能なエージェント一覧</param>
    /// <param name="number">選択したいエージェント番号</param>
    /// <returns>選択状態が変わったか</returns>
    public bool Select(IEnumerable<DeviceAgentProfile> agents, string? number)
    {
        return ApplySelection(agents, number, allowFallback: false);
    }

    private bool ApplySelection(IEnumerable<DeviceAgentProfile> agents, string? number, bool allowFallback)
    {
        var list = (agents ?? Enumerable.Empty<DeviceAgentProfile>()).ToList();
        var hasCandidate = !string.IsNullOrWhiteSpace(number)
                           && list.Any(a => string.Equals(a.Number, number, StringComparison.OrdinalIgnoreCase));

        var resolved = hasCandidate
            ? number
            : allowFallback
                ? list.FirstOrDefault()?.Number
                : SelectedAgentNumber;

        if (string.Equals(SelectedAgentNumber, resolved, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        SelectedAgentNumber = resolved;
        return true;
    }
}
