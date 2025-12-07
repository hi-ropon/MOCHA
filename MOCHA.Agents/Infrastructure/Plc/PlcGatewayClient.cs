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
        var (device, address, length) = ParseDevice(request.Spec);

        try
        {
            var uri = BuildUri(request.BaseUrl, $"api/read/{device}/{Uri.EscapeDataString(address)}/{length}");
            if (!string.IsNullOrWhiteSpace(request.Ip) || request.Port is not null || !string.IsNullOrWhiteSpace(request.PlcHost))
            {
                var query = new List<string>();
                if (!string.IsNullOrWhiteSpace(request.PlcHost))
                {
                    query.Add($"plc_host={Uri.EscapeDataString(request.PlcHost)}");
                }

                if (!string.IsNullOrWhiteSpace(request.Ip))
                {
                    query.Add($"ip={Uri.EscapeDataString(request.Ip)}");
                }

                if (request.Port is not null)
                {
                    query.Add($"port={request.Port.Value}");
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
                return new DeviceReadResult($"{device}{address}", null, false, "空の応答を受信しました");
            }

            return new DeviceReadResult($"{device}{address}", body.Values ?? Array.Empty<int>(), body.Success ?? true, body.Error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PLC Gateway 読み取りに失敗しました。");
            return new DeviceReadResult($"{device}{address}", null, false, ex.Message);
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
            var payload = new GatewayBatchRequest(request.Specs, request.Ip, request.Port, request.PlcHost);

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
            return new Uri(new Uri(baseUrl, UriKind.Absolute), path);
        }

        if (_http.BaseAddress is not null)
        {
            return new Uri(_http.BaseAddress, path);
        }

        return new Uri(path, UriKind.Relative);
    }

    /// <summary>
    /// デバイス指定をパース
    /// </summary>
    internal static (string Device, string Address, int Length) ParseDevice(string spec)
    {
        if (string.IsNullOrWhiteSpace(spec))
        {
            return ("D", "0", 1);
        }

        var span = spec.AsSpan().Trim();
        var length = 1;
        var colon = span.IndexOf(':');
        if (colon >= 0 && int.TryParse(span[(colon + 1)..], out var parsedLength) && parsedLength > 0)
        {
            length = parsedLength;
            span = span[..colon];
        }

        var core = span.ToString();
        if (string.IsNullOrWhiteSpace(core))
        {
            return ("D", "0", length);
        }

        var device = _devicePrefixes.FirstOrDefault(prefix => core.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                     ?? core[0].ToString().ToUpperInvariant();
        var address = core.Length > device.Length ? core.Substring(device.Length) : "0";
        if (string.IsNullOrWhiteSpace(address))
        {
            address = "0";
        }

        return (device.ToUpperInvariant(), address, length);
    }

    private static readonly string[] _devicePrefixes = { "ZR", "D", "W", "R", "X", "Y", "M" };

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
        [property: JsonPropertyName("plc_host")] string? PlcHost);
}
