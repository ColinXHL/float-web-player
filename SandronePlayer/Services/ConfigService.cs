using System;
using SandronePlayer.Helpers;
using SandronePlayer.Models;

namespace SandronePlayer.Services
{
    /// <summary>
    /// 配置管理服务（单例）
    /// 负责全局配置的加载、保存、访问
    /// </summary>
    public class ConfigService
    {
        #region Singleton

        private static ConfigService? _instance;
        private static readonly object _lock = new();

        /// <summary>
        /// 获取单例实例
        /// </summary>
        public static ConfigService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new ConfigService();
                    }
                }
                return _instance;
            }
        }

        #endregion

        #region Events

        /// <summary>
        /// 配置变更事件
        /// </summary>
        public event EventHandler<AppConfig>? ConfigChanged;

        #endregion

        #region Properties

        /// <summary>
        /// 当前配置
        /// </summary>
        public AppConfig Config { get; private set; }

        /// <summary>
        /// 配置文件路径
        /// </summary>
        public string ConfigFilePath { get; }

        #endregion

        #region Constructor

        private ConfigService()
        {
            // 配置文件路径：User/Data/config.json
            ConfigFilePath = AppPaths.ConfigFilePath;

            // 加载配置
            Config = Load();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 加载配置
        /// </summary>
        public AppConfig Load()
        {
            try
            {
                var config = JsonHelper.LoadFromFile<AppConfig>(ConfigFilePath);
                if (config != null)
                {
                    return config;
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Warn("ConfigService", $"加载配置失败，将使用默认配置: {ex.Message}");
            }

            return new AppConfig();
        }

        /// <summary>
        /// 保存配置
        /// </summary>
        public void Save()
        {
            try
            {
                JsonHelper.SaveToFile(ConfigFilePath, Config);
            }
            catch (Exception ex)
            {
                LogService.Instance.Debug("ConfigService", $"保存配置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新配置并保存
        /// </summary>
        public void UpdateConfig(AppConfig newConfig)
        {
            Config = newConfig;
            Save();
            ConfigChanged?.Invoke(this, Config);
        }

        /// <summary>
        /// 重置为默认配置
        /// </summary>
        public void ResetToDefault()
        {
            Config = new AppConfig();
            Save();
            ConfigChanged?.Invoke(this, Config);
        }

        #endregion
    }
}
