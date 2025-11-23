using System.Diagnostics.CodeAnalysis;

namespace MOCHA.Services.Plc;

/// <summary>
/// PLC Gateway を実際に呼ばず、決め打ちの値を返すフェイク実装。
/// </summary>
internal sealed class FakePlcGatewayClient : IPlcGatewayClient
{
    private readonly Dictionary<string, IReadOnlyList<int>> _data;

    /// <summary>
    /// 既定データまたはシードデータで初期化する。
    /// </summary>
    /// <param name="seed">キー（例: D100）と値のマップ。</param>
    public FakePlcGatewayClient(Dictionary<string, IReadOnlyList<int>>? seed = null)
    {
        _data = seed ?? new Dictionary<string, IReadOnlyList<int>>
        {
            { "D100", new List<int> { 42 } },
            { "M10", new List<int> { 1 } }
        };
    }

    /// <summary>
    /// メモリ上のデータから単一読み取りを行う。
    /// </summary>
    /// <param name="request">読み取り要求。</param>
    /// <param name="cancellationToken">キャンセル通知。</param>
    /// <returns>読み取り結果。</returns>
    public Task<PlcReadResult> ReadAsync(PlcReadRequest request, CancellationToken cancellationToken = default)
    {
        var key = $"{request.Device.ToUpperInvariant()}{request.Address}";
        if (_data.TryGetValue(key, out var values))
        {
            var trimmed = values.Take(request.Length).ToList();
            return Task.FromResult(new PlcReadResult(true, trimmed));
        }

        return Task.FromResult(new PlcReadResult(false, Array.Empty<int>(), $"device {key} not found"));
    }

    /// <summary>
    /// 複数デバイスの読み取りをシミュレートする。
    /// </summary>
    /// <param name="request">一括読み取り要求。</param>
    /// <param name="cancellationToken">キャンセル通知。</param>
    /// <returns>一括読み取り結果。</returns>
    public Task<PlcBatchReadResult> BatchReadAsync(PlcBatchReadRequest request, CancellationToken cancellationToken = default)
    {
        var items = new List<PlcReadResultItem>();
        foreach (var spec in request.Devices)
        {
            if (!TryParseSpec(spec, out var device, out var address, out var length, out var error))
            {
                items.Add(new PlcReadResultItem(spec, Array.Empty<int>(), false, error));
                continue;
            }

            var result = ReadAsync(new PlcReadRequest(device, address, length), cancellationToken).Result;
            items.Add(new PlcReadResultItem(spec, result.Values, result.Success, result.Error));
        }

        var success = items.All(x => x.Success);
        return Task.FromResult(new PlcBatchReadResult(success, items));
    }

    /// <summary>
    /// デバイス指定文字列をパースする。形式は例: D100 または D100:2。
    /// </summary>
    /// <param name="spec">デバイス指定文字列。</param>
    /// <param name="device">デバイス種別。</param>
    /// <param name="address">先頭アドレス。</param>
    /// <param name="length">読み取り長。</param>
    /// <param name="error">失敗時のエラー。</param>
    /// <returns>パース成功なら true。</returns>
    private static bool TryParseSpec(string spec, [NotNullWhen(true)] out string? device, out int address, out int length, out string? error)
    {
        device = null;
        address = 0;
        length = 1;
        error = null;

        var span = spec.AsSpan();
        var colon = span.IndexOf(':');
        if (colon >= 0)
        {
            if (!int.TryParse(span[(colon + 1)..], out length))
            {
                error = "invalid length";
                return false;
            }
            span = span[..colon];
        }

        if (span.Length < 2)
        {
            error = "invalid device spec";
            return false;
        }

        device = span[..1].ToString();
        if (!int.TryParse(span[1..], out address))
        {
            error = "invalid address";
            return false;
        }

        return true;
    }
}
