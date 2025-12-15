using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FloatWebPlayer.Helpers;
using FloatWebPlayer.Models;
using FloatWebPlayer.Plugins;

namespace FloatWebPlayer.Services
{
    /// <summary>
    /// 插件宿主服务
    /// 负责插件的加载、执行和生命周期管理
    /// </summary>
    public class PluginHost : IDisposable
    {
        #region Singleton

        private static PluginHost? _instance;
        private static readonly object _lock = new();

        /// <summary>
        /// 获取单例实例
        /// </summary>
        public static PluginHost Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new PluginHost();
                    }
                }
                return _instance;
            }
        }

        #endregion

        #region Events

        /// <summary>
        /// 插件加载完成事件
        /// </summary>
        public event EventHandler<PluginContext>? PluginLoaded;

        /// <summary>
        /// 插件卸载事件
        /// </summary>
        public event EventHandler<string>? PluginUnloaded;

        #endregion


        #region Fields

        private readonly List<PluginContext> _loadedPlugins = new();
        private readonly Dictionary<string, PluginConfig> _pluginConfigs = new();
        private readonly Dictionary<string, PluginApi> _pluginApis = new();
        private string? _currentProfileId;
        private bool _disposed;

        #endregion

        #region Properties

        /// <summary>
        /// 已加载的插件列表
        /// </summary>
        public IReadOnlyList<PluginContext> LoadedPlugins => _loadedPlugins.AsReadOnly();

        /// <summary>
        /// 当前 Profile ID
        /// </summary>
        public string? CurrentProfileId => _currentProfileId;

        #endregion

        #region Constructor

        private PluginHost()
        {
            // 私有构造函数，单例模式
        }

        /// <summary>
        /// 用于测试的内部构造函数
        /// </summary>
        internal PluginHost(bool forTesting)
        {
            // 测试用构造函数
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 加载指定 Profile 的所有插件
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        public void LoadPluginsForProfile(string profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId))
                return;

            // 如果已有插件加载，先卸载
            if (_loadedPlugins.Count > 0)
            {
                UnloadAllPlugins();
            }

            _currentProfileId = profileId;

            // 从 SubscriptionManager 获取订阅的插件列表
            var subscribedPlugins = SubscriptionManager.Instance.GetSubscribedPlugins(profileId);
            if (subscribedPlugins.Count == 0)
            {
                Log($"Profile '{profileId}' 没有订阅任何插件");
                return;
            }

            // 遍历订阅的插件
            foreach (var pluginId in subscribedPlugins)
            {
                // 从 PluginRegistry 获取插件源码目录
                var sourceDir = PluginRegistry.Instance.GetPluginSourceDirectory(pluginId);
                
                // 获取用户配置目录
                var configDir = GetPluginConfigDirectory(profileId, pluginId);
                
                // 加载插件
                LoadPlugin(sourceDir, configDir, pluginId);
            }

            Log($"已加载 {_loadedPlugins.Count} 个插件 (Profile: {profileId})");
        }

        /// <summary>
        /// 卸载所有插件
        /// </summary>
        public void UnloadAllPlugins()
        {
            foreach (var plugin in _loadedPlugins.ToList())
            {
                UnloadPlugin(plugin);
            }

            _loadedPlugins.Clear();
            _pluginConfigs.Clear();
            _pluginApis.Clear();
            _currentProfileId = null;

            Log("已卸载所有插件");
        }

        /// <summary>
        /// 启用或禁用插件
        /// </summary>
        /// <param name="pluginId">插件 ID</param>
        /// <param name="enabled">是否启用</param>
        public void SetPluginEnabled(string pluginId, bool enabled)
        {
            var plugin = _loadedPlugins.FirstOrDefault(p => p.PluginId == pluginId);
            if (plugin == null)
                return;

            if (plugin.IsEnabled == enabled)
                return;

            plugin.IsEnabled = enabled;

            // 更新配置（使用配置目录）
            if (_pluginConfigs.TryGetValue(pluginId, out var config))
            {
                config.Enabled = enabled;
                SavePluginConfig(config, plugin.ConfigDirectory);
            }

            Log($"插件 {pluginId} 已{(enabled ? "启用" : "禁用")}");
        }

        /// <summary>
        /// 根据 ID 获取插件
        /// </summary>
        public PluginContext? GetPlugin(string pluginId)
        {
            return _loadedPlugins.FirstOrDefault(p => p.PluginId == pluginId);
        }

        /// <summary>
        /// 获取插件配置
        /// </summary>
        /// <param name="pluginId">插件 ID</param>
        /// <returns>插件配置，不存在则返回 null</returns>
        public PluginConfig? GetPluginConfig(string pluginId)
        {
            return _pluginConfigs.TryGetValue(pluginId, out var config) ? config : null;
        }

        /// <summary>
        /// 保存插件配置
        /// </summary>
        /// <param name="pluginId">插件 ID</param>
        public void SavePluginConfig(string pluginId)
        {
            var plugin = GetPlugin(pluginId);
            if (plugin == null) return;

            if (_pluginConfigs.TryGetValue(pluginId, out var config))
            {
                SavePluginConfig(config, plugin.ConfigDirectory);
            }
        }

        /// <summary>
        /// 广播事件到所有启用的插件
        /// </summary>
        /// <param name="eventName">事件名称</param>
        /// <param name="data">事件数据</param>
        public void BroadcastEvent(string eventName, object data)
        {
            foreach (var kvp in _pluginApis)
            {
                var pluginId = kvp.Key;
                var pluginApi = kvp.Value;
                
                // 检查插件是否启用
                var plugin = GetPlugin(pluginId);
                if (plugin == null || !plugin.IsEnabled)
                    continue;

                // 检查插件是否有 events 权限
                if (!pluginApi.HasPermission(PluginPermissions.Events))
                    continue;

                // 触发事件
                try
                {
                    pluginApi.Event?.Emit(eventName, data);
                }
                catch (Exception ex)
                {
                    Log($"广播事件 {eventName} 到插件 {pluginId} 失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 广播播放状态变化事件
        /// </summary>
        /// <param name="playing">是否正在播放</param>
        public void BroadcastPlayStateChanged(bool playing)
        {
            BroadcastEvent(Plugins.EventApi.PlayStateChanged, new { playing });
        }

        /// <summary>
        /// 广播时间更新事件
        /// </summary>
        /// <param name="currentTime">当前时间（秒）</param>
        /// <param name="duration">总时长（秒）</param>
        public void BroadcastTimeUpdate(double currentTime, double duration)
        {
            BroadcastEvent(Plugins.EventApi.TimeUpdate, new { currentTime, duration });
        }

        /// <summary>
        /// 广播 URL 变化事件
        /// </summary>
        /// <param name="url">新 URL</param>
        public void BroadcastUrlChanged(string url)
        {
            BroadcastEvent(Plugins.EventApi.UrlChanged, new { url });
        }

        /// <summary>
        /// 取消订阅插件（停止运行并删除插件目录）
        /// </summary>
        /// <param name="pluginId">插件 ID</param>
        /// <returns>操作结果</returns>
        public UnsubscribeResult UnsubscribePlugin(string pluginId)
        {
            if (string.IsNullOrWhiteSpace(pluginId))
            {
                return UnsubscribeResult.Failed("插件 ID 不能为空");
            }

            // 查找插件
            var plugin = _loadedPlugins.FirstOrDefault(p => p.PluginId == pluginId);
            if (plugin == null)
            {
                // 插件不存在，静默成功（可能已被卸载）
                Log($"插件 {pluginId} 不存在，跳过取消订阅");
                return UnsubscribeResult.Succeeded();
            }

            var pluginDir = plugin.PluginDirectory;

            try
            {
                // 停止插件运行（调用 onUnload）
                UnloadPlugin(plugin);

                // 从列表移除
                _loadedPlugins.Remove(plugin);

                // 从配置字典移除
                _pluginConfigs.Remove(pluginId);

                // 删除插件目录
                if (Directory.Exists(pluginDir))
                {
                    Directory.Delete(pluginDir, recursive: true);
                    Log($"已删除插件目录: {pluginDir}");
                }

                Log($"插件 {pluginId} 已取消订阅");
                return UnsubscribeResult.Succeeded();
            }
            catch (UnauthorizedAccessException ex)
            {
                Log($"删除插件目录失败（权限不足）: {ex.Message}");
                return UnsubscribeResult.Failed($"删除插件目录失败：权限不足。请确保没有其他程序正在使用该目录。");
            }
            catch (IOException ex)
            {
                Log($"删除插件目录失败（文件被占用）: {ex.Message}");
                return UnsubscribeResult.Failed($"删除插件目录失败：文件被占用。请关闭相关程序后重试。");
            }
            catch (Exception ex)
            {
                Log($"取消订阅插件失败: {ex.Message}");
                return UnsubscribeResult.Failed($"取消订阅失败：{ex.Message}");
            }
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// 加载单个插件（新目录结构：源码目录 + 配置目录分离）
        /// </summary>
        /// <param name="sourceDir">插件源码目录（内置插件库，只读）</param>
        /// <param name="configDir">插件配置目录（用户数据目录，可写）</param>
        /// <param name="pluginId">插件 ID</param>
        private void LoadPlugin(string sourceDir, string configDir, string pluginId)
        {
            // 检查源码目录是否存在
            if (!Directory.Exists(sourceDir))
            {
                Log($"插件源码目录不存在 ({pluginId}): {sourceDir}");
                return;
            }

            var manifestPath = Path.Combine(sourceDir, AppConstants.PluginManifestFileName);
            
            // 加载清单
            var loadResult = PluginManifest.LoadFromFile(manifestPath);
            if (!loadResult.IsSuccess)
            {
                Log($"加载插件清单失败 ({pluginId}): {loadResult.ErrorMessage}");
                return;
            }

            var manifest = loadResult.Manifest!;

            // 检查是否已加载同 ID 插件
            if (_loadedPlugins.Any(p => p.PluginId == manifest.Id))
            {
                Log($"插件 {manifest.Id} 已加载，跳过");
                return;
            }

            // 确保配置目录存在
            if (!Directory.Exists(configDir))
            {
                Directory.CreateDirectory(configDir);
            }

            // 从用户配置目录加载配置
            var configPath = Path.Combine(configDir, AppConstants.PluginConfigFileName);
            var config = PluginConfig.LoadFromFile(configPath, manifest.Id!);
            
            // 应用默认配置
            config.ApplyDefaults(manifest.DefaultConfig);
            _pluginConfigs[manifest.Id!] = config;

            // 如果插件被禁用，跳过加载
            if (!config.Enabled)
            {
                Log($"插件 {manifest.Id} 已禁用，跳过加载");
                return;
            }

            // 创建插件上下文（使用源码目录）
            var context = new PluginContext(manifest, sourceDir)
            {
                IsEnabled = config.Enabled,
                ConfigDirectory = configDir  // 设置配置目录
            };

            // 加载脚本
            if (!context.LoadScript())
            {
                Log($"加载插件脚本失败 ({manifest.Id}): {context.LastError}");
                context.Dispose();
                return;
            }

            // 创建 PluginApi 并传入 onLoad
            var profileInfo = new ProfileInfo(
                _currentProfileId ?? string.Empty,
                _currentProfileId ?? string.Empty,
                GetPluginConfigDirectory(_currentProfileId ?? string.Empty, pluginId)
            );
            var pluginApi = new PluginApi(context, config, profileInfo);

            // 调用 onLoad
            if (!context.CallOnLoad(pluginApi))
            {
                Log($"插件 {manifest.Id} onLoad 调用失败: {context.LastError}");
                // 即使 onLoad 失败，也保留插件（异常隔离）
            }

            _loadedPlugins.Add(context);
            _pluginApis[manifest.Id!] = pluginApi;
            PluginLoaded?.Invoke(this, context);

            Log($"插件 {manifest.Name} (v{manifest.Version}) 加载成功");
        }

        /// <summary>
        /// 加载单个插件（旧方法，保留向后兼容）
        /// </summary>
        /// <param name="pluginDir">插件目录（源码和配置在同一目录）</param>
        [Obsolete("使用 LoadPlugin(sourceDir, configDir, pluginId) 代替")]
        private void LoadPluginLegacy(string pluginDir)
        {
            var manifestPath = Path.Combine(pluginDir, AppConstants.PluginManifestFileName);
            
            // 加载清单
            var loadResult = PluginManifest.LoadFromFile(manifestPath);
            if (!loadResult.IsSuccess)
            {
                Log($"加载插件清单失败 ({pluginDir}): {loadResult.ErrorMessage}");
                return;
            }

            var manifest = loadResult.Manifest!;

            // 检查是否已加载同 ID 插件
            if (_loadedPlugins.Any(p => p.PluginId == manifest.Id))
            {
                Log($"插件 {manifest.Id} 已加载，跳过");
                return;
            }

            // 加载配置
            var configPath = Path.Combine(pluginDir, AppConstants.PluginConfigFileName);
            var config = PluginConfig.LoadFromFile(configPath, manifest.Id!);
            
            // 应用默认配置
            config.ApplyDefaults(manifest.DefaultConfig);
            _pluginConfigs[manifest.Id!] = config;

            // 如果插件被禁用，跳过加载
            if (!config.Enabled)
            {
                Log($"插件 {manifest.Id} 已禁用，跳过加载");
                return;
            }

            // 创建插件上下文
            var context = new PluginContext(manifest, pluginDir)
            {
                IsEnabled = config.Enabled
            };

            // 加载脚本
            if (!context.LoadScript())
            {
                Log($"加载插件脚本失败 ({manifest.Id}): {context.LastError}");
                context.Dispose();
                return;
            }

            // 创建 PluginApi 并传入 onLoad
            var profileInfo = new ProfileInfo(
                _currentProfileId ?? string.Empty,
                _currentProfileId ?? string.Empty,
                GetPluginsDirectory(_currentProfileId ?? string.Empty)
            );
            var pluginApi = new PluginApi(context, config, profileInfo);

            // 调用 onLoad
            if (!context.CallOnLoad(pluginApi))
            {
                Log($"插件 {manifest.Id} onLoad 调用失败: {context.LastError}");
                // 即使 onLoad 失败，也保留插件（异常隔离）
            }

            _loadedPlugins.Add(context);
            _pluginApis[manifest.Id!] = pluginApi;
            PluginLoaded?.Invoke(this, context);

            Log($"插件 {manifest.Name} (v{manifest.Version}) 加载成功");
        }

        /// <summary>
        /// 卸载单个插件
        /// </summary>
        private void UnloadPlugin(PluginContext plugin)
        {
            var pluginId = plugin.PluginId;

            try
            {
                // 调用 onUnload
                plugin.CallOnUnload();
            }
            catch (Exception ex)
            {
                Log($"插件 {pluginId} onUnload 调用失败: {ex.Message}");
                // 继续执行清理流程
            }

            // 清理 PluginApi
            if (_pluginApis.TryGetValue(pluginId, out var pluginApi))
            {
                try
                {
                    pluginApi.Cleanup();
                }
                catch (Exception ex)
                {
                    Log($"清理插件 API 失败 ({pluginId}): {ex.Message}");
                    // 继续执行清理流程
                }
            }

            try
            {
                // 释放资源
                plugin.Dispose();
            }
            catch (Exception ex)
            {
                Log($"释放插件资源失败 ({pluginId}): {ex.Message}");
                // 继续执行清理流程
            }

            // 从 API 字典移除
            _pluginApis.Remove(pluginId);

            PluginUnloaded?.Invoke(this, pluginId);
            Log($"插件 {pluginId} 已卸载");
        }

        /// <summary>
        /// 获取 Profile 的插件目录（旧方法，保留向后兼容）
        /// </summary>
        private string GetPluginsDirectory(string profileId)
        {
            return Path.Combine(AppPaths.ProfilesDirectory, profileId, AppConstants.PluginsDirectoryName);
        }

        /// <summary>
        /// 获取插件用户配置目录
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        /// <param name="pluginId">插件 ID</param>
        /// <returns>配置目录路径</returns>
        public string GetPluginConfigDirectory(string profileId, string pluginId)
        {
            return Path.Combine(AppPaths.ProfilesDirectory, profileId, AppConstants.PluginsDirectoryName, pluginId);
        }

        /// <summary>
        /// 保存插件配置
        /// </summary>
        private void SavePluginConfig(PluginConfig config, string configDir)
        {
            var configPath = Path.Combine(configDir, AppConstants.PluginConfigFileName);
            config.SaveToFile(configPath);
        }

        /// <summary>
        /// 记录日志
        /// </summary>
        private void Log(string message)
        {
            LogService.Instance.Info("PluginHost", message);
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                UnloadAllPlugins();
            }

            _disposed = true;
        }

        /// <summary>
        /// 析构函数
        /// </summary>
        ~PluginHost()
        {
            Dispose(false);
        }

        #endregion
    }
}
