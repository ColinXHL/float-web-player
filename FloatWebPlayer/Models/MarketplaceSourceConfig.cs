using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;
using FloatWebPlayer.Helpers;

namespace FloatWebPlayer.Models
{
    /// <summary>
    /// 市场订阅源配置模型
    /// 用于存储用户配置的 Profile 市场订阅源列表
    /// </summary>
    public class MarketplaceSourceConfig
    {
        /// <summary>
        /// 配置版本号
        /// </summary>
        [JsonPropertyName("version")]
        public int Version { get; set; } = 1;

        /// <summary>
        /// 订阅源列表
        /// </summary>
        [JsonPropertyName("sources")]
        public List<MarketplaceSource> Sources { get; set; } = new();

        /// <summary>
        /// 从文件加载配置
        /// </summary>
        /// <param name="filePath">配置文件路径</param>
        /// <returns>配置实例，文件不存在或加载失败时返回默认配置</returns>
        public static MarketplaceSourceConfig LoadFromFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return new MarketplaceSourceConfig();
            }

            try
            {
                var config = JsonHelper.LoadFromFile<MarketplaceSourceConfig>(filePath);
                return config ?? new MarketplaceSourceConfig();
            }
            catch
            {
                return new MarketplaceSourceConfig();
            }
        }

        /// <summary>
        /// 保存配置到文件
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

        /// <summary>
        /// 添加订阅源
        /// </summary>
        /// <param name="url">订阅源 URL</param>
        /// <param name="name">订阅源名称（可选）</param>
        /// <returns>是否成功添加（已存在返回 false）</returns>
        public bool AddSource(string url, string? name = null)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            // 检查是否已存在
            if (Sources.Exists(s => s.Url.Equals(url, StringComparison.OrdinalIgnoreCase)))
                return false;

            Sources.Add(new MarketplaceSource
            {
                Url = url,
                Name = name ?? string.Empty,
                Enabled = true
            });

            return true;
        }

        /// <summary>
        /// 移除订阅源
        /// </summary>
        /// <param name="url">订阅源 URL</param>
        /// <returns>是否成功移除</returns>
        public bool RemoveSource(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            return Sources.RemoveAll(s => s.Url.Equals(url, StringComparison.OrdinalIgnoreCase)) > 0;
        }

        /// <summary>
        /// 获取启用的订阅源列表
        /// </summary>
        /// <returns>启用的订阅源列表</returns>
        public List<MarketplaceSource> GetEnabledSources()
        {
            return Sources.FindAll(s => s.Enabled);
        }
    }

    /// <summary>
    /// 市场订阅源
    /// </summary>
    public class MarketplaceSource
    {
        /// <summary>
        /// 订阅源 URL
        /// </summary>
        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// 订阅源名称
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 是否启用
        /// </summary>
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// 最后获取时间
        /// </summary>
        [JsonPropertyName("lastFetched")]
        public DateTime? LastFetched { get; set; }
    }
}
