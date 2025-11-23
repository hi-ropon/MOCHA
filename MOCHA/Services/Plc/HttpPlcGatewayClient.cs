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
internal sealed class HttpPlcGatewayClient : IPlcGatewayClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpPlcGatewayClient> _logger;

    /// <summary>
    /// HTTP クライアントと設定を受け取り、タイムアウトやベースURLを初期化する。
    /// </summary>
    /// <param name="httpClient">PLC Gateway への HTTP クライアント。</param>
    /// <param name="logger">ロガー。</param>
    /// <param name="options">ゲートウェイ設定。</param>
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

    /// <summary>
    /// 単一デバイスのアドレスから値を読み出す。
    /// </summary>
    /// <param name="request">読み出し要求。</param>
    /// <param name="cancellationToken">キャンセル通知。</param>
    /// <returns>読み出し結果。</returns>
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

    /// <summary>
    /// 複数デバイスの一括読み取りを行う。
    /// </summary>
    /// <param name="request">一括読み取り要求。</param>
    /// <param name="cancellationToken">キャンセル通知。</param>
    /// <returns>一括読み取り結果。</returns>
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

    /// <summary>
    /// JSON シリアライザーのオプションを返す。
    /// </summary>
    /// <returns>シリアライザー設定。</returns>
    private static JsonSerializerOptions JsonOptions() => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// 単一読み取り API のレスポンスモデル。
    /// </summary>
    private sealed record ReadResponse(
        [property: JsonPropertyName("values")] IReadOnlyList<int>? Values,
        [property: JsonPropertyName("success")] bool? Success,
        [property: JsonPropertyName("error")] string? Error);

    /// <summary>
    /// 一括読み取り API のレスポンスモデル。
    /// </summary>
    private sealed record BatchReadResponse(
        [property: JsonPropertyName("results")] IReadOnlyList<BatchReadItem>? Results,
        [property: JsonPropertyName("total_devices")] int? TotalDevices,
        [property: JsonPropertyName("successful_devices")] int? SuccessfulDevices,
        [property: JsonPropertyName("error")] string? Error);

    /// <summary>
    /// 一括読み取り結果の個別アイテム。
    /// </summary>
    private sealed record BatchReadItem(
        [property: JsonPropertyName("device")] string? Device,
        [property: JsonPropertyName("values")] IReadOnlyList<int>? Values,
        [property: JsonPropertyName("success")] bool? Success,
        [property: JsonPropertyName("error")] string? Error);
}
