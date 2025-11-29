using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Models.Chat;
using MOCHA.Services.Chat;

namespace MOCHA.Tests
{
    [TestClass]
    public class ActivityPanelServiceTests
    {
        [TestMethod]
        public void 選択ターンが指定されていればそのターンのみ返す()
        {
            var activities = new List<TurnActivity>
            {
                CreateActivity(0, new ActivityLogItem("a", null, ActivityKind.Assistant, System.DateTimeOffset.UtcNow)),
                CreateActivity(1, new ActivityLogItem("b", null, ActivityKind.Action, System.DateTimeOffset.UtcNow))
            };

            var logs = ActivityPanelService.GetPanelLogs(activities, 1);

            var list = new List<ActivityLogItem>(logs);
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual("b", list[0].Title);
        }

        [TestMethod]
        public void 選択なしなら全ターンのログを返す()
        {
            var activities = new List<TurnActivity>
            {
                CreateActivity(0, new ActivityLogItem("a", null, ActivityKind.Assistant, System.DateTimeOffset.UtcNow)),
                CreateActivity(1, new ActivityLogItem("b", null, ActivityKind.Action, System.DateTimeOffset.UtcNow))
            };

            var logs = ActivityPanelService.GetPanelLogs(activities, null);

            var list = new List<ActivityLogItem>(logs);
            Assert.AreEqual(2, list.Count);
        }

        [TestMethod]
        public void サマリは件数に応じて文面を返す()
        {
            var logs = new List<ActivityLogItem>
            {
                new("a", null, ActivityKind.Assistant, System.DateTimeOffset.UtcNow),
                new("b", null, ActivityKind.Action, System.DateTimeOffset.UtcNow)
            };

            var summary = ActivityPanelService.GetActivitySummary(logs);
            var emptySummary = ActivityPanelService.GetActivitySummary(new List<ActivityLogItem>());

            StringAssert.Contains(summary, "2 件");
            Assert.AreEqual("まだありません", emptySummary);
        }

        private static TurnActivity CreateActivity(int turn, ActivityLogItem item)
        {
            var activity = new TurnActivity(turn);
            activity.AddLog(item);
            activity.MarkCompleted();
            return activity;
        }
    }
}
