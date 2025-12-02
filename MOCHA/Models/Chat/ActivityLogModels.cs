using System;
using System.Collections.Generic;

namespace MOCHA.Models.Chat
{
    /// <summary>
    /// アクティビティ種別
    /// </summary>
    public enum ActivityKind
    {
        Assistant,
        Action,
        ToolResult,
        Error
    }

    /// <summary>
    /// 単一アクティビティ項目
    /// </summary>
    /// <param name="Title">表示タイトル</param>
    /// <param name="Detail">詳細</param>
    /// <param name="Kind">種別</param>
    /// <param name="Timestamp">タイムスタンプ</param>
    public sealed record ActivityLogItem(string Title, string? Detail, ActivityKind Kind, DateTimeOffset Timestamp);

    /// <summary>
    /// ターン単位のアクティビティ集合
    /// </summary>
    public sealed class TurnActivity
    {
        private readonly List<ActivityLogItem> _items = new();

        /// <summary>
        /// ターン番号指定の初期化
        /// </summary>
        /// <param name="turnNumber">ターン番号</param>
        public TurnActivity(int turnNumber)
        {
            TurnNumber = turnNumber;
            LastUpdated = DateTimeOffset.UtcNow;
            IsLive = true;
        }

        /// <summary>ターン番号</summary>
        public int TurnNumber { get; }
        /// <summary>アクティビティ一覧</summary>
        public IReadOnlyList<ActivityLogItem> Items => _items;
        /// <summary>進行中フラグ</summary>
        public bool IsLive { get; private set; }
        /// <summary>完了フラグ</summary>
        public bool IsCompleted { get; private set; }
        /// <summary>最終更新時刻</summary>
        public DateTimeOffset LastUpdated { get; private set; }

        /// <summary>
        /// ログ追加
        /// </summary>
        /// <param name="item">追加ログ</param>
        public void AddLog(ActivityLogItem item)
        {
            _items.Add(item);
            LastUpdated = item.Timestamp;
            IsLive = true;
        }

        /// <summary>
        /// 進行中状態の更新
        /// </summary>
        /// <param name="timestamp">更新時刻</param>
        public void RefreshLive(DateTimeOffset timestamp)
        {
            LastUpdated = timestamp;
            IsLive = true;
        }

        /// <summary>
        /// ターン完了マーク
        /// </summary>
        public void MarkCompleted()
        {
            IsLive = false;
            IsCompleted = true;
        }

        /// <summary>
        /// 直近更新判定
        /// </summary>
        /// <param name="window">許容時間幅</param>
        /// <param name="now">現在時刻</param>
        /// <returns>直近なら true</returns>
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
