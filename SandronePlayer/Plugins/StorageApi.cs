using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace SandronePlayer.Plugins
{
    /// <summary>
    /// 数据存储 API
    /// 提供插件数据持久化功能
    /// 需要 "storage" 权限
    /// 存储路径：{ProfileDirectory}/plugins/{PluginId}/storage/{key}.json
    /// </summary>
    public class StorageApi
    {
        #region Fields

        private readonly PluginContext _context;
        private readonly string _storageDirectory;
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        #endregion

        #region Constructor

        /// <summary>
        /// 创建存储 API 实例
        /// </summary>
        /// <param name="context">插件上下文</param>
        public StorageApi(PluginContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _storageDirectory = Path.Combine(context.PluginDirectory, "storage");
        }

        #endregion

        #region Storage Methods

        /// <summary>
        /// 保存数据
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="data">数据对象</param>
        /// <returns>是否成功</returns>
        public bool Save(string key, object data)
        {
            if (!ValidateKey(key))
                return false;

            try
            {
                EnsureStorageDirectory();
                var filePath = GetFilePath(key);
                var json = JsonSerializer.Serialize(data, _jsonOptions);
                File.WriteAllText(filePath, json);
                return true;
            }
            catch (Exception ex)
            {
                Services.LogService.Instance.Error($"Plugin:{_context.PluginId}", $"StorageApi.Save failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 加载数据
        /// </summary>
        /// <param name="key">键名</param>
        /// <returns>数据对象，不存在或失败返回 null</returns>
        public object? Load(string key)
        {
            if (!ValidateKey(key))
                return null;

            try
            {
                var filePath = GetFilePath(key);
                if (!File.Exists(filePath))
                    return null;

                var json = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize<JsonElement>(json, _jsonOptions);
            }
            catch (Exception ex)
            {
                Services.LogService.Instance.Error($"Plugin:{_context.PluginId}", $"StorageApi.Load failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 删除数据
        /// </summary>
        /// <param name="key">键名</param>
        /// <returns>是否成功</returns>
        public bool Delete(string key)
        {
            if (!ValidateKey(key))
                return false;

            try
            {
                var filePath = GetFilePath(key);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
                return true;
            }
            catch (Exception ex)
            {
                Services.LogService.Instance.Error($"Plugin:{_context.PluginId}", $"StorageApi.Delete failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 检查数据是否存在
        /// </summary>
        /// <param name="key">键名</param>
        /// <returns>是否存在</returns>
        public bool Exists(string key)
        {
            if (!ValidateKey(key))
                return false;

            var filePath = GetFilePath(key);
            return File.Exists(filePath);
        }

        /// <summary>
        /// 列出所有存储的键名
        /// </summary>
        /// <returns>键名数组</returns>
        public string[] List()
        {
            try
            {
                if (!Directory.Exists(_storageDirectory))
                    return Array.Empty<string>();

                var files = Directory.GetFiles(_storageDirectory, "*.json");
                var keys = new List<string>();
                foreach (var file in files)
                {
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    keys.Add(fileName);
                }
                return keys.ToArray();
            }
            catch (Exception ex)
            {
                Services.LogService.Instance.Error($"Plugin:{_context.PluginId}", $"StorageApi.List failed: {ex.Message}");
                return Array.Empty<string>();
            }
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// 清理资源（插件卸载时调用）
        /// </summary>
        internal void Cleanup()
        {
            // StorageApi 没有需要清理的缓存或引用
            // 文件系统操作不需要特殊清理
            Services.LogService.Instance.Debug($"Plugin:{_context.PluginId}", "StorageApi: cleaned up");
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// 验证键名是否有效
        /// </summary>
        private bool ValidateKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                Services.LogService.Instance.Warn($"Plugin:{_context.PluginId}", "StorageApi: key cannot be empty");
                return false;
            }

            // 检查是否包含非法字符
            var invalidChars = Path.GetInvalidFileNameChars();
            foreach (var c in key)
            {
                if (Array.IndexOf(invalidChars, c) >= 0)
                {
                    Services.LogService.Instance.Warn($"Plugin:{_context.PluginId}", $"StorageApi: key contains invalid character '{c}'");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 获取存储文件路径
        /// </summary>
        private string GetFilePath(string key)
        {
            return Path.Combine(_storageDirectory, $"{key}.json");
        }

        /// <summary>
        /// 确保存储目录存在
        /// </summary>
        private void EnsureStorageDirectory()
        {
            if (!Directory.Exists(_storageDirectory))
            {
                Directory.CreateDirectory(_storageDirectory);
            }
        }

        #endregion
    }
}
