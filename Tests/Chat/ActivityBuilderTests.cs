using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Models.Chat;
using MOCHA.Services.Chat;
using System.Linq;

namespace MOCHA.Tests
{
    /// <summary>
    /// ActivityBuilder のアクティビティ生成ロジック検証テスト
    /// </summary>
    [TestClass]
    public class ActivityBuilderTests
    {
        /// <summary>
        /// ツールのみの履歴ではアクティビティが生成されない確認
        /// </summary>
        [TestMethod]
        public void ツールのみならアクティビティを作らない()
        {
            var history = new List<ChatMessage>
            {
                new(ChatRole.Tool, "[action] test")
            };

            var activities = ActivityBuilder.BuildActivities(history);

            Assert.AreEqual(0, activities.Count);
            Assert.IsTrue(activities.All(a => a.IsCompleted));
        }

        /// <summary>
        /// ユーザーのみの履歴で空ターンが生成される確認
        /// </summary>
        [TestMethod]
        public void ユーザーのみなら空ターンを作る()
        {
            var history = new List<ChatMessage>
            {
                new(ChatRole.User, "hello")
            };

            var activities = ActivityBuilder.BuildActivities(history);

            Assert.AreEqual(1, activities.Count);
            Assert.AreEqual(0, activities[0].TurnNumber);
            Assert.AreEqual(0, activities[0].Items.Count);
            Assert.IsTrue(activities[0].IsCompleted);
            Assert.IsFalse(activities[0].IsLive);
        }

        /// <summary>
        /// ユーザー以前のツールメッセージを無視する確認
        /// </summary>
        [TestMethod]
        public void ツールがユーザーより先なら無視される()
        {
            var history = new List<ChatMessage>
            {
                new(ChatRole.Tool, "[action] orphan"),
                new(ChatRole.User, "hi"),
                new(ChatRole.Assistant, "ok")
            };

            var activities = ActivityBuilder.BuildActivities(history);

            Assert.AreEqual(1, activities.Count);
            Assert.AreEqual(1, activities[0].Items.Count);
            Assert.AreEqual(ActivityKind.Assistant, activities[0].Items[0].Kind);
            Assert.IsTrue(activities[0].IsCompleted);
        }

        /// <summary>
        /// ツールアクションと結果を同一ターンにまとめる確認
        /// </summary>
        [TestMethod]
        public void ツールアクションと結果は同じターンにまとまる()
        {
            var now = DateTimeOffset.UtcNow;
            var history = new List<ChatMessage>
            {
                new(ChatRole.User, "hi"),
                new(ChatRole.Tool, "[action] read"),
                new(ChatRole.Tool, "[result] ok"),
                new(ChatRole.Assistant, "done")
            };

            var activities = ActivityBuilder.BuildActivities(history);

            Assert.AreEqual(1, activities.Count);
            var items = activities[0].Items;
            Assert.AreEqual(3, items.Count);
            CollectionAssert.AreEqual(
                new[] { ActivityKind.Action, ActivityKind.ToolResult, ActivityKind.Assistant },
                new[] { items[0].Kind, items[1].Kind, items[2].Kind });
            Assert.IsTrue(activities[0].IsCompleted);
        }
    }
}
