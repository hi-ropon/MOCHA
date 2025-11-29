using System;
using System.Collections.Generic;

namespace MOCHA.Models.Chat
{
    public enum ActivityKind
    {
        Assistant,
        Action,
        ToolResult,
        Error
    }

    public sealed record ActivityLogItem(string Title, string? Detail, ActivityKind Kind, DateTimeOffset Timestamp);

    public sealed class TurnActivity
    {
        private readonly List<ActivityLogItem> _items = new();

        public TurnActivity(int turnNumber)
        {
            TurnNumber = turnNumber;
            LastUpdated = DateTimeOffset.UtcNow;
            IsLive = true;
        }

        public int TurnNumber { get; }
        public IReadOnlyList<ActivityLogItem> Items => _items;
        public bool IsLive { get; private set; }
        public bool IsCompleted { get; private set; }
        public DateTimeOffset LastUpdated { get; private set; }

        public void AddLog(ActivityLogItem item)
        {
            _items.Add(item);
            LastUpdated = item.Timestamp;
            IsLive = true;
        }

        public void RefreshLive(DateTimeOffset timestamp)
        {
            LastUpdated = timestamp;
            IsLive = true;
        }

        public void MarkCompleted()
        {
            IsLive = false;
            IsCompleted = true;
        }

        public bool IsRecentlyUpdated(TimeSpan window, DateTimeOffset now)
        {
            if (!IsLive)
            {
                return false;
            }

            return now - LastUpdated <= window;
        }
    }
}
