using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MOCHA.Agents.Application;

namespace MOCHA.Agents.Infrastructure.Orchestration;

/// <summary>
/// Organizer プロンプトを動的に構成するビルダー
/// </summary>
public sealed class OrganizerInstructionBuilder
{
    private readonly IOrganizerContextProvider _contextProvider;

    /// <summary>
    /// プロバイダー注入による初期化
    /// </summary>
    /// <param name="contextProvider">コンテキストプロバイダー</param>
    public OrganizerInstructionBuilder(IOrganizerContextProvider contextProvider)
    {
        _contextProvider = contextProvider ?? throw new ArgumentNullException(nameof(contextProvider));
    }

    /// <summary>
    /// テンプレートとコンテキストからプロンプトを生成
    /// </summary>
    /// <param name="template">テンプレート</param>
    /// <param name="userId">ユーザーID</param>
    /// <param name="agentNumber">装置エージェント番号</param>
    /// <param name="allowedSubAgents">許可されたサブエージェント</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>生成したプロンプト</returns>
    public async Task<string> BuildAsync(
        string template,
        string? userId,
        string? agentNumber,
        bool plcOnline,
        IReadOnlyCollection<string>? allowedSubAgents = null,
        CancellationToken cancellationToken = default)
    {
        var baseTemplate = string.IsNullOrWhiteSpace(template)
            ? OrganizerInstructions.Template
            : template;

        var context = await _contextProvider.BuildAsync(userId, agentNumber, cancellationToken) ?? OrganizerContext.Empty;
        var architecture = string.IsNullOrWhiteSpace(context.Architecture)
            ? "アーキテクチャ設定: 情報なし"
            : context.Architecture.Trim();
        var drawings = string.IsNullOrWhiteSpace(context.Drawings)
            ? "図面情報: 情報なし"
            : context.Drawings.Trim();
        var plcStatus = plcOnline
            ? "実機読み取りが許可されているため read_plc_values/read_multiple_plc_values/read_plc_gateway を即時実行してよい。ユーザーへの追加確認は不要。"
            : "実機読み取りはユーザー設定で無効。read_plc_values/read_multiple_plc_values/read_plc_gateway は呼び出さず、プログラム解析やマニュアルで回答する。";
        var subAgentPolicy = OrganizerInstructions.BuildSubAgentPolicy(allowedSubAgents);

        return baseTemplate
            .Replace("{{subagent_policy}}", subAgentPolicy, StringComparison.Ordinal)
            .Replace("{{architecture_context}}", architecture, StringComparison.Ordinal)
            .Replace("{{drawing_context}}", drawings, StringComparison.Ordinal)
            .Replace("{{plc_reading_status}}", plcStatus, StringComparison.Ordinal);
    }
}
