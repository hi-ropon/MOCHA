namespace MOCHA.Services.Plc;

/// <summary>
/// PLC Gateway への接続設定。
/// </summary>
public sealed class PlcGatewayOptions
{
    /// <summary>
    /// 実際のゲートウェイ呼び出しを有効にするかどうか。
    /// </summary>
    public bool Enabled { get; set; } = true;
    /// <summary>
    /// ゲートウェイのベースURL。
    /// </summary>
    public string BaseAddress { get; set; } = "http://localhost:8000";
    /// <summary>
    /// リクエストタイムアウト。
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
}
