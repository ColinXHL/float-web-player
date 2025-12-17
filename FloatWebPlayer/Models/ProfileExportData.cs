using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace FloatWebPlayer.Models
{
    /// <summary>
    /// Profile 导出数据
    /// 轻量化格式，仅包含插件引用清单和配置，不含插件本体
    /// </summary>
    public class ProfileExportData
    {
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        /// <summary>
        /// 导出格式版本
        /// </summary>
        public int Version { get; set; } = 1;

        /// <summary>
        /// Profile 唯一标识
        /// </summary>
        public string ProfileId { get; set; } = string.Empty;

        /// <summary>
        /// Profile 显示名称
        /// </summary>
        public string ProfileName { get; set; } = string.Empty;

        /// <summary>
        /// Profile 配置
        /// </summary>
        public GameProfile ProfileConfig { get; set; } = new();

        /// <summary>
        /// 插件引用清单
        /// </summary>
        public List<PluginReferenceEntry> PluginReferences { get; set; } = new();

        /// <summary>
        /// 插件配置（pluginId -> config）
        /// </summary>
        public Dictionary<string, Dictionary<string, object>> PluginConfigs { get; set; } = new();

        /// <summary>
        /// 导出时间
        /// </summary>
        public DateTime ExportedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// 序列化为 JSON 字符串
        /// </summary>
        public string ToJson()
        {
            return JsonSerializer.Serialize(this, _jsonOptions);
        }

        /// <summary>
        /// 保存到文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        public void SaveToFile(string filePath)
        {
            try
            {
                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                var json = ToJson();
                File.WriteAllText(filePath, json);
            }
            catch
            {
                // 忽略保存错误
            }
        }

        /// <summary>
        /// 从 JSON 字符串加载
        /// </summary>
        /// <param name="json">JSON 字符串</param>
        /// <returns>导出数据或 null</returns>
        public static ProfileExportData? FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                return JsonSerializer.Deserialize<ProfileExportData>(json, _jsonOptions);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 从文件加载
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>导出数据或 null</returns>
        public static ProfileExportData? LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath))
                return null;

            try
            {
                var json = File.ReadAllText(filePath);
                return FromJson(json);
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Profile 导入结果
    /// </summary>
    public class ProfileImportResult
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// 错误消息（失败时有值）
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 导入的 Profile ID
        /// </summary>
        public string? ProfileId { get; set; }

        /// <summary>
        /// 缺失的插件 ID 列表
        /// </summary>
        public List<string> MissingPlugins { get; set; } = new();

        /// <summary>
        /// 是否存在同名 Profile
        /// </summary>
        public bool ProfileExists { get; set; }

        /// <summary>
        /// 创建成功结果
        /// </summary>
        public static ProfileImportResult Success(string profileId, List<string>? missingPlugins = null)
        {
            return new ProfileImportResult
            {
                IsSuccess = true,
                ProfileId = profileId,
                MissingPlugins = missingPlugins ?? new List<string>()
            };
        }

        /// <summary>
        /// 创建失败结果
        /// </summary>
        public static ProfileImportResult Failure(string errorMessage)
        {
            return new ProfileImportResult
            {
                IsSuccess = false,
                ErrorMessage = errorMessage
            };
        }

        /// <summary>
        /// 创建 Profile 已存在结果
        /// </summary>
        public static ProfileImportResult Exists(string profileId)
        {
            return new ProfileImportResult
            {
                IsSuccess = false,
                ProfileId = profileId,
                ProfileExists = true,
                ErrorMessage = $"Profile '{profileId}' 已存在"
            };
        }
    }
}
