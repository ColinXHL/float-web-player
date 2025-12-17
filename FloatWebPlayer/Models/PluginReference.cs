using System;
using System.Text.Json.Serialization;

namespace FloatWebPlayer.Models
{
    /// <summary>
    /// 插件安装状态枚举
    /// </summary>
    public enum PluginInstallStatus
    {
        /// <summary>
        /// 已安装 - 插件存在于全局插件库中
        /// </summary>
        Installed,

        /// <summary>
        /// 缺失 - Profile 引用但未安装到全局插件库
        /// </summary>
        Missing,

        /// <summary>
        /// 已禁用 - 插件已安装但在当前 Profile 中被禁用
        /// </summary>
        Disabled
    }

    /// <summary>
    /// 插件引用模型
    /// Profile 中的插件清单项，仅存储引用信息而非插件本体
    /// </summary>
    public class PluginReference
    {
        /// <summary>
        /// 插件唯一标识
        /// </summary>
        public string PluginId { get; set; } = string.Empty;

        /// <summary>
        /// 是否在当前 Profile 中启用
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// 添加到 Profile 的时间
        /// </summary>
        public DateTime AddedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// 安装状态（运行时计算，不序列化）
        /// </summary>
        [JsonIgnore]
        public PluginInstallStatus Status { get; set; } = PluginInstallStatus.Installed;

        /// <summary>
        /// 创建新的插件引用
        /// </summary>
        public PluginReference()
        {
        }

        /// <summary>
        /// 创建指定插件 ID 的引用
        /// </summary>
        /// <param name="pluginId">插件 ID</param>
        /// <param name="enabled">是否启用</param>
        public PluginReference(string pluginId, bool enabled = true)
        {
            PluginId = pluginId;
            Enabled = enabled;
            AddedAt = DateTime.Now;
        }
    }
}
