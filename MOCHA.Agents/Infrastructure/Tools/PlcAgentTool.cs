using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using MOCHA.Agents.Application;
using MOCHA.Agents.Domain;

namespace MOCHA.Agents.Infrastructure.Tools;

/// <summary>
/// PLC 関連質問に対しゲートウェイ読み取りとマニュアル要約を実行するツール
/// </summary>
public sealed class PlcAgentTool
{
    private readonly IManualStore _manuals;
    private readonly ILogger<PlcAgentTool> _logger;
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// マニュアルストアとロガー注入による初期化
    /// </summary>
    /// <param name="manuals">マニュアルストア</param>
    /// <param name="logger">ロガー</param>
    public PlcAgentTool(IManualStore manuals, ILogger<PlcAgentTool> logger)
    {
        _manuals = manuals;
        _logger = logger;
    }

    /// <summary>
    /// 質問を基に読み取りとマニュアル要約を組み立てる
    /// </summary>
    /// <param name="question">質問文</param>
    /// <param name="optionsJson">読み取りオプション JSON</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>結果文字列</returns>
    public async Task<string> RunAsync(string question, string? optionsJson, CancellationToken cancellationToken)
    {
        var options = ParseOptions(optionsJson);

        var readResult = options.Devices.Any()
            ? await ReadDevicesAsync(options, cancellationToken)
            : null;

        var manualSummary = await SummarizeManualAsync(question, cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("## PLC解析結果");
        sb.AppendLine($"- 質問: {question}");

        if (readResult is not null)
        {
            sb.AppendLine();
            sb.AppendLine("### ゲートウェイ読み取り結果");
            sb.AppendLine(readResult);
        }
        else
        {
            sb.AppendLine();
            sb.AppendLine("### ゲートウェイ読み取り");
            sb.AppendLine("デバイス指定が無いためスキップしました。`optionsJson` に `devices` 配列を指定してください（例: [\"D100\", \"M10:2\"]）。");
        }

        if (!string.IsNullOrWhiteSpace(manualSummary))
        {
            sb.AppendLine();
            sb.AppendLine("### マニュアル候補");
            sb.AppendLine(manualSummary);
        }

        return sb.ToString();
    }

    /// <summary>
    /// JSON オプション解析
    /// </summary>
    /// <param name="optionsJson">オプション JSON</param>
    /// <returns>解析結果</returns>
    private PlcAgentOptions ParseOptions(string? optionsJson)
    {
        if (string.IsNullOrWhiteSpace(optionsJson))
        {
            return new PlcAgentOptions();
        }

        try
        {
            return JsonSerializer.Deserialize<PlcAgentOptions>(optionsJson, _serializerOptions) ?? new PlcAgentOptions();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "plc_agent options の JSON 解析に失敗しました。既定値で継続します。");
            return new PlcAgentOptions();
        }
    }

    /// <summary>
    /// マニュアル検索と要約生成
    /// </summary>
    /// <param name="question">質問文</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>要約</returns>
    private async Task<string> SummarizeManualAsync(string question, CancellationToken cancellationToken)
    {
        try
        {
            // MXia でのエージェント名に合わせて "plcAgent" を指定
            var hits = await _manuals.SearchAsync("plcAgent", question, cancellationToken);
            if (hits is null || hits.Count == 0)
            {
                return "関連するマニュアル候補は見つかりませんでした。";
            }

            var top = hits.Take(3).ToList();
            var lines = new List<string>();
            foreach (var hit in top)
            {
                var preview = await _manuals.ReadAsync("plcAgent", hit.RelativePath, maxBytes: 400, cancellationToken: cancellationToken);
                lines.Add($"- {hit.Title} (score: {hit.Score:0.00})\n  - path: {hit.RelativePath}\n  - preview: {(preview?.Content ?? string.Empty).ReplaceLineEndings(" ").Trim()}");
            }

            return string.Join("\n", lines);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "マニュアル検索に失敗しました。");
            return $"マニュアル検索でエラーが発生しました: {ex.Message}";
        }
    }

    /// <summary>
    /// PLC ゲートウェイへの読み取り実行
    /// </summary>
    /// <param name="options">読み取りオプション</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>読み取り結果</returns>
    private async Task<string> ReadDevicesAsync(PlcAgentOptions options, CancellationToken cancellationToken)
    {
        try
        {
            var baseUrl = options.BaseUrl ?? "http://localhost:8000";
            using var http = new HttpClient
            {
                BaseAddress = new Uri(baseUrl),
                Timeout = options.Timeout ?? TimeSpan.FromSeconds(10)
            };

            // バッチ読み取り: devices の配列をそのまま投げる
            if (options.Devices.Count > 1)
            {
                var payload = new
                {
                    devices = options.Devices,
                    ip = options.Ip,
                    port = options.Port
                };

                var response = await http.PostAsJsonAsync("api/batch_read", payload, cancellationToken);
                response.EnsureSuccessStatusCode();

                var body = await response.Content.ReadFromJsonAsync<BatchReadResponse>(cancellationToken: cancellationToken);
                if (body?.Results is null)
                {
                    return "バッチ読み取りの応答が空でした。";
                }

                var lines = body.Results.Select(r =>
                    $"- {r.Device ?? "(unknown)"} => {(r.Values is not null && r.Values.Count > 0 ? string.Join(", ", r.Values) : "(no values)")}" +
                    $"{(r.Success ?? false ? string.Empty : $" [error: {r.Error}]")}");

                return string.Join("\n", lines);
            }

            // 単一読み取り
            var (device, addr, length) = ParseDevice(options.Devices.First());
            var singlePayload = new
            {
                device,
                addr,
                length,
                ip = options.Ip,
                port = options.Port
            };

            var singleResponse = await http.PostAsJsonAsync("api/read", singlePayload, cancellationToken);
            singleResponse.EnsureSuccessStatusCode();

            var singleBody = await singleResponse.Content.ReadFromJsonAsync<ReadResponse>(cancellationToken: cancellationToken);
            if (singleBody is null)
            {
                return "単一読み取りの応答が空でした。";
            }

            var values = singleBody.Values is not null && singleBody.Values.Count > 0
                ? string.Join(", ", singleBody.Values)
                : "(no values)";

            return $"{device}{addr} => {values}" + (singleBody.Success ?? true ? string.Empty : $" [error: {singleBody.Error}]");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PLC Gateway への読み取りに失敗しました。");
            return $"ゲートウェイ読み取りでエラー: {ex.Message}";
        }
    }

    /// <summary>
    /// デバイス指定の解析
    /// </summary>
    /// <param name="spec">デバイス指定文字列</param>
    /// <returns>デバイス・アドレス・長さ</returns>
    private static (string Device, int Address, int Length) ParseDevice(string spec)
    {
        var span = spec.AsSpan();
        var colon = span.IndexOf(':');
        var length = 1;
        if (colon >= 0 && int.TryParse(span[(colon + 1)..], out var parsedLength))
        {
            length = parsedLength;
            span = span[..colon];
        }

        if (span.Length < 2)
        {
            return ("D", 0, length);
        }

        var device = span[..1].ToString();
        return int.TryParse(span[1..], out var address)
            ? (device, address, length)
            : (device, 0, length);
    }

    private sealed record PlcAgentOptions
    {
        public string? BaseUrl { get; init; }
        public string? Ip { get; init; }
        public int? Port { get; init; }
        public TimeSpan? Timeout { get; init; }
        public IReadOnlyList<string> Devices { get; init; } = Array.Empty<string>();
    }

    private sealed record ReadResponse(
        [property: JsonPropertyName("values")] IReadOnlyList<int>? Values,
        [property: JsonPropertyName("success")] bool? Success,
        [property: JsonPropertyName("error")] string? Error);

    private sealed record BatchReadResponse(
        [property: JsonPropertyName("results")] IReadOnlyList<BatchReadItem>? Results,
        [property: JsonPropertyName("error")] string? Error);

    private sealed record BatchReadItem(
        [property: JsonPropertyName("device")] string? Device,
        [property: JsonPropertyName("values")] IReadOnlyList<int>? Values,
        [property: JsonPropertyName("success")] bool? Success,
        [property: JsonPropertyName("error")] string? Error);
}
