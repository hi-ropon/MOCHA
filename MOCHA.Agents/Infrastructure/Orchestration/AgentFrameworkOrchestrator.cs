using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MOCHA.Agents.Application;
using MOCHA.Agents.Domain;
using MOCHA.Agents.Infrastructure.Clients;
using MOCHA.Agents.Infrastructure.Tools;
using MOCHA.Agents.Infrastructure.Options;
using System.Threading.Channels;

namespace MOCHA.Agents.Infrastructure.Orchestration;

/// <summary>
/// Microsoft Agent Framework の ChatClientAgent を使ったオーケストレーター。
/// </summary>
public sealed class AgentFrameworkOrchestrator : IAgentOrchestrator
{
    private readonly ChatClientAgent _agent;
    private readonly OrganizerToolset _tools;
    private readonly LlmOptions _options;
    private readonly ILogger<AgentFrameworkOrchestrator> _logger;
    private readonly ConcurrentDictionary<string, AgentThread> _threads = new();

    public AgentFrameworkOrchestrator(
        ILlmChatClientFactory chatClientFactory,
        OrganizerToolset tools,
        IOptions<LlmOptions> optionsAccessor,
        ILogger<AgentFrameworkOrchestrator> logger)
    {
        _options = optionsAccessor.Value ?? throw new ArgumentNullException(nameof(optionsAccessor));
        this._logger = logger;
        _tools = tools;

        var chatClient = chatClientFactory.Create();
        _agent = new ChatClientAgent(
            chatClient,
            name: _options.AgentName ?? "mocha-agent",
            description: _options.AgentDescription ?? "MOCHA agent powered by Microsoft Agent Framework",
            instructions: _options.Instructions ?? OrganizerInstructions.Default,
            tools: tools.All.ToList());
    }

    public Task<IAsyncEnumerable<AgentEvent>> ReplyAsync(ChatTurn userTurn, ChatContext context, CancellationToken cancellationToken = default)
    {
        var conversationId = string.IsNullOrWhiteSpace(context.ConversationId)
            ? Guid.NewGuid().ToString("N")
            : context.ConversationId;

        return Task.FromResult<IAsyncEnumerable<AgentEvent>>(ReplyStreamAsync(conversationId, userTurn, context, cancellationToken));
    }

    private async IAsyncEnumerable<AgentEvent> ReplyStreamAsync(
        string conversationId,
        ChatTurn userTurn,
        ChatContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var thread = _threads.GetOrAdd(conversationId, _ => _agent.GetNewThread());
        var messages = new List<ChatMessage>(context.History.Select(MapMessage))
        {
            new ChatMessage(MapRole(userTurn.Role), userTurn.Content)
        };

        var channel = Channel.CreateUnbounded<AgentEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        void Emit(AgentEvent ev)
        {
            channel.Writer.TryWrite(ev);
        }

        using var _ = _tools.UseContext(conversationId, Emit);

        var runTask = Task.Run(async () =>
        {
            try
            {
                var updates = _agent.RunStreamingAsync(
                    messages,
                    thread,
                    new AgentRunOptions(),
                    cancellationToken);

                await foreach (var update in updates.WithCancellation(cancellationToken))
                {
                    var replyText = ExtractPlainText(update.Text ?? string.Empty);
                    if (!string.IsNullOrWhiteSpace(replyText))
                    {
                        Emit(AgentEventFactory.Message(conversationId, replyText));
                    }
                }

                Emit(AgentEventFactory.Completed(conversationId));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "エージェント応答の取得に失敗しました。");
                Emit(AgentEventFactory.Error(conversationId, ex.Message));
                Emit(AgentEventFactory.Completed(conversationId));
            }
            finally
            {
                channel.Writer.TryComplete();
            }
        }, cancellationToken);

        await foreach (var ev in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return ev;
        }

        await runTask;
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
