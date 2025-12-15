using System;
using System.Collections.Generic;
using System.IO;
using FloatWebPlayer.Helpers;
using FloatWebPlayer.Models;
using FloatWebPlayer.Plugins;

namespace FloatWebPlayer.Services
{
    /// <summary>
    /// Profile 管理服务
    /// 负责加载、切换、保存 Profile 配置
    /// 集成订阅机制：只加载已订阅的 Profile
    /// </summary>
    public class ProfileManager
    {
        #region Singleton

        private static ProfileManager? _instance;
        private static readonly object _lock = new();

        /// <summary>
        /// 获取单例实例
        /// </summary>
        public static ProfileManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new ProfileManager();
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

        #region Events

        /// <summary>
        /// Profile 切换事件
        /// </summary>
        public event EventHandler<GameProfile>? ProfileChanged;

        #endregion

        #region Properties

        /// <summary>
        /// 当前激活的 Profile
        /// </summary>
        public GameProfile CurrentProfile { get; private set; }

        /// <summary>
        /// 所有已加载的 Profile 列表
        /// </summary>
        public List<GameProfile> Profiles { get; } = new();

        /// <summary>
        /// 已安装的 Profile 只读列表
        /// </summary>
        public IReadOnlyList<GameProfile> InstalledProfiles => Profiles.AsReadOnly();

        /// <summary>
        /// 数据根目录
        /// </summary>
        public string DataDirectory { get; }

        /// <summary>
        /// Profiles 目录
        /// </summary>
        public string ProfilesDirectory { get; }

        #endregion

        #region Constructor

        private ProfileManager()
        {
            // 数据目录：User/Data/
            DataDirectory = AppPaths.DataDirectory;
            ProfilesDirectory = AppPaths.ProfilesDirectory;

            // 加载所有 Profile
            LoadAllProfiles();

            // 设置默认 Profile
            CurrentProfile = GetProfileById(AppConstants.DefaultProfileId) ?? CreateDefaultProfile();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 切换到指定 Profile
        /// </summary>
        public bool SwitchProfile(string profileId)
        {
            var profile = GetProfileById(profileId);
            if (profile == null)
                return false;

            // 卸载当前 Profile 的插件
            PluginHost.Instance.UnloadAllPlugins();

            CurrentProfile = profile;
            
            // 加载新 Profile 的插件
            PluginHost.Instance.LoadPluginsForProfile(profileId);
            
            // 广播 profileChanged 事件到插件
            PluginHost.Instance.BroadcastEvent(Plugins.EventApi.ProfileChanged, new { profileId = profile.Id });
            
            ProfileChanged?.Invoke(this, profile);
            return true;
        }

        /// <summary>
        /// 根据 ID 获取 Profile
        /// </summary>
        public GameProfile? GetProfileById(string id)
        {
            return Profiles.Find(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 获取当前 Profile 的数据目录
        /// </summary>
        public string GetCurrentProfileDirectory()
        {
            return GetProfileDirectory(CurrentProfile.Id);
        }

        /// <summary>
        /// 获取指定 Profile 的数据目录
        /// </summary>
        public string GetProfileDirectory(string profileId)
        {
            return Path.Combine(ProfilesDirectory, profileId);
        }

        /// <summary>
        /// 保存当前 Profile 配置
        /// </summary>
        public void SaveCurrentProfile()
        {
            SaveProfile(CurrentProfile);
        }

        /// <summary>
        /// 保存指定 Profile 配置
        /// </summary>
        public void SaveProfile(GameProfile profile)
        {
            var profileDir = GetProfileDirectory(profile.Id);
            var profilePath = Path.Combine(profileDir, AppConstants.ProfileFileName);
            
            try
            {
                Directory.CreateDirectory(profileDir);
                JsonHelper.SaveToFile(profilePath, profile);
            }
            catch (Exception ex)
            {
                LogService.Instance.Debug("ProfileManager", $"保存 Profile 失败 [{profilePath}]: {ex.Message}");
            }
        }

        /// <summary>
        /// 取消订阅 Profile（删除 Profile 目录）
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        /// <returns>操作结果</returns>
        public UnsubscribeResult UnsubscribeProfile(string profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId))
            {
                return UnsubscribeResult.Failed("Profile ID 不能为空");
            }

            // 不允许删除默认 Profile
            if (profileId.Equals(AppConstants.DefaultProfileId, StringComparison.OrdinalIgnoreCase))
            {
                return UnsubscribeResult.Failed("不能删除默认 Profile");
            }

            // 查找 Profile
            var profile = GetProfileById(profileId);
            if (profile == null)
            {
                // Profile 不存在，静默成功
                return UnsubscribeResult.Succeeded();
            }

            var profileDir = GetProfileDirectory(profileId);

            try
            {
                // 如果是当前 Profile，先切换到默认 Profile
                if (CurrentProfile.Id.Equals(profileId, StringComparison.OrdinalIgnoreCase))
                {
                    SwitchProfile(AppConstants.DefaultProfileId);
                }
                else
                {
                    // 卸载该 Profile 的插件（如果有加载的话）
                    // 注意：由于我们已经切换了 Profile，这里不需要额外卸载
                }

                // 从列表中移除
                Profiles.RemoveAll(p => p.Id.Equals(profileId, StringComparison.OrdinalIgnoreCase));

                // 删除 Profile 目录
                if (Directory.Exists(profileDir))
                {
                    Directory.Delete(profileDir, recursive: true);
                }

                return UnsubscribeResult.Succeeded();
            }
            catch (UnauthorizedAccessException ex)
            {
                return UnsubscribeResult.Failed($"删除 Profile 目录失败：权限不足。{ex.Message}");
            }
            catch (IOException ex)
            {
                return UnsubscribeResult.Failed($"删除 Profile 目录失败：文件被占用。{ex.Message}");
            }
            catch (Exception ex)
            {
                return UnsubscribeResult.Failed($"取消订阅失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 重新加载所有 Profile
        /// </summary>
        public void ReloadProfiles()
        {
            Profiles.Clear();
            LoadAllProfiles();
            
            // 如果当前 Profile 不存在，切换到 Default
            if (GetProfileById(CurrentProfile.Id) == null)
            {
                CurrentProfile = GetProfileById(AppConstants.DefaultProfileId) ?? CreateDefaultProfile();
                ProfileChanged?.Invoke(this, CurrentProfile);
            }
        }

        #endregion

        #region Subscription Methods

        /// <summary>
        /// 订阅 Profile（调用 SubscriptionManager）
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        /// <returns>是否成功</returns>
        public bool SubscribeProfile(string profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId))
            {
                LogService.Instance.Warn("ProfileManager", "订阅 Profile 失败: profileId 为空");
                return false;
            }

            // 调用 SubscriptionManager 执行订阅
            var success = SubscriptionManager.Instance.SubscribeProfile(profileId);
            
            if (success)
            {
                // 重新加载 Profiles 列表
                ReloadProfiles();
                LogService.Instance.Info("ProfileManager", $"成功订阅 Profile '{profileId}'");
            }

            return success;
        }

        /// <summary>
        /// 取消订阅 Profile（调用 SubscriptionManager）
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        /// <returns>取消订阅结果</returns>
        public UnsubscribeResult UnsubscribeProfileViaSubscription(string profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId))
            {
                return UnsubscribeResult.Failed("Profile ID 不能为空");
            }

            // 不允许取消订阅默认 Profile
            if (profileId.Equals(AppConstants.DefaultProfileId, StringComparison.OrdinalIgnoreCase))
            {
                return UnsubscribeResult.Failed("不能取消订阅默认 Profile");
            }

            // 如果是当前 Profile，先切换到默认 Profile
            if (CurrentProfile.Id.Equals(profileId, StringComparison.OrdinalIgnoreCase))
            {
                SwitchProfile(AppConstants.DefaultProfileId);
            }

            // 调用 SubscriptionManager 执行取消订阅
            var result = SubscriptionManager.Instance.UnsubscribeProfile(profileId);
            
            if (result.Success)
            {
                // 从列表中移除
                Profiles.RemoveAll(p => p.Id.Equals(profileId, StringComparison.OrdinalIgnoreCase));
                LogService.Instance.Info("ProfileManager", $"成功取消订阅 Profile '{profileId}'");
            }

            return result;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// 加载所有已订阅的 Profile
        /// 只加载 SubscriptionManager 中已订阅的 Profile
        /// </summary>
        private void LoadAllProfiles()
        {
            // 确保 SubscriptionManager 已加载
            SubscriptionManager.Instance.Load();

            // 获取已订阅的 Profile 列表
            var subscribedProfiles = SubscriptionManager.Instance.GetSubscribedProfiles();

            // 如果没有订阅任何 Profile，确保默认 Profile 存在
            if (subscribedProfiles.Count == 0)
            {
                // 自动订阅默认 Profile
                EnsureDefaultProfileSubscribed();
                subscribedProfiles = SubscriptionManager.Instance.GetSubscribedProfiles();
            }

            // 只加载已订阅的 Profile
            foreach (var profileId in subscribedProfiles)
            {
                var profileDir = Path.Combine(ProfilesDirectory, profileId);
                var profilePath = Path.Combine(profileDir, AppConstants.ProfileFileName);

                if (!File.Exists(profilePath))
                {
                    LogService.Instance.Warn("ProfileManager", $"已订阅的 Profile 文件不存在: {profilePath}");
                    continue;
                }

                try
                {
                    var profile = JsonHelper.LoadFromFile<GameProfile>(profilePath);
                    if (profile != null)
                    {
                        Profiles.Add(profile);
                        LogService.Instance.Debug("ProfileManager", $"已加载订阅的 Profile: {profileId}");
                    }
                }
                catch (Exception ex)
                {
                    LogService.Instance.Warn("ProfileManager", $"加载 Profile 失败 [{profilePath}]: {ex.Message}");
                }
            }

            // 确保默认 Profile 存在于列表中
            if (GetProfileById(AppConstants.DefaultProfileId) == null)
            {
                var defaultProfile = CreateDefaultProfile();
                Profiles.Add(defaultProfile);
            }
        }

        /// <summary>
        /// 确保默认 Profile 已订阅
        /// </summary>
        private void EnsureDefaultProfileSubscribed()
        {
            if (!SubscriptionManager.Instance.IsProfileSubscribed(AppConstants.DefaultProfileId))
            {
                // 检查内置模板是否存在
                if (ProfileRegistry.Instance.ProfileExists(AppConstants.DefaultProfileId))
                {
                    // 从内置模板订阅
                    SubscriptionManager.Instance.SubscribeProfile(AppConstants.DefaultProfileId);
                    LogService.Instance.Info("ProfileManager", "已自动订阅默认 Profile（从内置模板）");
                }
                else
                {
                    // 内置模板不存在，创建默认 Profile 并手动添加到订阅
                    CreateDefaultProfile();
                    // 手动添加到订阅配置（因为没有内置模板）
                    AddDefaultProfileToSubscription();
                    LogService.Instance.Info("ProfileManager", "已创建并订阅默认 Profile");
                }
            }
        }

        /// <summary>
        /// 手动将默认 Profile 添加到订阅配置
        /// 用于内置模板不存在的情况
        /// </summary>
        private void AddDefaultProfileToSubscription()
        {
            // 直接操作订阅配置文件
            var subscriptionsPath = AppPaths.SubscriptionsFilePath;
            var config = new SubscriptionConfig();
            
            if (File.Exists(subscriptionsPath))
            {
                try
                {
                    config = SubscriptionConfig.LoadFromFile(subscriptionsPath);
                }
                catch
                {
                    config = new SubscriptionConfig();
                }
            }

            if (!config.IsProfileSubscribed(AppConstants.DefaultProfileId))
            {
                config.AddProfile(AppConstants.DefaultProfileId);
                config.SaveToFile(subscriptionsPath);
            }

            // 重新加载 SubscriptionManager
            SubscriptionManager.Instance.Load();
        }

        /// <summary>
        /// 创建默认 Profile
        /// 优先从内置模板复制，否则创建新的
        /// </summary>
        private GameProfile CreateDefaultProfile()
        {
            var profileDir = GetProfileDirectory(AppConstants.DefaultProfileId);
            var profilePath = Path.Combine(profileDir, AppConstants.ProfileFileName);

            // 检查内置模板是否存在
            var templateDir = ProfileRegistry.Instance.GetProfileTemplateDirectory(AppConstants.DefaultProfileId);
            var templatePath = Path.Combine(templateDir, AppConstants.ProfileFileName);

            if (File.Exists(templatePath))
            {
                // 从内置模板复制
                try
                {
                    Directory.CreateDirectory(profileDir);
                    CopyDirectory(templateDir, profileDir);
                    
                    var profile = JsonHelper.LoadFromFile<GameProfile>(profilePath);
                    if (profile != null)
                    {
                        LogService.Instance.Info("ProfileManager", "已从内置模板创建默认 Profile");
                        return profile;
                    }
                }
                catch (Exception ex)
                {
                    LogService.Instance.Warn("ProfileManager", $"从模板复制默认 Profile 失败: {ex.Message}");
                }
            }

            // 内置模板不存在或复制失败，创建新的默认 Profile
            var newProfile = new GameProfile
            {
                Id = AppConstants.DefaultProfileId,
                Name = AppConstants.DefaultProfileName,
                Icon = "🌐",
                Version = 1,
                Defaults = new ProfileDefaults
                {
                    Url = AppConstants.DefaultHomeUrl,
                    Opacity = 1.0,
                    SeekSeconds = AppConstants.DefaultSeekSeconds
                }
            };

            // 保存到文件
            SaveProfile(newProfile);
            LogService.Instance.Info("ProfileManager", "已创建新的默认 Profile");
            return newProfile;
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
