using MOCHA.Agents.Application;
using MOCHA.Agents.Domain;

namespace MOCHA.Agents.Infrastructure.Agents;

/// <summary>
/// 簡易なテンプレート応答を返すベースエージェント。
/// </summary>
public abstract class BaseTaskAgent : ITaskAgent
{
    public string Name { get; }
    public string Description { get; }
    private readonly string _prefix;
    private readonly Func<string, string> _bodyFactory;

    protected BaseTaskAgent(string name, string description, string prefix, Func<string, string>? bodyFactory = null)
    {
        Name = name;
        Description = description;
        _prefix = prefix;
        _bodyFactory = bodyFactory ?? (q => q);
    }

    public Task<AgentResult> ExecuteAsync(string question, CancellationToken cancellationToken = default)
    {
        var body = _bodyFactory(question);
        var content = $"{_prefix} {body}".Trim();
        return Task.FromResult(new AgentResult(Name, content));
    }
}
