using System;

namespace AkashaNavigator.Models
{
    /// <summary>
    /// 归档目录模型
    /// 支持多级嵌套的树形结构
    /// </summary>
    public class ArchiveFolder
    {
        /// <summary>
        /// 唯一标识符 (GUID)
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// 目录名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 父目录 ID（null 表示根目录）
        /// </summary>
        public string? ParentId { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 目录图标（可选，用于自定义显示）
        /// </summary>
        public string? Icon { get; set; }

        /// <summary>
        /// 排序顺序（同级目录内的排序）
        /// </summary>
        public int SortOrder { get; set; }
    }
}
