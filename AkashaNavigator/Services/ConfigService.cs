using System;
using AkashaNavigator.Helpers;
using AkashaNavigator.Models.Config;
using AkashaNavigator.Core.Interfaces;

namespace AkashaNavigator.Services
{
/// <summary>
/// 配置管理服务
/// </summary>
public class ConfigService : IConfigService
{
#region Singleton

    private static IConfigService? _instance;

    /// <summary>
    /// 单例实例（插件系统使用）
    /// </summary>
    public static IConfigService Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new ConfigService(LogService.Instance);
            }
            return _instance;
        }
        set => _instance = value;
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

#region Fields

    private readonly ILogService _logService;

#endregion

#region Constructor

    public ConfigService(ILogService logService)
    {
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        ConfigFilePath = AppPaths.ConfigFilePath;
        Config = Load();
    }

#endregion

#region Public Methods

    /// <summary>
    /// 加载配置文件
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
            _logService.Warn("ConfigService", "加载配置失败，将使用默认配置: {ErrorMessage}", ex.Message);
        }

        return new AppConfig();
    }

    /// <summary>
    /// 保存配置到文件
    /// </summary>
    public void Save()
    {
        try
        {
            JsonHelper.SaveToFile(ConfigFilePath, Config);
        }
        catch (Exception ex)
        {
            _logService.Debug("ConfigService", "保存配置失败: {ErrorMessage}", ex.Message);
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
