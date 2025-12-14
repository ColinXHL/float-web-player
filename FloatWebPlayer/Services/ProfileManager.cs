using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using FloatWebPlayer.Helpers;
using FloatWebPlayer.Models;

namespace FloatWebPlayer.Services
{
    /// <summary>
    /// Profile 管理服务
    /// 负责加载、切换、保存 Profile 配置
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
            CurrentProfile = GetProfileById("default") ?? CreateDefaultProfile();
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
            Directory.CreateDirectory(profileDir);

            var profilePath = Path.Combine(profileDir, "profile.json");
            var options = new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            var json = JsonSerializer.Serialize(profile, options);
            File.WriteAllText(profilePath, json);
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
            if (profileId.Equals("default", StringComparison.OrdinalIgnoreCase))
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
                    SwitchProfile("default");
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
                CurrentProfile = GetProfileById("default") ?? CreateDefaultProfile();
                ProfileChanged?.Invoke(this, CurrentProfile);
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// 加载所有 Profile
        /// </summary>
        private void LoadAllProfiles()
        {
            if (!Directory.Exists(ProfilesDirectory))
                return;

            var profileDirs = Directory.GetDirectories(ProfilesDirectory);
            foreach (var dir in profileDirs)
            {
                var profilePath = Path.Combine(dir, "profile.json");
                if (File.Exists(profilePath))
                {
                    try
                    {
                        var json = File.ReadAllText(profilePath);
                        var options = new JsonSerializerOptions
                        {
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                            PropertyNameCaseInsensitive = true
                        };
                        var profile = JsonSerializer.Deserialize<GameProfile>(json, options);
                        if (profile != null)
                        {
                            Profiles.Add(profile);
                        }
                    }
                    catch
                    {
                        // 跳过无效的 Profile
                    }
                }
            }

            // 如果没有 Default Profile，创建一个
            if (GetProfileById("default") == null)
            {
                var defaultProfile = CreateDefaultProfile();
                Profiles.Add(defaultProfile);
            }
        }

        /// <summary>
        /// 创建默认 Profile
        /// </summary>
        private GameProfile CreateDefaultProfile()
        {
            var profile = new GameProfile
            {
                Id = "default",
                Name = "Default",
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
            SaveProfile(profile);
            return profile;
        }

        #endregion
    }
}
