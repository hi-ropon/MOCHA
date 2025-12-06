using System;
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
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>生成したプロンプト</returns>
    public async Task<string> BuildAsync(
        string template,
        string? userId,
        string? agentNumber,
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

        return baseTemplate
            .Replace("{{architecture_context}}", architecture, StringComparison.Ordinal)
            .Replace("{{drawing_context}}", drawings, StringComparison.Ordinal);
    }
}
