using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using MOCHA.Agents.Application;
using MOCHA.Agents.Domain;

namespace MOCHA.Agents.Infrastructure.Tools;

/// <summary>
/// マニュアル検索と読取りを提供する共通ツールセット
/// </summary>
public sealed class ManualToolset
{
    private readonly IManualStore _manuals;
    private readonly ILogger<ManualToolset> _logger;
    private readonly AsyncLocal<ScopeContext?> _context = new();
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>提供するマニュアルツール一覧</summary>
    public IReadOnlyList<AITool> All { get; }

    /// <summary>
    /// マニュアルツールセットの初期化
    /// </summary>
    /// <param name="manuals">マニュアルストア</param>
    /// <param name="logger">ロガー</param>
    public ManualToolset(IManualStore manuals, ILogger<ManualToolset> logger)
    {
        _manuals = manuals;
        _logger = logger;

        All = new AITool[]
        {
            AIFunctionFactory.Create(
                new Func<string, string, CancellationToken, Task<string>>(FindManualsAsync),
                name: "find_manuals",
                description: "マニュアルインデックスから候補を検索します。agentName は該当エージェント名を指定します。"),

            AIFunctionFactory.Create(
                new Func<string, string, CancellationToken, Task<string>>(ReadManualAsync),
                name: "read_manual",
                description: "検索で得た相対パスのテキストを読み出します（先頭のみ）。")
        };
    }

    /// <summary>
    /// ストリーミングコンテキストのスコープ設定
    /// </summary>
    /// <param name="chatContext">チャットコンテキスト</param>
    /// <param name="sink">イベント受け取りコールバック</param>
    /// <returns>スコープ破棄用ハンドル</returns>
    public IDisposable UseContext(ChatContext chatContext, Action<AgentEvent> sink)
    {
        _context.Value = new ScopeContext(chatContext, sink);
        return new Scope(this);
    }

    /// <summary>
    /// マニュアル検索ツール実行
    /// </summary>
    /// <param name="agentName">エージェント名</param>
    /// <param name="query">検索クエリ</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>検索結果 JSON</returns>
    private async Task<string> FindManualsAsync(string agentName, string query, CancellationToken cancellationToken)
    {
        var ctx = _context.Value;
        var normalized = NormalizeAgentName(agentName);
        var call = new ToolCall("find_manuals", JsonSerializer.Serialize(new { agentName = normalized, query }, _serializerOptions));
        ctx?.Emit(AgentEventFactory.ToolRequested(ctx.ChatContext.ConversationId, call));
        ctx?.Emit(AgentEventFactory.ToolStarted(ctx.ChatContext.ConversationId, call));

        try
        {
            if (ctx is not null)
            {
                ctx.LastQuery = query;
            }
            var manualContext = ctx?.ToManualContext();
            var hits = await _manuals.SearchAsync(normalized, query, manualContext, cancellationToken);
            var payload = JsonSerializer.Serialize(hits, _serializerOptions);
            ctx?.Emit(AgentEventFactory.ToolCompleted(ctx.ChatContext.ConversationId, new ToolResult(call.Name, payload, true)));
            return payload;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "find_manuals 実行に失敗しました。");
            ctx?.Emit(AgentEventFactory.ToolCompleted(ctx.ChatContext.ConversationId, new ToolResult(call.Name, ex.Message, false, ex.Message)));
            return JsonSerializer.Serialize(new { error = ex.Message }, _serializerOptions);
        }
    }

    /// <summary>
    /// マニュアル読み取りツール実行
    /// </summary>
    /// <param name="agentName">エージェント名</param>
    /// <param name="relativePath">マニュアル相対パス</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>読み取り結果 JSON</returns>
    private async Task<string> ReadManualAsync(string agentName, string relativePath, CancellationToken cancellationToken)
    {
        var ctx = _context.Value;
        var normalized = NormalizeAgentName(agentName);
        var call = new ToolCall("read_manual", JsonSerializer.Serialize(new { agentName = normalized, relativePath }, _serializerOptions));
        ctx?.Emit(AgentEventFactory.ToolRequested(ctx.ChatContext.ConversationId, call));
        ctx?.Emit(AgentEventFactory.ToolStarted(ctx.ChatContext.ConversationId, call));

        try
        {
            var manualContext = ctx?.ToManualContext();
            var isDrawing = relativePath.StartsWith("drawing:", StringComparison.OrdinalIgnoreCase);
            var limit = isDrawing ? 10_000_000 : 800;
            var content = await _manuals.ReadAsync(normalized, relativePath, maxBytes: limit, context: manualContext, cancellationToken: cancellationToken);
            var payload = JsonSerializer.Serialize(content, _serializerOptions);
            ctx?.Emit(AgentEventFactory.ToolCompleted(ctx.ChatContext.ConversationId, new ToolResult(call.Name, payload, content is not null)));
            return payload;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "read_manual 実行に失敗しました。");
            ctx?.Emit(AgentEventFactory.ToolCompleted(ctx.ChatContext.ConversationId, new ToolResult(call.Name, ex.Message, false, ex.Message)));
            return JsonSerializer.Serialize(new { error = ex.Message }, _serializerOptions);
        }
    }

    /// <summary>
    /// エージェント名正規化
    /// </summary>
    /// <param name="agentName">入力エージェント名</param>
    /// <returns>正規化済みエージェント名</returns>
    private static string NormalizeAgentName(string agentName)
    {
        if (string.IsNullOrWhiteSpace(agentName))
        {
            return "iaiAgent";
        }

        var first = agentName.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        var token = (first ?? agentName).Trim();
        if (token.EndsWith("Agent", StringComparison.OrdinalIgnoreCase))
        {
            return token;
        }

        return token.ToLowerInvariant() switch
        {
            "iai" => "iaiAgent",
            "oriental" => "orientalAgent",
            "plc" => "plcAgent",
            _ => token
        };
    }

    private sealed class ScopeContext
    {
        public ScopeContext(ChatContext chatContext, Action<AgentEvent> sink)
        {
            ChatContext = chatContext;
            Sink = sink;
        }

        public ChatContext ChatContext { get; }
        public Action<AgentEvent> Sink { get; }
        public string? LastQuery { get; set; }

        public void Emit(AgentEvent ev) => Sink(ev);

        public ManualSearchContext ToManualContext() =>
            new(ChatContext.UserId, ChatContext.AgentNumber, LastQuery);
    }

    private sealed class Scope : IDisposable
    {
        private readonly ManualToolset _owner;

        /// <summary>
        /// スコープ生成
        /// </summary>
        /// <param name="owner">元のツールセット</param>
        public Scope(ManualToolset owner)
        {
            _owner = owner;
        }

        /// <summary>
        /// スコープ破棄とコンテキスト解除
        /// </summary>
        public void Dispose()
        {
            _owner._context.Value = null;
        }
    }
}
