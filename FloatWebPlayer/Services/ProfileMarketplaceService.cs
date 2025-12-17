using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using FloatWebPlayer.Helpers;
using FloatWebPlayer.Models;

namespace FloatWebPlayer.Services
{
    #region Result Types

    /// <summary>
    /// 添加订阅源结果
    /// </summary>
    public class AddSourceResult
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// 错误消息
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 订阅源名称
        /// </summary>
        public string? SourceName { get; set; }

        /// <summary>
        /// Profile 数量
        /// </summary>
        public int ProfileCount { get; set; }

        /// <summary>
        /// 创建成功结果
        /// </summary>
        public static AddSourceResult Success(string sourceName, int profileCount)
        {
            return new AddSourceResult
            {
                IsSuccess = true,
                SourceName = sourceName,
                ProfileCount = profileCount
            };
        }

        /// <summary>
        /// 创建失败结果
        /// </summary>
        public static AddSourceResult Failure(string errorMessage)
        {
            return new AddSourceResult
            {
                IsSuccess = false,
                ErrorMessage = errorMessage
            };
        }
    }

    /// <summary>
    /// Profile 卸载结果
    /// </summary>
    public class ProfileUninstallResult
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// 错误消息
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 成功卸载的插件列表
        /// </summary>
        public List<string> UninstalledPlugins { get; set; } = new();

        /// <summary>
        /// 卸载失败的插件列表
        /// </summary>
        public List<string> FailedPlugins { get; set; } = new();

        /// <summary>
        /// 创建成功结果
        /// </summary>
        /// <param name="uninstalledPlugins">成功卸载的插件列表</param>
        public static ProfileUninstallResult Success(List<string>? uninstalledPlugins = null)
        {
            return new ProfileUninstallResult
            {
                IsSuccess = true,
                UninstalledPlugins = uninstalledPlugins ?? new List<string>()
            };
        }

        /// <summary>
        /// 创建失败结果
        /// </summary>
        /// <param name="errorMessage">错误消息</param>
        public static ProfileUninstallResult Failure(string errorMessage)
        {
            return new ProfileUninstallResult
            {
                IsSuccess = false,
                ErrorMessage = errorMessage
            };
        }

        /// <summary>
        /// 创建部分成功结果（Profile 卸载成功，部分插件卸载失败）
        /// </summary>
        /// <param name="uninstalledPlugins">成功卸载的插件列表</param>
        /// <param name="failedPlugins">卸载失败的插件列表</param>
        public static ProfileUninstallResult PartialSuccess(List<string> uninstalledPlugins, List<string> failedPlugins)
        {
            return new ProfileUninstallResult
            {
                IsSuccess = true,
                UninstalledPlugins = uninstalledPlugins,
                FailedPlugins = failedPlugins
            };
        }
    }

    /// <summary>
    /// Profile 安装结果
    /// </summary>
    public class ProfileInstallResult
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// 错误消息
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 安装的 Profile ID
        /// </summary>
        public string? ProfileId { get; set; }

        /// <summary>
        /// 缺失的插件列表
        /// </summary>
        public List<string> MissingPlugins { get; set; } = new();

        /// <summary>
        /// Profile 是否已存在
        /// </summary>
        public bool ProfileExisted { get; set; }

        /// <summary>
        /// 创建成功结果
        /// </summary>
        public static ProfileInstallResult Success(string profileId, List<string>? missingPlugins = null)
        {
            return new ProfileInstallResult
            {
                IsSuccess = true,
                ProfileId = profileId,
                MissingPlugins = missingPlugins ?? new List<string>()
            };
        }

        /// <summary>
        /// 创建失败结果
        /// </summary>
        public static ProfileInstallResult Failure(string errorMessage)
        {
            return new ProfileInstallResult
            {
                IsSuccess = false,
                ErrorMessage = errorMessage
            };
        }

        /// <summary>
        /// 创建 Profile 已存在结果
        /// </summary>
        public static ProfileInstallResult Exists(string profileId, List<string>? missingPlugins = null)
        {
            return new ProfileInstallResult
            {
                IsSuccess = false,
                ProfileId = profileId,
                ProfileExisted = true,
                MissingPlugins = missingPlugins ?? new List<string>(),
                ErrorMessage = $"Profile '{profileId}' 已存在"
            };
        }
    }

    #endregion

    /// <summary>
    /// Profile 市场服务
    /// 负责从订阅源获取 Profile 列表和安装 Profile
    /// </summary>
    public class ProfileMarketplaceService
    {
        #region Singleton

        private static ProfileMarketplaceService? _instance;
        private static readonly object _lock = new();

        /// <summary>
        /// 获取单例实例
        /// </summary>
        public static ProfileMarketplaceService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new ProfileMarketplaceService();
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

        #region Fields

        private MarketplaceSourceConfig _sourceConfig;
        private readonly string _configFilePath;
        private readonly HttpClient _httpClient;
        private List<MarketplaceProfile> _cachedProfiles = new();
        private readonly object _cacheLock = new();

        #endregion

        #region Properties

        /// <summary>
        /// 订阅源配置文件路径
        /// </summary>
        public string ConfigFilePath => _configFilePath;

        /// <summary>
        /// HTTP 请求超时时间（秒）
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;

        #endregion

        #region Constructor

        private ProfileMarketplaceService()
        {
            _configFilePath = Path.Combine(AppPaths.DataDirectory, "marketplace-sources.json");
            _sourceConfig = LoadConfig();
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(TimeoutSeconds)
            };
        }

        /// <summary>
        /// 用于测试的构造函数
        /// </summary>
        internal ProfileMarketplaceService(string configFilePath, HttpClient? httpClient = null)
        {
            _configFilePath = configFilePath;
            _sourceConfig = LoadConfig();
            _httpClient = httpClient ?? new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(TimeoutSeconds)
            };
        }

        #endregion

        #region Config Management

        /// <summary>
        /// 加载订阅源配置
        /// </summary>
        private MarketplaceSourceConfig LoadConfig()
        {
            return MarketplaceSourceConfig.LoadFromFile(_configFilePath);
        }

        /// <summary>
        /// 保存订阅源配置
        /// </summary>
        private void SaveConfig()
        {
            _sourceConfig.SaveToFile(_configFilePath);
        }

        /// <summary>
        /// 重新加载配置
        /// </summary>
        public void ReloadConfig()
        {
            _sourceConfig = LoadConfig();
        }

        #endregion


        #region Subscription Source Management

        /// <summary>
        /// 获取所有订阅源
        /// </summary>
        /// <returns>订阅源列表</returns>
        public List<MarketplaceSource> GetSubscriptionSources()
        {
            return new List<MarketplaceSource>(_sourceConfig.Sources);
        }

        /// <summary>
        /// 验证 URL 格式
        /// </summary>
        /// <param name="url">URL 字符串</param>
        /// <returns>是否为有效的 HTTP/HTTPS URL</returns>
        public static bool IsValidUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return false;

            return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
        }

        /// <summary>
        /// 添加订阅源
        /// </summary>
        /// <param name="url">订阅源 URL</param>
        /// <returns>添加结果</returns>
        public async Task<AddSourceResult> AddSubscriptionSourceAsync(string url)
        {
            // 验证 URL 格式
            if (!IsValidUrl(url))
            {
                return AddSourceResult.Failure("无效的 URL 格式，请输入有效的 HTTP 或 HTTPS 地址");
            }

            // 检查是否已存在
            if (_sourceConfig.Sources.Any(s => s.Url.Equals(url, StringComparison.OrdinalIgnoreCase)))
            {
                return AddSourceResult.Failure("该订阅源已存在");
            }

            // 尝试获取注册表以验证订阅源
            try
            {
                var registry = await FetchRegistryAsync(url);
                if (registry == null)
                {
                    return AddSourceResult.Failure("无法解析订阅源数据，请检查 URL 是否正确");
                }

                // 添加到配置
                var source = new MarketplaceSource
                {
                    Url = url,
                    Name = registry.Name,
                    Enabled = true,
                    LastFetched = DateTime.Now
                };

                _sourceConfig.Sources.Add(source);
                SaveConfig();

                return AddSourceResult.Success(registry.Name, registry.Profiles.Count);
            }
            catch (HttpRequestException ex)
            {
                return AddSourceResult.Failure($"网络请求失败: {ex.Message}");
            }
            catch (TaskCanceledException)
            {
                return AddSourceResult.Failure("请求超时，请检查网络连接");
            }
            catch (Exception ex)
            {
                return AddSourceResult.Failure($"添加订阅源失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 同步添加订阅源（用于简单场景）
        /// </summary>
        /// <param name="url">订阅源 URL</param>
        /// <returns>添加结果</returns>
        public AddSourceResult AddSubscriptionSource(string url)
        {
            // 验证 URL 格式
            if (!IsValidUrl(url))
            {
                return AddSourceResult.Failure("无效的 URL 格式，请输入有效的 HTTP 或 HTTPS 地址");
            }

            // 检查是否已存在
            if (_sourceConfig.Sources.Any(s => s.Url.Equals(url, StringComparison.OrdinalIgnoreCase)))
            {
                return AddSourceResult.Failure("该订阅源已存在");
            }

            // 添加到配置（不验证远程）
            var source = new MarketplaceSource
            {
                Url = url,
                Name = string.Empty,
                Enabled = true
            };

            _sourceConfig.Sources.Add(source);
            SaveConfig();

            return AddSourceResult.Success(string.Empty, 0);
        }

        /// <summary>
        /// 移除订阅源
        /// </summary>
        /// <param name="url">订阅源 URL</param>
        /// <returns>是否成功移除</returns>
        public bool RemoveSubscriptionSource(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            var removed = _sourceConfig.RemoveSource(url);
            if (removed)
            {
                SaveConfig();
                
                // 清除缓存中来自该订阅源的 Profile
                lock (_cacheLock)
                {
                    _cachedProfiles.RemoveAll(p => 
                        p.SourceUrl.Equals(url, StringComparison.OrdinalIgnoreCase));
                }
            }

            return removed;
        }

        #endregion

        #region Profile Fetching

        /// <summary>
        /// 从 URL 获取注册表
        /// </summary>
        /// <param name="url">注册表 URL</param>
        /// <returns>注册表数据，失败返回 null</returns>
        private async Task<ProfileMarketplaceRegistry?> FetchRegistryAsync(string url)
        {
            try
            {
                var response = await _httpClient.GetStringAsync(url);
                return ProfileMarketplaceRegistry.FromJson(response);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 从所有订阅源获取可用 Profile 列表
        /// </summary>
        /// <returns>Profile 列表</returns>
        public async Task<List<MarketplaceProfile>> FetchAvailableProfilesAsync()
        {
            var profiles = new List<MarketplaceProfile>();
            
            // 首先加载本地内置的 Profile 市场
            var builtInProfiles = LoadBuiltInMarketplaceProfiles();
            profiles.AddRange(builtInProfiles);
            
            // 然后从远程订阅源获取
            var enabledSources = _sourceConfig.GetEnabledSources();

            foreach (var source in enabledSources)
            {
                try
                {
                    var registry = await FetchRegistryAsync(source.Url);
                    if (registry != null)
                    {
                        // 更新订阅源名称和最后获取时间
                        source.Name = registry.Name;
                        source.LastFetched = DateTime.Now;

                        // 转换为 MarketplaceProfile
                        foreach (var entry in registry.Profiles)
                        {
                            var profile = entry.ToMarketplaceProfile(source.Url);
                            profiles.Add(profile);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogService.Instance.Warn("ProfileMarketplaceService", 
                        $"获取订阅源 '{source.Url}' 失败: {ex.Message}");
                }
            }

            // 保存更新后的订阅源信息
            SaveConfig();

            // 更新缓存
            lock (_cacheLock)
            {
                _cachedProfiles = new List<MarketplaceProfile>(profiles);
            }

            return profiles;
        }
        
        /// <summary>
        /// 加载本地内置的 Profile 市场数据
        /// </summary>
        /// <returns>内置 Profile 列表</returns>
        private List<MarketplaceProfile> LoadBuiltInMarketplaceProfiles()
        {
            var profiles = new List<MarketplaceProfile>();
            
            // 内置注册表路径：exe 同级的 profiles/registry.json
            var registryPath = Path.Combine(AppPaths.BuiltInProfilesDirectory, "registry.json");
            
            if (!File.Exists(registryPath))
            {
                LogService.Instance.Debug("ProfileMarketplaceService", 
                    $"内置 Profile 市场注册表不存在: {registryPath}");
                return profiles;
            }
            
            try
            {
                var json = File.ReadAllText(registryPath);
                var registry = ProfileMarketplaceRegistry.FromJson(json);
                
                if (registry != null)
                {
                    const string builtInSourceUrl = "builtin://profiles";
                    
                    foreach (var entry in registry.Profiles)
                    {
                        var profile = entry.ToMarketplaceProfile(builtInSourceUrl);
                        profiles.Add(profile);
                    }
                    
                    LogService.Instance.Info("ProfileMarketplaceService", 
                        $"加载了 {profiles.Count} 个内置 Profile");
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Warn("ProfileMarketplaceService", 
                    $"加载内置 Profile 市场失败: {ex.Message}");
            }
            
            return profiles;
        }
        
        /// <summary>
        /// 从本地内置目录加载 Profile 配置
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        /// <returns>Profile 导出数据，失败返回 null</returns>
        private ProfileExportData? LoadBuiltInProfileConfig(string profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId))
                return null;
            
            // 内置 Profile 配置路径：exe 同级的 profiles/{profileId}/profile.json
            var profilePath = Path.Combine(AppPaths.BuiltInProfilesDirectory, profileId, "profile.json");
            
            if (!File.Exists(profilePath))
            {
                LogService.Instance.Debug("ProfileMarketplaceService", 
                    $"内置 Profile 配置不存在: {profilePath}");
                return null;
            }
            
            try
            {
                var json = File.ReadAllText(profilePath);
                var gameProfile = JsonHelper.Deserialize<GameProfile>(json);
                
                if (gameProfile == null)
                    return null;
                
                // 从注册表获取插件列表
                var registryPath = Path.Combine(AppPaths.BuiltInProfilesDirectory, "registry.json");
                var pluginIds = new List<string>();
                
                if (File.Exists(registryPath))
                {
                    var registryJson = File.ReadAllText(registryPath);
                    var registry = ProfileMarketplaceRegistry.FromJson(registryJson);
                    var entry = registry?.Profiles.FirstOrDefault(p => 
                        p.Id.Equals(profileId, StringComparison.OrdinalIgnoreCase));
                    if (entry != null)
                    {
                        pluginIds = entry.PluginIds;
                    }
                }
                
                // 创建导出数据
                return new ProfileExportData
                {
                    Version = 1,
                    ProfileId = gameProfile.Id,
                    ProfileName = gameProfile.Name,
                    ProfileConfig = gameProfile,
                    PluginReferences = pluginIds
                        .Select(id => new PluginReferenceEntry { PluginId = id, Enabled = true })
                        .ToList(),
                    ExportedAt = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                LogService.Instance.Warn("ProfileMarketplaceService", 
                    $"加载内置 Profile 配置失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取缓存的 Profile 列表
        /// </summary>
        /// <returns>缓存的 Profile 列表</returns>
        public List<MarketplaceProfile> GetCachedProfiles()
        {
            lock (_cacheLock)
            {
                return new List<MarketplaceProfile>(_cachedProfiles);
            }
        }

        /// <summary>
        /// 获取单个 Profile 详情
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        /// <param name="sourceUrl">来源订阅源 URL</param>
        /// <returns>Profile 详情，未找到返回 null</returns>
        public async Task<MarketplaceProfile?> FetchProfileDetailsAsync(string profileId, string sourceUrl)
        {
            if (string.IsNullOrWhiteSpace(profileId) || string.IsNullOrWhiteSpace(sourceUrl))
                return null;

            try
            {
                var registry = await FetchRegistryAsync(sourceUrl);
                if (registry == null)
                    return null;

                var entry = registry.Profiles.FirstOrDefault(p => 
                    p.Id.Equals(profileId, StringComparison.OrdinalIgnoreCase));

                return entry?.ToMarketplaceProfile(sourceUrl);
            }
            catch
            {
                return null;
            }
        }

        #endregion


        #region Profile Search/Filter

        /// <summary>
        /// 过滤 Profile 列表
        /// </summary>
        /// <param name="profiles">Profile 列表</param>
        /// <param name="query">搜索关键词</param>
        /// <returns>过滤后的 Profile 列表</returns>
        public static List<MarketplaceProfile> FilterProfiles(List<MarketplaceProfile> profiles, string query)
        {
            if (profiles == null)
                return new List<MarketplaceProfile>();

            if (string.IsNullOrWhiteSpace(query))
                return new List<MarketplaceProfile>(profiles);

            var lowerQuery = query.ToLowerInvariant();

            return profiles.Where(p =>
                (p.Name?.ToLowerInvariant().Contains(lowerQuery) ?? false) ||
                (p.Description?.ToLowerInvariant().Contains(lowerQuery) ?? false) ||
                (p.TargetGame?.ToLowerInvariant().Contains(lowerQuery) ?? false)
            ).ToList();
        }

        /// <summary>
        /// 从缓存中过滤 Profile
        /// </summary>
        /// <param name="query">搜索关键词</param>
        /// <returns>过滤后的 Profile 列表</returns>
        public List<MarketplaceProfile> FilterCachedProfiles(string query)
        {
            lock (_cacheLock)
            {
                return FilterProfiles(_cachedProfiles, query);
            }
        }

        #endregion

        #region Unique Plugin Detection

        /// <summary>
        /// 获取仅被指定 Profile 使用的插件列表（唯一插件）
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        /// <returns>唯一插件 ID 列表</returns>
        public List<string> GetUniquePlugins(string profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId))
                return new List<string>();

            // 获取目标 Profile 的所有插件
            var targetPlugins = PluginAssociationManager.Instance.GetPluginsInProfile(profileId);
            if (targetPlugins.Count == 0)
                return new List<string>();

            var uniquePlugins = new List<string>();

            // 获取所有已安装 Profile 的 ID
            var allProfileIds = PluginAssociationManager.Instance.GetAllProfileIds();

            foreach (var pluginRef in targetPlugins)
            {
                var pluginId = pluginRef.PluginId;
                var isUnique = true;

                // 检查该插件是否被其他 Profile 使用
                foreach (var otherProfileId in allProfileIds)
                {
                    // 跳过目标 Profile 自身
                    if (otherProfileId.Equals(profileId, StringComparison.OrdinalIgnoreCase))
                        continue;

                    // 检查其他 Profile 是否包含该插件
                    if (PluginAssociationManager.Instance.ProfileContainsPlugin(otherProfileId, pluginId))
                    {
                        isUnique = false;
                        break;
                    }
                }

                if (isUnique)
                {
                    uniquePlugins.Add(pluginId);
                }
            }

            return uniquePlugins;
        }

        #endregion

        #region Profile Uninstallation

        /// <summary>
        /// 卸载 Profile 及可选的插件
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        /// <param name="pluginsToUninstall">要一并卸载的插件 ID 列表（可选）</param>
        /// <returns>卸载结果</returns>
        public ProfileUninstallResult UninstallProfile(string profileId, List<string>? pluginsToUninstall = null)
        {
            if (string.IsNullOrWhiteSpace(profileId))
            {
                return ProfileUninstallResult.Failure("Profile ID 不能为空");
            }

            // 检查是否是默认 Profile
            if (profileId.Equals(AppConstants.DefaultProfileId, StringComparison.OrdinalIgnoreCase))
            {
                return ProfileUninstallResult.Failure("默认 Profile 不能卸载");
            }

            // 删除 Profile
            var deleteResult = ProfileManager.Instance.DeleteProfile(profileId);
            if (!deleteResult.IsSuccess)
            {
                return ProfileUninstallResult.Failure(deleteResult.ErrorMessage ?? "删除 Profile 失败");
            }

            // 如果没有要卸载的插件，直接返回成功
            if (pluginsToUninstall == null || pluginsToUninstall.Count == 0)
            {
                return ProfileUninstallResult.Success();
            }

            // 卸载选中的插件
            var uninstalledPlugins = new List<string>();
            var failedPlugins = new List<string>();

            foreach (var pluginId in pluginsToUninstall)
            {
                try
                {
                    // 先从所有 Profile 中移除该插件的关联
                    PluginAssociationManager.Instance.RemovePluginFromAllProfiles(pluginId);

                    // 卸载插件
                    var uninstallResult = PluginLibrary.Instance.UninstallPlugin(pluginId, force: true);
                    if (uninstallResult.IsSuccess)
                    {
                        uninstalledPlugins.Add(pluginId);
                    }
                    else
                    {
                        failedPlugins.Add(pluginId);
                        LogService.Instance.Warn("ProfileMarketplaceService", 
                            $"卸载插件 '{pluginId}' 失败: {uninstallResult.ErrorMessage}");
                    }
                }
                catch (Exception ex)
                {
                    failedPlugins.Add(pluginId);
                    LogService.Instance.Error("ProfileMarketplaceService", 
                        $"卸载插件 '{pluginId}' 时发生异常: {ex.Message}");
                }
            }

            // 返回结果
            if (failedPlugins.Count == 0)
            {
                return ProfileUninstallResult.Success(uninstalledPlugins);
            }
            else
            {
                return ProfileUninstallResult.PartialSuccess(uninstalledPlugins, failedPlugins);
            }
        }

        #endregion

        #region Profile Installation

        /// <summary>
        /// 检测缺失的插件
        /// </summary>
        /// <param name="profile">市场 Profile</param>
        /// <returns>缺失的插件 ID 列表</returns>
        public List<string> GetMissingPlugins(MarketplaceProfile profile)
        {
            if (profile == null || profile.PluginIds == null)
                return new List<string>();

            var missingPlugins = new List<string>();

            foreach (var pluginId in profile.PluginIds)
            {
                if (!PluginLibrary.Instance.IsInstalled(pluginId))
                {
                    missingPlugins.Add(pluginId);
                }
            }

            return missingPlugins;
        }

        /// <summary>
        /// 检查 Profile 是否已存在
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        /// <returns>是否存在</returns>
        public bool ProfileExists(string profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId))
                return false;

            return ProfileManager.Instance.GetProfileById(profileId) != null;
        }

        /// <summary>
        /// 安装市场 Profile
        /// </summary>
        /// <param name="profile">市场 Profile</param>
        /// <param name="overwrite">如果已存在是否覆盖</param>
        /// <returns>安装结果</returns>
        public async Task<ProfileInstallResult> InstallProfileAsync(MarketplaceProfile profile, bool overwrite = false)
        {
            if (profile == null)
                return ProfileInstallResult.Failure("Profile 数据为空");

            if (string.IsNullOrWhiteSpace(profile.Id))
                return ProfileInstallResult.Failure("Profile ID 为空");

            // 检测缺失插件
            var missingPlugins = GetMissingPlugins(profile);

            // 检查 Profile 是否已存在
            if (ProfileExists(profile.Id) && !overwrite)
            {
                return ProfileInstallResult.Exists(profile.Id, missingPlugins);
            }

            // 获取 Profile 配置
            ProfileExportData? exportData = null;
            
            // 检查是否是内置 Profile
            if (profile.SourceUrl == "builtin://profiles")
            {
                // 从本地内置目录加载
                exportData = LoadBuiltInProfileConfig(profile.Id);
            }
            else if (!string.IsNullOrWhiteSpace(profile.DownloadUrl))
            {
                // 从远程下载
                try
                {
                    var json = await _httpClient.GetStringAsync(profile.DownloadUrl);
                    exportData = ProfileExportData.FromJson(json);
                }
                catch (Exception ex)
                {
                    LogService.Instance.Warn("ProfileMarketplaceService", 
                        $"下载 Profile 配置失败: {ex.Message}");
                }
            }

            // 如果没有下载到配置，创建基本配置
            if (exportData == null)
            {
                exportData = new ProfileExportData
                {
                    Version = 1,
                    ProfileId = profile.Id,
                    ProfileName = profile.Name,
                    ProfileConfig = new GameProfile
                    {
                        Id = profile.Id,
                        Name = profile.Name
                    },
                    PluginReferences = profile.PluginIds
                        .Select(id => new PluginReferenceEntry { PluginId = id, Enabled = true })
                        .ToList(),
                    ExportedAt = DateTime.Now
                };
            }

            // 使用 ProfileManager 导入
            var importResult = ProfileManager.Instance.ImportProfile(exportData, overwrite);

            if (importResult.IsSuccess)
            {
                // 保存原始插件列表（用于后续显示缺失状态）
                PluginAssociationManager.Instance.SetOriginalPlugins(profile.Id, profile.PluginIds);
                
                return ProfileInstallResult.Success(profile.Id, missingPlugins);
            }
            else if (importResult.ProfileExists)
            {
                return ProfileInstallResult.Exists(profile.Id, missingPlugins);
            }
            else
            {
                return ProfileInstallResult.Failure(importResult.ErrorMessage ?? "安装失败");
            }
        }

        #endregion
    }
}
