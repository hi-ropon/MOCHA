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
        public TurnActivity(int turnNumber)
        {
            TurnNumber = turnNumber;
        }

        public int TurnNumber { get; }
        public List<ActivityLogItem> Items { get; } = new();
    }
}
