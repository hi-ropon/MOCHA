using System.Collections.Generic;

namespace MOCHA.Models.Chat;

/// <summary>
/// チャット履歴を UI 表示用に整形するヘルパー
/// </summary>
public static class ChatMessageProjector
{
    /// <summary>
    /// ユーザー発話とそのターンで最後に届いたアシスタント発話のみを残す処理
    /// </summary>
    /// <param name="messages">永続化されたメッセージ列</param>
    /// <returns>UI に表示するメッセージ列</returns>
    public static IReadOnlyList<ChatMessage> KeepUserAndFinalAssistant(IReadOnlyList<ChatMessage> messages)
    {
        var result = new List<ChatMessage>();
        ChatMessage? pendingAssistant = null;

        foreach (var message in messages)
        {
            switch (message.Role)
            {
                case ChatRole.User:
                    if (pendingAssistant is not null)
                    {
                        result.Add(pendingAssistant);
                        pendingAssistant = null;
                    }
                    result.Add(message);
                    break;
                case ChatRole.Assistant:
                    pendingAssistant = message;
                    break;
                default:
                    // ツール/System はチャット画面には出さず、アクティビティで扱う
                    break;
            }
        }

        if (pendingAssistant is not null)
        {
            result.Add(pendingAssistant);
        }

        return result;
    }
}
