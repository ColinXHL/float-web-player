using System;
using System.IO;
using System.Text.Json;

namespace FloatWebPlayer.Helpers
{
    /// <summary>
    /// JSON 序列化辅助类
    /// 提供统一的 JSON 序列化/反序列化配置和便捷方法
    /// </summary>
    public static class JsonHelper
    {
        /// <summary>
        /// 用于读取的选项（大小写不敏感）
        /// </summary>
        public static JsonSerializerOptions ReadOptions { get; } = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// 用于写入的选项（缩进格式）
        /// </summary>
        public static JsonSerializerOptions WriteOptions { get; } = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        /// <summary>
        /// 反序列化 JSON 字符串
        /// </summary>
        /// <typeparam name="T">目标类型</typeparam>
        /// <param name="json">JSON 字符串</param>
        /// <returns>反序列化后的对象，失败返回 default</returns>
        public static T? Deserialize<T>(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return default;

            return JsonSerializer.Deserialize<T>(json, ReadOptions);
        }

        /// <summary>
        /// 序列化对象为 JSON 字符串
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="obj">要序列化的对象</param>
        /// <returns>JSON 字符串</returns>
        public static string Serialize<T>(T obj)
        {
            return JsonSerializer.Serialize(obj, WriteOptions);
        }

        /// <summary>
        /// 从文件加载并反序列化 JSON
        /// </summary>
        /// <typeparam name="T">目标类型</typeparam>
        /// <param name="filePath">文件路径</param>
        /// <returns>反序列化后的对象，文件不存在或失败返回 default</returns>
        public static T? LoadFromFile<T>(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return default;

            var json = File.ReadAllText(filePath);
            return Deserialize<T>(json);
        }

        /// <summary>
        /// 序列化对象并保存到文件
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="filePath">文件路径</param>
        /// <param name="obj">要保存的对象</param>
        public static void SaveToFile<T>(string filePath, T obj)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return;

            // 确保目录存在
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = Serialize(obj);
            File.WriteAllText(filePath, json);
        }
    }
}
