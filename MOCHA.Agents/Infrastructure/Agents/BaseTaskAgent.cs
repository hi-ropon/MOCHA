using MOCHA.Agents.Application;
using MOCHA.Agents.Domain;

namespace MOCHA.Agents.Infrastructure.Agents;

/// <summary>
/// 簡易なテンプレート応答を返すベースエージェント
/// </summary>
public abstract class BaseTaskAgent : ITaskAgent
{
    /// <summary>エージェント名</summary>
    public string Name { get; }
    /// <summary>エージェント説明</summary>
    public string Description { get; }
    private readonly string _prefix;
    private readonly Func<string, string> _bodyFactory;

    /// <summary>
    /// テンプレートエージェント初期化
    /// </summary>
    /// <param name="name">エージェント名</param>
    /// <param name="description">エージェント説明</param>
    /// <param name="prefix">レスポンス接頭辞</param>
    /// <param name="bodyFactory">本文生成関数</param>
    protected BaseTaskAgent(string name, string description, string prefix, Func<string, string>? bodyFactory = null)
    {
        Name = name;
        Description = description;
        _prefix = prefix;
        _bodyFactory = bodyFactory ?? (q => q);
    }

    /// <summary>
    /// テンプレートレスポンス生成
    /// </summary>
    /// <param name="question">問い合わせ内容</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>エージェント結果</returns>
    public Task<AgentResult> ExecuteAsync(string question, CancellationToken cancellationToken = default)
    {
        var body = _bodyFactory(question);
        var content = $"{_prefix} {body}".Trim();
        return Task.FromResult(new AgentResult(Name, content));
    }
}
