using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Models.Chat;

namespace MOCHA.Tests
{
    [TestClass]
    public class TurnActivityTests
    {
        [TestMethod]
        public void ログ追加でライブ状態と件数が更新される()
        {
            var activity = new TurnActivity(0);
            var before = activity.LastUpdated;

            var item = new ActivityLogItem("a", "detail", ActivityKind.Assistant, DateTimeOffset.UtcNow.AddMinutes(1));
            activity.AddLog(item);

            Assert.AreEqual(1, activity.Items.Count);
            Assert.IsTrue(activity.IsLive);
            Assert.AreEqual(item.Timestamp, activity.LastUpdated);
            Assert.IsTrue(activity.LastUpdated > before);
        }

        [TestMethod]
        public void 完了するとライブが止まり完了フラグが立つ()
        {
            var activity = new TurnActivity(1);

            activity.MarkCompleted();

            Assert.IsFalse(activity.IsLive);
            Assert.IsTrue(activity.IsCompleted);
        }

        [TestMethod]
        public void 最近の更新判定は窓内のみ真になる()
        {
            var now = DateTimeOffset.UtcNow;
            var activity = new TurnActivity(2);
            activity.RefreshLive(now.AddSeconds(-1));

            Assert.IsTrue(activity.IsRecentlyUpdated(TimeSpan.FromSeconds(2), now));
            Assert.IsFalse(activity.IsRecentlyUpdated(TimeSpan.FromMilliseconds(500), now));
        }
    }
}
