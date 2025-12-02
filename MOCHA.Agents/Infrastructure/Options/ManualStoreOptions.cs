namespace MOCHA.Agents.Infrastructure.Options;

/// <summary>
/// マニュアル格納場所の設定
/// </summary>
public sealed class ManualStoreOptions
{
    /// <summary>リポジトリ内のベースパス（例: "Resources"）</summary>
    public string BasePath { get; set; } = "Resources";

    /// <summary>エージェント名とフォルダー名のマッピング</summary>
    public Dictionary<string, string> AgentFolders { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["iaiAgent"] = "IAI",
        ["orientalAgent"] = "Oriental",
        ["plcAgent"] = "PLC"
    };

    /// <summary>読み出し時の最大バイト数（null なら全件）</summary>
    public int? MaxReadBytes { get; set; } = 32_000;
}
