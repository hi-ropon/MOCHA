using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using MOCHA.Agents.Application;
using MOCHA.Agents.Domain;
using MOCHA.Agents.Infrastructure.Clients;
using MOCHA.Agents.Infrastructure.Orchestration;

namespace MOCHA.Agents.Infrastructure.Tools;

/// <summary>
/// マニュアル検索をサブエージェントに委譲するツール
/// </summary>
public sealed class ManualAgentTool
{
    private readonly ILlmChatClientFactory _chatClientFactory;
    private readonly ManualToolset _manualTools;
    private readonly ILogger<ManualAgentTool> _logger;
    private readonly AsyncLocal<ScopeContext?> _context = new();

    /// <summary>
    /// サブエージェント実行に必要な依存性を注入
    /// </summary>
    /// <param name="chatClientFactory">チャットクライアントファクトリー</param>
    /// <param name="manualTools">マニュアルツールセット</param>
    /// <param name="logger">ロガー</param>
    public ManualAgentTool(
        ILlmChatClientFactory chatClientFactory,
        ManualToolset manualTools,
        ILogger<ManualAgentTool> logger)
    {
        _chatClientFactory = chatClientFactory;
        _manualTools = manualTools;
        _logger = logger;
    }

    /// <summary>
    /// ストリーミングイベントの受け口を設定
    /// </summary>
    /// <param name="conversationId">会話ID</param>
    /// <param name="sink">イベントシンク</param>
    /// <returns>スコープ</returns>
    public IDisposable UseContext(string conversationId, Action<AgentEvent> sink)
    {
        _context.Value = new ScopeContext(conversationId, sink);
        return new Scope(this);
    }

    /// <summary>
    /// マニュアル検索を行うサブエージェントの起動
    /// </summary>
    /// <param name="agentName">エージェント名</param>
    /// <param name="question">問い合わせ内容</param>
    /// <param name="extraTools">追加ツール</param>
    /// <param name="contextHint">システム側ヒント</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>検索結果要約</returns>
    public async Task<string> RunAsync(
        string agentName,
        string question,
        IEnumerable<AITool>? extraTools = null,
        string? contextHint = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalized = NormalizeAgentName(agentName);
            var chatClient = _chatClientFactory.Create();
            var description = normalized.Equals("plcAgent", StringComparison.OrdinalIgnoreCase)
                ? PlcAgentInstructions.Description(normalized)
                : ManualAgentInstructions.Description(normalized);
            var instructions = normalized.Equals("plcAgent", StringComparison.OrdinalIgnoreCase)
                ? PlcAgentInstructions.For(normalized)
                : ManualAgentInstructions.For(normalized);
            if (normalized.Equals("plcAgent", StringComparison.OrdinalIgnoreCase))
            {
                var promptForLog = FormatPlcPrompt(instructions, contextHint);
                _logger.LogDebug("PLCエージェントへのプロンプト: {Instructions}", promptForLog);
            }
            var agent = new ChatClientAgent(
                chatClient,
                name: normalized,
                description: description,
                instructions: instructions,
                tools: _manualTools.All.Concat(extraTools ?? Enumerable.Empty<AITool>()).ToList());

            var messages = new List<ChatMessage>();
            if (!string.IsNullOrWhiteSpace(contextHint))
            {
                messages.Add(new ChatMessage(ChatRole.System, contextHint));
            }

            messages.Add(new ChatMessage(ChatRole.User, question));

            var thread = agent.GetNewThread();
            var sb = new StringBuilder();
            await foreach (var update in agent.RunStreamingAsync(messages, thread, new AgentRunOptions(), cancellationToken))
            {
                AppendChunk(sb, update.Text);
            }

            return sb.ToString().Trim();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "マニュアルサブエージェントの起動に失敗しました。");
            return $"マニュアルサブエージェントでエラーが発生しました: {ex.Message}";
        }
    }

    private void AppendChunk(StringBuilder sb, string? text)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            sb.Append(text);
        }
    }

    private sealed record ScopeContext(string ConversationId, Action<AgentEvent> Sink)
    {
        public void Emit(AgentEvent ev) => Sink(ev);
    }

    private sealed class Scope : IDisposable
    {
        private readonly ManualAgentTool _owner;

        public Scope(ManualAgentTool owner)
        {
            _owner = owner;
        }

        public void Dispose()
        {
            _owner._context.Value = null;
        }
    }

    private static string NormalizeAgentName(string agentName)
    {
        if (string.IsNullOrWhiteSpace(agentName))
        {
            return "plcAgent";
        }

        var trimmed = agentName.Trim();
        if (trimmed.EndsWith("Agent", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        return trimmed.ToLowerInvariant() switch
        {
            "iai" => "iaiAgent",
            "oriental" => "orientalAgent",
            "drawing" => "drawingAgent",
            "plc" => "plcAgent",
            _ => trimmed
        };
    }

    private static string FormatPlcPrompt(string instructions, string? contextHint)
    {
        if (string.IsNullOrWhiteSpace(contextHint))
        {
            return instructions;
        }

        var trimmedContext = contextHint.Trim();
        return $"{instructions}{Environment.NewLine}{Environment.NewLine}[Context]{Environment.NewLine}{trimmedContext}";
    }
}
