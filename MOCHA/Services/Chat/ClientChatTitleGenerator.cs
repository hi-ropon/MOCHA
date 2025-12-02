using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using MOCHA.Agents.Infrastructure.Clients;

namespace MOCHA.Services.Chat;

/// <summary>
/// ClientChat を用いてユーザー発話からタイトルを生成する実装
/// </summary>
internal sealed class ClientChatTitleGenerator : IChatTitleGenerator
{
    private const int _maxTitleLength = 20;

    private readonly ILlmChatClientFactory _chatClientFactory;
    private readonly ILogger<ClientChatTitleGenerator> _logger;

    /// <summary>
    /// チャットクライアントファクトリーとロガー注入による初期化
    /// </summary>
    /// <param name="chatClientFactory">チャットクライアントファクトリー</param>
    /// <param name="logger">ロガー</param>
    public ClientChatTitleGenerator(ILlmChatClientFactory chatClientFactory, ILogger<ClientChatTitleGenerator> logger)
    {
        _chatClientFactory = chatClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// タイトル生成
    /// </summary>
    /// <param name="request">タイトル生成リクエスト</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>生成タイトル</returns>
    public async Task<string> GenerateAsync(ChatTitleRequest request, CancellationToken cancellationToken = default)
    {
        var chatClient = _chatClientFactory.Create();
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, BuildSystemPrompt()),
            new(ChatRole.User, request.UserMessage)
        };

        var response = await chatClient.GetResponseAsync(messages, cancellationToken: cancellationToken);
        var raw = ExtractText(response);
        var normalized = Normalize(raw);

        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("タイトル生成に失敗しました");
        }

        return normalized;
    }

    /// <summary>
    /// 応答からテキスト抽出
    /// </summary>
    /// <param name="response">チャット応答</param>
    /// <returns>抽出テキスト</returns>
    private static string ExtractText(ChatResponse response)
    {
        if (response is null)
        {
            return string.Empty;
        }

        var type = response.GetType();

        if (type.GetProperty("Message")?.GetValue(response) is ChatMessage message && !string.IsNullOrWhiteSpace(message.Text))
        {
            return message.Text!;
        }

        if (type.GetProperty("OutputText")?.GetValue(response) is string output && !string.IsNullOrWhiteSpace(output))
        {
            return output;
        }

        if (type.GetProperty("Messages")?.GetValue(response) is IEnumerable<ChatMessage> messages)
        {
            var last = messages.LastOrDefault()?.Text;
            if (!string.IsNullOrWhiteSpace(last))
            {
                return last!;
            }
        }

        return response.ToString() ?? string.Empty;
    }

    /// <summary>
    /// システムプロンプト生成
    /// </summary>
    /// <returns>プロンプト文字列</returns>
    private static string BuildSystemPrompt()
    {
        var sb = new StringBuilder();
        sb.AppendLine("以下のユーザー入力からチャット履歴のタイトルを一言で生成してください。");
        sb.AppendLine("・体言止めの名詞句にすること。");
        sb.AppendLine("・句読点や記号は末尾に付けないこと。");
        sb.AppendLine("・20文字以内で要点だけ残すこと。");
        sb.AppendLine("返答はタイトルのみ。解説や補足は不要。");
        return sb.ToString();
    }

    /// <summary>
    /// タイトル文字列の正規化
    /// </summary>
    /// <param name="raw">生成結果の生テキスト</param>
    /// <returns>整形タイトル</returns>
    private static string Normalize(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var firstLine = raw.Replace("\r", string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()
            ?.Trim() ?? string.Empty;

        var sentence = firstLine
            .Split(new[] { '。', '．', '.', '！', '!', '？', '?', ';', '；', ':', '：' }, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()
            ?.Trim() ?? string.Empty;

        var trimmed = sentence.Trim('\"', '\'', '「', '」', '『', '』', '【', '】', '《', '》');
        trimmed = trimmed.TrimEnd('。', '．', '.', '、', ',', '，', '！', '!', '？', '?', ';', '；', ':', '：');

        if (trimmed.Length > _maxTitleLength)
        {
            trimmed = trimmed[.._maxTitleLength];
        }

        return trimmed;
    }
}
