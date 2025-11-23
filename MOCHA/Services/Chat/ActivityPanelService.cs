using System.Collections.Generic;
using System.Linq;
using MOCHA.Models.Chat;

namespace MOCHA.Services.Chat
{
    public static class ActivityPanelService
    {
        public static IEnumerable<ActivityLogItem> GetPanelLogs(IEnumerable<TurnActivity> activities, int? selectedTurnNumber)
        {
            if (activities is null)
            {
                return Enumerable.Empty<ActivityLogItem>();
            }

            if (selectedTurnNumber is int turn)
            {
                return activities.FirstOrDefault(a => a.TurnNumber == turn)?.Items ?? Enumerable.Empty<ActivityLogItem>();
            }

            return activities.SelectMany(a => a.Items);
        }

        public static string GetActivitySummary(IEnumerable<ActivityLogItem> logs)
        {
            var count = logs?.Count() ?? 0;
            return count > 0 ? $"{count} 件の途中経過" : "まだありません";
        }
    }
}
