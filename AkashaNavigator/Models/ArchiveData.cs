using System.Collections.Generic;

namespace AkashaNavigator.Models
{
    /// <summary>
    /// 归档数据容器
    /// 用于 JSON 序列化整个归档结构
    /// </summary>
    public class ArchiveData
    {
        /// <summary>
        /// 所有归档目录
        /// </summary>
        public List<ArchiveFolder> Folders { get; set; } = new();

        /// <summary>
        /// 所有归档项目
        /// </summary>
        public List<ArchiveItem> Items { get; set; } = new();

        /// <summary>
        /// 当前排序方向
        /// </summary>
        public SortDirection SortOrder { get; set; } = SortDirection.Descending;

        /// <summary>
        /// 数据版本（用于未来数据迁移）
        /// </summary>
        public int Version { get; set; } = 1;
    }

    /// <summary>
    /// 排序方向枚举
    /// </summary>
    public enum SortDirection
    {
        /// <summary>
        /// 升序（旧的在前）
        /// </summary>
        Ascending,

        /// <summary>
        /// 降序（新的在前，默认）
        /// </summary>
        Descending
    }
}
