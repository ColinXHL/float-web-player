using System;

namespace SandronePlayer.Models
{
    /// <summary>
    /// 插件更新检查结果
    /// 用于表示单个插件的更新检查状态
    /// </summary>
    public class UpdateCheckResult
    {
        /// <summary>
        /// 插件唯一标识
        /// </summary>
        public string PluginId { get; set; } = string.Empty;

        /// <summary>
        /// 当前已安装版本
        /// </summary>
        public string CurrentVersion { get; set; } = string.Empty;

        /// <summary>
        /// 可用的新版本（如果有更新）
        /// </summary>
        public string? AvailableVersion { get; set; }

        /// <summary>
        /// 是否有可用更新
        /// </summary>
        public bool HasUpdate { get; set; }

        /// <summary>
        /// 更新源路径（内置插件目录路径）
        /// </summary>
        public string? SourcePath { get; set; }

        /// <summary>
        /// 创建无更新的结果
        /// </summary>
        /// <param name="pluginId">插件 ID</param>
        /// <param name="currentVersion">当前版本</param>
        /// <returns>无更新的检查结果</returns>
        public static UpdateCheckResult NoUpdate(string pluginId, string currentVersion)
        {
            return new UpdateCheckResult
            {
                PluginId = pluginId,
                CurrentVersion = currentVersion,
                HasUpdate = false
            };
        }

        /// <summary>
        /// 创建有更新的结果
        /// </summary>
        /// <param name="pluginId">插件 ID</param>
        /// <param name="currentVersion">当前版本</param>
        /// <param name="availableVersion">可用新版本</param>
        /// <param name="sourcePath">更新源路径</param>
        /// <returns>有更新的检查结果</returns>
        public static UpdateCheckResult WithUpdate(string pluginId, string currentVersion, string availableVersion, string sourcePath)
        {
            return new UpdateCheckResult
            {
                PluginId = pluginId,
                CurrentVersion = currentVersion,
                AvailableVersion = availableVersion,
                HasUpdate = true,
                SourcePath = sourcePath
            };
        }
    }
}
