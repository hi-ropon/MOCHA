using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MOCHA.Services.Plc;

/// <summary>
/// PLC Gateway HTTP クライアント。
/// UIで指定された接続先が無い場合はベースアドレス（既定 http://localhost:8000）を利用する。
/// </summary>
public sealed class HttpPlcGatewayClient : IPlcGatewayClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpPlcGatewayClient> _logger;

    public HttpPlcGatewayClient(HttpClient httpClient, ILogger<HttpPlcGatewayClient> logger, IOptions<PlcGatewayOptions> options)
    {
        _logger = logger;
        _httpClient = httpClient;
        var opt = options.Value;
        if (_httpClient.Timeout == default || _httpClient.Timeout == TimeSpan.Zero)
        {
            _httpClient.Timeout = opt.Timeout;
        }
        if (_httpClient.BaseAddress is null && Uri.TryCreate(opt.BaseAddress, UriKind.Absolute, out var uri))
        {
            _httpClient.BaseAddress = uri;
        }
    }

    public async Task<PlcReadResult> ReadAsync(PlcReadRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new
            {
                device = request.Device,
                addr = request.Address,
                length = request.Length,
                ip = request.Host,
                port = request.Port
            };

            var response = await _httpClient.PostAsJsonAsync("api/read", payload, cancellationToken);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<ReadResponse>(JsonOptions(), cancellationToken);
            if (body is null)
            {
                return new PlcReadResult(false, Array.Empty<int>(), "empty response");
            }

            var values = body.Values ?? Array.Empty<int>();
            var success = body.Success ?? true;
            return new PlcReadResult(success && values.Any(), values, body.Error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PLC Gateway read failed: {Device}{Addr}", request.Device, request.Address);
            return new PlcReadResult(false, Array.Empty<int>(), ex.Message);
        }
    }

    public async Task<PlcBatchReadResult> BatchReadAsync(PlcBatchReadRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new
            {
                devices = request.Devices,
                ip = request.Host,
                port = request.Port
            };

            var response = await _httpClient.PostAsJsonAsync("api/batch_read", payload, cancellationToken);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<BatchReadResponse>(JsonOptions(), cancellationToken);
            if (body?.Results is null)
            {
                return new PlcBatchReadResult(false, Array.Empty<PlcReadResultItem>(), "empty response");
            }

            var items = body.Results
                .Select(r => new PlcReadResultItem(
                    r.Device ?? string.Empty,
                    r.Values ?? Array.Empty<int>(),
                    r.Success ?? r.Error is null,
                    r.Error))
                .ToList();

            var success = items.All(x => x.Success);
            return new PlcBatchReadResult(success, items, body.Error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PLC Gateway batch read failed");
            return new PlcBatchReadResult(false, Array.Empty<PlcReadResultItem>(), ex.Message);
        }
    }

    private static JsonSerializerOptions JsonOptions() => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private sealed record ReadResponse(
        [property: JsonPropertyName("values")] IReadOnlyList<int>? Values,
        [property: JsonPropertyName("success")] bool? Success,
        [property: JsonPropertyName("error")] string? Error);

    private sealed record BatchReadResponse(
        [property: JsonPropertyName("results")] IReadOnlyList<BatchReadItem>? Results,
        [property: JsonPropertyName("total_devices")] int? TotalDevices,
        [property: JsonPropertyName("successful_devices")] int? SuccessfulDevices,
        [property: JsonPropertyName("error")] string? Error);

    private sealed record BatchReadItem(
        [property: JsonPropertyName("device")] string? Device,
        [property: JsonPropertyName("values")] IReadOnlyList<int>? Values,
        [property: JsonPropertyName("success")] bool? Success,
        [property: JsonPropertyName("error")] string? Error);
}
