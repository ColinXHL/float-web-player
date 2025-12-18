using System;

namespace SandronePlayer.Models
{
    /// <summary>
    /// 收藏夹模型
    /// 用于 SQLite 存储收藏项（Phase 14）
    /// </summary>
    public class BookmarkItem
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
        /// 添加时间
        /// </summary>
        public DateTime AddTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 排序顺序
        /// </summary>
        public int SortOrder { get; set; }
    }
}
