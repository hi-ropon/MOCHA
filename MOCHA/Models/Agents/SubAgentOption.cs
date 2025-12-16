using System;
using System.Collections.Generic;
using System.Linq;

namespace MOCHA.Models.Agents;

/// <summary>サブエージェントの選択肢を表す</summary>
public sealed class SubAgentOption
{
    public SubAgentOption(string id, string name, string description)
    {
        Id = id;
        Name = name;
        Description = description;
    }

    /// <summary>識別子</summary>
    public string Id { get; }

    /// <summary>表示名</summary>
    public string Name { get; }

    /// <summary>説明</summary>
    public string Description { get; }
}

/// <summary>サブエージェント選択肢の一覧</summary>
public static class SubAgentOptions
{
    private static readonly Lazy<IReadOnlyList<SubAgentOption>> _options = new(() => new[]
    {
        new SubAgentOption("iaiAgent", "IAIエージェント", "IAI関連マニュアルを検索します"),
        new SubAgentOption("orientalAgent", "Orientalエージェント", "Oriental Motor関連マニュアルを検索します"),
        new SubAgentOption("drawingAgent", "図面エージェント", "登録済み図面の検索・要約を行います"),
        new SubAgentOption("plcAgent", "PLCエージェント", "ゲートウェイ読み取りとPLC解析を行います")
    });

    /// <summary>全選択肢</summary>
    public static IReadOnlyList<SubAgentOption> All => _options.Value;

    /// <summary>許可されている識別子集合</summary>
    public static HashSet<string> AllowedIds { get; } = new HashSet<string>(
        All.Select(x => x.Id),
        StringComparer.OrdinalIgnoreCase);
}
