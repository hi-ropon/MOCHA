using MOCHA.Models.Chat;

namespace MOCHA.Services.Chat;

public interface IChatRepository
{
    Task<IReadOnlyList<ConversationSummary>> GetSummariesAsync(string userObjectId, string? agentNumber, CancellationToken cancellationToken = default);

    Task UpsertConversationAsync(string userObjectId, string conversationId, string title, string? agentNumber, CancellationToken cancellationToken = default);

    Task AddMessageAsync(string userObjectId, string conversationId, ChatMessage message, string? agentNumber, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(string userObjectId, string conversationId, string? agentNumber = null, CancellationToken cancellationToken = default);

    Task DeleteConversationAsync(string userObjectId, string conversationId, string? agentNumber, CancellationToken cancellationToken = default);
}
