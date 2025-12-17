using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace FloatWebPlayer.Models
{
    /// <summary>
    /// 插件引用条目（用于关联索引文件序列化）
    /// </summary>
    public class PluginReferenceEntry
    {
        /// <summary>
        /// 插件唯一标识
        /// </summary>
        public string PluginId { get; set; } = string.Empty;

        /// <summary>
        /// 是否启用
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// 添加时间
        /// </summary>
        public DateTime AddedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// 从 PluginReference 创建条目
        /// </summary>
        public static PluginReferenceEntry FromReference(PluginReference reference)
        {
            return new PluginReferenceEntry
            {
                PluginId = reference.PluginId,
                Enabled = reference.Enabled,
                AddedAt = reference.AddedAt
            };
        }

        /// <summary>
        /// 转换为 PluginReference
        /// </summary>
        public PluginReference ToReference()
        {
            return new PluginReference
            {
                PluginId = PluginId,
                Enabled = Enabled,
                AddedAt = AddedAt
            };
        }
    }

    /// <summary>
    /// 关联索引
    /// 对应 associations.json 文件，记录 Profile 与插件的关联关系
    /// </summary>
    public class AssociationIndex
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
        /// Profile -> 插件引用列表的映射
        /// Key: Profile ID, Value: 该 Profile 引用的插件列表
        /// </summary>
        public Dictionary<string, List<PluginReferenceEntry>> ProfilePlugins { get; set; } = new();

        /// <summary>
        /// Profile -> 原始插件列表的映射（来自市场定义）
        /// Key: Profile ID, Value: 该 Profile 原始定义的插件 ID 列表
        /// 用于在用户移除插件后仍能显示"缺失"状态
        /// </summary>
        public Dictionary<string, List<string>> OriginalPlugins { get; set; } = new();

        /// <summary>
        /// 从文件加载索引
        /// </summary>
        /// <param name="filePath">索引文件路径</param>
        /// <returns>关联索引</returns>
        public static AssociationIndex LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath))
                return new AssociationIndex();

            try
            {
                var json = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize<AssociationIndex>(json, _jsonOptions)
                    ?? new AssociationIndex();
            }
            catch
            {
                return new AssociationIndex();
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
