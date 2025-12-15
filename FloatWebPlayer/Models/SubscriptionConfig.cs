using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using FloatWebPlayer.Helpers;

namespace FloatWebPlayer.Models
{
    /// <summary>
    /// 订阅配置模型
    /// 用于存储用户订阅的 Profile 和插件信息
    /// 对应 User/Data/subscriptions.json 文件
    /// </summary>
    public class SubscriptionConfig
    {
        #region Properties

        /// <summary>
        /// 配置版本号
        /// </summary>
        [JsonPropertyName("version")]
        public int Version { get; set; } = 1;

        /// <summary>
        /// 已订阅的 Profile ID 列表
        /// </summary>
        [JsonPropertyName("profiles")]
        public List<string> Profiles { get; set; } = new();

        /// <summary>
        /// 插件订阅映射
        /// Key: Profile ID
        /// Value: 该 Profile 订阅的插件 ID 列表
        /// </summary>
        [JsonPropertyName("pluginSubscriptions")]
        public Dictionary<string, List<string>> PluginSubscriptions { get; set; } = new();

        #endregion

        #region File Operations

        /// <summary>
        /// 从文件加载订阅配置
        /// </summary>
        /// <param name="filePath">配置文件路径</param>
        /// <returns>订阅配置实例，文件不存在或加载失败时返回默认配置</returns>
        public static SubscriptionConfig LoadFromFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return new SubscriptionConfig();
            }

            try
            {
                var config = JsonHelper.LoadFromFile<SubscriptionConfig>(filePath);
                return config ?? new SubscriptionConfig();
            }
            catch
            {
                return new SubscriptionConfig();
            }
        }

        /// <summary>
        /// 保存订阅配置到文件
        /// </summary>
        /// <param name="filePath">配置文件路径</param>
        public void SaveToFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return;

            try
            {
                JsonHelper.SaveToFile(filePath, this);
            }
            catch
            {
                // 忽略保存错误
            }
        }

        #endregion

        #region Profile Methods

        /// <summary>
        /// 检查 Profile 是否已订阅
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        /// <returns>是否已订阅</returns>
        public bool IsProfileSubscribed(string profileId)
        {
            return !string.IsNullOrWhiteSpace(profileId) && Profiles.Contains(profileId);
        }

        /// <summary>
        /// 添加 Profile 订阅
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        /// <returns>是否成功添加（已存在返回 false）</returns>
        public bool AddProfile(string profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId) || Profiles.Contains(profileId))
                return false;

            Profiles.Add(profileId);
            
            // 确保插件订阅字典中有对应条目
            if (!PluginSubscriptions.ContainsKey(profileId))
            {
                PluginSubscriptions[profileId] = new List<string>();
            }
            
            return true;
        }

        /// <summary>
        /// 移除 Profile 订阅
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        /// <returns>是否成功移除</returns>
        public bool RemoveProfile(string profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId))
                return false;

            var removed = Profiles.Remove(profileId);
            
            // 同时移除该 Profile 的插件订阅
            PluginSubscriptions.Remove(profileId);
            
            return removed;
        }

        #endregion

        #region Plugin Methods

        /// <summary>
        /// 获取指定 Profile 订阅的插件列表
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        /// <returns>插件 ID 列表，Profile 不存在时返回空列表</returns>
        public List<string> GetSubscribedPlugins(string profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId))
                return new List<string>();

            return PluginSubscriptions.TryGetValue(profileId, out var plugins)
                ? new List<string>(plugins)
                : new List<string>();
        }

        /// <summary>
        /// 检查插件是否已订阅到指定 Profile
        /// </summary>
        /// <param name="pluginId">插件 ID</param>
        /// <param name="profileId">Profile ID</param>
        /// <returns>是否已订阅</returns>
        public bool IsPluginSubscribed(string pluginId, string profileId)
        {
            if (string.IsNullOrWhiteSpace(pluginId) || string.IsNullOrWhiteSpace(profileId))
                return false;

            return PluginSubscriptions.TryGetValue(profileId, out var plugins) 
                && plugins.Contains(pluginId);
        }

        /// <summary>
        /// 添加插件订阅到指定 Profile
        /// </summary>
        /// <param name="pluginId">插件 ID</param>
        /// <param name="profileId">Profile ID</param>
        /// <returns>是否成功添加（已存在返回 false）</returns>
        public bool AddPlugin(string pluginId, string profileId)
        {
            if (string.IsNullOrWhiteSpace(pluginId) || string.IsNullOrWhiteSpace(profileId))
                return false;

            // 确保 Profile 订阅列表存在
            if (!PluginSubscriptions.ContainsKey(profileId))
            {
                PluginSubscriptions[profileId] = new List<string>();
            }

            var plugins = PluginSubscriptions[profileId];
            if (plugins.Contains(pluginId))
                return false;

            plugins.Add(pluginId);
            return true;
        }

        /// <summary>
        /// 从指定 Profile 移除插件订阅
        /// </summary>
        /// <param name="pluginId">插件 ID</param>
        /// <param name="profileId">Profile ID</param>
        /// <returns>是否成功移除</returns>
        public bool RemovePlugin(string pluginId, string profileId)
        {
            if (string.IsNullOrWhiteSpace(pluginId) || string.IsNullOrWhiteSpace(profileId))
                return false;

            if (!PluginSubscriptions.TryGetValue(profileId, out var plugins))
                return false;

            return plugins.Remove(pluginId);
        }

        #endregion
    }
}
