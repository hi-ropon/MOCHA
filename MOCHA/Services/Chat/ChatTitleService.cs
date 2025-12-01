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

    /// <summary>
    /// 依存サービス注入による初期化
    /// </summary>
    /// <param name="generator">タイトル生成器</param>
    /// <param name="repository">チャットリポジトリ</param>
    /// <param name="history">会話履歴状態</param>
    /// <param name="logger">ロガー</param>
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

    /// <summary>
    /// タイトル生成要求を非同期で受け付ける
    /// </summary>
    /// <param name="user">ユーザー情報</param>
    /// <param name="conversationId">会話ID</param>
    /// <param name="userMessage">ユーザー発話</param>
    /// <param name="agentNumber">エージェント番号</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>生成タスク</returns>
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

    /// <summary>
    /// タイトル生成と保存処理
    /// </summary>
    /// <param name="user">ユーザー情報</param>
    /// <param name="conversationId">会話ID</param>
    /// <param name="userMessage">ユーザー発話</param>
    /// <param name="agentNumber">エージェント番号</param>
    /// <param name="cancellationToken">キャンセル通知</param>
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
