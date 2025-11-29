using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using MOCHA.Agents.Application;
using MOCHA.Models.Chat;
using MOCHA.Services.Plc;

namespace MOCHA.Services.Chat;

/// <summary>
/// エージェントとのやり取りと PLC Gateway 呼び出しを仲介するサービス。
/// 実際の接続先がなくてもフェイク実装で動作をテストできるように抽象化している。
/// </summary>
internal sealed class ChatOrchestrator : IChatOrchestrator
{
    private readonly IAgentChatClient _agentChatClient;
    private readonly IPlcGatewayClient _plcGateway;
    private readonly IChatRepository _chatRepository;
    private readonly ConversationHistoryState _history;
    private readonly IManualStore _manualStore;
    private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// 依存するクライアントと状態管理を受け取って初期化する。
    /// </summary>
    /// <param name="agentChatClient">エージェント クライアント。</param>
    /// <param name="plcGateway">PLC Gateway クライアント。</param>
    /// <param name="chatRepository">チャットリポジトリ。</param>
    /// <param name="history">会話履歴状態。</param>
    /// <param name="manualStore">マニュアルストア。</param>
    public ChatOrchestrator(
        IAgentChatClient agentChatClient,
        IPlcGatewayClient plcGateway,
        IChatRepository chatRepository,
        ConversationHistoryState history,
        IManualStore manualStore)
    {
        _agentChatClient = agentChatClient;
        _plcGateway = plcGateway;
        _chatRepository = chatRepository;
        _history = history;
        _manualStore = manualStore;
    }

    /// <summary>
    /// ユーザーの発話をエージェントに送り、ツール要求や応答をストリームで返す。
    /// </summary>
    /// <param name="user">ユーザー情報。</param>
    /// <param name="conversationId">既存の会話ID。未指定なら新規を生成。</param>
    /// <param name="text">発話内容。</param>
    /// <param name="agentNumber">装置エージェント番号。</param>
    /// <param name="cancellationToken">キャンセル通知。</param>
    /// <returns>発生するチャットイベントの列。</returns>
    public async IAsyncEnumerable<ChatStreamEvent> HandleUserMessageAsync(
        UserContext user,
        string? conversationId,
        string text,
        string? agentNumber,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var convId = string.IsNullOrWhiteSpace(conversationId)
            ? Guid.NewGuid().ToString("N")
            : conversationId;

        await _history.UpsertAsync(user.UserId, convId, text, agentNumber, cancellationToken);
        await _chatRepository.AddMessageAsync(user.UserId, convId, new ChatMessage(ChatRole.User, text), agentNumber, cancellationToken);

        var turn = new ChatTurn(convId, new List<ChatMessage>
        {
            new(ChatRole.User, text)
        });

        var stream = await _agentChatClient.SendAsync(turn, cancellationToken);
        await foreach (var ev in stream.WithCancellation(cancellationToken))
        {
            if (ev.Type == ChatStreamEventType.ActionRequest && ev.ActionRequest is not null)
            {
                var actionRequest = ev.ActionRequest with { ConversationId = convId };

                await SaveMessageAsync(
                    user,
                    convId,
                    new ChatMessage(ChatRole.Tool, $"[action] {actionRequest.ActionName}: {JsonSerializer.Serialize(actionRequest.Payload, _serializerOptions)}"),
                    agentNumber,
                    cancellationToken);

                yield return ev; // UI に「ツール実行開始」を通知

                var actionResult = await ExecuteActionAsync(actionRequest, cancellationToken);

                await SaveMessageAsync(
                    user,
                    convId,
                    new ChatMessage(ChatRole.Tool, $"[result] {actionResult.ActionName}: {JsonSerializer.Serialize(actionResult.Payload, _serializerOptions)}"),
                    agentNumber,
                    cancellationToken);

                yield return new ChatStreamEvent(
                    ChatStreamEventType.ToolResult,
                    ActionResult: actionResult);

                await _agentChatClient.SubmitActionResultAsync(actionResult, cancellationToken);
            }
            else if (ev.Type == ChatStreamEventType.ToolResult && ev.ActionResult is not null)
            {
                var actionResult = ev.ActionResult with { ConversationId = ev.ActionResult.ConversationId ?? convId };
                await SaveMessageAsync(
                    user,
                    convId,
                    new ChatMessage(ChatRole.Tool, $"[result] {actionResult.ActionName}: {JsonSerializer.Serialize(actionResult.Payload, _serializerOptions)}"),
                    agentNumber,
                    cancellationToken);

                yield return new ChatStreamEvent(
                    ChatStreamEventType.ToolResult,
                    ActionResult: actionResult);
            }
            else
            {
                if (ev.Message is not null)
                {
                    yield return ChatStreamEvent.FromMessage(
                        await SaveMessageAsync(user, convId, ev.Message, agentNumber, cancellationToken));
                }
                else
                {
                    yield return ev;
                }
            }
        }
    }

    /// <summary>
    /// アクション名に応じて適切な処理を実行する。
    /// </summary>
    /// <param name="request">アクション要求。</param>
    /// <param name="cancellationToken">キャンセル通知。</param>
    /// <returns>アクション結果。</returns>
    private async Task<AgentActionResult> ExecuteActionAsync(AgentActionRequest request, CancellationToken cancellationToken)
    {
        switch (request.ActionName)
        {
            case "read_device":
                return await HandleReadDeviceAsync(request, cancellationToken);
            case "batch_read_devices":
                return await HandleBatchReadAsync(request, cancellationToken);
            case "find_manuals":
                return await HandleFindManualsAsync(request, cancellationToken);
            case "read_manual":
                return await HandleReadManualAsync(request, cancellationToken);
            default:
                return new AgentActionResult(
                    request.ActionName,
                    request.ConversationId,
                    false,
                    new Dictionary<string, object?>(),
                    $"unsupported action: {request.ActionName}");
        }
    }

    /// <summary>
    /// 単一デバイス読み取りアクションを実行し、結果ペイロードを構築する。
    /// </summary>
    /// <param name="request">アクション要求。</param>
    /// <param name="cancellationToken">キャンセル通知。</param>
    /// <returns>アクション結果。</returns>
    private async Task<AgentActionResult> HandleReadDeviceAsync(AgentActionRequest request, CancellationToken cancellationToken)
    {
        var payload = request.Payload;
        var device = ReadString(payload, "device") ?? "D";
        var addr = ReadInt(payload, "addr") ?? 0;
        var length = ReadInt(payload, "length") ?? 1;
        var host = ReadString(payload, "plc_host") ?? ReadString(payload, "ip");
        var port = ReadInt(payload, "port");

        var result = await _plcGateway.ReadAsync(new PlcReadRequest(device, addr, length, host, port), cancellationToken);

        var responsePayload = new Dictionary<string, object?>
        {
            ["device"] = device,
            ["addr"] = addr,
            ["length"] = length,
            ["values"] = result.Values,
            ["success"] = result.Success
        };

        return new AgentActionResult(
            request.ActionName,
            request.ConversationId,
            result.Success,
            responsePayload,
            result.Error);
    }

    /// <summary>
    /// 複数デバイスの一括読み取りアクションを実行する。
    /// </summary>
    /// <param name="request">アクション要求。</param>
    /// <param name="cancellationToken">キャンセル通知。</param>
    /// <returns>アクション結果。</returns>
    private async Task<AgentActionResult> HandleBatchReadAsync(AgentActionRequest request, CancellationToken cancellationToken)
    {
        var payload = request.Payload;
        var devices = ReadStringList(payload, "devices") ?? new List<string>();
        var host = ReadString(payload, "plc_host") ?? ReadString(payload, "ip");
        var port = ReadInt(payload, "port");

        var result = await _plcGateway.BatchReadAsync(new PlcBatchReadRequest(devices, host, port), cancellationToken);

        var responsePayload = new Dictionary<string, object?>
        {
            ["devices"] = devices,
            ["results"] = result.Results.Select(x => new
            {
                x.Device,
                x.Values,
                x.Success,
                x.Error
            }).ToList(),
            ["success"] = result.Success
        };

        return new AgentActionResult(
            request.ActionName,
            request.ConversationId,
            result.Success,
            responsePayload,
            result.Error);
    }

    /// <summary>
    /// マニュアル検索アクションを実行する。
    /// </summary>
    private async Task<AgentActionResult> HandleFindManualsAsync(AgentActionRequest request, CancellationToken cancellationToken)
    {
        var payload = request.Payload;
        var agentName = ReadString(payload, "agentName") ?? "iaiAgent";
        var query = ReadString(payload, "query") ?? string.Empty;

        try
        {
            var hits = await _manualStore.SearchAsync(agentName, query, cancellationToken);
            var responsePayload = new Dictionary<string, object?>
            {
                ["agentName"] = agentName,
                ["query"] = query,
                ["hits"] = hits.Select(h => new { h.Title, h.RelativePath, h.Score }).ToList()
            };

            var success = responsePayload["hits"] is List<object?> list && list.Count > 0;
            return new AgentActionResult(
                request.ActionName,
                request.ConversationId,
                success,
                responsePayload,
                success ? null : "no manual hits");
        }
        catch (Exception ex)
        {
            return new AgentActionResult(
                request.ActionName,
                request.ConversationId,
                false,
                new Dictionary<string, object?>
                {
                    ["agentName"] = agentName,
                    ["query"] = query
                },
                ex.Message);
        }
    }

    /// <summary>
    /// マニュアル読取アクションを実行する。
    /// </summary>
    private async Task<AgentActionResult> HandleReadManualAsync(AgentActionRequest request, CancellationToken cancellationToken)
    {
        var payload = request.Payload;
        var agentName = ReadString(payload, "agentName") ?? "iaiAgent";
        var relativePath = ReadString(payload, "relativePath") ?? string.Empty;

        try
        {
            var content = await _manualStore.ReadAsync(agentName, relativePath, cancellationToken: cancellationToken);
            var success = content is not null;

            var responsePayload = new Dictionary<string, object?>
            {
                ["agentName"] = agentName,
                ["relativePath"] = relativePath,
                ["content"] = content?.Content,
                ["length"] = content?.Length
            };

            return new AgentActionResult(
                request.ActionName,
                request.ConversationId,
                success,
                responsePayload,
                success ? null : "manual not found");
        }
        catch (Exception ex)
        {
            return new AgentActionResult(
                request.ActionName,
                request.ConversationId,
                false,
                new Dictionary<string, object?>
                {
                    ["agentName"] = agentName,
                    ["relativePath"] = relativePath
                },
                ex.Message);
        }
    }

    /// <summary>
    /// ペイロードから文字列値を取り出す。
    /// </summary>
    /// <param name="dict">ペイロード辞書。</param>
    /// <param name="key">抽出するキー。</param>
    /// <returns>取得した文字列。存在しない場合は null。</returns>
    private static string? ReadString(IReadOnlyDictionary<string, object?> dict, string key)
    {
        if (!dict.TryGetValue(key, out var value) || value is null) return null;
        return value switch
        {
            JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.String => jsonElement.GetString(),
            _ => value.ToString()
        };
    }

    /// <summary>
    /// ペイロードから整数値を取り出す。
    /// </summary>
    /// <param name="dict">ペイロード辞書。</param>
    /// <param name="key">抽出するキー。</param>
    /// <returns>取得した整数。存在しない場合は null。</returns>
    private static int? ReadInt(IReadOnlyDictionary<string, object?> dict, string key)
    {
        if (!dict.TryGetValue(key, out var value) || value is null) return null;
        return value switch
        {
            int i => i,
            JsonElement json when json.ValueKind == JsonValueKind.Number && json.TryGetInt32(out var j) => j,
            _ when int.TryParse(value.ToString(), out var k) => k,
            _ => null
        };
    }

    /// <summary>
    /// ペイロードから文字列リストを取り出す。
    /// </summary>
    /// <param name="dict">ペイロード辞書。</param>
    /// <param name="key">抽出するキー。</param>
    /// <returns>取得した文字列リスト。存在しない場合は null。</returns>
    private static List<string>? ReadStringList(IReadOnlyDictionary<string, object?> dict, string key)
    {
        if (!dict.TryGetValue(key, out var value) || value is null) return null;

        if (value is JsonElement json && json.ValueKind == JsonValueKind.Array)
        {
            var list = new List<string>();
            foreach (var item in json.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String && item.GetString() is { } s)
                {
                    list.Add(s);
                }
            }
            return list;
        }

        if (value is IEnumerable<string> enumerable)
        {
            return enumerable.ToList();
        }

        var asString = value.ToString();
        if (string.IsNullOrWhiteSpace(asString))
        {
            return null;
        }

        return asString
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    /// <summary>
    /// メッセージをリポジトリと履歴状態に保存する。
    /// </summary>
    /// <param name="user">ユーザー情報。</param>
    /// <param name="conversationId">会話ID。</param>
    /// <param name="message">保存するメッセージ。</param>
    /// <param name="agentNumber">装置エージェント番号。</param>
    /// <param name="cancellationToken">キャンセル通知。</param>
    /// <returns>保存したメッセージ。</returns>
    private async Task<ChatMessage> SaveMessageAsync(UserContext user, string conversationId, ChatMessage message, string? agentNumber, CancellationToken cancellationToken)
    {
        await _chatRepository.AddMessageAsync(user.UserId, conversationId, message, agentNumber, cancellationToken);
        await _history.UpsertAsync(user.UserId, conversationId, message.Content, agentNumber, cancellationToken);
        return message;
    }
}
