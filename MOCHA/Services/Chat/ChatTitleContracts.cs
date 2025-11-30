using MOCHA.Models.Chat;

namespace MOCHA.Services.Chat;

/// <summary>
/// タイトル生成に必要な情報
/// </summary>
public sealed record ChatTitleRequest(string ConversationId, string UserMessage);

/// <summary>
/// タイトルを生成するドメインサービス
/// </summary>
public interface IChatTitleGenerator
{
    /// <summary>
    /// ユーザー発話を元にタイトルを生成する
    /// </summary>
    /// <param name="request">生成リクエスト</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>生成したタイトル</returns>
    Task<string> GenerateAsync(ChatTitleRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// タイトル生成の実行と永続化を調停するサービス
/// </summary>
public interface IChatTitleService
{
    /// <summary>
    /// 指定会話のタイトル生成を非同期で開始する
    /// </summary>
    /// <param name="user">ユーザー情報</param>
    /// <param name="conversationId">会話ID</param>
    /// <param name="userMessage">最新のユーザー発話</param>
    /// <param name="agentNumber">エージェント番号</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    Task RequestAsync(UserContext user, string conversationId, string userMessage, string? agentNumber, CancellationToken cancellationToken = default);
}
