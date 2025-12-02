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
using MOCHA.Agents.Domain;
using MOCHA.Agents.Domain.Plc;
using MOCHA.Agents.Infrastructure.Plc;

namespace MOCHA.Agents.Infrastructure.Tools;

/// <summary>
/// PLC向けの機能ツールセット
/// </summary>
public sealed class PlcToolset
{
    private readonly IPlcDataStore _store;
    private readonly IPlcGatewayClient _gateway;
    private readonly PlcProgramAnalyzer _programAnalyzer;
    private readonly PlcReasoner _reasoner;
    private readonly PlcManualService _manuals;
    private readonly ILogger<PlcToolset> _logger;
    private readonly AsyncLocal<ScopeContext?> _context = new();
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>提供するツール一覧</summary>
    public IReadOnlyList<AITool> All { get; }

    public PlcToolset(
        IPlcDataStore store,
        IPlcGatewayClient gateway,
        PlcProgramAnalyzer programAnalyzer,
        PlcReasoner reasoner,
        PlcManualService manuals,
        ILogger<PlcToolset> logger)
    {
        _store = store;
        _gateway = gateway;
        _programAnalyzer = programAnalyzer;
        _reasoner = reasoner;
        _manuals = manuals;
        _logger = logger;

        All = new AITool[]
        {
            AIFunctionFactory.Create(new Func<string, int, int, CancellationToken, Task<string>>(GetProgramLinesAsync),
                name: "program_lines",
                description: "指定デバイスの周辺プログラム行を返します。dev 例: D, M"),

            AIFunctionFactory.Create(new Func<string, int, CancellationToken, Task<string>>(GetRelatedDevicesAsync),
                name: "related_devices",
                description: "指定デバイスと同じ行に出る関連デバイスを列挙します。"),

            AIFunctionFactory.Create(new Func<string, int, CancellationToken, Task<string>>(GetCommentAsync),
                name: "get_comment",
                description: "デバイスコメントを取得します。"),

            AIFunctionFactory.Create(new Func<string, CancellationToken, Task<string>>(InferDeviceAsync),
                name: "reasoning_device",
                description: "質問文から単一デバイスを推定します。"),

            AIFunctionFactory.Create(new Func<string, CancellationToken, Task<string>>(InferDevicesAsync),
                name: "reasoning_multiple_devices",
                description: "質問文から複数デバイスを推定します。"),

            AIFunctionFactory.Create(new Func<string, string, int, int, string?, CancellationToken, Task<string>>(ReadValuesAsync),
                name: "read_plc_values",
                description: "ゲートウェイ経由でデバイス値を読み取ります。spec 例: D100、timeout 秒指定可。"),

            AIFunctionFactory.Create(new Func<IEnumerable<string>, string?, CancellationToken, Task<string>>(ReadMultipleValuesAsync),
                name: "read_multiple_plc_values",
                description: "複数デバイスを一括で読み取ります。"),

            AIFunctionFactory.Create(new Func<string, CancellationToken, Task<string>>(SearchManualAsync),
                name: "search_manual",
                description: "PLCマニュアルをキーワードで検索します。"),

            AIFunctionFactory.Create(new Func<string, CancellationToken, Task<string>>(SearchInstructionAsync),
                name: "search_instruction",
                description: "命令名でマニュアル検索します。"),

            AIFunctionFactory.Create(new Func<CancellationToken, Task<string>>(GetCommandOverviewAsync),
                name: "get_command_overview",
                description: "命令一覧の概要を取得します。")
        };
    }

    /// <summary>
    /// ストリーミングコンテキストのスコープ設定
    /// </summary>
    public IDisposable UseContext(string conversationId, Action<AgentEvent> sink)
    {
        _context.Value = new ScopeContext(conversationId, sink);
        return new Scope(this);
    }

    private Task<string> GetProgramLinesAsync(string dev, int address, int context, CancellationToken cancellationToken)
    {
        var call = new ToolCall("program_lines", JsonSerializer.Serialize(new { dev, address, context }, _serializerOptions));
        EmitRequested(call);

        try
        {
            var blocks = _programAnalyzer.GetProgramBlocks(dev, address, context);
            var payload = JsonSerializer.Serialize(blocks, _serializerOptions);
            EmitCompleted(call, payload, true);
            return Task.FromResult(payload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "program_lines 実行に失敗しました。");
            EmitCompleted(call, ex.Message, false, ex.Message);
            return Task.FromResult(ex.Message);
        }
    }

    private Task<string> GetRelatedDevicesAsync(string dev, int address, CancellationToken cancellationToken)
    {
        var call = new ToolCall("related_devices", JsonSerializer.Serialize(new { dev, address }, _serializerOptions));
        EmitRequested(call);

        try
        {
            var devices = _programAnalyzer.GetRelatedDevices(dev, address);
            var payload = string.Join(",", devices);
            EmitCompleted(call, payload, true);
            return Task.FromResult(payload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "related_devices 実行に失敗しました。");
            EmitCompleted(call, ex.Message, false, ex.Message);
            return Task.FromResult(ex.Message);
        }
    }

    private Task<string> GetCommentAsync(string dev, int address, CancellationToken cancellationToken)
    {
        var call = new ToolCall("get_comment", JsonSerializer.Serialize(new { dev, address }, _serializerOptions));
        EmitRequested(call);

        try
        {
            var comment = _programAnalyzer.GetComment(dev, address);
            EmitCompleted(call, comment, true);
            return Task.FromResult(comment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "get_comment 実行に失敗しました。");
            EmitCompleted(call, ex.Message, false, ex.Message);
            return Task.FromResult(ex.Message);
        }
    }

    private Task<string> InferDeviceAsync(string query, CancellationToken cancellationToken)
    {
        var call = new ToolCall("reasoning_device", JsonSerializer.Serialize(new { query }, _serializerOptions));
        EmitRequested(call);
        var result = _reasoner.InferSingle(query);
        EmitCompleted(call, result, true);
        return Task.FromResult(result);
    }

    private Task<string> InferDevicesAsync(string query, CancellationToken cancellationToken)
    {
        var call = new ToolCall("reasoning_multiple_devices", JsonSerializer.Serialize(new { query }, _serializerOptions));
        EmitRequested(call);
        var result = _reasoner.InferMultiple(query);
        EmitCompleted(call, result, true);
        return Task.FromResult(result);
    }

    private async Task<string> ReadValuesAsync(string spec, string ip, int port, int timeoutSeconds, string? baseUrl, CancellationToken cancellationToken)
    {
        var call = new ToolCall("read_plc_values", JsonSerializer.Serialize(new { spec, ip, port, timeoutSeconds, baseUrl }, _serializerOptions));
        EmitRequested(call);

        try
        {
            var result = await _gateway.ReadAsync(
                new DeviceReadRequest(spec, ip, port, TimeSpan.FromSeconds(timeoutSeconds > 0 ? timeoutSeconds : 10), baseUrl),
                cancellationToken);

            var payload = JsonSerializer.Serialize(result, _serializerOptions);
            EmitCompleted(call, payload, result.Success);
            return payload;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "read_plc_values 実行に失敗しました。");
            EmitCompleted(call, ex.Message, false, ex.Message);
            return ex.Message;
        }
    }

    private async Task<string> ReadMultipleValuesAsync(IEnumerable<string> specs, string? baseUrl, CancellationToken cancellationToken)
    {
        var call = new ToolCall("read_multiple_plc_values", JsonSerializer.Serialize(new { specs, baseUrl }, _serializerOptions));
        EmitRequested(call);

        try
        {
            var result = await _gateway.ReadBatchAsync(
                new BatchReadRequest(specs.ToList(), BaseUrl: baseUrl),
                cancellationToken);
            var payload = JsonSerializer.Serialize(result, _serializerOptions);
            EmitCompleted(call, payload, string.IsNullOrEmpty(result.Error));
            return payload;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "read_multiple_plc_values 実行に失敗しました。");
            EmitCompleted(call, ex.Message, false, ex.Message);
            return ex.Message;
        }
    }

    private async Task<string> SearchManualAsync(string query, CancellationToken cancellationToken)
    {
        var call = new ToolCall("search_manual", JsonSerializer.Serialize(new { query }, _serializerOptions));
        EmitRequested(call);
        try
        {
            var result = await _manuals.SearchAsync(query, cancellationToken);
            EmitCompleted(call, result, true);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "search_manual 実行に失敗しました。");
            EmitCompleted(call, ex.Message, false, ex.Message);
            return ex.Message;
        }
    }

    private async Task<string> SearchInstructionAsync(string instruction, CancellationToken cancellationToken)
    {
        var call = new ToolCall("search_instruction", JsonSerializer.Serialize(new { instruction }, _serializerOptions));
        EmitRequested(call);
        try
        {
            var result = await _manuals.SearchInstructionAsync(instruction, cancellationToken);
            EmitCompleted(call, result, true);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "search_instruction 実行に失敗しました。");
            EmitCompleted(call, ex.Message, false, ex.Message);
            return ex.Message;
        }
    }

    private async Task<string> GetCommandOverviewAsync(CancellationToken cancellationToken)
    {
        var call = new ToolCall("get_command_overview", JsonSerializer.Serialize(new { }, _serializerOptions));
        EmitRequested(call);
        try
        {
            var result = await _manuals.GetCommandOverviewAsync(cancellationToken);
            EmitCompleted(call, result, true);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "get_command_overview 実行に失敗しました。");
            EmitCompleted(call, ex.Message, false, ex.Message);
            return ex.Message;
        }
    }

    private void EmitRequested(ToolCall call)
    {
        var ctx = _context.Value;
        ctx?.Emit(AgentEventFactory.ToolRequested(ctx.ConversationId, call));
        ctx?.Emit(AgentEventFactory.ToolStarted(ctx.ConversationId, call));
    }

    private void EmitCompleted(ToolCall call, string result, bool success, string? error = null)
    {
        var ctx = _context.Value;
        ctx?.Emit(AgentEventFactory.ToolCompleted(ctx.ConversationId, new ToolResult(call.Name, result, success, error)));
    }

    private sealed record ScopeContext(string ConversationId, Action<AgentEvent> Sink)
    {
        public void Emit(AgentEvent ev) => Sink(ev);
    }

    private sealed class Scope : IDisposable
    {
        private readonly PlcToolset _owner;

        public Scope(PlcToolset owner)
        {
            _owner = owner;
        }

        public void Dispose()
        {
            _owner._context.Value = null;
        }
    }
}
