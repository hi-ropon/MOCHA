using System.Collections.Generic;
using System.Linq;
using MOCHA.Models.Chat;

namespace MOCHA.Services.Chat
{
    /// <summary>
    /// アクティビティパネル表示用のヘルパー
    /// </summary>
    public static class ActivityPanelService
    {
        /// <summary>
        /// 選択ターンに応じたログ一覧取得
        /// </summary>
        /// <param name="activities">ターンごとのアクティビティ</param>
        /// <param name="selectedTurnNumber">選択中ターン番号</param>
        /// <returns>表示用ログ一覧</returns>
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

        /// <summary>
        /// アクティビティ件数サマリ生成
        /// </summary>
        /// <param name="logs">アクティビティログ</param>
        /// <returns>サマリ文字列</returns>
        public static string GetActivitySummary(IEnumerable<ActivityLogItem> logs)
        {
            var count = logs?.Count() ?? 0;
            return count > 0 ? $"{count} 件の途中経過" : "まだありません";
        }
    }
}
