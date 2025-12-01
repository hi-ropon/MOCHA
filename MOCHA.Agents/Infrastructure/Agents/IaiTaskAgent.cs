namespace MOCHA.Agents.Infrastructure.Agents;

/// <summary>
/// IAI 機器解析向けの簡易エージェント
/// </summary>
public sealed class IaiTaskAgent : BaseTaskAgent
{
    /// <summary>
    /// IAI エージェント設定による初期化
    /// </summary>
    public IaiTaskAgent()
        : base(
            "iaiAgent",
            "IAI機器マニュアル検索・情報提供エージェント",
            "[IAI Agent]",
            question => $"結論: IAI関連のガイダンスを返します。\n- 質問: {question}\n- 参照: RCON/RSEL/SCON/XSELマニュアルを確認し、設定値・エラーログ・配線チェックを提案します。") { }
}
