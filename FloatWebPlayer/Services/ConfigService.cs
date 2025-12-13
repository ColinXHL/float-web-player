using System;
using System.IO;
using System.Text.Json;
using FloatWebPlayer.Models;

namespace FloatWebPlayer.Services
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
            // 配置文件路径：Data/config.json
            var dataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FloatWebPlayer",
                "Data"
            );
            Directory.CreateDirectory(dataDir);
            ConfigFilePath = Path.Combine(dataDir, "config.json");

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
                if (File.Exists(ConfigFilePath))
                {
                    var json = File.ReadAllText(ConfigFilePath);
                    var options = new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        PropertyNameCaseInsensitive = true
                    };
                    var config = JsonSerializer.Deserialize<AppConfig>(json, options);
                    if (config != null)
                    {
                        return config;
                    }
                }
            }
            catch
            {
                // 加载失败，使用默认配置
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
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                var json = JsonSerializer.Serialize(Config, options);
                File.WriteAllText(ConfigFilePath, json);
            }
            catch
            {
                // 保存失败，忽略
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
