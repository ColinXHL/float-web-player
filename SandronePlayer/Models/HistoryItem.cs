using System;

namespace SandronePlayer.Models
{
    /// <summary>
    /// 历史记录模型
    /// 用于 SQLite 存储访问历史（Phase 14）
    /// </summary>
    public class HistoryItem
    {
        /// <summary>
        /// 记录 ID（主键）
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 页面 URL
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// 页面标题
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// 访问时间
        /// </summary>
        public DateTime VisitTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 访问次数
        /// </summary>
        public int VisitCount { get; set; } = 1;
    }
}
