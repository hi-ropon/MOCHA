using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MOCHA.Agents.Application;
using MOCHA.Agents.Domain;
using MOCHA.Agents.Infrastructure.Clients;
using MOCHA.Agents.Infrastructure.Options;

namespace MOCHA.Agents.Infrastructure.Orchestration;

/// <summary>
/// Microsoft Agent Framework の ChatClientAgent を使ったオーケストレーター。
/// </summary>
public sealed class AgentFrameworkOrchestrator : IAgentOrchestrator
{
    private readonly ChatClientAgent _agent;
    private readonly LlmOptions _options;
    private readonly ILogger<AgentFrameworkOrchestrator> _logger;
    private readonly ConcurrentDictionary<string, AgentThread> _threads = new();
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web)
    {
        // Agent SDK が options を読み取り専用にする前に型情報リゾルバーを設定しておく
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };

    public AgentFrameworkOrchestrator(
        ILlmChatClientFactory chatClientFactory,
        IOptions<LlmOptions> optionsAccessor,
        ILogger<AgentFrameworkOrchestrator> logger)
    {
        _options = optionsAccessor.Value ?? throw new ArgumentNullException(nameof(optionsAccessor));
        this._logger = logger;

        var chatClient = chatClientFactory.Create();
        _agent = new ChatClientAgent(chatClient, new ChatClientAgentOptions
        {
            Name = _options.AgentName ?? "mocha-agent",
            Description = _options.AgentDescription ?? "MOCHA agent powered by Microsoft Agent Framework",
            Instructions = _options.Instructions ?? "You are a helpful assistant for MOCHA.",
        });
    }

    public async Task<AgentReply> ReplyAsync(ChatTurn userTurn, ChatContext context, CancellationToken cancellationToken = default)
    {
        var conversationId = string.IsNullOrWhiteSpace(context.ConversationId)
            ? Guid.NewGuid().ToString("N")
            : context.ConversationId;

        var thread = _threads.GetOrAdd(conversationId, _ => _agent.GetNewThread());

        var messages = new List<ChatMessage>(context.History.Select(MapMessage))
        {
            new ChatMessage(MapRole(userTurn.Role), userTurn.Content)
        };

        var response = await _agent.RunAsync<string>(
            messages,
            thread,
            _serializerOptions,
            options: new AgentRunOptions(),
            useJsonSchemaResponseFormat: false,
            cancellationToken);

        var rawText = response.Result ?? response.Text ?? string.Empty;
        var replyText = ExtractPlainText(rawText);

        return new AgentReply(conversationId, replyText, Array.Empty<ToolCall>(), Array.Empty<string>());
    }

    private static ChatMessage MapMessage(ChatTurn turn) =>
        new(MapRole(turn.Role), turn.Content);

    private static ChatRole MapRole(AuthorRole role) =>
        role switch
        {
            AuthorRole.System => ChatRole.System,
            AuthorRole.Assistant => ChatRole.Assistant,
            AuthorRole.Tool => ChatRole.Tool,
            _ => ChatRole.User
        };

    /// <summary>
    /// エージェント応答からプレーンテキストを抽出する（JSON オブジェクトなら data/text プロパティを優先）。
    /// </summary>
    private static string ExtractPlainText(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        if (raw.Length > 1 && (raw.TrimStart().StartsWith("{") || raw.TrimStart().StartsWith("[")))
        {
            try
            {
                using var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;

                if (root.ValueKind == JsonValueKind.String)
                {
                    return root.GetString() ?? raw;
                }

                if (root.ValueKind == JsonValueKind.Object)
                {
                    if (root.TryGetProperty("data", out var dataProp) && dataProp.ValueKind == JsonValueKind.String)
                    {
                        return dataProp.GetString() ?? raw;
                    }

                    if (root.TryGetProperty("text", out var textProp) && textProp.ValueKind == JsonValueKind.String)
                    {
                        return textProp.GetString() ?? raw;
                    }
                }
            }
            catch (JsonException)
            {
                // パース失敗時はそのまま返す
            }
        }

        return raw;
    }

}
