using MOCHA.Models.Chat;

namespace MOCHA.Services.Chat;

public interface IChatRepository
{
    Task<IReadOnlyList<ConversationSummary>> GetSummariesAsync(string userObjectId, CancellationToken cancellationToken = default);

    Task UpsertConversationAsync(string userObjectId, string conversationId, string title, CancellationToken cancellationToken = default);

    Task AddMessageAsync(string userObjectId, string conversationId, ChatMessage message, CancellationToken cancellationToken = default);
}
