namespace MOCHA.Services.Plc;

/// <summary>
/// 単一デバイス読み取り要求。
/// </summary>
/// <param name="Device">デバイス種別。</param>
/// <param name="Address">先頭アドレス。</param>
/// <param name="Length">読み取り長。</param>
/// <param name="Host">接続先ホスト。</param>
/// <param name="Port">接続先ポート。</param>
public record PlcReadRequest(string Device, int Address, int Length, string? Host = null, int? Port = null);

/// <summary>
/// 単一読み取り結果。
/// </summary>
/// <param name="Success">成功フラグ。</param>
/// <param name="Values">取得値。</param>
/// <param name="Error">エラー内容。</param>
public record PlcReadResult(bool Success, IReadOnlyList<int> Values, string? Error = null);

/// <summary>
/// 複数デバイスの一括読み取り要求。
/// </summary>
/// <param name="Devices">デバイス指定のリスト。</param>
/// <param name="Host">接続先ホスト。</param>
/// <param name="Port">接続先ポート。</param>
public record PlcBatchReadRequest(IReadOnlyList<string> Devices, string? Host = null, int? Port = null);

/// <summary>
/// 一括読み取り結果。
/// </summary>
/// <param name="Success">全体の成功フラグ。</param>
/// <param name="Results">デバイスごとの結果。</param>
/// <param name="Error">全体エラー。</param>
public record PlcBatchReadResult(
    bool Success,
    IReadOnlyList<PlcReadResultItem> Results,
    string? Error = null
);

/// <summary>
/// 個別デバイスの読み取り結果。
/// </summary>
/// <param name="Device">デバイス指定。</param>
/// <param name="Values">取得値。</param>
/// <param name="Success">成功フラグ。</param>
/// <param name="Error">エラー内容。</param>
public record PlcReadResultItem(string Device, IReadOnlyList<int> Values, bool Success, string? Error = null);

/// <summary>
/// PLC Gateway への読み取り操作を抽象化するインターフェース。
/// </summary>
public interface IPlcGatewayClient
{
    /// <summary>
    /// 単一デバイスの値を読み取る。
    /// </summary>
    /// <param name="request">読み取り要求。</param>
    /// <param name="cancellationToken">キャンセル通知。</param>
    /// <returns>読み取り結果。</returns>
    Task<PlcReadResult> ReadAsync(PlcReadRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 複数デバイスの値をまとめて読み取る。
    /// </summary>
    /// <param name="request">一括読み取り要求。</param>
    /// <param name="cancellationToken">キャンセル通知。</param>
    /// <returns>一括読み取り結果。</returns>
    Task<PlcBatchReadResult> BatchReadAsync(PlcBatchReadRequest request, CancellationToken cancellationToken = default);
}
