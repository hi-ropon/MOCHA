using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Components.Pages;
using MOCHA.Models.Chat;

namespace MOCHA.Tests;

/// <summary>
/// Home コンポーネントのフィードバック表示可否を確認するテスト
/// </summary>
[TestClass]
public class HomeFeedbackVisibilityTests
{
    /// <summary>
    /// ストリーミング中は対象メッセージのフィードバックを非表示にする確認
    /// </summary>
    [TestMethod]
    public void フィードバック表示判定_ストリーミング中は非表示()
    {
        var streamingAssistant = new ChatMessage(ChatRole.Assistant, "生成中");

        var result = Home.ShouldShowFeedback(streamingAssistant, 3, 3);

        Assert.IsFalse(result);
    }

    /// <summary>
    /// ストリーミング完了後はフィードバックを表示する確認
    /// </summary>
    [TestMethod]
    public void フィードバック表示判定_ストリーミング完了後は表示()
    {
        var completedAssistant = new ChatMessage(ChatRole.Assistant, "完了済み");

        var result = Home.ShouldShowFeedback(completedAssistant, 3, null);

        Assert.IsTrue(result);
    }

    /// <summary>
    /// 初期挨拶メッセージはフィードバック対象外にする確認
    /// </summary>
    [TestMethod]
    public void フィードバック表示判定_初期挨拶は非表示()
    {
        var greeting = new ChatMessage(ChatRole.Assistant, "こんにちは、何をお手伝いしましょうか？");

        var result = Home.ShouldShowFeedback(greeting, 0, null);

        Assert.IsFalse(result);
    }
}
