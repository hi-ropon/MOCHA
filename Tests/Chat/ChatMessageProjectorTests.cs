using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Models.Chat;

namespace MOCHA.Tests;

/// <summary>
/// チャット表示向けの整形ロジック検証テスト
/// </summary>
[TestClass]
public class ChatMessageProjectorTests
{
    /// <summary>
    /// 1ターン内の複数アシスタントを最後だけ残す確認
    /// </summary>
    [TestMethod]
    public void 複数アシスタントは最後だけ残す()
    {
        var source = new List<ChatMessage>
        {
            new(ChatRole.User, "最初の質問"),
            new(ChatRole.Assistant, "途中の説明"),
            new(ChatRole.Assistant, "最終回答")
        };

        var result = ChatMessageProjector.KeepUserAndFinalAssistant(source);

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("最初の質問", result[0].Content);
        Assert.AreEqual("最終回答", result[1].Content);
    }

    /// <summary>
    /// 複数ターンでもユーザーと最後の応答だけ残す確認
    /// </summary>
    [TestMethod]
    public void 複数ターンでもユーザーと最後の応答だけ並べる()
    {
        var source = new List<ChatMessage>
        {
            new(ChatRole.Assistant, "ようこそ"), // 先頭のあいさつ
            new(ChatRole.User, "1回目"),
            new(ChatRole.Assistant, "途中1"),
            new(ChatRole.Tool, "ツールログ"),
            new(ChatRole.Assistant, "完了1"),
            new(ChatRole.User, "2回目"),
            new(ChatRole.Assistant, "完了2")
        };

        var result = ChatMessageProjector.KeepUserAndFinalAssistant(source);

        Assert.AreEqual(5, result.Count);
        CollectionAssert.AreEqual(
            new[]
            {
                "ようこそ",
                "1回目",
                "完了1",
                "2回目",
                "完了2"
            },
            result.Select(m => m.Content).ToList());
    }
}
