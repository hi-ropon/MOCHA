using System.Runtime.CompilerServices;
using System.Text.Json;
using MOCHA.Models.Chat;
using MOCHA.Services.Copilot;
using MOCHA.Services.Plc;

namespace MOCHA.Services.Chat;

/// <summary>
/// Copilot とのやり取りと PLC Gateway 呼び出しを仲介するサービス。
/// 実際の接続先がなくてもフェイク実装で動作をテストできるように抽象化している。
/// </summary>
internal sealed class ChatOrchestrator : IChatOrchestrator
{
    private readonly ICopilotChatClient _copilot;
    private readonly IPlcGatewayClient _plcGateway;
    private readonly IChatRepository _chatRepository;
    private readonly ConversationHistoryState _history;

    /// <summary>
    /// 依存するクライアントと状態管理を受け取って初期化する。
    /// </summary>
    /// <param name="copilot">Copilot クライアント。</param>
    /// <param name="plcGateway">PLC Gateway クライアント。</param>
    /// <param name="chatRepository">チャットリポジトリ。</param>
    /// <param name="history">会話履歴状態。</param>
    public ChatOrchestrator(
        ICopilotChatClient copilot,
        IPlcGatewayClient plcGateway,
        IChatRepository chatRepository,
        ConversationHistoryState history)
    {
        _copilot = copilot;
        _plcGateway = plcGateway;
        _chatRepository = chatRepository;
        _history = history;
    }

    /// <summary>
    /// ユーザーの発話を Copilot に送り、ツール要求や応答をストリームで返す。
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

        var stream = await _copilot.SendAsync(turn, cancellationToken);
        await foreach (var ev in stream.WithCancellation(cancellationToken))
        {
            if (ev.Type == ChatStreamEventType.ActionRequest && ev.ActionRequest is not null)
            {
                var actionRequest = ev.ActionRequest with { ConversationId = convId };

                yield return ev; // UI に「ツール実行開始」を通知

                var actionResult = await ExecuteActionAsync(actionRequest, cancellationToken);

                yield return new ChatStreamEvent(
                    ChatStreamEventType.ToolResult,
                    ActionResult: actionResult);

                await _copilot.SubmitActionResultAsync(actionResult, cancellationToken);

                var device = ReadString(actionResult.Payload, "device") ?? "D";
                var addr = ReadInt(actionResult.Payload, "addr") ?? 0;
                var values = ReadValues(actionResult.Payload, "values");

                // ツール結果の簡易表示
                yield return ChatStreamEvent.FromMessage(
                    await SaveMessageAsync(user, convId, new ChatMessage(ChatRole.Assistant, BuildActionResultText(actionResult, device, addr, values)), agentNumber, cancellationToken));

                // Copilot Studio に渡したことを示すフェイクメッセージ
                yield return ChatStreamEvent.FromMessage(
                    await SaveMessageAsync(user, convId, new ChatMessage(ChatRole.Assistant, BuildActionSubmitText(device, addr, values)), agentNumber, cancellationToken));

                // Copilot Studio が最終回答した想定のフェイクメッセージ
                yield return ChatStreamEvent.FromMessage(
                    await SaveMessageAsync(user, convId, new ChatMessage(ChatRole.Assistant, BuildCopilotReplyText(device, addr, values, actionResult.Success, actionResult.Error)), agentNumber, cancellationToken));
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
    private async Task<CopilotActionResult> ExecuteActionAsync(CopilotActionRequest request, CancellationToken cancellationToken)
    {
        switch (request.ActionName)
        {
            case "read_device":
                return await HandleReadDeviceAsync(request, cancellationToken);
            case "batch_read_devices":
                return await HandleBatchReadAsync(request, cancellationToken);
            default:
                return new CopilotActionResult(
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
    private async Task<CopilotActionResult> HandleReadDeviceAsync(CopilotActionRequest request, CancellationToken cancellationToken)
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

        return new CopilotActionResult(
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
    private async Task<CopilotActionResult> HandleBatchReadAsync(CopilotActionRequest request, CancellationToken cancellationToken)
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

        return new CopilotActionResult(
            request.ActionName,
            request.ConversationId,
            result.Success,
            responsePayload,
            result.Error);
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
    /// 読み取り結果を UI 向けの文面に整形する。
    /// </summary>
    /// <param name="result">アクション結果。</param>
    /// <param name="device">デバイス種別。</param>
    /// <param name="addr">アドレス。</param>
    /// <param name="values">取得値。</param>
    /// <returns>表示用メッセージ。</returns>
    private static string BuildActionResultText(CopilotActionResult result, string device, int addr, List<int> values)
    {
        if (result.Success)
        {
            var valuesText = values.Any() ? string.Join(", ", values) : "(no values)";
            return $"(fake) {device}{addr} の読み取り結果: {valuesText}";
        }

        var error = result.Error ?? "unknown error";
        return $"(fake) {device}{addr} の読み取りに失敗しました: {error}";
    }

    /// <summary>
    /// ペイロードから整数リストを取り出す。型が合わない場合は空リストを返す。
    /// </summary>
    /// <param name="dict">ペイロード辞書。</param>
    /// <param name="key">抽出するキー。</param>
    /// <returns>取得した整数リスト。</returns>
    private static List<int> ReadValues(IReadOnlyDictionary<string, object?> dict, string key)
    {
        if (!dict.TryGetValue(key, out var value) || value is null) return new List<int>();

        if (value is IEnumerable<int> enumerable)
        {
            return enumerable.ToList();
        }

        if (value is JsonElement json && json.ValueKind == JsonValueKind.Array)
        {
            var list = new List<int>();
            foreach (var item in json.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out var n))
                {
                    list.Add(n);
                }
            }
            return list;
        }

        return new List<int>();
    }

    /// <summary>
    /// Copilot Studio へ送信した旨の簡易メッセージを生成する。
    /// </summary>
    /// <param name="device">デバイス種別。</param>
    /// <param name="addr">アドレス。</param>
    /// <param name="values">送信値。</param>
    /// <returns>表示用メッセージ。</returns>
    private static string BuildActionSubmitText(string device, int addr, List<int> values)
    {
        var valuesText = values.Any() ? string.Join(", ", values) : "(no values)";
        return $"(fake) Copilot Studio に送信: {device}{addr} -> [{valuesText}]";
    }

    /// <summary>
    /// Copilot から返ってきたと想定したメッセージを生成する。
    /// </summary>
    /// <param name="device">デバイス種別。</param>
    /// <param name="addr">アドレス。</param>
    /// <param name="values">取得値。</param>
    /// <param name="success">成功フラグ。</param>
    /// <param name="error">エラー内容。</param>
    /// <returns>表示用メッセージ。</returns>
    private static string BuildCopilotReplyText(string device, int addr, List<int> values, bool success, string? error)
    {
        if (success && values.Any())
        {
            return $"(fake Copilot) {device}{addr} は {string.Join(", ", values)} でした。";
        }

        if (success)
        {
            return $"(fake Copilot) {device}{addr} の値は空でした。";
        }

        var err = error ?? "unknown error";
        return $"(fake Copilot) {device}{addr} の読み取りに失敗しました: {err}";
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
