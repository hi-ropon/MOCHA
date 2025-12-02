namespace MOCHA.Agents.Infrastructure.Agents;

/// <summary>
/// PLC 診断向けの簡易エージェント
/// </summary>
public sealed class PlcTaskAgent : BaseTaskAgent
{
    /// <summary>
    /// PLC エージェント設定による初期化
    /// </summary>
    public PlcTaskAgent()
        : base(
            "plcAgent",
            "PLC診断・プログラム解析エージェント",
            "[PLC Agent]",
            question => $"""
                結論: PLC観点での対応案を提示します。
                - 要求: {question}
                - 次のアクション: PLCデバイス値確認、異常コード確認、ラダー該当箇所の参照を提案します。
                """) { }
}
