using System;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using MOCHA.Agents.Application;
using MOCHA.Agents.Domain;

namespace MOCHA.Agents.Infrastructure.Tools;

/// <summary>
/// Organizer が利用するツール群の生成セット
/// </summary>
public sealed class OrganizerToolset
{
    private readonly IManualStore _manuals;
    private readonly ILogger<OrganizerToolset> _logger;
    private readonly PlcAgentTool _plcAgentTool;
    private readonly ILogger<PlcAgentTool> _plcLogger;
    private readonly AsyncLocal<ScopeContext?> _context = new();
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>提供するツール一覧</summary>
    public IReadOnlyList<AITool> All { get; }

    /// <summary>
    /// ツールセットの依存関係注入による初期化
    /// </summary>
    /// <param name="manuals">マニュアルストア</param>
    /// <param name="logger">ロガー</param>
    /// <param name="loggerFactory">ロガーファクトリー</param>
    public OrganizerToolset(IManualStore manuals, ILogger<OrganizerToolset> logger, ILoggerFactory loggerFactory)
    {
        _manuals = manuals;
        _logger = logger;
        _plcLogger = loggerFactory.CreateLogger<PlcAgentTool>();
        _plcAgentTool = new PlcAgentTool(_manuals, _plcLogger);

        All = new AITool[]
        {
            AIFunctionFactory.Create(
                new Func<string, string, CancellationToken, Task<string>>(FindManualsAsync),
                name: "find_manuals",
                description: "IAI/Oriental/PLC などのマニュアルインデックスから候補を検索します。"),

            AIFunctionFactory.Create(
                new Func<string, string, CancellationToken, Task<string>>(ReadManualAsync),
                name: "read_manual",
                description: "検索で得た相対パスのテキストを読み出します（先頭のみ）。"),

            AIFunctionFactory.Create(
                new Func<string, string?, CancellationToken, Task<string>>(InvokePlcAgentAsync),
                name: "invoke_plc_agent",
                description: "三菱PLC/MCプロトコル関連の解析を行います。optionsJsonで Gateway接続情報やdevicesを指定可能です。")
        };
    }

    /// <summary>
    /// ストリーミングコンテキストのスコープ設定
    /// </summary>
    /// <param name="conversationId">会話ID</param>
    /// <param name="sink">イベント受け取りコールバック</param>
    /// <returns>スコープ破棄用ハンドル</returns>
    public IDisposable UseContext(string conversationId, Action<AgentEvent> sink)
    {
        _context.Value = new ScopeContext(conversationId, sink);
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
        ctx?.Emit(AgentEventFactory.ToolRequested(ctx.ConversationId, call));
        ctx?.Emit(AgentEventFactory.ToolStarted(ctx.ConversationId, call));

        try
        {
            var hits = await _manuals.SearchAsync(normalized, query, cancellationToken);
            var payload = JsonSerializer.Serialize(hits, _serializerOptions);
            ctx?.Emit(AgentEventFactory.ToolCompleted(ctx.ConversationId, new ToolResult(call.Name, payload, true)));
            return payload;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "find_manuals 実行に失敗しました。");
            ctx?.Emit(AgentEventFactory.ToolCompleted(ctx.ConversationId, new ToolResult(call.Name, ex.Message, false, ex.Message)));
            return JsonSerializer.Serialize(new { error = ex.Message }, _serializerOptions);
        }
    }

    /// <summary>
    /// PLC エージェント呼び出しツール実行
    /// </summary>
    /// <param name="question">問い合わせ内容</param>
    /// <param name="optionsJson">追加オプション JSON</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>実行結果 JSON</returns>
    private Task<string> InvokePlcAgentAsync(string question, string? optionsJson, CancellationToken cancellationToken)
    {
        var ctx = _context.Value;
        var call = new ToolCall("invoke_plc_agent", JsonSerializer.Serialize(new { question, options = optionsJson }, _serializerOptions));
        ctx?.Emit(AgentEventFactory.ToolRequested(ctx.ConversationId, call));
        ctx?.Emit(AgentEventFactory.ToolStarted(ctx.ConversationId, call));

        return RunPlcAsync(call, question, optionsJson, cancellationToken);
    }

    /// <summary>
    /// PLC エージェント処理実行
    /// </summary>
    /// <param name="call">ツール呼び出し</param>
    /// <param name="question">問い合わせ内容</param>
    /// <param name="optionsJson">追加オプション JSON</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>実行結果 JSON</returns>
    private async Task<string> RunPlcAsync(ToolCall call, string question, string? optionsJson, CancellationToken cancellationToken)
    {
        var ctx = _context.Value;
        try
        {
            var result = await _plcAgentTool.RunAsync(question, optionsJson, cancellationToken);
            ctx?.Emit(AgentEventFactory.ToolCompleted(ctx.ConversationId, new ToolResult(call.Name, result, true)));
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "invoke_plc_agent 実行に失敗しました。");
            ctx?.Emit(AgentEventFactory.ToolCompleted(ctx.ConversationId, new ToolResult(call.Name, ex.Message, false, ex.Message)));
            return $"PLC Agent 実行エラー: {ex.Message}";
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
        ctx?.Emit(AgentEventFactory.ToolRequested(ctx.ConversationId, call));
        ctx?.Emit(AgentEventFactory.ToolStarted(ctx.ConversationId, call));

        try
        {
            var content = await _manuals.ReadAsync(normalized, relativePath, cancellationToken: cancellationToken);
            var payload = JsonSerializer.Serialize(content, _serializerOptions);
            ctx?.Emit(AgentEventFactory.ToolCompleted(ctx.ConversationId, new ToolResult(call.Name, payload, content is not null)));
            return payload;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "read_manual 実行に失敗しました。");
            ctx?.Emit(AgentEventFactory.ToolCompleted(ctx.ConversationId, new ToolResult(call.Name, ex.Message, false, ex.Message)));
            return JsonSerializer.Serialize(new { error = ex.Message }, _serializerOptions);
        }
    }

    private sealed record ScopeContext(string ConversationId, Action<AgentEvent> Sink)
    {
        public void Emit(AgentEvent ev) => Sink(ev);
    }

    private sealed class Scope : IDisposable
    {
        private readonly OrganizerToolset _owner;

        /// <summary>
        /// スコープ生成
        /// </summary>
        /// <param name="owner">元のツールセット</param>
        public Scope(OrganizerToolset owner)
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
}
