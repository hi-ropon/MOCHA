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
public sealed class ChatOrchestrator : IChatOrchestrator
{
    private readonly ICopilotChatClient _copilot;
    private readonly IPlcGatewayClient _plcGateway;

    public ChatOrchestrator(ICopilotChatClient copilot, IPlcGatewayClient plcGateway)
    {
        _copilot = copilot;
        _plcGateway = plcGateway;
    }

    public async IAsyncEnumerable<ChatStreamEvent> HandleUserMessageAsync(
        UserContext user,
        string? conversationId,
        string text,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var turn = new ChatTurn(conversationId, new List<ChatMessage>
        {
            new(ChatRole.User, text)
        });

        var stream = await _copilot.SendAsync(turn, cancellationToken);
        await foreach (var ev in stream.WithCancellation(cancellationToken))
        {
            if (ev.Type == ChatStreamEventType.ActionRequest && ev.ActionRequest is not null)
            {
                yield return ev; // UI に「ツール実行開始」を通知

                var actionResult = await ExecuteActionAsync(ev.ActionRequest, cancellationToken);

                yield return new ChatStreamEvent(
                    ChatStreamEventType.ToolResult,
                    ActionResult: actionResult);

                await _copilot.SubmitActionResultAsync(actionResult, cancellationToken);

                yield return ChatStreamEvent.FromMessage(
                    new ChatMessage(ChatRole.Assistant, BuildActionResultText(actionResult)));
            }
            else
            {
                yield return ev;
            }
        }
    }

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

    private static string? ReadString(IReadOnlyDictionary<string, object?> dict, string key)
    {
        if (!dict.TryGetValue(key, out var value) || value is null) return null;
        return value switch
        {
            JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.String => jsonElement.GetString(),
            _ => value.ToString()
        };
    }

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

    private static string BuildActionResultText(CopilotActionResult result)
    {
        var device = ReadString(result.Payload, "device") ?? "D";
        var addr = ReadInt(result.Payload, "addr") ?? 0;
        var values = ReadValues(result.Payload, "values");

        if (result.Success)
        {
            var valuesText = values.Any() ? string.Join(", ", values) : "(no values)";
            return $"(fake) {device}{addr} の読み取り結果: {valuesText}";
        }

        var error = result.Error ?? "unknown error";
        return $"(fake) {device}{addr} の読み取りに失敗しました: {error}";
    }

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
}
