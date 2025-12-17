using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace FloatWebPlayer.Models
{
    /// <summary>
    /// 已安装插件条目（用于索引文件序列化）
    /// </summary>
    public class InstalledPluginEntry
    {
        /// <summary>
        /// 插件唯一标识
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// 插件版本号
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// 安装时间
        /// </summary>
        public DateTime InstalledAt { get; set; } = DateTime.Now;

        /// <summary>
        /// 安装来源（builtin/remote）
        /// </summary>
        public string Source { get; set; } = "builtin";
    }

    /// <summary>
    /// 全局插件库索引
    /// 对应 library.json 文件，记录所有已安装的插件
    /// </summary>
    public class PluginLibraryIndex
    {
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        /// <summary>
        /// 索引文件版本
        /// </summary>
        public int Version { get; set; } = 1;

        /// <summary>
        /// 已安装插件列表
        /// </summary>
        public List<InstalledPluginEntry> Plugins { get; set; } = new();

        /// <summary>
        /// 从文件加载索引
        /// </summary>
        /// <param name="filePath">索引文件路径</param>
        /// <returns>插件库索引</returns>
        public static PluginLibraryIndex LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath))
                return new PluginLibraryIndex();

            try
            {
                var json = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize<PluginLibraryIndex>(json, _jsonOptions)
                    ?? new PluginLibraryIndex();
            }
            catch
            {
                return new PluginLibraryIndex();
            }
        }

        /// <summary>
        /// 保存索引到文件
        /// </summary>
        /// <param name="filePath">索引文件路径</param>
        public void SaveToFile(string filePath)
        {
            try
            {
                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(this, _jsonOptions);
                File.WriteAllText(filePath, json);
            }
            catch
            {
                // 忽略保存错误
            }
        }
    }
}
