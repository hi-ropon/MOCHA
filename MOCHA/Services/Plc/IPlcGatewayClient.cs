namespace MOCHA.Services.Plc;

public record PlcReadRequest(string Device, int Address, int Length, string? Host = null, int? Port = null);

public record PlcReadResult(bool Success, IReadOnlyList<int> Values, string? Error = null);

public record PlcBatchReadRequest(IReadOnlyList<string> Devices, string? Host = null, int? Port = null);

public record PlcBatchReadResult(
    bool Success,
    IReadOnlyList<PlcReadResultItem> Results,
    string? Error = null
);

public record PlcReadResultItem(string Device, IReadOnlyList<int> Values, bool Success, string? Error = null);

public interface IPlcGatewayClient
{
    Task<PlcReadResult> ReadAsync(PlcReadRequest request, CancellationToken cancellationToken = default);

    Task<PlcBatchReadResult> BatchReadAsync(PlcBatchReadRequest request, CancellationToken cancellationToken = default);
}
