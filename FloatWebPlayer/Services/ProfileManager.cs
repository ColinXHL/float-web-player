using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FloatWebPlayer.Helpers;
using FloatWebPlayer.Models;
using FloatWebPlayer.Plugins;

namespace FloatWebPlayer.Services
{
    #region Result Types

    /// <summary>
    /// Profile 创建结果
    /// </summary>
    public class CreateProfileResult
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// 错误消息（失败时）
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 创建的 Profile ID（成功时）
        /// </summary>
        public string? ProfileId { get; set; }

        /// <summary>
        /// 创建成功结果
        /// </summary>
        public static CreateProfileResult Success(string profileId) => new()
        {
            IsSuccess = true,
            ProfileId = profileId
        };

        /// <summary>
        /// 创建失败结果
        /// </summary>
        public static CreateProfileResult Failure(string errorMessage) => new()
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }

    /// <summary>
    /// Profile 删除结果
    /// </summary>
    public class DeleteProfileResult
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// 错误消息（失败时）
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 创建成功结果
        /// </summary>
        public static DeleteProfileResult Success() => new()
        {
            IsSuccess = true
        };

        /// <summary>
        /// 创建失败结果
        /// </summary>
        public static DeleteProfileResult Failure(string errorMessage) => new()
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }

    #endregion

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

            // 从配置中恢复上次选择的 Profile，如果不存在则使用默认 Profile
            var savedProfileId = ConfigService.Instance.Config.CurrentProfileId;
            CurrentProfile = GetProfileById(savedProfileId) 
                ?? GetProfileById(AppConstants.DefaultProfileId) 
                ?? CreateDefaultProfile();
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
            
            // 保存当前选择的 Profile ID 到配置
            var config = ConfigService.Instance.Config;
            config.CurrentProfileId = profileId;
            ConfigService.Instance.Save();
            
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

        #region Profile CRUD Operations

        /// <summary>
        /// 预定义的 Profile 图标列表
        /// </summary>
        public static readonly string[] ProfileIcons = new[]
        {
            "📦", "🎮", "🎬", "📺", "🎵", "📚", "🎯", "⚡", "🔧", "💡"
        };

        /// <summary>
        /// 创建新的 Profile
        /// </summary>
        /// <param name="id">Profile ID（如果为空则自动生成）</param>
        /// <param name="name">Profile 名称</param>
        /// <param name="icon">Profile 图标</param>
        /// <param name="pluginIds">要关联的插件 ID 列表</param>
        /// <returns>创建结果</returns>
        public CreateProfileResult CreateProfile(string? id, string name, string icon, List<string>? pluginIds)
        {
            // 验证名称
            if (string.IsNullOrWhiteSpace(name))
            {
                return CreateProfileResult.Failure("Profile 名称不能为空");
            }

            // 生成或验证 ID
            var profileId = string.IsNullOrWhiteSpace(id) ? GenerateProfileId(name) : id;

            // 检查 ID 是否已存在
            if (ProfileIdExists(profileId))
            {
                return CreateProfileResult.Failure("已存在同名 Profile");
            }

            // 验证图标
            if (string.IsNullOrWhiteSpace(icon))
            {
                icon = "📦";
            }

            try
            {
                // 创建 Profile 对象
                var profile = new GameProfile
                {
                    Id = profileId,
                    Name = name.Trim(),
                    Icon = icon,
                    Version = 1,
                    Defaults = new ProfileDefaults
                    {
                        Url = AppConstants.DefaultHomeUrl,
                        Opacity = 1.0,
                        SeekSeconds = AppConstants.DefaultSeekSeconds
                    }
                };

                // 创建 Profile 目录和配置文件
                var profileDir = GetProfileDirectory(profileId);
                Directory.CreateDirectory(profileDir);
                SaveProfile(profile);

                // 添加到订阅
                AddProfileToSubscription(profileId);

                // 添加到内存列表
                Profiles.Add(profile);

                // 关联插件
                if (pluginIds != null && pluginIds.Count > 0)
                {
                    PluginAssociationManager.Instance.AddPluginsToProfile(pluginIds, profileId);
                }

                LogService.Instance.Info("ProfileManager", $"成功创建 Profile '{profileId}'");
                return CreateProfileResult.Success(profileId);
            }
            catch (Exception ex)
            {
                LogService.Instance.Error("ProfileManager", $"创建 Profile 失败: {ex.Message}");
                return CreateProfileResult.Failure($"创建失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新 Profile 名称和图标
        /// </summary>
        /// <param name="id">Profile ID</param>
        /// <param name="newName">新名称</param>
        /// <param name="newIcon">新图标</param>
        /// <returns>是否成功</returns>
        public bool UpdateProfile(string id, string newName, string newIcon)
        {
            // 验证名称
            if (string.IsNullOrWhiteSpace(newName))
            {
                LogService.Instance.Warn("ProfileManager", "更新 Profile 失败: 名称不能为空");
                return false;
            }

            // 查找 Profile
            var profile = GetProfileById(id);
            if (profile == null)
            {
                LogService.Instance.Warn("ProfileManager", $"更新 Profile 失败: Profile '{id}' 不存在");
                return false;
            }

            try
            {
                // 更新属性
                profile.Name = newName.Trim();
                if (!string.IsNullOrWhiteSpace(newIcon))
                {
                    profile.Icon = newIcon;
                }

                // 保存到文件
                SaveProfile(profile);

                LogService.Instance.Info("ProfileManager", $"成功更新 Profile '{id}'");
                return true;
            }
            catch (Exception ex)
            {
                LogService.Instance.Error("ProfileManager", $"更新 Profile 失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 删除 Profile
        /// </summary>
        /// <param name="id">Profile ID</param>
        /// <returns>删除结果</returns>
        public DeleteProfileResult DeleteProfile(string id)
        {
            // 验证 ID
            if (string.IsNullOrWhiteSpace(id))
            {
                return DeleteProfileResult.Failure("Profile ID 不能为空");
            }

            // 不允许删除默认 Profile
            if (IsDefaultProfile(id))
            {
                return DeleteProfileResult.Failure("默认 Profile 不能删除");
            }

            // 查找 Profile
            var profile = GetProfileById(id);
            if (profile == null)
            {
                // Profile 不存在，静默成功
                return DeleteProfileResult.Success();
            }

            try
            {
                // 如果是当前 Profile，先切换到默认 Profile
                if (CurrentProfile.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
                {
                    SwitchProfile(AppConstants.DefaultProfileId);
                }

                // 删除插件关联
                PluginAssociationManager.Instance.RemoveProfile(id);

                // 从订阅中移除
                RemoveProfileFromSubscription(id);

                // 从内存列表中移除
                Profiles.RemoveAll(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

                // 删除 Profile 目录
                var profileDir = GetProfileDirectory(id);
                if (Directory.Exists(profileDir))
                {
                    Directory.Delete(profileDir, recursive: true);
                }

                LogService.Instance.Info("ProfileManager", $"成功删除 Profile '{id}'");
                return DeleteProfileResult.Success();
            }
            catch (UnauthorizedAccessException ex)
            {
                return DeleteProfileResult.Failure($"删除 Profile 目录失败：权限不足。{ex.Message}");
            }
            catch (IOException ex)
            {
                return DeleteProfileResult.Failure($"删除 Profile 目录失败：文件被占用。{ex.Message}");
            }
            catch (Exception ex)
            {
                LogService.Instance.Error("ProfileManager", $"删除 Profile 失败: {ex.Message}");
                return DeleteProfileResult.Failure($"删除失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 检查是否是默认 Profile
        /// </summary>
        /// <param name="id">Profile ID</param>
        /// <returns>是否是默认 Profile</returns>
        public bool IsDefaultProfile(string id)
        {
            return id.Equals(AppConstants.DefaultProfileId, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 检查 Profile ID 是否已存在
        /// </summary>
        /// <param name="id">Profile ID</param>
        /// <returns>是否存在</returns>
        public bool ProfileIdExists(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return false;

            return GetProfileById(id) != null;
        }

        /// <summary>
        /// 根据名称生成 Profile ID
        /// </summary>
        /// <param name="name">Profile 名称</param>
        /// <returns>生成的 ID</returns>
        public string GenerateProfileId(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return $"profile-{Guid.NewGuid():N}".Substring(0, 16);
            }

            // 将名称转换为 kebab-case ID
            var id = name.Trim().ToLowerInvariant();
            
            // 替换空格和特殊字符为连字符
            id = Regex.Replace(id, @"[^a-z0-9\u4e00-\u9fa5]", "-");
            
            // 移除连续的连字符
            id = Regex.Replace(id, @"-+", "-");
            
            // 移除首尾连字符
            id = id.Trim('-');

            // 如果 ID 为空或太短，添加随机后缀
            if (string.IsNullOrEmpty(id) || id.Length < 2)
            {
                id = $"profile-{Guid.NewGuid():N}".Substring(0, 16);
            }

            // 如果 ID 已存在，添加数字后缀
            var baseId = id;
            var counter = 1;
            while (ProfileIdExists(id))
            {
                id = $"{baseId}-{counter}";
                counter++;
            }

            return id;
        }

        /// <summary>
        /// 将 Profile 添加到订阅配置
        /// </summary>
        private void AddProfileToSubscription(string profileId)
        {
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

            if (!config.IsProfileSubscribed(profileId))
            {
                config.AddProfile(profileId);
                config.SaveToFile(subscriptionsPath);
            }

            // 重新加载 SubscriptionManager
            SubscriptionManager.Instance.Load();
        }

        /// <summary>
        /// 从订阅配置中移除 Profile
        /// </summary>
        private void RemoveProfileFromSubscription(string profileId)
        {
            var subscriptionsPath = AppPaths.SubscriptionsFilePath;
            
            if (!File.Exists(subscriptionsPath))
                return;

            try
            {
                var config = SubscriptionConfig.LoadFromFile(subscriptionsPath);
                config.RemoveProfile(profileId);
                config.SaveToFile(subscriptionsPath);
                
                // 重新加载 SubscriptionManager
                SubscriptionManager.Instance.Load();
            }
            catch (Exception ex)
            {
                LogService.Instance.Warn("ProfileManager", $"从订阅配置移除 Profile 失败: {ex.Message}");
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

        #region Profile Import/Export (导入导出)

        /// <summary>
        /// 导出 Profile（仅清单+配置，不含插件本体）
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        /// <returns>导出数据，如果 Profile 不存在则返回 null</returns>
        public ProfileExportData? ExportProfile(string profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId))
                return null;

            // 获取 Profile 配置
            var profile = GetProfileById(profileId);
            if (profile == null)
            {
                LogService.Instance.Warn("ProfileManager", $"导出失败：Profile '{profileId}' 不存在");
                return null;
            }

            // 获取插件引用清单
            var pluginReferences = PluginAssociationManager.Instance.GetPluginsInProfile(profileId);
            var referenceEntries = pluginReferences
                .Select(r => PluginReferenceEntry.FromReference(r))
                .ToList();

            // 获取所有插件配置
            var pluginConfigs = GetAllPluginConfigs(profileId);

            // 创建导出数据
            var exportData = new ProfileExportData
            {
                Version = 1,
                ProfileId = profile.Id,
                ProfileName = profile.Name,
                ProfileConfig = profile,
                PluginReferences = referenceEntries,
                PluginConfigs = pluginConfigs,
                ExportedAt = DateTime.Now
            };

            LogService.Instance.Info("ProfileManager", 
                $"导出 Profile '{profileId}'：{referenceEntries.Count} 个插件引用，{pluginConfigs.Count} 个插件配置");

            return exportData;
        }

        /// <summary>
        /// 导出 Profile 到文件
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        /// <param name="filePath">目标文件路径</param>
        /// <returns>是否成功导出</returns>
        public bool ExportProfileToFile(string profileId, string filePath)
        {
            var exportData = ExportProfile(profileId);
            if (exportData == null)
                return false;

            try
            {
                exportData.SaveToFile(filePath);
                LogService.Instance.Info("ProfileManager", $"Profile '{profileId}' 已导出到 {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                LogService.Instance.Error("ProfileManager", $"导出 Profile 到文件失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 导入 Profile（检查缺失插件）
        /// </summary>
        /// <param name="data">导出数据</param>
        /// <param name="overwrite">如果 Profile 已存在是否覆盖</param>
        /// <returns>导入结果</returns>
        public ProfileImportResult ImportProfile(ProfileExportData data, bool overwrite = false)
        {
            if (data == null)
                return ProfileImportResult.Failure("导入数据为空");

            if (string.IsNullOrWhiteSpace(data.ProfileId))
                return ProfileImportResult.Failure("Profile ID 为空");

            // 检查版本兼容性
            if (data.Version > 1)
            {
                return ProfileImportResult.Failure($"不支持的导出格式版本: {data.Version}");
            }

            // 检查 Profile 是否已存在
            var existingProfile = GetProfileById(data.ProfileId);
            if (existingProfile != null && !overwrite)
            {
                return ProfileImportResult.Exists(data.ProfileId);
            }

            // 检测缺失的插件
            var missingPlugins = new List<string>();
            foreach (var reference in data.PluginReferences)
            {
                if (!PluginLibrary.Instance.IsInstalled(reference.PluginId))
                {
                    missingPlugins.Add(reference.PluginId);
                }
            }

            try
            {
                // 创建或更新 Profile 配置
                var profileDir = GetProfileDirectory(data.ProfileId);
                Directory.CreateDirectory(profileDir);

                // 保存 Profile 配置
                var profileConfig = data.ProfileConfig ?? new GameProfile
                {
                    Id = data.ProfileId,
                    Name = data.ProfileName
                };
                profileConfig.Id = data.ProfileId; // 确保 ID 一致
                SaveProfile(profileConfig);

                // 创建插件关联
                foreach (var reference in data.PluginReferences)
                {
                    PluginAssociationManager.Instance.AddPluginToProfile(
                        reference.PluginId, 
                        data.ProfileId, 
                        reference.Enabled);
                }

                // 保存插件配置
                foreach (var kvp in data.PluginConfigs)
                {
                    SavePluginConfig(data.ProfileId, kvp.Key, kvp.Value);
                }

                // 添加到订阅
                if (!SubscriptionManager.Instance.IsProfileSubscribed(data.ProfileId))
                {
                    // 手动添加到订阅配置
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

                    config.AddProfile(data.ProfileId);
                    config.SaveToFile(subscriptionsPath);
                    SubscriptionManager.Instance.Load();
                }

                // 重新加载 Profiles 列表
                ReloadProfiles();

                LogService.Instance.Info("ProfileManager", 
                    $"导入 Profile '{data.ProfileId}'：{data.PluginReferences.Count} 个插件引用，{missingPlugins.Count} 个缺失");

                return ProfileImportResult.Success(data.ProfileId, missingPlugins);
            }
            catch (Exception ex)
            {
                LogService.Instance.Error("ProfileManager", $"导入 Profile 失败: {ex.Message}");
                return ProfileImportResult.Failure($"导入失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从文件导入 Profile
        /// </summary>
        /// <param name="filePath">导入文件路径</param>
        /// <param name="overwrite">如果 Profile 已存在是否覆盖</param>
        /// <returns>导入结果</returns>
        public ProfileImportResult ImportProfileFromFile(string filePath, bool overwrite = false)
        {
            if (!File.Exists(filePath))
                return ProfileImportResult.Failure($"文件不存在: {filePath}");

            var data = ProfileExportData.LoadFromFile(filePath);
            if (data == null)
                return ProfileImportResult.Failure("无法解析导入文件");

            return ImportProfile(data, overwrite);
        }

        /// <summary>
        /// 预览导入（不实际导入，只检查缺失插件）
        /// </summary>
        /// <param name="data">导出数据</param>
        /// <returns>导入预览结果</returns>
        public ProfileImportResult PreviewImport(ProfileExportData data)
        {
            if (data == null)
                return ProfileImportResult.Failure("导入数据为空");

            if (string.IsNullOrWhiteSpace(data.ProfileId))
                return ProfileImportResult.Failure("Profile ID 为空");

            // 检查 Profile 是否已存在
            var existingProfile = GetProfileById(data.ProfileId);
            if (existingProfile != null)
            {
                var result = ProfileImportResult.Exists(data.ProfileId);
                // 仍然检测缺失插件
                foreach (var reference in data.PluginReferences)
                {
                    if (!PluginLibrary.Instance.IsInstalled(reference.PluginId))
                    {
                        result.MissingPlugins.Add(reference.PluginId);
                    }
                }
                return result;
            }

            // 检测缺失的插件
            var missingPlugins = new List<string>();
            foreach (var reference in data.PluginReferences)
            {
                if (!PluginLibrary.Instance.IsInstalled(reference.PluginId))
                {
                    missingPlugins.Add(reference.PluginId);
                }
            }

            return ProfileImportResult.Success(data.ProfileId, missingPlugins);
        }

        #endregion

        #region Plugin Reference Management (插件引用管理)

        /// <summary>
        /// 获取 Profile 的插件引用清单
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        /// <returns>插件引用列表</returns>
        public List<PluginReference> GetPluginReferences(string profileId)
        {
            return PluginAssociationManager.Instance.GetPluginsInProfile(profileId);
        }

        /// <summary>
        /// 设置插件在 Profile 中的启用状态
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        /// <param name="pluginId">插件 ID</param>
        /// <param name="enabled">是否启用</param>
        /// <returns>是否成功设置</returns>
        public bool SetPluginEnabled(string profileId, string pluginId, bool enabled)
        {
            return PluginAssociationManager.Instance.SetPluginEnabled(profileId, pluginId, enabled);
        }

        /// <summary>
        /// 获取插件的 Profile 特定配置
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        /// <param name="pluginId">插件 ID</param>
        /// <returns>配置字典，如果不存在则返回 null</returns>
        public Dictionary<string, object>? GetPluginConfig(string profileId, string pluginId)
        {
            if (string.IsNullOrWhiteSpace(profileId) || string.IsNullOrWhiteSpace(pluginId))
                return null;

            var configPath = GetPluginConfigPath(profileId, pluginId);
            if (!File.Exists(configPath))
                return null;

            try
            {
                return JsonHelper.LoadFromFile<Dictionary<string, object>>(configPath);
            }
            catch (Exception ex)
            {
                LogService.Instance.Debug("ProfileManager", $"加载插件配置失败 [{configPath}]: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 保存插件的 Profile 特定配置
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        /// <param name="pluginId">插件 ID</param>
        /// <param name="config">配置字典</param>
        /// <returns>是否成功保存</returns>
        public bool SavePluginConfig(string profileId, string pluginId, Dictionary<string, object> config)
        {
            if (string.IsNullOrWhiteSpace(profileId) || string.IsNullOrWhiteSpace(pluginId))
                return false;

            var configPath = GetPluginConfigPath(profileId, pluginId);

            try
            {
                var configDir = Path.GetDirectoryName(configPath);
                if (!string.IsNullOrEmpty(configDir))
                    Directory.CreateDirectory(configDir);

                JsonHelper.SaveToFile(configPath, config);
                return true;
            }
            catch (Exception ex)
            {
                LogService.Instance.Debug("ProfileManager", $"保存插件配置失败 [{configPath}]: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 删除插件的 Profile 特定配置
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        /// <param name="pluginId">插件 ID</param>
        /// <returns>是否成功删除</returns>
        public bool DeletePluginConfig(string profileId, string pluginId)
        {
            if (string.IsNullOrWhiteSpace(profileId) || string.IsNullOrWhiteSpace(pluginId))
                return false;

            var configPath = GetPluginConfigPath(profileId, pluginId);

            try
            {
                if (File.Exists(configPath))
                {
                    File.Delete(configPath);
                }
                return true;
            }
            catch (Exception ex)
            {
                LogService.Instance.Debug("ProfileManager", $"删除插件配置失败 [{configPath}]: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取插件配置文件路径
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        /// <param name="pluginId">插件 ID</param>
        /// <returns>配置文件路径</returns>
        private string GetPluginConfigPath(string profileId, string pluginId)
        {
            var profileDir = GetProfileDirectory(profileId);
            return Path.Combine(profileDir, "plugin-configs", $"{pluginId}.json");
        }

        /// <summary>
        /// 获取 Profile 的插件配置目录
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        /// <returns>插件配置目录路径</returns>
        public string GetPluginConfigsDirectory(string profileId)
        {
            return Path.Combine(GetProfileDirectory(profileId), "plugin-configs");
        }

        /// <summary>
        /// 获取 Profile 中所有插件的配置
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        /// <returns>插件ID到配置的字典</returns>
        public Dictionary<string, Dictionary<string, object>> GetAllPluginConfigs(string profileId)
        {
            var result = new Dictionary<string, Dictionary<string, object>>();
            var configsDir = GetPluginConfigsDirectory(profileId);

            if (!Directory.Exists(configsDir))
                return result;

            try
            {
                foreach (var file in Directory.GetFiles(configsDir, "*.json"))
                {
                    var pluginId = Path.GetFileNameWithoutExtension(file);
                    var config = JsonHelper.LoadFromFile<Dictionary<string, object>>(file);
                    if (config != null)
                    {
                        result[pluginId] = config;
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Debug("ProfileManager", $"加载所有插件配置失败: {ex.Message}");
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
