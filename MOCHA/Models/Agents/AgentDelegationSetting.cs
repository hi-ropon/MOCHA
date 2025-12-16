using System;
using System.Collections.Generic;
using System.Linq;

namespace MOCHA.Models.Agents;

/// <summary>装置エージェントごとのサブエージェント設定</summary>
public sealed class AgentDelegationSetting
{
    public AgentDelegationSetting(string agentNumber, IReadOnlyCollection<string> allowedSubAgents)
    {
        if (string.IsNullOrWhiteSpace(agentNumber))
        {
            throw new ArgumentException("agentNumber must not be empty", nameof(agentNumber));
        }

        AgentNumber = agentNumber;
        AllowedSubAgents = allowedSubAgents ?? Array.Empty<string>();
    }

    /// <summary>装置エージェント番号</summary>
    public string AgentNumber { get; }

    /// <summary>許可されたサブエージェント</summary>
    public IReadOnlyCollection<string> AllowedSubAgents { get; }
}

/// <summary>サブエージェント設定保存用ドラフト</summary>
public sealed class AgentDelegationSettingDraft
{
    /// <summary>許可するサブエージェント</summary>
    public IReadOnlyCollection<string> AllowedSubAgents { get; set; } = Array.Empty<string>();
}

/// <summary>サブエージェント設定の保存結果</summary>
public sealed class AgentDelegationSettingResult
{
    private AgentDelegationSettingResult(bool succeeded, AgentDelegationSetting? setting, string? error)
    {
        Succeeded = succeeded;
        Setting = setting;
        Error = error;
    }

    /// <summary>成功可否</summary>
    public bool Succeeded { get; }

    /// <summary>保存された設定</summary>
    public AgentDelegationSetting? Setting { get; }

    /// <summary>エラー</summary>
    public string? Error { get; }

    public static AgentDelegationSettingResult Success(AgentDelegationSetting setting) =>
        new(true, setting, null);

    public static AgentDelegationSettingResult Fail(string error) =>
        new(false, null, string.IsNullOrWhiteSpace(error) ? "サブエージェント設定に失敗しました" : error);
}
