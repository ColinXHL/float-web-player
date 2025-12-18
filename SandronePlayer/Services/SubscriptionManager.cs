using System;
using System.Collections.Generic;
using System.IO;
using SandronePlayer.Helpers;
using SandronePlayer.Models;

namespace SandronePlayer.Services
{
    /// <summary>
    /// 订阅管理服务（单例）
    /// 管理用户的 Profile 和插件订阅
    /// </summary>
    public class SubscriptionManager
    {
        #region Singleton

        private static SubscriptionManager? _instance;
        private static readonly object _lock = new();

        /// <summary>
        /// 获取单例实例
        /// </summary>
        public static SubscriptionManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new SubscriptionManager();
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
        /// 订阅配置文件路径
        /// </summary>
        public string SubscriptionsFilePath { get; }

        /// <summary>
        /// 用户 Profiles 目录
        /// </summary>
        public string UserProfilesDirectory { get; }

        /// <summary>
        /// 订阅配置
        /// </summary>
        private SubscriptionConfig _config;

        /// <summary>
        /// 是否已加载
        /// </summary>
        private bool _isLoaded = false;

        #endregion

        #region Constructor

        private SubscriptionManager()
        {
            SubscriptionsFilePath = AppPaths.SubscriptionsFilePath;
            UserProfilesDirectory = AppPaths.ProfilesDirectory;
            _config = new SubscriptionConfig();
        }

        /// <summary>
        /// 用于测试的构造函数
        /// </summary>
        /// <param name="subscriptionsFilePath">订阅配置文件路径</param>
        /// <param name="userProfilesDirectory">用户 Profiles 目录</param>
        internal SubscriptionManager(string subscriptionsFilePath, string userProfilesDirectory)
        {
            SubscriptionsFilePath = subscriptionsFilePath;
            UserProfilesDirectory = userProfilesDirectory;
            _config = new SubscriptionConfig();
        }

        #endregion

        #region Load/Save Methods

        /// <summary>
        /// 加载订阅配置
        /// </summary>
        public void Load()
        {
            try
            {
                _config = SubscriptionConfig.LoadFromFile(SubscriptionsFilePath);
                _isLoaded = true;
                LogService.Instance.Debug("SubscriptionManager", 
                    $"已加载订阅配置: {_config.Profiles.Count} 个 Profile");
            }
            catch (Exception ex)
            {
                LogService.Instance.Error("SubscriptionManager", $"加载订阅配置失败: {ex.Message}");
                _config = new SubscriptionConfig();
                _isLoaded = true;
            }
        }

        /// <summary>
        /// 保存订阅配置
        /// </summary>
        public void Save()
        {
            try
            {
                // 确保目录存在
                var dir = Path.GetDirectoryName(SubscriptionsFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                _config.SaveToFile(SubscriptionsFilePath);
                LogService.Instance.Debug("SubscriptionManager", "订阅配置已保存");
            }
            catch (Exception ex)
            {
                LogService.Instance.Error("SubscriptionManager", $"保存订阅配置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 确保配置已加载
        /// </summary>
        private void EnsureLoaded()
        {
            if (!_isLoaded)
            {
                Load();
            }
        }

        #endregion


        #region Profile Subscription Methods

        /// <summary>
        /// 获取已订阅的 Profile 列表
        /// </summary>
        /// <returns>Profile ID 列表</returns>
        public List<string> GetSubscribedProfiles()
        {
            EnsureLoaded();
            return new List<string>(_config.Profiles);
        }

        /// <summary>
        /// 检查 Profile 是否已订阅
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        /// <returns>是否已订阅</returns>
        public bool IsProfileSubscribed(string profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId))
                return false;

            EnsureLoaded();
            return _config.IsProfileSubscribed(profileId);
        }

        /// <summary>
        /// 订阅 Profile（复制模板到用户目录，自动订阅推荐插件）
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        /// <returns>是否成功</returns>
        public bool SubscribeProfile(string profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId))
            {
                LogService.Instance.Warn("SubscriptionManager", "订阅 Profile 失败: profileId 为空");
                return false;
            }

            EnsureLoaded();

            // 检查是否已订阅
            if (_config.IsProfileSubscribed(profileId))
            {
                LogService.Instance.Debug("SubscriptionManager", $"Profile '{profileId}' 已订阅");
                return true;
            }

            // 获取内置 Profile 信息
            var profileInfo = ProfileRegistry.Instance.GetProfile(profileId);
            if (profileInfo == null)
            {
                LogService.Instance.Warn("SubscriptionManager", $"订阅 Profile 失败: 未找到内置 Profile '{profileId}'");
                return false;
            }

            try
            {
                // 复制模板到用户目录
                var templateDir = ProfileRegistry.Instance.GetProfileTemplateDirectory(profileId);
                var userProfileDir = Path.Combine(UserProfilesDirectory, profileId);

                if (!CopyProfileTemplate(templateDir, userProfileDir))
                {
                    return false;
                }

                // 添加到订阅列表
                _config.AddProfile(profileId);

                // 自动订阅推荐插件
                if (profileInfo.RecommendedPlugins != null && profileInfo.RecommendedPlugins.Count > 0)
                {
                    foreach (var pluginId in profileInfo.RecommendedPlugins)
                    {
                        // 检查插件是否存在于注册表
                        if (PluginRegistry.Instance.PluginExists(pluginId))
                        {
                            _config.AddPlugin(pluginId, profileId);
                            LogService.Instance.Debug("SubscriptionManager", 
                                $"自动订阅推荐插件 '{pluginId}' 到 Profile '{profileId}'");
                        }
                        else
                        {
                            LogService.Instance.Warn("SubscriptionManager", 
                                $"推荐插件 '{pluginId}' 不存在于注册表，跳过");
                        }
                    }
                }

                // 保存配置
                Save();

                LogService.Instance.Info("SubscriptionManager", $"成功订阅 Profile '{profileId}'");
                return true;
            }
            catch (Exception ex)
            {
                LogService.Instance.Error("SubscriptionManager", $"订阅 Profile '{profileId}' 失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 取消订阅 Profile（删除用户目录中的 Profile 配置）
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        /// <returns>取消订阅结果</returns>
        public UnsubscribeResult UnsubscribeProfile(string profileId)
        {
            var result = new UnsubscribeResult();

            if (string.IsNullOrWhiteSpace(profileId))
            {
                result.Success = false;
                result.ErrorMessage = "profileId 为空";
                return result;
            }

            EnsureLoaded();

            // 检查是否已订阅
            if (!_config.IsProfileSubscribed(profileId))
            {
                result.Success = false;
                result.ErrorMessage = $"Profile '{profileId}' 未订阅";
                return result;
            }

            try
            {
                // 获取该 Profile 订阅的插件列表（用于返回）
                result.UnsubscribedPlugins = _config.GetSubscribedPlugins(profileId);

                // 从订阅列表移除（同时会移除插件订阅）
                _config.RemoveProfile(profileId);

                // 删除用户目录中的 Profile 配置
                var userProfileDir = Path.Combine(UserProfilesDirectory, profileId);
                if (Directory.Exists(userProfileDir))
                {
                    Directory.Delete(userProfileDir, true);
                    LogService.Instance.Debug("SubscriptionManager", $"已删除用户 Profile 目录: {userProfileDir}");
                }

                // 保存配置
                Save();

                result.Success = true;
                LogService.Instance.Info("SubscriptionManager", $"成功取消订阅 Profile '{profileId}'");
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                LogService.Instance.Error("SubscriptionManager", $"取消订阅 Profile '{profileId}' 失败: {ex.Message}");
                return result;
            }
        }

        #endregion


        #region Plugin Subscription Methods

        /// <summary>
        /// 获取指定 Profile 订阅的插件列表
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        /// <returns>插件 ID 列表</returns>
        public List<string> GetSubscribedPlugins(string profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId))
                return new List<string>();

            EnsureLoaded();
            return _config.GetSubscribedPlugins(profileId);
        }

        /// <summary>
        /// 检查插件是否已订阅到指定 Profile
        /// </summary>
        /// <param name="pluginId">插件 ID</param>
        /// <param name="profileId">Profile ID</param>
        /// <returns>是否已订阅</returns>
        public bool IsPluginSubscribed(string pluginId, string profileId)
        {
            if (string.IsNullOrWhiteSpace(pluginId) || string.IsNullOrWhiteSpace(profileId))
                return false;

            EnsureLoaded();
            return _config.IsPluginSubscribed(pluginId, profileId);
        }

        /// <summary>
        /// 订阅插件到指定 Profile
        /// </summary>
        /// <param name="pluginId">插件 ID</param>
        /// <param name="profileId">Profile ID</param>
        /// <returns>是否成功</returns>
        public bool SubscribePlugin(string pluginId, string profileId)
        {
            if (string.IsNullOrWhiteSpace(pluginId) || string.IsNullOrWhiteSpace(profileId))
            {
                LogService.Instance.Warn("SubscriptionManager", "订阅插件失败: pluginId 或 profileId 为空");
                return false;
            }

            EnsureLoaded();

            // 检查 Profile 是否已订阅
            if (!_config.IsProfileSubscribed(profileId))
            {
                LogService.Instance.Warn("SubscriptionManager", 
                    $"订阅插件失败: Profile '{profileId}' 未订阅");
                return false;
            }

            // 检查插件是否存在于注册表
            if (!PluginRegistry.Instance.PluginExists(pluginId))
            {
                LogService.Instance.Warn("SubscriptionManager", 
                    $"订阅插件失败: 插件 '{pluginId}' 不存在于注册表");
                return false;
            }

            // 检查是否已订阅
            if (_config.IsPluginSubscribed(pluginId, profileId))
            {
                LogService.Instance.Debug("SubscriptionManager", 
                    $"插件 '{pluginId}' 已订阅到 Profile '{profileId}'");
                return true;
            }

            try
            {
                // 添加到订阅列表
                _config.AddPlugin(pluginId, profileId);

                // 保存配置
                Save();

                LogService.Instance.Info("SubscriptionManager", 
                    $"成功订阅插件 '{pluginId}' 到 Profile '{profileId}'");
                return true;
            }
            catch (Exception ex)
            {
                LogService.Instance.Error("SubscriptionManager", 
                    $"订阅插件 '{pluginId}' 到 Profile '{profileId}' 失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 取消订阅插件
        /// </summary>
        /// <param name="pluginId">插件 ID</param>
        /// <param name="profileId">Profile ID</param>
        /// <returns>是否成功</returns>
        public bool UnsubscribePlugin(string pluginId, string profileId)
        {
            if (string.IsNullOrWhiteSpace(pluginId) || string.IsNullOrWhiteSpace(profileId))
            {
                LogService.Instance.Warn("SubscriptionManager", "取消订阅插件失败: pluginId 或 profileId 为空");
                return false;
            }

            EnsureLoaded();

            // 检查是否已订阅
            if (!_config.IsPluginSubscribed(pluginId, profileId))
            {
                LogService.Instance.Debug("SubscriptionManager", 
                    $"插件 '{pluginId}' 未订阅到 Profile '{profileId}'");
                return true; // 未订阅视为成功
            }

            try
            {
                // 从订阅列表移除
                _config.RemovePlugin(pluginId, profileId);

                // 删除用户配置目录
                var pluginConfigDir = GetPluginConfigDirectory(profileId, pluginId);
                if (Directory.Exists(pluginConfigDir))
                {
                    Directory.Delete(pluginConfigDir, true);
                    LogService.Instance.Debug("SubscriptionManager", 
                        $"已删除插件配置目录: {pluginConfigDir}");
                }

                // 保存配置
                Save();

                LogService.Instance.Info("SubscriptionManager", 
                    $"成功取消订阅插件 '{pluginId}' 从 Profile '{profileId}'");
                return true;
            }
            catch (Exception ex)
            {
                LogService.Instance.Error("SubscriptionManager", 
                    $"取消订阅插件 '{pluginId}' 从 Profile '{profileId}' 失败: {ex.Message}");
                return false;
            }
        }

        #endregion


        #region Helper Methods

        /// <summary>
        /// 获取插件用户配置目录
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        /// <param name="pluginId">插件 ID</param>
        /// <returns>配置目录路径</returns>
        public string GetPluginConfigDirectory(string profileId, string pluginId)
        {
            return Path.Combine(UserProfilesDirectory, profileId, "plugins", pluginId);
        }

        /// <summary>
        /// 复制 Profile 模板到用户目录
        /// </summary>
        /// <param name="templateDir">模板目录</param>
        /// <param name="targetDir">目标目录</param>
        /// <returns>是否成功</returns>
        private bool CopyProfileTemplate(string templateDir, string targetDir)
        {
            if (!Directory.Exists(templateDir))
            {
                LogService.Instance.Warn("SubscriptionManager", $"模板目录不存在: {templateDir}");
                return false;
            }

            try
            {
                // 确保目标目录存在
                Directory.CreateDirectory(targetDir);

                // 复制所有文件
                foreach (var file in Directory.GetFiles(templateDir))
                {
                    var fileName = Path.GetFileName(file);
                    var destFile = Path.Combine(targetDir, fileName);
                    File.Copy(file, destFile, true);
                }

                // 递归复制子目录
                foreach (var dir in Directory.GetDirectories(templateDir))
                {
                    var dirName = Path.GetFileName(dir);
                    var destDir = Path.Combine(targetDir, dirName);
                    CopyDirectory(dir, destDir);
                }

                LogService.Instance.Debug("SubscriptionManager", 
                    $"已复制 Profile 模板: {templateDir} -> {targetDir}");
                return true;
            }
            catch (Exception ex)
            {
                LogService.Instance.Error("SubscriptionManager", 
                    $"复制 Profile 模板失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 递归复制目录
        /// </summary>
        /// <param name="sourceDir">源目录</param>
        /// <param name="targetDir">目标目录</param>
        private void CopyDirectory(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var fileName = Path.GetFileName(file);
                var destFile = Path.Combine(targetDir, fileName);
                File.Copy(file, destFile, true);
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var dirName = Path.GetFileName(dir);
                var destDir = Path.Combine(targetDir, dirName);
                CopyDirectory(dir, destDir);
            }
        }

        #endregion
    }
}
