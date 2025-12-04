using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Agents.Infrastructure.Orchestration;

namespace MOCHA.Tests;

/// <summary>
/// OrganizerInstructions のポリシー文言を検証するテスト
/// </summary>
[TestClass]
public class OrganizerInstructionsTests
{
    /// <summary>
    /// 既定プロンプトにサブエージェント委譲ルールが含まれる確認
    /// </summary>
    [TestMethod]
    public void Default_マニュアル処理委譲ルールを含む()
    {
        var text = OrganizerInstructions.Default;

        StringAssert.Contains(text, "Organizer は振り分けのみを担当");
        StringAssert.Contains(text, "find_manuals");
        StringAssert.Contains(text, "read_manual");
        StringAssert.Contains(text, "invoke_plc_agent");
        StringAssert.Contains(text, "invoke_iai_agent");
        StringAssert.Contains(text, "invoke_oriental_agent");
        StringAssert.Contains(text, "invoke_drawing_agent");
    }
}
