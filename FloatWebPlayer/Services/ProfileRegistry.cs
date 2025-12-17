using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using FloatWebPlayer.Helpers;

namespace FloatWebPlayer.Services
{
    /// <summary>
    /// 内置 Profile 模板信息（用于 Profile 市场）
    /// 与 FloatWebPlayer.Plugins.ProfileInfo 不同，此类包含市场展示所需的额外信息
    /// </summary>
    public class BuiltInProfileInfo
    {
        /// <summary>
        /// Profile ID
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Profile 名称
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Profile 图标（emoji）
        /// </summary>
        [JsonPropertyName("icon")]
        public string Icon { get; set; } = string.Empty;

        /// <summary>
        /// Profile 描述
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 推荐的插件 ID 列表
        /// </summary>
        [JsonPropertyName("recommendedPlugins")]
        public List<string> RecommendedPlugins { get; set; } = new();
    }

    /// <summary>
    /// 内置 Profile 索引文件结构
    /// </summary>
    internal class ProfileRegistryData
    {
        [JsonPropertyName("version")]
        public int Version { get; set; } = 1;

        [JsonPropertyName("profiles")]
        public List<BuiltInProfileInfo> Profiles { get; set; } = new();
    }

    /// <summary>
    /// Profile 注册表服务
    /// 管理内置 Profile 模板索引（只读）
    /// </summary>
    public class ProfileRegistry
    {
        #region Singleton

        private static ProfileRegistry? _instance;
        private static readonly object _lock = new();

        /// <summary>
        /// 获取单例实例
        /// </summary>
        public static ProfileRegistry Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new ProfileRegistry();
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
        /// 内置 Profile 目录（exe 同级的 Profiles/）
        /// </summary>
        public string BuiltInProfilesDirectory { get; }

        /// <summary>
        /// 索引文件路径
        /// </summary>
        private string RegistryFilePath => Path.Combine(BuiltInProfilesDirectory, "registry.json");

        /// <summary>
        /// 缓存的 Profile 列表
        /// </summary>
        private List<BuiltInProfileInfo> _profiles = new();

        /// <summary>
        /// 是否已加载
        /// </summary>
        private bool _isLoaded = false;

        #endregion

        #region Constructor

        private ProfileRegistry()
        {
            BuiltInProfilesDirectory = AppPaths.BuiltInProfilesDirectory;
        }

        /// <summary>
        /// 用于测试的构造函数
        /// </summary>
        /// <param name="builtInProfilesDirectory">内置 Profile 目录路径</param>
        internal ProfileRegistry(string builtInProfilesDirectory)
        {
            BuiltInProfilesDirectory = builtInProfilesDirectory;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 获取所有内置 Profile 信息
        /// </summary>
        /// <returns>Profile 信息列表</returns>
        public List<BuiltInProfileInfo> GetAllProfiles()
        {
            EnsureLoaded();
            return new List<BuiltInProfileInfo>(_profiles);
        }

        /// <summary>
        /// 根据 ID 获取 Profile 信息
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        /// <returns>Profile 信息，不存在时返回 null</returns>
        public BuiltInProfileInfo? GetProfile(string profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId))
                return null;

            EnsureLoaded();
            return _profiles.Find(p => p.Id.Equals(profileId, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 获取 Profile 模板目录
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        /// <returns>模板目录路径</returns>
        public string GetProfileTemplateDirectory(string profileId)
        {
            return Path.Combine(BuiltInProfilesDirectory, profileId);
        }

        /// <summary>
        /// 检查 Profile 是否存在于注册表中
        /// </summary>
        /// <param name="profileId">Profile ID</param>
        /// <returns>是否存在</returns>
        public bool ProfileExists(string profileId)
        {
            return GetProfile(profileId) != null;
        }

        /// <summary>
        /// 重新加载索引
        /// </summary>
        public void Reload()
        {
            _isLoaded = false;
            _profiles.Clear();
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
            _profiles.Clear();

            if (!File.Exists(RegistryFilePath))
            {
                LogService.Instance.Warn("ProfileRegistry", $"索引文件不存在: {RegistryFilePath}");
                return;
            }

            try
            {
                var data = JsonHelper.LoadFromFile<ProfileRegistryData>(RegistryFilePath);
                if (data?.Profiles != null)
                {
                    _profiles = data.Profiles;
                    LogService.Instance.Debug("ProfileRegistry", $"已加载 {_profiles.Count} 个内置 Profile");
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Error("ProfileRegistry", $"加载索引文件失败: {ex.Message}");
            }
        }

        #endregion
    }
}
