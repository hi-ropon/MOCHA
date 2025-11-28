using System.Linq;
using System.Runtime.CompilerServices;
using MOCHA.Agents.Application;
using MOCHA.Agents.Domain;
using MOCHA.Models.Chat;

using DomainChatTurn = MOCHA.Agents.Domain.ChatTurn;
using DomainAuthorRole = MOCHA.Agents.Domain.AuthorRole;
using ChatTurnModel = MOCHA.Models.Chat.ChatTurn;

namespace MOCHA.Services.Copilot;

/// <summary>
/// IAgentOrchestrator を ICopilotChatClient としてラップするアダプタ。
/// </summary>
public sealed class AgentOrchestratorChatClient : ICopilotChatClient
{
    private readonly IAgentOrchestrator orchestrator;

    public AgentOrchestratorChatClient(IAgentOrchestrator orchestrator)
    {
        this.orchestrator = orchestrator;
    }

    public Task<IAsyncEnumerable<ChatStreamEvent>> SendAsync(ChatTurnModel turn, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(SendCoreAsync(turn, cancellationToken));
    }

    public Task SubmitActionResultAsync(CopilotActionResult result, CancellationToken cancellationToken = default)
    {
        // いまのところツール結果の往復は行わないため no-op
        return Task.CompletedTask;
    }

    private async IAsyncEnumerable<ChatStreamEvent> SendCoreAsync(
        ChatTurnModel turn,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var conversationId = string.IsNullOrWhiteSpace(turn.ConversationId)
            ? Guid.NewGuid().ToString("N")
            : turn.ConversationId;

        var history = turn.Messages
            .Select(m => new DomainChatTurn(MapRole(m.Role), m.Content, DateTimeOffset.UtcNow))
            .ToList();

        var userTurn = history.LastOrDefault() ?? DomainChatTurn.User(string.Empty);
        var context = new ChatContext(conversationId, history);

        var reply = await orchestrator.ReplyAsync(userTurn, context, cancellationToken);

        if (!string.IsNullOrWhiteSpace(reply.Text))
        {
            yield return ChatStreamEvent.FromMessage(new ChatMessage(ChatRole.Assistant, reply.Text!));
        }

        yield return ChatStreamEvent.Completed(conversationId);
    }

    private static DomainAuthorRole MapRole(ChatRole role) =>
        role switch
        {
            ChatRole.System => DomainAuthorRole.System,
            ChatRole.Assistant => DomainAuthorRole.Assistant,
            ChatRole.Tool => DomainAuthorRole.Tool,
            _ => DomainAuthorRole.User
        };
}
