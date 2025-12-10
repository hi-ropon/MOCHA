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
/// Organizer が利用するツール群の生成セット
/// </summary>
public sealed class OrganizerToolset
{
    private readonly ILogger<OrganizerToolset> _logger;
    private readonly ManualToolset _manualTools;
    private readonly ManualAgentTool _manualAgentTool;
    private readonly PlcAgentTool _plcAgentTool;
    private readonly PlcToolset _plcToolset;
    private readonly IPlcDataLoader _plcDataLoader;
    private readonly IPlcAgentContextProvider _plcAgentContextProvider;
    private readonly AgentDelegationPolicy _delegationPolicy;
    private readonly AsyncLocal<ScopeContext?> _context = new();
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
    private readonly AITool _invokeIaiTool;
    private readonly AITool _invokeOrientalTool;
    private readonly AITool _invokePlcTool;
    private readonly AITool _invokeDrawingTool;
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
    /// <param name="plcDataLoader">PLC データローダー</param>
    /// <param name="plcAgentContextProvider">PLC エージェントコンテキスト</param>
    /// <param name="delegationPolicy">委譲ポリシー</param>
    /// <param name="logger">ロガー</param>
    public OrganizerToolset(
        ManualToolset manualTools,
        ManualAgentTool manualAgentTool,
        PlcAgentTool plcAgentTool,
        PlcToolset plcToolset,
        IPlcDataLoader plcDataLoader,
        IPlcAgentContextProvider plcAgentContextProvider,
        AgentDelegationPolicy delegationPolicy,
        ILogger<OrganizerToolset> logger)
    {
        _logger = logger;
        _manualTools = manualTools;
        _manualAgentTool = manualAgentTool;
        _plcAgentTool = plcAgentTool;
        _plcToolset = plcToolset;
        _plcDataLoader = plcDataLoader;
        _plcAgentContextProvider = plcAgentContextProvider;
        _delegationPolicy = delegationPolicy;
        _invokeIaiTool = AIFunctionFactory.Create(
            new Func<string, CancellationToken, Task<string>>(InvokeIaiAgentAsync),
            name: "invoke_iai_agent",
            description: "IAI 関連のマニュアルを検索し要約を返します。");

        _invokeOrientalTool = AIFunctionFactory.Create(
            new Func<string, CancellationToken, Task<string>>(InvokeOrientalAgentAsync),
            name: "invoke_oriental_agent",
            description: "Oriental Motor 関連のマニュアルを検索し要約を返します。");

        _invokePlcTool = AIFunctionFactory.Create(
            new Func<string, string?, CancellationToken, Task<string>>(InvokePlcAgentAsync),
            name: "invoke_plc_agent",
            description: "三菱PLC関連の解析を行います。optionsJsonで Gateway接続情報やdevicesを指定可能です。");

        _invokeDrawingTool = AIFunctionFactory.Create(
            new Func<string, CancellationToken, Task<string>>(InvokeDrawingAgentAsync),
            name: "invoke_drawing_agent",
            description: "登録済み図面を検索・要約します。図面に関する質問で利用します。");
        _plcGatewayTool = AIFunctionFactory.Create(
            new Func<string?, CancellationToken, Task<string>>(RunPlcGatewayAsync),
            name: "read_plc_gateway",
            description: "PLC Gateway からデバイスを読み取ります。optionsJson に devices/IP/port を含めます。");

        All = BuildDelegationTools("organizer");
    }

    private IReadOnlyList<AITool> BuildDelegationTools(string caller)
    {
        var tools = new List<AITool>();
        foreach (var callee in _delegationPolicy.GetAllowedCallees(caller))
        {
            if (string.Equals(callee, "iaiAgent", StringComparison.OrdinalIgnoreCase))
            {
                tools.Add(_invokeIaiTool);
                continue;
            }

            if (string.Equals(callee, "orientalAgent", StringComparison.OrdinalIgnoreCase))
            {
                tools.Add(_invokeOrientalTool);
                continue;
            }

            if (string.Equals(callee, "plcAgent", StringComparison.OrdinalIgnoreCase))
            {
                tools.Add(_invokePlcTool);
                continue;
            }

            if (string.Equals(callee, "drawingAgent", StringComparison.OrdinalIgnoreCase))
            {
                tools.Add(_invokeDrawingTool);
            }
        }

        return tools;
    }

    private bool TryEnterDelegation(string callee, ToolCall call, out IDisposable? frame, out string? rejection)
    {
        frame = null;
        rejection = null;
        var ctx = _context.Value;
        var caller = ctx?.CallStack.Current ?? "organizer";
        var depth = ctx?.CallStack.Depth ?? 0;

        if (!_delegationPolicy.CanInvoke(caller, callee, depth, out var reason))
        {
            rejection = reason;
            if (ctx is not null)
            {
                ctx.Emit(AgentEventFactory.ToolCompleted(ctx.ChatContext.ConversationId, new ToolResult(call.Name, reason ?? string.Empty, false, reason)));
            }

            return false;
        }

        frame = ctx?.CallStack.Push(callee);
        return true;
    }

    private string BuildDelegationHint(string caller)
    {
        var allowed = _delegationPolicy.GetAllowedCallees(caller);
        var depth = _context.Value?.CallStack.Depth ?? 0;
        var remainingDepth = Math.Max(0, _delegationPolicy.MaxDepth - depth);
        var allowedList = allowed.Count > 0 ? string.Join(", ", allowed) : "なし";
        return $"呼び出し元: {caller}\n許可された委譲先: {allowedList}\n残り呼び出し深さ: {remainingDepth}";
    }

    private static string? MergeContextHints(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left))
        {
            return right;
        }

        if (string.IsNullOrWhiteSpace(right))
        {
            return left;
        }

        return $"{left}\n\n{right}";
    }

    /// <summary>
    /// ストリーミングコンテキストのスコープ設定
    /// </summary>
    /// <param name="chatContext">チャットコンテキスト</param>
    /// <param name="sink">イベント受け取りコールバック</param>
    /// <returns>スコープ破棄用ハンドル</returns>
    public IDisposable UseContext(ChatContext chatContext, Action<AgentEvent> sink)
    {
        var callStack = new AgentCallStack();
        var rootFrame = callStack.Push("organizer");
        _context.Value = new ScopeContext(chatContext, sink, callStack);
        var manualScope = _manualTools.UseContext(chatContext, sink);
        var agentScope = _manualAgentTool.UseContext(chatContext.ConversationId, sink);
        var plcScope = _plcToolset.UseContext(chatContext.ConversationId, sink);
        return new Scope(this, manualScope, agentScope, plcScope, rootFrame);
    }

    /// <summary>
    /// IAI エージェント呼び出しツール実行
    /// </summary>
    /// <param name="question">問い合わせ内容</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>実行結果</returns>
    private async Task<string> InvokeIaiAgentAsync(string question, CancellationToken cancellationToken)
    {
        var ctx = _context.Value;
        var call = new ToolCall("invoke_iai_agent", JsonSerializer.Serialize(new { question }, _serializerOptions));
        ctx?.Emit(AgentEventFactory.ToolRequested(ctx.ChatContext.ConversationId, call));
        ctx?.Emit(AgentEventFactory.ToolStarted(ctx.ChatContext.ConversationId, call));

        if (!TryEnterDelegation("iaiAgent", call, out var frame, out var rejection))
        {
            return rejection ?? "エージェント呼び出しが許可されていません";
        }

        if (frame is not null)
        {
            using (frame)
            {
                return await RunManualAgentAsync(call, "iaiAgent", question, cancellationToken);
            }
        }

        return await RunManualAgentAsync(call, "iaiAgent", question, cancellationToken);
    }

    /// <summary>
    /// Oriental エージェント呼び出しツール実行
    /// </summary>
    /// <param name="question">問い合わせ内容</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>実行結果</returns>
    private async Task<string> InvokeOrientalAgentAsync(string question, CancellationToken cancellationToken)
    {
        var ctx = _context.Value;
        var call = new ToolCall("invoke_oriental_agent", JsonSerializer.Serialize(new { question }, _serializerOptions));
        ctx?.Emit(AgentEventFactory.ToolRequested(ctx.ChatContext.ConversationId, call));
        ctx?.Emit(AgentEventFactory.ToolStarted(ctx.ChatContext.ConversationId, call));

        if (!TryEnterDelegation("orientalAgent", call, out var frame, out var rejection))
        {
            return rejection ?? "エージェント呼び出しが許可されていません";
        }

        if (frame is not null)
        {
            using (frame)
            {
                return await RunManualAgentAsync(call, "orientalAgent", question, cancellationToken);
            }
        }

        return await RunManualAgentAsync(call, "orientalAgent", question, cancellationToken);
    }

    /// <summary>
    /// PLC エージェント呼び出しツール実行
    /// </summary>
    /// <param name="question">問い合わせ内容</param>
    /// <param name="optionsJson">追加オプション JSON</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>実行結果 JSON</returns>
    private async Task<string> InvokePlcAgentAsync(string question, string? optionsJson, CancellationToken cancellationToken)
    {
        var ctx = _context.Value;
        var call = new ToolCall("invoke_plc_agent", JsonSerializer.Serialize(new { question, options = optionsJson }, _serializerOptions));
        ctx?.Emit(AgentEventFactory.ToolRequested(ctx.ChatContext.ConversationId, call));
        ctx?.Emit(AgentEventFactory.ToolStarted(ctx.ChatContext.ConversationId, call));

        if (!TryEnterDelegation("plcAgent", call, out var frame, out var rejection))
        {
            return rejection ?? "エージェント呼び出しが許可されていません";
        }

        if (frame is not null)
        {
            using (frame)
            {
                return await RunPlcAsync(call, question, optionsJson, cancellationToken);
            }
        }

        return await RunPlcAsync(call, question, optionsJson, cancellationToken);
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
            var parsedOptions = ParsePlcOptions(optionsJson);
            var unitId = parsedOptions.PlcUnitId is not null && Guid.TryParse(parsedOptions.PlcUnitId, out var guid) ? guid : (Guid?)null;
            await _plcDataLoader.LoadAsync(ctx?.ChatContext.UserId, ctx?.ChatContext.AgentNumber, unitId, parsedOptions.EnableFunctionBlocks, cancellationToken);
            var connectionContext = await _plcAgentContextProvider.BuildAsync(ctx?.ChatContext.UserId, ctx?.ChatContext.AgentNumber, unitId, cancellationToken);
            var plcOnline = ctx?.ChatContext.PlcOnline ?? true;
            var delegationTools = BuildDelegationTools("plcAgent");
            var plcTools = _plcToolset.GetTools(plcOnline);
            var gatewayTools = plcOnline ? new[] { _plcGatewayTool } : Array.Empty<AITool>();
            var extraTools = plcTools.Concat(gatewayTools).Concat(delegationTools);
            var plcHint = _plcToolset.BuildContextHint(parsedOptions.GatewayOptionsJson, parsedOptions.PlcUnitId, parsedOptions.PlcUnitName, parsedOptions.EnableFunctionBlocks, parsedOptions.Note, plcOnline, connectionContext);
            var contextHint = MergeContextHints(plcHint, BuildDelegationHint("plcAgent"));
            var result = await _manualAgentTool.RunAsync("plcAgent", question, extraTools, contextHint, cancellationToken);
            ctx?.Emit(AgentEventFactory.ToolCompleted(ctx.ChatContext.ConversationId, new ToolResult(call.Name, result, true)));
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "invoke_plc_agent 実行に失敗しました。");
            ctx?.Emit(AgentEventFactory.ToolCompleted(ctx.ChatContext.ConversationId, new ToolResult(call.Name, ex.Message, false, ex.Message)));
            return $"PLC Agent 実行エラー: {ex.Message}";
        }
    }

    private static PlcAgentOptions ParsePlcOptions(string? optionsJson)
    {
        if (string.IsNullOrWhiteSpace(optionsJson))
        {
            return new PlcAgentOptions();
        }

        try
        {
            return JsonSerializer.Deserialize<PlcAgentOptions>(optionsJson, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? new PlcAgentOptions();
        }
        catch
        {
            return new PlcAgentOptions { GatewayOptionsJson = optionsJson };
        }
    }

    /// <summary>
    /// 図面エージェント呼び出しツール実行
    /// </summary>
    /// <param name="question">問い合わせ内容</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>実行結果</returns>
    private async Task<string> InvokeDrawingAgentAsync(string question, CancellationToken cancellationToken)
    {
        var ctx = _context.Value;
        var call = new ToolCall("invoke_drawing_agent", JsonSerializer.Serialize(new { question }, _serializerOptions));
        ctx?.Emit(AgentEventFactory.ToolRequested(ctx.ChatContext.ConversationId, call));
        ctx?.Emit(AgentEventFactory.ToolStarted(ctx.ChatContext.ConversationId, call));

        if (!TryEnterDelegation("drawingAgent", call, out var frame, out var rejection))
        {
            return rejection ?? "エージェント呼び出しが許可されていません";
        }

        if (frame is not null)
        {
            using (frame)
            {
                return await RunManualAgentAsync(call, "drawingAgent", question, cancellationToken);
            }
        }

        return await RunManualAgentAsync(call, "drawingAgent", question, cancellationToken);
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
            ctx?.Emit(AgentEventFactory.ToolCompleted(ctx.ChatContext.ConversationId, new ToolResult(call.Name, result, true)));
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Tool} 実行に失敗しました。", call.Name);
            ctx?.Emit(AgentEventFactory.ToolCompleted(ctx.ChatContext.ConversationId, new ToolResult(call.Name, ex.Message, false, ex.Message)));
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
        ctx?.Emit(AgentEventFactory.ToolRequested(ctx.ChatContext.ConversationId, call));
        ctx?.Emit(AgentEventFactory.ToolStarted(ctx.ChatContext.ConversationId, call));

        try
        {
            var result = await _plcAgentTool.RunGatewayAsync(optionsJson, cancellationToken);
            ctx?.Emit(AgentEventFactory.ToolCompleted(ctx.ChatContext.ConversationId, new ToolResult(call.Name, result, true)));
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "read_plc_gateway 実行に失敗しました。");
            ctx?.Emit(AgentEventFactory.ToolCompleted(ctx.ChatContext.ConversationId, new ToolResult(call.Name, ex.Message, false, ex.Message)));
            return $"PLC Gateway 実行エラー: {ex.Message}";
        }
    }

    private sealed record ScopeContext(ChatContext ChatContext, Action<AgentEvent> Sink, AgentCallStack CallStack)
    {
        public void Emit(AgentEvent ev) => Sink(ev);
    }

    private sealed class AgentCallStack
    {
        private readonly Stack<string> _stack = new();

        public int Depth => _stack.Count;
        public string Current => _stack.Count > 0 ? _stack.Peek() : "organizer";

        public IDisposable Push(string agent)
        {
            _stack.Push(agent);
            return new PopHandle(this);
        }

        private void Pop()
        {
            if (_stack.Count > 0)
            {
                _stack.Pop();
            }
        }

        private sealed class PopHandle : IDisposable
        {
            private readonly AgentCallStack _owner;
            private bool _disposed;

            public PopHandle(AgentCallStack owner)
            {
                _owner = owner;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _owner.Pop();
                _disposed = true;
            }
        }
    }

    private sealed class Scope : IDisposable
    {
        private readonly OrganizerToolset _owner;
        private readonly IDisposable _manualScope;
        private readonly IDisposable _agentScope;
        private readonly IDisposable _plcScope;
        private readonly IDisposable _rootFrame;

        /// <summary>
        /// スコープ生成
        /// </summary>
        /// <param name="owner">元のツールセット</param>
        /// <param name="manualScope">マニュアルツールスコープ</param>
        /// <param name="agentScope">サブエージェントツールスコープ</param>
        /// <param name="plcScope">PLCツールスコープ</param>
        /// <param name="rootFrame">呼び出しスタック初期フレーム</param>
        public Scope(OrganizerToolset owner, IDisposable manualScope, IDisposable agentScope, IDisposable plcScope, IDisposable rootFrame)
        {
            _owner = owner;
            _manualScope = manualScope;
            _agentScope = agentScope;
            _plcScope = plcScope;
            _rootFrame = rootFrame;
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
            _rootFrame.Dispose();
        }
    }

}
