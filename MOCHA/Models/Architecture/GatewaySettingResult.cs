namespace MOCHA.Models.Architecture;

/// <summary>
/// ゲートウェイ設定の保存結果
/// </summary>
public sealed class GatewaySettingResult
{
    private GatewaySettingResult(bool succeeded, string? error, GatewaySetting? setting)
    {
        Succeeded = succeeded;
        Error = error;
        Setting = setting;
    }

    /// <summary>成功フラグ</summary>
    public bool Succeeded { get; }
    /// <summary>エラーメッセージ</summary>
    public string? Error { get; }
    /// <summary>設定</summary>
    public GatewaySetting? Setting { get; }

    /// <summary>成功を表す結果</summary>
    public static GatewaySettingResult Success(GatewaySetting setting) => new(true, null, setting);

    /// <summary>失敗を表す結果</summary>
    public static GatewaySettingResult Fail(string error) => new(false, error, null);
}
