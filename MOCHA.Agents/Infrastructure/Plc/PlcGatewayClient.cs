using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MOCHA.Agents.Domain.Plc;

namespace MOCHA.Agents.Infrastructure.Plc;

/// <summary>
/// PLCゲートウェイとのHTTP通信を行うクライアント
/// </summary>
public sealed class PlcGatewayClient : IPlcGatewayClient
{
    private readonly HttpClient _http;
    private readonly ILogger<PlcGatewayClient> _logger;
    private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public PlcGatewayClient(HttpClient http, ILogger<PlcGatewayClient> logger)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<DeviceReadResult> ReadAsync(DeviceReadRequest request, CancellationToken cancellationToken = default)
    {
        var address = DeviceAddress.Parse(request.Spec);

        try
        {
            var uri = BuildUri(request.BaseUrl, $"api/read/{address.Device}/{Uri.EscapeDataString(address.Address)}/{address.Length}");
            if (!string.IsNullOrWhiteSpace(request.Ip) || request.Port is not null || !string.IsNullOrWhiteSpace(request.Transport))
            {
                var query = new List<string>();
                if (!string.IsNullOrWhiteSpace(request.Ip))
                {
                    query.Add($"ip={Uri.EscapeDataString(request.Ip)}");
                }

                if (request.Port is not null)
                {
                    query.Add($"port={request.Port.Value}");
                }

                if (!string.IsNullOrWhiteSpace(request.Transport))
                {
                    query.Add($"transport={Uri.EscapeDataString(request.Transport)}");
                }

                var builder = new UriBuilder(uri) { Query = string.Join("&", query) };
                uri = builder.Uri;
            }

            _logger.LogInformation("PLC Gateway 読み取りリクエスト: GET {Uri}", uri);

            using var response = await _http.GetAsync(uri, cancellationToken);

            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<GatewayReadResponse>(cancellationToken: cancellationToken);
            if (body is null)
            {
                return new DeviceReadResult(address.Display, null, false, "空の応答を受信しました");
            }

            return new DeviceReadResult(address.Display, body.Values ?? Array.Empty<int>(), body.Success ?? true, body.Error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PLC Gateway 読み取りに失敗しました。");
            return new DeviceReadResult(address.Display, null, false, ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<BatchReadResult> ReadBatchAsync(BatchReadRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Specs is null || request.Specs.Count == 0)
        {
            return new BatchReadResult(Array.Empty<DeviceReadResult>(), "devices が指定されていません");
        }

        try
        {
            var uri = BuildUri(request.BaseUrl, "api/batch_read");
            var specs = request.Specs.Select(s => DeviceAddress.Parse(s).ToSpec()).ToList();
            var payload = new GatewayBatchRequest(specs, request.Ip, request.Port, request.Transport);

            _logger.LogInformation("PLC Gateway バッチ読み取りリクエスト: POST {Uri} payload={Payload}", uri, JsonSerializer.Serialize(payload, _serializerOptions));

            using var response = await _http.PostAsJsonAsync(
                uri,
                payload,
                _serializerOptions,
                cancellationToken);

            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<GatewayBatchResponse>(cancellationToken: cancellationToken);
            if (body?.Results is null)
            {
                return new BatchReadResult(Array.Empty<DeviceReadResult>(), "バッチ読み取りの応答が空でした");
            }

            var results = body.Results.Select(r =>
                new DeviceReadResult(
                    r.Device ?? string.Empty,
                    r.Values ?? Array.Empty<int>(),
                    r.Success ?? false,
                    r.Error)).ToList();

            return new BatchReadResult(results, body.Error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PLC Gateway バッチ読み取りに失敗しました。");
            return new BatchReadResult(Array.Empty<DeviceReadResult>(), ex.Message);
        }
    }

    private Uri BuildUri(string? baseUrl, string path)
    {
        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            var normalized = NormalizeBaseUrl(baseUrl);
            return new Uri(new Uri(normalized, UriKind.Absolute), path);
        }

        if (_http.BaseAddress is not null)
        {
            return new Uri(_http.BaseAddress, path);
        }

        return new Uri(path, UriKind.Relative);
    }

    private sealed record GatewayReadResponse(
        int[]? Values,
        bool? Success,
        string? Error);

    private sealed record GatewayBatchResponse(
        IReadOnlyList<GatewayBatchItem>? Results,
        string? Error);

    private sealed record GatewayBatchItem(
        string? Device,
        IReadOnlyList<int>? Values,
        bool? Success,
        string? Error);

    private sealed record GatewayBatchRequest(
        [property: JsonPropertyName("devices")] IReadOnlyList<string> Devices,
        [property: JsonPropertyName("ip")] string? Ip,
        [property: JsonPropertyName("port")] int? Port,
        [property: JsonPropertyName("transport")] string? Transport);

    private static string NormalizeBaseUrl(string baseUrl)
    {
        var trimmed = baseUrl.Trim();
        if (!trimmed.Contains("://", StringComparison.Ordinal))
        {
            return $"http://{trimmed}";
        }

        return trimmed;
    }
}
