using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
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
            using var response = await _http.PostAsJsonAsync(
                BuildUri(request.BaseUrl, "api/read"),
                new
                {
                    device,
                    addr = address,
                    length,
                    ip = request.Ip,
                    port = request.Port
                },
                cancellationToken);

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
        try
        {
            var payload = new
            {
                devices = request.Specs,
                ip = request.Ip,
                port = request.Port
            };

            using var response = await _http.PostAsJsonAsync(
                BuildUri(request.BaseUrl, "api/batch_read"),
                payload,
                cancellationToken);

            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<GatewayBatchResponse>(cancellationToken: cancellationToken);
            if (body?.Results is null)
            {
                return new BatchReadResult(Array.Empty<DeviceReadResult>(), "空の応答を受信しました");
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
    internal static (string Device, int Address, int Length) ParseDevice(string spec)
    {
        if (string.IsNullOrWhiteSpace(spec))
        {
            return ("D", 0, 1);
        }

        var span = spec.AsSpan().Trim();
        var length = 1;
        var colon = span.IndexOf(':');
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
}
