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
/// Microsoft Agent Framework の ChatClientAgent を使ったオーケストレーター
/// </summary>
public sealed class AgentFrameworkOrchestrator : IAgentOrchestrator
{
    private readonly ILlmChatClientFactory _chatClientFactory;
    private readonly OrganizerToolset _tools;
    private readonly OrganizerInstructionBuilder _instructionBuilder;
    private readonly LlmOptions _options;
    private readonly ILogger<AgentFrameworkOrchestrator> _logger;
    private readonly ConcurrentDictionary<string, ConversationState> _threads = new();

    /// <summary>
    /// 必要なサービス注入による初期化
    /// </summary>
    /// <param name="chatClientFactory">チャットクライアントファクトリー</param>
    /// <param name="instructionBuilder">Organizer プロンプトビルダー</param>
    /// <param name="tools">ツールセット</param>
    /// <param name="optionsAccessor">LLM オプション</param>
    /// <param name="logger">ロガー</param>
    public AgentFrameworkOrchestrator(
        ILlmChatClientFactory chatClientFactory,
        OrganizerInstructionBuilder instructionBuilder,
        OrganizerToolset tools,
        IOptions<LlmOptions> optionsAccessor,
        ILogger<AgentFrameworkOrchestrator> logger)
    {
        _options = optionsAccessor.Value ?? throw new ArgumentNullException(nameof(optionsAccessor));
        _logger = logger;
        _tools = tools;
        _instructionBuilder = instructionBuilder ?? throw new ArgumentNullException(nameof(instructionBuilder));
        _chatClientFactory = chatClientFactory ?? throw new ArgumentNullException(nameof(chatClientFactory));
    }

    /// <summary>
    /// エージェント応答ストリーム生成
    /// </summary>
    /// <param name="userTurn">ユーザーターン</param>
    /// <param name="context">チャットコンテキスト</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>ストリーミングイベント列</returns>
    public Task<IAsyncEnumerable<AgentEvent>> ReplyAsync(ChatTurn userTurn, ChatContext context, CancellationToken cancellationToken = default)
    {
        var conversationId = string.IsNullOrWhiteSpace(context.ConversationId)
            ? Guid.NewGuid().ToString("N")
            : context.ConversationId;

        var scopedContext = context with { ConversationId = conversationId };

        return Task.FromResult<IAsyncEnumerable<AgentEvent>>(ReplyStreamAsync(conversationId, userTurn, scopedContext, cancellationToken));
    }

    /// <summary>
    /// 応答ストリーム処理本体
    /// </summary>
    /// <param name="conversationId">会話ID</param>
    /// <param name="userTurn">ユーザーターン</param>
    /// <param name="context">チャットコンテキスト</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>イベント列</returns>
    private async IAsyncEnumerable<AgentEvent> ReplyStreamAsync(
        string conversationId,
        ChatTurn userTurn,
        ChatContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var agent = await CreateAgentAsync(context, cancellationToken);
        var thread = _threads.GetOrAdd(conversationId, _ => new ConversationState(agent.Client, agent.Agent.GetNewThread()));
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

        using var _ = _tools.UseContext(context, Emit);

        var runTask = Task.Run(async () =>
        {
            try
            {
                var updates = agent.Agent.RunStreamingAsync(
                    messages,
                    thread.Thread,
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

    /// <summary>
    /// チャットターンのメッセージ変換
    /// </summary>
    /// <param name="turn">変換元ターン</param>
    /// <returns>チャットメッセージ</returns>
    private static ChatMessage MapMessage(ChatTurn turn) =>
        new(MapRole(turn.Role), turn.Content);

    /// <summary>
    /// ドメインロールからチャットロールへの変換
    /// </summary>
    /// <param name="role">ドメインロール</param>
    /// <returns>チャットロール</returns>
    private static ChatRole MapRole(AuthorRole role) =>
        role switch
        {
            AuthorRole.System => ChatRole.System,
            AuthorRole.Assistant => ChatRole.Assistant,
            AuthorRole.Tool => ChatRole.Tool,
            _ => ChatRole.User
        };

    /// <summary>
    /// エージェント応答からプレーンテキスト抽出（JSON オブジェクトなら data/text プロパティを優先）
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

    private async Task<AgentHandle> CreateAgentAsync(ChatContext context, CancellationToken cancellationToken)
    {
        var baseTemplate = _options.Instructions ?? OrganizerInstructions.Template;
        var instructions = await _instructionBuilder.BuildAsync(baseTemplate, context.UserId, context.AgentNumber, cancellationToken);

        if (_threads.TryGetValue(context.ConversationId, out var existing))
        {
            var agentWithContext = new ChatClientAgent(
                existing.Client,
                name: _options.AgentName ?? "mocha-agent",
                description: _options.AgentDescription ?? "MOCHA agent powered by Microsoft Agent Framework",
                instructions: instructions,
                tools: _tools.All.ToList());

            return new AgentHandle(existing.Client, agentWithContext);
        }

        var chatClient = _chatClientFactory.Create();
        var agent = new ChatClientAgent(
            chatClient,
            name: _options.AgentName ?? "mocha-agent",
            description: _options.AgentDescription ?? "MOCHA agent powered by Microsoft Agent Framework",
            instructions: instructions,
            tools: _tools.All.ToList());

        return new AgentHandle(chatClient, agent);
    }

    private sealed record ConversationState(IChatClient Client, AgentThread Thread);

    private sealed record AgentHandle(IChatClient Client, ChatClientAgent Agent);
}
