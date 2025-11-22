using MOCHA.Models.Chat;

namespace MOCHA.Services.Chat;

public interface IChatOrchestrator
{
    IAsyncEnumerable<ChatStreamEvent> HandleUserMessageAsync(
        UserContext user,
        string? conversationId,
        string text,
        CancellationToken cancellationToken = default);
}
