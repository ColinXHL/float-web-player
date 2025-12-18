using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SandronePlayer.Models
{
    /// <summary>
    /// 插件配置模型
    /// 用于存储插件的启用状态和自定义设置
    /// </summary>
    public class PluginConfig
    {
        #region Fields

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        private JsonObject _settings;
        private string? _filePath;

        #endregion

        #region Properties

        /// <summary>
        /// 插件 ID
        /// </summary>
        public string PluginId { get; set; } = string.Empty;

        /// <summary>
        /// 是否启用
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// 配置设置（内部 JSON 对象）
        /// </summary>
        public JsonObject Settings
        {
            get => _settings;
            set => _settings = value ?? new JsonObject();
        }

        #endregion

        #region Constructor

        /// <summary>
        /// 创建新的插件配置
        /// </summary>
        public PluginConfig()
        {
            _settings = new JsonObject();
        }

        /// <summary>
        /// 创建指定插件 ID 的配置
        /// </summary>
        public PluginConfig(string pluginId) : this()
        {
            PluginId = pluginId;
        }

        #endregion

        #region Get/Set Methods

        /// <summary>
        /// 获取配置值
        /// 支持点号分隔的路径，如 "overlay.x"
        /// </summary>
        /// <typeparam name="T">值类型</typeparam>
        /// <param name="key">配置键（支持点号路径）</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns>配置值或默认值</returns>
        public T Get<T>(string key, T defaultValue = default!)
        {
            if (string.IsNullOrWhiteSpace(key))
                return defaultValue;

            try
            {
                var node = GetNodeByPath(key);
                if (node == null)
                    return defaultValue;

                // 尝试转换为目标类型
                return node.Deserialize<T>(_jsonOptions) ?? defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// 设置配置值
        /// 支持点号分隔的路径，如 "overlay.x"
        /// </summary>
        /// <param name="key">配置键（支持点号路径）</param>
        /// <param name="value">配置值</param>
        public void Set(string key, object? value)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            var parts = key.Split('.');
            var current = _settings;

            // 遍历路径，创建中间节点
            for (int i = 0; i < parts.Length - 1; i++)
            {
                var part = parts[i];
                if (!current.ContainsKey(part) || current[part] is not JsonObject)
                {
                    current[part] = new JsonObject();
                }
                current = (JsonObject)current[part]!;
            }

            // 设置最终值
            var lastKey = parts[^1];
            if (value == null)
            {
                current.Remove(lastKey);
            }
            else
            {
                current[lastKey] = JsonSerializer.SerializeToNode(value, _jsonOptions);
            }
        }

        /// <summary>
        /// 检查配置键是否存在
        /// </summary>
        public bool ContainsKey(string key)
        {
            return GetNodeByPath(key) != null;
        }

        /// <summary>
        /// 移除配置键
        /// </summary>
        public bool Remove(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return false;

            var parts = key.Split('.');
            var current = _settings;

            // 遍历到父节点
            for (int i = 0; i < parts.Length - 1; i++)
            {
                var part = parts[i];
                if (!current.ContainsKey(part) || current[part] is not JsonObject obj)
                {
                    return false;
                }
                current = obj;
            }

            return current.Remove(parts[^1]);
        }

        #endregion

        #region File Operations

        /// <summary>
        /// 从文件加载配置
        /// </summary>
        public static PluginConfig LoadFromFile(string filePath, string pluginId)
        {
            var config = new PluginConfig(pluginId)
            {
                _filePath = filePath
            };

            if (!File.Exists(filePath))
                return config;

            try
            {
                var json = File.ReadAllText(filePath);
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("pluginId", out var idProp))
                    config.PluginId = idProp.GetString() ?? pluginId;

                if (root.TryGetProperty("enabled", out var enabledProp))
                    config.Enabled = enabledProp.GetBoolean();

                if (root.TryGetProperty("settings", out var settingsProp))
                {
                    var settingsJson = settingsProp.GetRawText();
                    config._settings = JsonNode.Parse(settingsJson) as JsonObject ?? new JsonObject();
                }
            }
            catch
            {
                // 加载失败时返回默认配置
            }

            return config;
        }

        /// <summary>
        /// 保存配置到文件
        /// </summary>
        public void SaveToFile(string? filePath = null)
        {
            var path = filePath ?? _filePath;
            if (string.IsNullOrEmpty(path))
                throw new InvalidOperationException("未指定配置文件路径");

            _filePath = path;

            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                var configObj = new JsonObject
                {
                    ["pluginId"] = PluginId,
                    ["enabled"] = Enabled,
                    ["settings"] = _settings.DeepClone()
                };

                var json = configObj.ToJsonString(_jsonOptions);
                File.WriteAllText(path, json);
            }
            catch
            {
                // 忽略保存错误
            }
        }

        /// <summary>
        /// 应用默认配置（来自插件清单）
        /// </summary>
        public void ApplyDefaults(Dictionary<string, JsonElement>? defaults)
        {
            if (defaults == null)
            {
                Services.LogService.Instance.Debug("PluginConfig", $"ApplyDefaults: defaults is null for {PluginId}");
                return;
            }

            Services.LogService.Instance.Debug("PluginConfig", $"ApplyDefaults: applying {defaults.Count} defaults for {PluginId}");
            
            foreach (var kvp in defaults)
            {
                // 只在键不存在时应用默认值
                if (!ContainsKey(kvp.Key))
                {
                    var node = JsonNode.Parse(kvp.Value.GetRawText());
                    if (node != null)
                    {
                        SetNodeByPath(kvp.Key, node);
                        Services.LogService.Instance.Debug("PluginConfig", $"  Applied default: {kvp.Key} = {kvp.Value.GetRawText()}");
                    }
                }
                else
                {
                    Services.LogService.Instance.Debug("PluginConfig", $"  Skipped (exists): {kvp.Key}");
                }
            }
            
            Services.LogService.Instance.Debug("PluginConfig", $"ApplyDefaults complete. Settings: {_settings.ToJsonString()}");
        }

        #endregion

        #region Private Methods

        private JsonNode? GetNodeByPath(string path)
        {
            var parts = path.Split('.');
            JsonNode? current = _settings;

            foreach (var part in parts)
            {
                if (current is JsonObject obj && obj.ContainsKey(part))
                {
                    current = obj[part];
                }
                else
                {
                    return null;
                }
            }

            return current;
        }

        private void SetNodeByPath(string path, JsonNode value)
        {
            var parts = path.Split('.');
            var current = _settings;

            for (int i = 0; i < parts.Length - 1; i++)
            {
                var part = parts[i];
                if (!current.ContainsKey(part) || current[part] is not JsonObject)
                {
                    current[part] = new JsonObject();
                }
                current = (JsonObject)current[part]!;
            }

            current[parts[^1]] = value.DeepClone();
        }

        #endregion
    }
}
