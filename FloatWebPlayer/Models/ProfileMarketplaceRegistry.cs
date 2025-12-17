using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using FloatWebPlayer.Helpers;

namespace FloatWebPlayer.Models
{
    /// <summary>
    /// Profile 市场注册表模型
    /// 用于表示订阅源的 Profile 索引
    /// </summary>
    public class ProfileMarketplaceRegistry
    {
        /// <summary>
        /// 注册表版本号
        /// </summary>
        [JsonPropertyName("version")]
        public int Version { get; set; } = 1;

        /// <summary>
        /// 注册表名称
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 注册表描述
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Profile 列表
        /// </summary>
        [JsonPropertyName("profiles")]
        public List<MarketplaceProfileEntry> Profiles { get; set; } = new();

        /// <summary>
        /// 序列化为 JSON 字符串
        /// </summary>
        public string ToJson()
        {
            return JsonHelper.Serialize(this);
        }

        /// <summary>
        /// 从 JSON 字符串反序列化
        /// </summary>
        /// <param name="json">JSON 字符串</param>
        /// <returns>ProfileMarketplaceRegistry 实例，失败返回 null</returns>
        public static ProfileMarketplaceRegistry? FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                return JsonHelper.Deserialize<ProfileMarketplaceRegistry>(json);
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// 市场 Profile 条目
    /// 注册表中的单个 Profile 信息
    /// </summary>
    public class MarketplaceProfileEntry
    {
        /// <summary>
        /// Profile 唯一标识
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Profile 显示名称
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Profile 描述
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 作者名称
        /// </summary>
        [JsonPropertyName("author")]
        public string Author { get; set; } = string.Empty;

        /// <summary>
        /// 目标游戏名称
        /// </summary>
        [JsonPropertyName("targetGame")]
        public string TargetGame { get; set; } = string.Empty;

        /// <summary>
        /// Profile 版本号
        /// </summary>
        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.0.0";

        /// <summary>
        /// 最后更新时间
        /// </summary>
        [JsonPropertyName("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// 包含的插件 ID 列表
        /// </summary>
        [JsonPropertyName("pluginIds")]
        public List<string> PluginIds { get; set; } = new();

        /// <summary>
        /// Profile 配置下载地址
        /// </summary>
        [JsonPropertyName("downloadUrl")]
        public string DownloadUrl { get; set; } = string.Empty;

        /// <summary>
        /// 转换为 MarketplaceProfile
        /// </summary>
        /// <param name="sourceUrl">来源订阅源 URL</param>
        /// <returns>MarketplaceProfile 实例</returns>
        public MarketplaceProfile ToMarketplaceProfile(string sourceUrl)
        {
            return new MarketplaceProfile
            {
                Id = Id,
                Name = Name,
                Description = Description,
                Author = Author,
                TargetGame = TargetGame,
                Version = Version,
                UpdatedAt = UpdatedAt,
                PluginIds = new List<string>(PluginIds),
                SourceUrl = sourceUrl,
                DownloadUrl = DownloadUrl
            };
        }
    }
}
