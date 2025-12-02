using System.Threading;
using System.Threading.Tasks;

namespace MOCHA.Agents.Domain.Plc;

/// <summary>
/// PLCゲートウェイ読み取りクライアント
/// </summary>
public interface IPlcGatewayClient
{
    /// <summary>
    /// 単体デバイスの読み取り
    /// </summary>
    Task<DeviceReadResult> ReadAsync(DeviceReadRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// バッチ読み取り
    /// </summary>
    Task<BatchReadResult> ReadBatchAsync(BatchReadRequest request, CancellationToken cancellationToken = default);
}
