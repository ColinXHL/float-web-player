using System.Collections.Generic;

namespace SandronePlayer.Models
{
    /// <summary>
    /// 完整字幕数据
    /// 包含所有字幕条目和元信息
    /// </summary>
    public class SubtitleData
    {
        /// <summary>
        /// 字幕语言代码
        /// </summary>
        public string Language { get; set; } = string.Empty;

        /// <summary>
        /// 字幕条目列表（按时间排序）
        /// </summary>
        public List<SubtitleEntry> Body { get; set; } = new();

        /// <summary>
        /// 原始 URL
        /// </summary>
        public string SourceUrl { get; set; } = string.Empty;
    }
}
