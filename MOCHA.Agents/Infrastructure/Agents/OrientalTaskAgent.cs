namespace MOCHA.Agents.Infrastructure.Agents;

/// <summary>
/// Oriental Motor 解析向けの簡易エージェント。
/// </summary>
public sealed class OrientalTaskAgent : BaseTaskAgent
{
    public OrientalTaskAgent()
        : base(
            "orientalAgent",
            "Oriental Motor機器解析・診断エージェント",
            "[Oriental Agent]",
            question => $"結論: Oriental Motor視点での対処をまとめます。\n- 質問: {question}\n- 確認: AZシリーズ/MEXE02設定、アラームコード、押し当て運転や脱調検出の設定を点検します。") { }
}
