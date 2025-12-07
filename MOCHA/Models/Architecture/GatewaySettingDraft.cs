namespace MOCHA.Models.Architecture;

/// <summary>
/// ゲートウェイ設定入力用ドラフト
/// </summary>
public sealed class GatewaySettingDraft
{
    /// <summary>ゲートウェイIP</summary>
    public string Host { get; init; } = string.Empty;
    /// <summary>ゲートウェイポート</summary>
    public int? Port { get; init; }

    /// <summary>
    /// 入力値のバリデーション
    /// </summary>
    /// <returns>検証結果</returns>
    public (bool IsValid, string? Error) Validate()
    {
        if (string.IsNullOrWhiteSpace(Host))
        {
            return (false, "ゲートウェイIPアドレスを入力してください");
        }

        if (Port is null || Port <= 0 || Port > 65535)
        {
            return (false, "ゲートウェイポートは1-65535で入力してください");
        }

        return (true, null);
    }
}
