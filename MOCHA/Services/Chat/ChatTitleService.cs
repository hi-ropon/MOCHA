using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using MOCHA.Models.Chat;

namespace MOCHA.Services.Chat;

/// <summary>
/// タイトル生成と保存を非同期で管理するサービス
/// </summary>
internal sealed class ChatTitleService : IChatTitleService
{
    private readonly IChatTitleGenerator _generator;
    private readonly IChatRepository _repository;
    private readonly ConversationHistoryState _history;
    private readonly ILogger<ChatTitleService> _logger;
    private readonly ConcurrentDictionary<string, Task> _running = new();
    private readonly ConcurrentDictionary<string, bool> _completed = new();

    public ChatTitleService(
        IChatTitleGenerator generator,
        IChatRepository repository,
        ConversationHistoryState history,
        ILogger<ChatTitleService> logger)
    {
        _generator = generator;
        _repository = repository;
        _history = history;
        _logger = logger;
    }

    public Task RequestAsync(UserContext user, string conversationId, string userMessage, string? agentNumber, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(conversationId) || string.IsNullOrWhiteSpace(userMessage))
        {
            return Task.CompletedTask;
        }

        if (_completed.ContainsKey(conversationId))
        {
            return Task.CompletedTask;
        }

        var task = _running.GetOrAdd(conversationId, _ => RunAsync(user, conversationId, userMessage, agentNumber, cancellationToken));

        if (task.IsCompleted)
        {
            _running.TryRemove(conversationId, out _);
        }

        return task;
    }

    private async Task RunAsync(UserContext user, string conversationId, string userMessage, string? agentNumber, CancellationToken cancellationToken)
    {
        try
        {
            var title = await _generator.GenerateAsync(new ChatTitleRequest(conversationId, userMessage), cancellationToken);
            if (string.IsNullOrWhiteSpace(title))
            {
                return;
            }

            await _repository.UpsertConversationAsync(user.UserId, conversationId, title, agentNumber, cancellationToken);
            await _history.UpsertAsync(user.UserId, conversationId, title, agentNumber, cancellationToken);
            _completed[conversationId] = true;
        }
        catch (OperationCanceledException)
        {
            // キャンセルは利用者側の意図とみなしログのみ
            _logger.LogDebug("タイトル生成がキャンセルされました: {ConversationId}", conversationId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "タイトル生成に失敗しました: {ConversationId}", conversationId);
        }
        finally
        {
            _running.TryRemove(conversationId, out _);
        }
    }
}
