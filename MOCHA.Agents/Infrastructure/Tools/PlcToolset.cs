using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.IO;
using System.Text;
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
using MOCHA.Agents.Application;

namespace MOCHA.Agents.Infrastructure.Tools;

/// <summary>
/// PLC向けの機能ツールセット
/// </summary>
public sealed class PlcToolset
{
    private readonly IPlcDataStore _store;
    private readonly IPlcGatewayClient _gateway;
    private readonly PlcProgramAnalyzer _programAnalyzer;
    private readonly PlcCommentSearchService _commentSearch;
    private readonly PlcReasoner _reasoner;
    private readonly PlcFaultTracer _faultTracer;
    private readonly PlcManualService _manuals;
    private readonly ILogger<PlcToolset> _logger;
    private IReadOnlyList<AITool>? _toolsWithoutGatewayReads;
    private readonly AsyncLocal<ScopeContext?> _context = new();
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// 提供するツール一覧
    /// </summary>
    public IReadOnlyList<AITool> All { get; }

    /// <summary>
    /// 接続状態に応じたツール一覧を返す
    /// </summary>
    /// <param name="includeGatewayReads">ゲートウェイ読み取りツールを含めるか</param>
    /// <returns>使用可能なツール一覧</returns>
    public IReadOnlyList<AITool> GetTools(bool includeGatewayReads)
    {
        if (includeGatewayReads)
        {
            return All;
        }

        _toolsWithoutGatewayReads ??= All
            .Where(t => !string.Equals(t.Name, "read_plc_values", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(t.Name, "read_multiple_plc_values", StringComparison.OrdinalIgnoreCase))
            .ToList();

        return _toolsWithoutGatewayReads;
    }

    /// <summary>
    /// 依存関係の注入による初期化
    /// </summary>
    /// <param name="store">PLCデータストア</param>
    /// <param name="gateway">PLCゲートウェイクライアント</param>
    /// <param name="programAnalyzer">プログラム解析器</param>
    /// <param name="reasoner">デバイス推論器</param>
    /// <param name="faultTracer">異常コイルトレーサー</param>
    /// <param name="manuals">PLCマニュアルサービス</param>
    /// <param name="logger">ロガー</param>
    public PlcToolset(
        IPlcDataStore store,
        IPlcGatewayClient gateway,
        PlcProgramAnalyzer programAnalyzer,
        PlcCommentSearchService commentSearch,
        PlcReasoner reasoner,
        PlcFaultTracer faultTracer,
        PlcManualService manuals,
        ILogger<PlcToolset> logger)
    {
        _store = store;
        _gateway = gateway;
        _programAnalyzer = programAnalyzer;
        _commentSearch = commentSearch;
        _reasoner = reasoner;
        _faultTracer = faultTracer;
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

            AIFunctionFactory.Create(new Func<string, CancellationToken, Task<string>>(SearchCommentsAsync),
                name: "search_comment_devices",
                description: "質問文をコメントに対して全文検索し関連デバイス候補を返します。"),

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
                description: "命令一覧の概要を取得します。"),

            AIFunctionFactory.Create(new Func<CancellationToken, Task<string>>(ListFunctionBlocksAsync),
                name: "list_function_blocks",
                description: "登録済みファンクションブロックの一覧を返します。"),

            AIFunctionFactory.Create(new Func<string, CancellationToken, Task<string>>(AnalyzeFunctionBlockAsync),
                name: "analyze_function_block",
                description: "指定ファンクションブロックのラベルとプログラムを要約します。"),

            AIFunctionFactory.Create(new Func<string, CancellationToken, Task<string>>(SearchFunctionBlocksAsync),
                name: "search_function_blocks",
                description: "キーワードでファンクションブロックを検索します。"),

            AIFunctionFactory.Create(new Func<CancellationToken, Task<string>>(TraceErrorCoilsAsync),
                name: "trace_error_coil",
                description: "コメントに異常/ERRを含むLコイルをOUT命令から追跡し関連接点を返します。")
        };
    }

    /// <summary>
    /// データロード後のコンテキストヒントを構築
    /// </summary>
    /// <param name="gatewayOptionsJson">ゲートウェイオプション</param>
    /// <param name="plcUnitId">対象PLCユニットID</param>
    /// <param name="plcUnitName">対象PLCユニット名</param>
    /// <param name="enableFunctionBlocks">ファンクションブロックの有効化</param>
    /// <param name="note">補足情報</param>
    /// <param name="plcOnline">実機読み取り可否</param>
    /// <returns>ヒント文字列</returns>
    public string BuildContextHint(string? gatewayOptionsJson, string? plcUnitId, string? plcUnitName, bool enableFunctionBlocks, string? note, bool plcOnline, PlcAgentContext? connectionContext)
    {
        var sb = new StringBuilder();
        sb.AppendLine("登録済みPLCデータ概要");
        sb.AppendLine("- プログラムファイル:");
        var programNames = _store.Programs.Keys
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (programNames.Count > 0)
        {
            foreach (var programName in programNames)
            {
                sb.AppendLine($"  - {programName}");
            }
        }
        else
        {
            sb.AppendLine("  - なし");
        }

        sb.AppendLine($"- ファンクションブロック: {(enableFunctionBlocks ? "enabled" : "disabled")}");
        var functionBlockNames = _store.FunctionBlocks
            .Select(block => block.Name)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (functionBlockNames.Count > 0)
        {
            foreach (var blockName in functionBlockNames)
            {
                sb.AppendLine($"  - {blockName}");
            }
        }
        else
        {
            sb.AppendLine("  - なし");
        }
        sb.AppendLine($"- 実機接続: {(plcOnline ? "オンライン" : "オフライン（read_plc_values/read_multiple_plc_values は無効）")}");

        if (!string.IsNullOrWhiteSpace(plcUnitId) || !string.IsNullOrWhiteSpace(plcUnitName))
        {
            sb.AppendLine($"対象ユニット: {plcUnitName ?? "(unknown)"} ({plcUnitId ?? "n/a"})");
        }

        if (!string.IsNullOrWhiteSpace(gatewayOptionsJson))
        {
            sb.AppendLine($"ゲートウェイオプション: {gatewayOptionsJson}");
        }

        if (connectionContext is not null && !connectionContext.IsEmpty)
        {
            sb.AppendLine("[接続設定]");
            var gatewayPort = connectionContext.GatewayPort?.ToString(CultureInfo.InvariantCulture) ?? "-";
            if (!string.IsNullOrWhiteSpace(connectionContext.GatewayHost))
            {
                sb.AppendLine($"- デフォルトゲートウェイ: {connectionContext.GatewayHost}:{gatewayPort}");
            }

            foreach (var unit in connectionContext.Units)
            {
                var ip = string.IsNullOrWhiteSpace(unit.IpAddress) ? "-" : unit.IpAddress;
                var port = unit.Port?.ToString(CultureInfo.InvariantCulture) ?? "-";
                var gw = FormatGateway(unit.GatewayHost, unit.GatewayPort);
                sb.AppendLine($"- ユニット: {unit.Name} ip={ip} port={port}{gw}");
            }
        }

        if (!string.IsNullOrWhiteSpace(note))
        {
            sb.AppendLine($"補足: {note}");
        }

        return sb.ToString().Trim();
    }

    private static string FormatGateway(string? host, int? port)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return string.Empty;
        }

        var text = port is not null ? $"{host}:{port.Value.ToString(CultureInfo.InvariantCulture)}" : host;
        return $" gw={text}";
    }

    /// <summary>
    /// ストリーミングコンテキストのスコープ設定
    /// </summary>
    /// <param name="conversationId">会話ID</param>
    /// <param name="sink">イベント受け口</param>
    /// <returns>スコープハンドル</returns>
    public IDisposable UseContext(string conversationId, Action<AgentEvent> sink)
    {
        _context.Value = new ScopeContext(conversationId, sink);
        return new Scope(this);
    }

    /// <summary>
    /// プログラム行取得
    /// </summary>
    /// <param name="dev">デバイス種別</param>
    /// <param name="address">アドレス</param>
    /// <param name="context">前後行数</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>プログラム行 JSON</returns>
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

    /// <summary>
    /// 関連デバイス取得
    /// </summary>
    /// <param name="dev">デバイス種別</param>
    /// <param name="address">アドレス</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>関連デバイス一覧</returns>
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

    /// <summary>
    /// コメント取得
    /// </summary>
    /// <param name="dev">デバイス種別</param>
    /// <param name="address">アドレス</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>コメント文字列</returns>
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

    /// <summary>
    /// コメント横断検索
    /// </summary>
    private Task<string> SearchCommentsAsync(string question, CancellationToken cancellationToken)
    {
        var call = new ToolCall("search_comment_devices", JsonSerializer.Serialize(new { question }, _serializerOptions));
        EmitRequested(call);

        try
        {
            var results = _commentSearch.Search(question, 10);
            var payload = JsonSerializer.Serialize(new
            {
                status = "success",
                question,
                matchCount = results.Count,
                results = results.Select(r => new
                {
                    device = r.Device,
                    comment = r.Comment,
                    score = Math.Round(r.Score, 2, MidpointRounding.AwayFromZero),
                    matchedTerms = r.MatchedTerms
                })
            }, _serializerOptions);

            EmitCompleted(call, payload, true);
            return Task.FromResult(payload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "search_comment_devices 実行に失敗しました。");
            EmitCompleted(call, ex.Message, false, ex.Message);
            return Task.FromResult(ex.Message);
        }
    }

    /// <summary>
    /// ファンクションブロック一覧取得
    /// </summary>
    private Task<string> ListFunctionBlocksAsync(CancellationToken cancellationToken)
    {
        var call = new ToolCall("list_function_blocks", "{}");
        EmitRequested(call);

        try
        {
            var blocks = _store.FunctionBlocks
                .Select(b => new
                {
                    b.Name,
                    b.SafeName,
                    HasLabel = !string.IsNullOrWhiteSpace(b.LabelContent),
                    HasProgram = !string.IsNullOrWhiteSpace(b.ProgramContent),
                    b.CreatedAt,
                    b.UpdatedAt
                })
                .ToList();

            var payload = JsonSerializer.Serialize(new
            {
                status = "success",
                count = blocks.Count,
                functionBlocks = blocks
            }, _serializerOptions);

            EmitCompleted(call, payload, true);
            return Task.FromResult(payload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "list_function_blocks 実行に失敗しました。");
            EmitCompleted(call, ex.Message, false, ex.Message);
            return Task.FromResult(ex.Message);
        }
    }

    /// <summary>
    /// ファンクションブロック解析
    /// </summary>
    private Task<string> AnalyzeFunctionBlockAsync(string name, CancellationToken cancellationToken)
    {
        var call = new ToolCall("analyze_function_block", JsonSerializer.Serialize(new { name }, _serializerOptions));
        EmitRequested(call);

        try
        {
            if (!_store.TryGetFunctionBlock(name, out var block) || block is null)
            {
                const string notFound = "ファンクションブロックが見つかりません";
                EmitCompleted(call, notFound, false, notFound);
                return Task.FromResult(notFound);
            }

            var labels = ParseLabels(block.LabelContent);
            var program = ParseProgram(block.ProgramContent);
            var payload = JsonSerializer.Serialize(new
            {
                status = "success",
                name = block.Name,
                labels,
                program
            }, _serializerOptions);

            EmitCompleted(call, payload, true);
            return Task.FromResult(payload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "analyze_function_block 実行に失敗しました。");
            EmitCompleted(call, ex.Message, false, ex.Message);
            return Task.FromResult(ex.Message);
        }
    }

    /// <summary>
    /// ファンクションブロック検索
    /// </summary>
    private Task<string> SearchFunctionBlocksAsync(string keyword, CancellationToken cancellationToken)
    {
        var call = new ToolCall("search_function_blocks", JsonSerializer.Serialize(new { keyword }, _serializerOptions));
        EmitRequested(call);

        try
        {
            var matches = _store.FunctionBlocks
                .Where(b => (!string.IsNullOrWhiteSpace(b.Name) && b.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)) ||
                            (!string.IsNullOrWhiteSpace(b.LabelContent) && b.LabelContent.Contains(keyword, StringComparison.OrdinalIgnoreCase)) ||
                            (!string.IsNullOrWhiteSpace(b.ProgramContent) && b.ProgramContent.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                .Select(b => new { b.Name, b.SafeName, b.CreatedAt, b.UpdatedAt })
                .ToList();

            var payload = JsonSerializer.Serialize(new
            {
                status = "success",
                keyword,
                matchCount = matches.Count,
                results = matches
            }, _serializerOptions);

            EmitCompleted(call, payload, true);
            return Task.FromResult(payload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "search_function_blocks 実行に失敗しました。");
            EmitCompleted(call, ex.Message, false, ex.Message);
            return Task.FromResult(ex.Message);
        }
    }

    /// <summary>
    /// エラーコメント付きLコイルをトレース
    /// </summary>
    private Task<string> TraceErrorCoilsAsync(CancellationToken cancellationToken)
    {
        var call = new ToolCall("trace_error_coil", "{}");
        EmitRequested(call);

        try
        {
            var result = _faultTracer.TraceErrorCoils();
            EmitCompleted(call, result, true);
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "trace_error_coil 実行に失敗しました。");
            EmitCompleted(call, ex.Message, false, ex.Message);
            return Task.FromResult(ex.Message);
        }
    }

    private static IReadOnlyCollection<Dictionary<string, string>> ParseLabels(string content)
    {
        var rows = ParseCsv(content);
        if (rows.Count <= 1)
        {
            return Array.Empty<Dictionary<string, string>>();
        }

        var list = new List<Dictionary<string, string>>();
        foreach (var row in rows.Skip(1))
        {
            if (row.Count < 2 || row.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            list.Add(new Dictionary<string, string>
            {
                ["name"] = row.ElementAtOrDefault(0)?.Trim() ?? string.Empty,
                ["type"] = row.ElementAtOrDefault(1)?.Trim() ?? string.Empty,
                ["description"] = row.ElementAtOrDefault(2)?.Trim() ?? string.Empty
            });

            if (list.Count >= 10)
            {
                break;
            }
        }

        return list;
    }

    private static object ParseProgram(string content)
    {
        var rows = ParseCsv(content);
        if (rows.Count <= 1)
        {
            return new { lineCount = 0 };
        }

        var instructions = new List<object>();
        foreach (var row in rows.Skip(1))
        {
            if (row.Count < 2 || row.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            instructions.Add(new
            {
                line = row[0]?.Trim(),
                instruction = string.Join(" ", row.Skip(1).Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()))
            });

            if (instructions.Count >= 20)
            {
                break;
            }
        }

        return new
        {
            lineCount = rows.Count - 1,
            instructions
        };
    }

    private static List<List<string>> ParseCsv(string content)
    {
        var result = new List<List<string>>();
        if (string.IsNullOrWhiteSpace(content))
        {
            return result;
        }

        var delimiter = content.Contains('\t') ? '\t' : ',';
        using var reader = new System.IO.StringReader(content);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var parts = line.Split(delimiter);
            result.Add(parts.ToList());
        }

        return result;
    }

    /// <summary>
    /// 単一デバイス推論
    /// </summary>
    /// <param name="query">質問文</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>推論結果</returns>
    private Task<string> InferDeviceAsync(string query, CancellationToken cancellationToken)
    {
        var call = new ToolCall("reasoning_device", JsonSerializer.Serialize(new { query }, _serializerOptions));
        EmitRequested(call);
        var result = _reasoner.InferSingle(query);
        EmitCompleted(call, result, true);
        return Task.FromResult(result);
    }

    /// <summary>
    /// 複数デバイス推論
    /// </summary>
    /// <param name="query">質問文</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>推論結果</returns>
    private Task<string> InferDevicesAsync(string query, CancellationToken cancellationToken)
    {
        var call = new ToolCall("reasoning_multiple_devices", JsonSerializer.Serialize(new { query }, _serializerOptions));
        EmitRequested(call);
        var programContexts = BuildProgramContexts(query);
        var result = _reasoner.InferMultiple(query, programContexts);
        EmitCompleted(call, result, true);
        return Task.FromResult(result);
    }

    /// <summary>
    /// 単一デバイス読み取り
    /// </summary>
    /// <param name="spec">デバイス指定</param>
    /// <param name="ip">IPアドレス</param>
    /// <param name="port">ポート</param>
    /// <param name="timeoutSeconds">タイムアウト秒</param>
    /// <param name="baseUrl">ゲートウェイURL</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>読み取り結果</returns>
    private async Task<string> ReadValuesAsync(string spec, string ip, int port, int timeoutSeconds, string? baseUrl, CancellationToken cancellationToken)
    {
        var address = NormalizeAddress(DeviceAddress.Parse(spec));
        var normalizedSpec = address.ToSpec();
        var call = new ToolCall("read_plc_values", JsonSerializer.Serialize(new { spec = normalizedSpec, ip, port, timeoutSeconds, baseUrl }, _serializerOptions));
        EmitRequested(call);

        try
        {
            var result = await _gateway.ReadAsync(
                new DeviceReadRequest(
                    normalizedSpec,
                    string.IsNullOrWhiteSpace(ip) ? null : ip,
                    port <= 0 ? null : port,
                    PlcHost: null,
                    Timeout: TimeSpan.FromSeconds(timeoutSeconds > 0 ? timeoutSeconds : 10),
                    BaseUrl: baseUrl),
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

    /// <summary>
    /// 複数デバイス読み取り
    /// </summary>
    /// <param name="specs">デバイス指定一覧</param>
    /// <param name="baseUrl">ゲートウェイURL</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>読み取り結果</returns>
    private async Task<string> ReadMultipleValuesAsync(IEnumerable<string> specs, string? baseUrl, CancellationToken cancellationToken)
    {
        var normalizedSpecs = specs.Select(s => NormalizeAddress(DeviceAddress.Parse(s)).ToSpec()).ToList();
        var call = new ToolCall("read_multiple_plc_values", JsonSerializer.Serialize(new { specs = normalizedSpecs, baseUrl }, _serializerOptions));
        EmitRequested(call);

        try
        {
            var result = await _gateway.ReadBatchAsync(
                new BatchReadRequest(normalizedSpecs, BaseUrl: baseUrl),
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

    /// <summary>
    /// マニュアル検索
    /// </summary>
    /// <param name="query">検索語</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>検索結果</returns>
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

    /// <summary>
    /// 命令検索
    /// </summary>
    /// <param name="instruction">命令名</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>検索結果</returns>
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

    /// <summary>
    /// 命令一覧概要取得
    /// </summary>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>概要文字列</returns>
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

    /// <summary>
    /// 読み取り用にデバイス指定を正規化
    /// </summary>
    /// <param name="address">読み取り対象デバイス</param>
    /// <returns>長さ推論済みデバイス</returns>
    private DeviceAddress NormalizeAddress(DeviceAddress address)
    {
        if (address.Length > 1)
        {
            return address;
        }

        if (!IsWordDevice(address.Device) || !int.TryParse(address.Address, out var parsedAddress))
        {
            return address;
        }

        var dataType = _programAnalyzer.InferDeviceDataType(address.Device, parsedAddress);
        var inferredLength = dataType switch
        {
            DeviceDataType.DoubleWord => 2,
            DeviceDataType.Float => 2,
            _ => address.Length
        };

        return inferredLength != address.Length ? address.WithLength(inferredLength) : address;
    }

    private static bool IsWordDevice(string device)
    {
        return string.Equals(device, "D", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(device, "W", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// ツール開始イベント送出
    /// </summary>
    /// <param name="call">ツール呼び出し</param>
    private void EmitRequested(ToolCall call)
    {
        var ctx = _context.Value;
        ctx?.Emit(AgentEventFactory.ToolRequested(ctx.ConversationId, call));
        ctx?.Emit(AgentEventFactory.ToolStarted(ctx.ConversationId, call));
    }

    /// <summary>
    /// ツール完了イベント送出
    /// </summary>
    /// <param name="call">ツール呼び出し</param>
    /// <param name="result">結果</param>
    /// <param name="success">成功フラグ</param>
    /// <param name="error">エラーメッセージ</param>
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

    private IReadOnlyList<ProgramContext> BuildProgramContexts(string query)
    {
        var contexts = new List<ProgramContext>();
        if (string.IsNullOrWhiteSpace(query) || _store.Programs.Count == 0)
        {
            return contexts;
        }

        foreach (var program in _store.Programs)
        {
            var name = program.Key;
            var baseName = Path.GetFileNameWithoutExtension(name) ?? string.Empty;
            if (ContainsIgnoreCase(query, name) || (!string.IsNullOrWhiteSpace(baseName) && ContainsIgnoreCase(query, baseName)))
            {
                contexts.Add(new ProgramContext(name, program.Value));
            }
        }

        return contexts;
    }

    private static bool ContainsIgnoreCase(string source, string value)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
