using System;
using System.Collections.Generic;

namespace MOCHA.Agents.Domain;

/// <summary>
/// エージェント委譲ポリシー設定
/// </summary>
public sealed class AgentDelegationOptions
{
    /// <summary>許可する呼び出し深さ</summary>
    public int MaxDepth { get; set; } = 3;

    /// <summary>呼び出し許可エッジ</summary>
    public Dictionary<string, string[]> AllowedEdges { get; set; } = CreateDefaultEdges();

    /// <summary>
    /// 既定の許可エッジを生成
    /// </summary>
    /// <returns>既定エッジ</returns>
    public static Dictionary<string, string[]> CreateDefaultEdges()
    {
        return new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["organizer"] = new[] { "iaiAgent", "orientalAgent", "drawingAgent", "plcAgent" },
            ["plcAgent"] = new[] { "iaiAgent", "orientalAgent", "drawingAgent" }
        };
    }
}
