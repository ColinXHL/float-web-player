using System;
using System.Collections.Generic;

namespace AkashaNavigator.Models
{
    /// <summary>
    /// 归档项模型
    /// 用于 JSON 存储用户归档的页面/视频
    /// </summary>
    public class ArchiveItem
    {
        /// <summary>
        /// 唯一标识符 (GUID)
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// 归档标题（用户自定义，如 "UID12345-亚洲服"）
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// 页面 URL
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// 所属目录 ID（null 表示根目录）
        /// </summary>
        public string? FolderId { get; set; }

        /// <summary>
        /// 归档时间
        /// </summary>
        public DateTime ArchivedTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 扩展数据（预留，便于未来与其他模块联动）
        /// 例如：关联的 Profile ID、标签、备注等
        /// </summary>
        public Dictionary<string, string>? Metadata { get; set; }
    }
}
