namespace MOCHA.Models.Architecture;

/// <summary>
/// PC設定の結果モデル
/// </summary>
public sealed class PcSettingResult
{
    private PcSettingResult(bool succeeded, PcSetting? setting, string? error)
    {
        Succeeded = succeeded;
        Setting = setting;
        Error = error;
    }

    /// <summary>成功フラグ</summary>
    public bool Succeeded { get; }
    /// <summary>設定</summary>
    public PcSetting? Setting { get; }
    /// <summary>エラーメッセージ</summary>
    public string? Error { get; }

    /// <summary>
    /// 成功結果生成
    /// </summary>
    /// <param name="setting">設定</param>
    /// <returns>結果</returns>
    public static PcSettingResult Success(PcSetting setting) => new(true, setting, null);

    /// <summary>
    /// 失敗結果生成
    /// </summary>
    /// <param name="error">エラー内容</param>
    /// <returns>結果</returns>
    public static PcSettingResult Fail(string error) => new(false, null, error);
}
