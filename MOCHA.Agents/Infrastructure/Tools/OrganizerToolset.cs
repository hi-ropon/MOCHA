using System;
using System.Collections.Generic;
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
/// Organizer が利用するツール群の生成セット
/// </summary>
public sealed class OrganizerToolset
{
    private readonly ILogger<OrganizerToolset> _logger;
    private readonly ManualToolset _manualTools;
    private readonly ManualAgentTool _manualAgentTool;
    private readonly PlcAgentTool _plcAgentTool;
    private readonly PlcToolset _plcToolset;
    private readonly AsyncLocal<ScopeContext?> _context = new();
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
    private readonly AITool _plcGatewayTool;

    /// <summary>提供するツール一覧</summary>
    public IReadOnlyList<AITool> All { get; }

    /// <summary>
    /// ツールセットの依存関係注入による初期化
    /// </summary>
    /// <param name="manualTools">マニュアルツールセット</param>
    /// <param name="manualAgentTool">マニュアルエージェントツール</param>
    /// <param name="plcAgentTool">PLC エージェントツール</param>
    /// <param name="plcToolset">PLC 専用ツールセット</param>
    /// <param name="logger">ロガー</param>
    public OrganizerToolset(
        ManualToolset manualTools,
        ManualAgentTool manualAgentTool,
        PlcAgentTool plcAgentTool,
        PlcToolset plcToolset,
        ILogger<OrganizerToolset> logger)
    {
        _logger = logger;
        _manualTools = manualTools;
        _manualAgentTool = manualAgentTool;
        _plcAgentTool = plcAgentTool;
        _plcToolset = plcToolset;
        _plcGatewayTool = AIFunctionFactory.Create(
            new Func<string?, CancellationToken, Task<string>>(RunPlcGatewayAsync),
            name: "read_plc_gateway",
            description: "PLC Gateway からデバイスを読み取ります。optionsJson に devices/IP/port を含めます。");

        All = new AITool[]
        {
            AIFunctionFactory.Create(
                new Func<string, CancellationToken, Task<string>>(InvokeIaiAgentAsync),
                name: "invoke_iai_agent",
                description: "IAI 関連のマニュアルを検索し要約を返します。"),

            AIFunctionFactory.Create(
                new Func<string, CancellationToken, Task<string>>(InvokeOrientalAgentAsync),
                name: "invoke_oriental_agent",
                description: "Oriental Motor 関連のマニュアルを検索し要約を返します。"),

            AIFunctionFactory.Create(
                new Func<string, string?, CancellationToken, Task<string>>(InvokePlcAgentAsync),
                name: "invoke_plc_agent",
                description: "三菱PLC関連の解析を行います。optionsJsonで Gateway接続情報やdevicesを指定可能です。")
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
        var manualScope = _manualTools.UseContext(conversationId, sink);
        var agentScope = _manualAgentTool.UseContext(conversationId, sink);
        var plcScope = _plcToolset.UseContext(conversationId, sink);
        return new Scope(this, manualScope, agentScope, plcScope);
    }

    /// <summary>
    /// IAI エージェント呼び出しツール実行
    /// </summary>
    /// <param name="question">問い合わせ内容</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>実行結果</returns>
    private Task<string> InvokeIaiAgentAsync(string question, CancellationToken cancellationToken)
    {
        var ctx = _context.Value;
        var call = new ToolCall("invoke_iai_agent", JsonSerializer.Serialize(new { question }, _serializerOptions));
        ctx?.Emit(AgentEventFactory.ToolRequested(ctx.ConversationId, call));
        ctx?.Emit(AgentEventFactory.ToolStarted(ctx.ConversationId, call));

        return RunManualAgentAsync(call, "iaiAgent", question, cancellationToken);
    }

    /// <summary>
    /// Oriental エージェント呼び出しツール実行
    /// </summary>
    /// <param name="question">問い合わせ内容</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>実行結果</returns>
    private Task<string> InvokeOrientalAgentAsync(string question, CancellationToken cancellationToken)
    {
        var ctx = _context.Value;
        var call = new ToolCall("invoke_oriental_agent", JsonSerializer.Serialize(new { question }, _serializerOptions));
        ctx?.Emit(AgentEventFactory.ToolRequested(ctx.ConversationId, call));
        ctx?.Emit(AgentEventFactory.ToolStarted(ctx.ConversationId, call));

        return RunManualAgentAsync(call, "orientalAgent", question, cancellationToken);
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
            var extraTools = new[] { _plcGatewayTool };
            var contextHint = BuildPlcContextHint(optionsJson);
            var result = await _manualAgentTool.RunAsync("plcAgent", question, _plcToolset.All.Concat(extraTools), contextHint, cancellationToken);
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
    /// マニュアルエージェント処理実行
    /// </summary>
    /// <param name="call">ツール呼び出し</param>
    /// <param name="agentName">エージェント名</param>
    /// <param name="question">問い合わせ内容</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>実行結果</returns>
    private async Task<string> RunManualAgentAsync(ToolCall call, string agentName, string question, CancellationToken cancellationToken)
    {
        var ctx = _context.Value;
        try
        {
            var result = await _manualAgentTool.RunAsync(agentName, question, cancellationToken: cancellationToken);
            ctx?.Emit(AgentEventFactory.ToolCompleted(ctx.ConversationId, new ToolResult(call.Name, result, true)));
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Tool} 実行に失敗しました。", call.Name);
            ctx?.Emit(AgentEventFactory.ToolCompleted(ctx.ConversationId, new ToolResult(call.Name, ex.Message, false, ex.Message)));
            return $"{call.Name} 実行エラー: {ex.Message}";
        }
    }

    /// <summary>
    /// PLC ゲートウェイ読み取りの実行
    /// </summary>
    /// <param name="optionsJson">ゲートウェイオプション</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>読み取り結果</returns>
    private async Task<string> RunPlcGatewayAsync(string? optionsJson, CancellationToken cancellationToken)
    {
        var ctx = _context.Value;
        var call = new ToolCall("read_plc_gateway", JsonSerializer.Serialize(new { options = optionsJson }, _serializerOptions));
        ctx?.Emit(AgentEventFactory.ToolRequested(ctx.ConversationId, call));
        ctx?.Emit(AgentEventFactory.ToolStarted(ctx.ConversationId, call));

        try
        {
            var result = await _plcAgentTool.RunGatewayAsync(optionsJson, cancellationToken);
            ctx?.Emit(AgentEventFactory.ToolCompleted(ctx.ConversationId, new ToolResult(call.Name, result, true)));
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "read_plc_gateway 実行に失敗しました。");
            ctx?.Emit(AgentEventFactory.ToolCompleted(ctx.ConversationId, new ToolResult(call.Name, ex.Message, false, ex.Message)));
            return $"PLC Gateway 実行エラー: {ex.Message}";
        }
    }

    /// <summary>
    /// PLC エージェントへのコンテキストヒント生成
    /// </summary>
    /// <param name="optionsJson">ゲートウェイオプション</param>
    /// <returns>システムメッセージ</returns>
    private static string? BuildPlcContextHint(string? optionsJson)
    {
        if (string.IsNullOrWhiteSpace(optionsJson))
        {
            return "ゲートウェイ読み取りが必要なら read_plc_gateway を呼び出して devices/IP/port を指定してください。";
        }

        return $"ゲートウェイ読み取りに使う optionsJson: {optionsJson}";
    }

    private sealed record ScopeContext(string ConversationId, Action<AgentEvent> Sink)
    {
        public void Emit(AgentEvent ev) => Sink(ev);
    }

    private sealed class Scope : IDisposable
    {
        private readonly OrganizerToolset _owner;
        private readonly IDisposable _manualScope;
        private readonly IDisposable _agentScope;
        private readonly IDisposable _plcScope;

        /// <summary>
        /// スコープ生成
        /// </summary>
        /// <param name="owner">元のツールセット</param>
        /// <param name="manualScope">マニュアルツールスコープ</param>
        /// <param name="agentScope">サブエージェントツールスコープ</param>
        /// <param name="plcScope">PLCツールスコープ</param>
        public Scope(OrganizerToolset owner, IDisposable manualScope, IDisposable agentScope, IDisposable plcScope)
        {
            _owner = owner;
            _manualScope = manualScope;
            _agentScope = agentScope;
            _plcScope = plcScope;
        }

        /// <summary>
        /// スコープ破棄とコンテキスト解除
        /// </summary>
        public void Dispose()
        {
            _owner._context.Value = null;
            _manualScope.Dispose();
            _agentScope.Dispose();
            _plcScope.Dispose();
        }
    }

}
