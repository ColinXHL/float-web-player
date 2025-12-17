using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using FloatWebPlayer.Helpers;

namespace FloatWebPlayer.Services
{
    /// <summary>
    /// 内置插件信息（用于插件市场）
    /// </summary>
    public class BuiltInPluginInfo
    {
        /// <summary>
        /// 插件 ID
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// 插件名称
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 插件版本
        /// </summary>
        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// 插件作者
        /// </summary>
        [JsonPropertyName("author")]
        public string Author { get; set; } = string.Empty;

        /// <summary>
        /// 插件描述
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 插件标签
        /// </summary>
        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; } = new();

        /// <summary>
        /// 所需权限列表
        /// </summary>
        [JsonPropertyName("permissions")]
        public List<string> Permissions { get; set; } = new();

        /// <summary>
        /// 推荐的 Profile ID 列表
        /// </summary>
        [JsonPropertyName("profiles")]
        public List<string> Profiles { get; set; } = new();
    }


    /// <summary>
    /// 内置插件索引文件结构
    /// </summary>
    internal class PluginRegistryData
    {
        [JsonPropertyName("version")]
        public int Version { get; set; } = 1;

        [JsonPropertyName("plugins")]
        public List<BuiltInPluginInfo> Plugins { get; set; } = new();
    }

    /// <summary>
    /// 插件注册表服务
    /// 管理内置插件索引（只读）
    /// </summary>
    public class PluginRegistry
    {
        #region Singleton

        private static PluginRegistry? _instance;
        private static readonly object _lock = new();

        /// <summary>
        /// 获取单例实例
        /// </summary>
        public static PluginRegistry Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new PluginRegistry();
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// 重置单例实例（仅用于测试）
        /// </summary>
        internal static void ResetInstance()
        {
            lock (_lock)
            {
                _instance = null;
            }
        }

        #endregion


        #region Properties

        /// <summary>
        /// 内置插件目录（exe 同级的 Plugins/）
        /// </summary>
        public string BuiltInPluginsDirectory { get; }

        /// <summary>
        /// 索引文件路径
        /// </summary>
        private string RegistryFilePath => Path.Combine(BuiltInPluginsDirectory, "registry.json");

        /// <summary>
        /// 缓存的插件列表
        /// </summary>
        private List<BuiltInPluginInfo> _plugins = new();

        /// <summary>
        /// 是否已加载
        /// </summary>
        private bool _isLoaded = false;

        #endregion

        #region Constructor

        private PluginRegistry()
        {
            BuiltInPluginsDirectory = AppPaths.BuiltInPluginsDirectory;
        }

        /// <summary>
        /// 用于测试的构造函数
        /// </summary>
        /// <param name="builtInPluginsDirectory">内置插件目录路径</param>
        internal PluginRegistry(string builtInPluginsDirectory)
        {
            BuiltInPluginsDirectory = builtInPluginsDirectory;
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// 获取所有内置插件信息
        /// </summary>
        /// <returns>插件信息列表</returns>
        public List<BuiltInPluginInfo> GetAllPlugins()
        {
            EnsureLoaded();
            return new List<BuiltInPluginInfo>(_plugins);
        }

        /// <summary>
        /// 根据 ID 获取插件信息
        /// </summary>
        /// <param name="pluginId">插件 ID</param>
        /// <returns>插件信息，不存在时返回 null</returns>
        public BuiltInPluginInfo? GetPlugin(string pluginId)
        {
            if (string.IsNullOrWhiteSpace(pluginId))
                return null;

            EnsureLoaded();
            return _plugins.Find(p => p.Id.Equals(pluginId, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 获取插件源码目录
        /// </summary>
        /// <param name="pluginId">插件 ID</param>
        /// <returns>源码目录路径</returns>
        public string GetPluginSourceDirectory(string pluginId)
        {
            return Path.Combine(BuiltInPluginsDirectory, pluginId);
        }

        /// <summary>
        /// 检查插件是否存在于注册表中
        /// </summary>
        /// <param name="pluginId">插件 ID</param>
        /// <returns>是否存在</returns>
        public bool PluginExists(string pluginId)
        {
            return GetPlugin(pluginId) != null;
        }

        /// <summary>
        /// 重新加载索引
        /// </summary>
        public void Reload()
        {
            _isLoaded = false;
            _plugins.Clear();
            EnsureLoaded();
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// 确保索引已加载
        /// </summary>
        private void EnsureLoaded()
        {
            if (_isLoaded)
                return;

            LoadRegistry();
            _isLoaded = true;
        }

        /// <summary>
        /// 从文件加载索引
        /// </summary>
        private void LoadRegistry()
        {
            _plugins.Clear();

            if (!File.Exists(RegistryFilePath))
            {
                LogService.Instance.Warn("PluginRegistry", $"索引文件不存在: {RegistryFilePath}");
                return;
            }

            try
            {
                var data = JsonHelper.LoadFromFile<PluginRegistryData>(RegistryFilePath);
                if (data?.Plugins != null)
                {
                    _plugins = data.Plugins;
                    LogService.Instance.Debug("PluginRegistry", $"已加载 {_plugins.Count} 个内置插件");
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Error("PluginRegistry", $"加载索引文件失败: {ex.Message}");
            }
        }

        #endregion
    }
}
