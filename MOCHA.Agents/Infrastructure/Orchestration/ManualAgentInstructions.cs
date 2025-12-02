using System;

namespace MOCHA.Agents.Infrastructure.Orchestration;

/// <summary>
/// マニュアルサブエージェント向けプロンプト
/// </summary>
public static class ManualAgentInstructions
{
    /// <summary>
    /// エージェント名に対応するプロンプト取得
    /// </summary>
    /// <param name="agentName">エージェント名</param>
    /// <returns>プロンプト文字列</returns>
    public static string For(string agentName) =>
        Normalize(agentName) switch
        {
            "plcAgent" => _plc,
            "orientalAgent" => _oriental,
            _ => _iai
        };

    /// <summary>
    /// エージェント説明取得
    /// </summary>
    /// <param name="agentName">エージェント名</param>
    /// <returns>説明</returns>
    public static string Description(string agentName) =>
        Normalize(agentName) switch
        {
            "plcAgent" => "PLC診断・プログラム解析エージェント",
            "orientalAgent" => "Oriental Motor機器解析・診断エージェント",
            _ => "IAI機器マニュアル検索・情報提供エージェント"
        };

    private const string _iai =
        """
        あなたは IAI/RCON/RSEL/SCON/XSEL 専門のサブエージェントです。
        1) 必ず find_manuals ツールを agentName=iaiAgent で呼び出し、質問に関連するマニュアル候補を取得する。
        2) 必要に応じて read_manual で根拠を読み取り、重要な設定値や注意点を要約する。
        3) 答えは最初の1-2文で結論、その後に根拠箇所や次の確認手順を短くまとめる。数字やエラーコードがあればそのまま示す。
        4) ツール呼び出しが不要な場合だけ簡潔に直接回答する。
        """;

    private const string _oriental =
        """
        あなたは Oriental Motor/AZ/MEXE02 専門のサブエージェントです。
        1) find_manuals を agentName=orientalAgent で実行し、該当マニュアルを探す。
        2) 見つかったら read_manual で根拠を読み、アラームコードや設定手順を要約する。
        3) 先頭で結論を述べ、続けて根拠ページや次の確認を箇条書きで返す。
        """;

    private const string _plc =
        """
        あなたは 三菱PLC/MCプロトコル専任のサブエージェントです。
        1) find_manuals/search_instruction/get_command_overview で関連マニュアルを候補提示し、必要に応じ read_manual で根拠を読む。
        2) プログラム確認は program_lines/related_devices/get_comment を優先して使い、デバイス推定は reasoning_device/reasoning_multiple_devices を呼び出す。
        3) ゲートウェイ値が必要な場合は read_plc_values または read_multiple_plc_values を使う（devices/IP/port/timeout を指定）。
        4) 回答は最初の1-2文で結論、その後に根拠の抜粋や次の確認手順を箇条書きで示す。ラダー記述が必要なら簡潔に。
        """;

    private static string Normalize(string agentName)
    {
        if (string.IsNullOrWhiteSpace(agentName))
        {
            return "iaiAgent";
        }

        var trimmed = agentName.Trim();
        if (trimmed.EndsWith("Agent", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        return trimmed.ToLowerInvariant() switch
        {
            "plc" => "plcAgent",
            "oriental" => "orientalAgent",
            _ => "iaiAgent"
        };
    }
}
