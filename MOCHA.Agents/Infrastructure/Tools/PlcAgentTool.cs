using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MOCHA.Agents.Domain.Plc;

namespace MOCHA.Agents.Infrastructure.Tools;

/// <summary>
/// PLC 関連質問に対しゲートウェイ読み取りを実行するツール
/// </summary>
public sealed class PlcAgentTool
{
    private readonly ILogger<PlcAgentTool> _logger;
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// ロガー注入による初期化
    /// </summary>
    /// <param name="logger">ロガー</param>
    public PlcAgentTool(ILogger<PlcAgentTool> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 質問を基にゲートウェイ読み取り結果を組み立てる
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

        return sb.ToString();
    }

    /// <summary>
    /// ゲートウェイ読み取りのみ実行
    /// </summary>
    /// <param name="optionsJson">読み取りオプション JSON</param>
    /// <param name="cancellationToken">キャンセル通知</param>
    /// <returns>読み取り結果</returns>
    public Task<string> RunGatewayAsync(string? optionsJson, CancellationToken cancellationToken)
    {
        return RunAsync("PLC Gateway 読み取り", optionsJson, cancellationToken);
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
                Timeout = options.TimeoutSeconds is > 0
                    ? TimeSpan.FromSeconds(options.TimeoutSeconds.Value)
                    : TimeSpan.FromSeconds(10)
            };

            // バッチ読み取り: devices の配列をそのまま投げる
            if (options.Devices.Count > 1)
            {
                var specs = options.Devices.Select(d => DeviceAddress.Parse(d).ToSpec()).ToList();
                var payload = new
                {
                    devices = specs,
                    ip = options.Ip,
                    port = options.Port,
                    transport = options.Transport,
                    plc_host = options.PlcHost
                };

                _logger.LogInformation("PLC Gateway バッチ読み取りリクエスト: POST api/batch_read payload={Payload}", JsonSerializer.Serialize(payload));
                var response = await http.PostAsJsonAsync("api/batch_read", payload, _serializerOptions, cancellationToken);
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
            var address = DeviceAddress.Parse(options.Devices.First());
            var path = $"api/read/{address.Device}/{Uri.EscapeDataString(address.Address)}/{address.Length}";
            var query = new List<string>();
            if (!string.IsNullOrWhiteSpace(options.PlcHost))
            {
                query.Add($"plc_host={Uri.EscapeDataString(options.PlcHost)}");
            }

            if (!string.IsNullOrWhiteSpace(options.Transport))
            {
                query.Add($"transport={Uri.EscapeDataString(options.Transport)}");
            }

            if (!string.IsNullOrWhiteSpace(options.Ip))
            {
                query.Add($"ip={Uri.EscapeDataString(options.Ip)}");
            }

            if (options.Port is not null)
            {
                query.Add($"port={options.Port.Value}");
            }

            if (query.Count > 0)
            {
                path += $"?{string.Join("&", query)}";
            }

            _logger.LogInformation("PLC Gateway 単体読み取りリクエスト: GET {Path}", path);
            var singleResponse = await http.GetAsync(path, cancellationToken);
            singleResponse.EnsureSuccessStatusCode();

            var singleBody = await singleResponse.Content.ReadFromJsonAsync<ReadResponse>(cancellationToken: cancellationToken);
            if (singleBody is null)
            {
                return "単一読み取りの応答が空でした。";
            }

            var values = singleBody.Values is not null && singleBody.Values.Count > 0
                ? string.Join(", ", singleBody.Values)
                : "(no values)";

            return $"{address.Display} => {values}" + (singleBody.Success ?? true ? string.Empty : $" [error: {singleBody.Error}]");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PLC Gateway への読み取りに失敗しました。");
            return $"ゲートウェイ読み取りでエラー: {ex.Message}";
        }
    }

    private sealed record PlcAgentOptions
    {
        public string? BaseUrl { get; init; }
        public string? Ip { get; init; }
        public int? Port { get; init; }
        [JsonPropertyName("plc_host")]
        public string? PlcHost { get; init; }
        [JsonPropertyName("transport")]
        public string? Transport { get; init; }
        [JsonPropertyName("timeout")]
        public double? TimeoutSeconds { get; init; }
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
