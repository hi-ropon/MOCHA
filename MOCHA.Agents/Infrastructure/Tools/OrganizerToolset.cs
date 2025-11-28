using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using MOCHA.Agents.Application;
using MOCHA.Agents.Domain;

namespace MOCHA.Agents.Infrastructure.Tools;

/// <summary>
/// Organizer が利用するツール群を生成する。
/// </summary>
public sealed class OrganizerToolset
{
    private readonly IManualStore _manuals;
    private readonly ILogger<OrganizerToolset> _logger;
    private readonly AsyncLocal<ScopeContext?> _context = new();
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public IReadOnlyList<AITool> All { get; }

    public OrganizerToolset(IManualStore manuals, ILogger<OrganizerToolset> logger)
    {
        _manuals = manuals;
        _logger = logger;

        All = new AITool[]
        {
            AIFunctionFactory.Create(
                new Func<string, string, CancellationToken, Task<string>>(FindManualsAsync),
                name: "find_manuals",
                description: "IAI/Oriental/PLC などのマニュアルインデックスから候補を検索します。"),

            AIFunctionFactory.Create(
                new Func<string, string, CancellationToken, Task<string>>(ReadManualAsync),
                name: "read_manual",
                description: "検索で得た相対パスのテキストを読み出します（先頭のみ）。")
        };
    }

    public IDisposable UseContext(string conversationId, Action<AgentEvent> sink)
    {
        _context.Value = new ScopeContext(conversationId, sink);
        return new Scope(this);
    }

    private async Task<string> FindManualsAsync(string agentName, string query, CancellationToken cancellationToken)
    {
        var ctx = _context.Value;
        var call = new ToolCall("find_manuals", JsonSerializer.Serialize(new { agentName, query }, _serializerOptions));
        ctx?.Emit(AgentEventFactory.ToolRequested(ctx.ConversationId, call));
        ctx?.Emit(AgentEventFactory.ToolStarted(ctx.ConversationId, call));

        try
        {
            var hits = await _manuals.SearchAsync(agentName, query, cancellationToken);
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

    private async Task<string> ReadManualAsync(string agentName, string relativePath, CancellationToken cancellationToken)
    {
        var ctx = _context.Value;
        var call = new ToolCall("read_manual", JsonSerializer.Serialize(new { agentName, relativePath }, _serializerOptions));
        ctx?.Emit(AgentEventFactory.ToolRequested(ctx.ConversationId, call));
        ctx?.Emit(AgentEventFactory.ToolStarted(ctx.ConversationId, call));

        try
        {
            var content = await _manuals.ReadAsync(agentName, relativePath, cancellationToken: cancellationToken);
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

        public Scope(OrganizerToolset owner)
        {
            _owner = owner;
        }

        public void Dispose()
        {
            _owner._context.Value = null;
        }
    }
}
