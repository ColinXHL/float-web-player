using System;
using System.Text.Json.Serialization;

namespace FloatWebPlayer.Models
{
    /// <summary>
    /// 已安装插件信息
    /// 存储在全局插件库中的插件元数据
    /// </summary>
    public class InstalledPluginInfo
    {
        /// <summary>
        /// 插件唯一标识
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// 插件显示名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 插件版本号
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// 插件描述
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// 插件作者
        /// </summary>
        public string? Author { get; set; }

        /// <summary>
        /// 安装时间
        /// </summary>
        public DateTime InstalledAt { get; set; } = DateTime.Now;

        /// <summary>
        /// 安装来源（builtin/remote）
        /// </summary>
        public string Source { get; set; } = "builtin";

        /// <summary>
        /// 被引用的 Profile 数量（运行时计算，不序列化）
        /// </summary>
        [JsonIgnore]
        public int ReferenceCount { get; set; }

        /// <summary>
        /// 从插件清单创建已安装插件信息
        /// </summary>
        /// <param name="manifest">插件清单</param>
        /// <param name="source">安装来源</param>
        /// <returns>已安装插件信息</returns>
        public static InstalledPluginInfo FromManifest(PluginManifest manifest, string source = "builtin")
        {
            return new InstalledPluginInfo
            {
                Id = manifest.Id ?? string.Empty,
                Name = manifest.Name ?? string.Empty,
                Version = manifest.Version ?? string.Empty,
                Description = manifest.Description,
                Author = manifest.Author,
                InstalledAt = DateTime.Now,
                Source = source
            };
        }
    }
}
