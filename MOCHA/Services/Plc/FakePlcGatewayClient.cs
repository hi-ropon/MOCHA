using System.Diagnostics.CodeAnalysis;

namespace MOCHA.Services.Plc;

/// <summary>
/// PLC Gateway を実際に呼ばず、決め打ちの値を返すフェイク実装。
/// </summary>
public sealed class FakePlcGatewayClient : IPlcGatewayClient
{
    private readonly Dictionary<string, IReadOnlyList<int>> _data;

    public FakePlcGatewayClient(Dictionary<string, IReadOnlyList<int>>? seed = null)
    {
        _data = seed ?? new Dictionary<string, IReadOnlyList<int>>
        {
            { "D100", new List<int> { 42 } },
            { "M10", new List<int> { 1 } }
        };
    }

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
