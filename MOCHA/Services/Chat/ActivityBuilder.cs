using System;
using System.Collections.Generic;
using System.Linq;
using MOCHA.Models.Chat;

namespace MOCHA.Services.Chat
{
    public static class ActivityBuilder
    {
        public static IReadOnlyList<TurnActivity> BuildActivities(IReadOnlyList<ChatMessage> historyMessages)
        {
            var activities = new List<TurnActivity>();
            TurnActivity? current = null;
            var turnNumber = -1;

            for (var i = 0; i < historyMessages.Count; i++)
            {
                var message = historyMessages[i];
                switch (message.Role)
                {
                    case ChatRole.User:
                        turnNumber++;
                        current = new TurnActivity(turnNumber);
                        activities.Add(current);
                        break;
                    case ChatRole.Assistant when current is not null:
                        current.AddLog(new ActivityLogItem($"アシスタント: {TrimForPreview(message.Content)}", message.Content, ActivityKind.Assistant, DateTimeOffset.UtcNow));
                        break;
                    case ChatRole.Tool when current is not null:
                        current.AddLog(BuildToolLog(message));
                        break;
                }
            }

            foreach (var activity in activities)
            {
                activity.MarkCompleted();
            }

            return activities;
        }

        public static string TrimForPreview(string text, int maxLength = 80)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            return text.Length <= maxLength
                ? text
                : text[..maxLength] + "…";
        }

        private static ActivityLogItem BuildToolLog(ChatMessage message)
        {
            if (message.Content.StartsWith("[action]", StringComparison.OrdinalIgnoreCase))
            {
                return new ActivityLogItem($"ツール要求: {TrimForPreview(message.Content)}", message.Content, ActivityKind.Action, DateTimeOffset.UtcNow);
            }

            if (message.Content.StartsWith("[result]", StringComparison.OrdinalIgnoreCase))
            {
                return new ActivityLogItem($"ツール結果: {TrimForPreview(message.Content)}", message.Content, ActivityKind.ToolResult, DateTimeOffset.UtcNow);
            }

            return new ActivityLogItem($"ツール: {TrimForPreview(message.Content)}", message.Content, ActivityKind.ToolResult, DateTimeOffset.UtcNow);
        }
    }
}
