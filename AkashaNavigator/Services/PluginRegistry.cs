using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using AkashaNavigator.Helpers;
using AkashaNavigator.Core.Interfaces;

namespace AkashaNavigator.Services
{
/// <summary>
/// 内置插件信息（用于插件市场）
/// </summary>
public class BuiltInPluginInfo
{
    /// <summary>
    /// 插件 ID
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 插件名称
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 插件版本
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// 插件作者
    /// </summary>
    [JsonPropertyName("author")]
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// 插件描述
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 插件标签
    /// </summary>
    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// 所需权限列表
    /// </summary>
    [JsonPropertyName("permissions")]
    public List<string> Permissions { get; set; } = new();

    /// <summary>
    /// 推荐的 Profile ID 列表
    /// </summary>
    [JsonPropertyName("profiles")]
    public List<string> Profiles { get; set; } = new();
}

/// <summary>
/// 内置插件索引文件结构
/// </summary>
internal class PluginRegistryData
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("plugins")]
    public List<BuiltInPluginInfo> Plugins { get; set; } = new();
}

/// <summary>
/// 插件注册表服务
/// 管理内置插件索引（只读）
/// </summary>
public class PluginRegistry : IPluginRegistry
{
#region Singleton

    private static IPluginRegistry? _instance;

    /// <summary>
    /// 获取单例实例（插件系统使用）
    /// </summary>
    public static IPluginRegistry Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new PluginRegistry(LogService.Instance);
            }
            return _instance;
        }
        set => _instance = value;
    }

    /// <summary>
    /// 重置单例实例（仅用于测试）
    /// </summary>
    internal static void ResetInstance()
    {
        _instance = null;
    }

#endregion

#region Properties

    /// <summary>
    /// 内置插件目录（exe 同级的 Plugins/）
    /// </summary>
    public string BuiltInPluginsDirectory { get; }

    /// <summary>
    /// 索引文件路径
    /// </summary>
    private string RegistryFilePath => Path.Combine(BuiltInPluginsDirectory, "registry.json");

    /// <summary>
    /// 缓存的插件列表
    /// </summary>
    private List<BuiltInPluginInfo> _plugins = new();

    /// <summary>
    /// 是否已加载
    /// </summary>
    private bool _isLoaded = false;

    /// <summary>
    /// 日志服务
    /// </summary>
    private readonly ILogService _logService;

#endregion

#region Constructor

    public PluginRegistry()
    {
        _logService = _logService;
        BuiltInPluginsDirectory = AppPaths.BuiltInPluginsDirectory;
    }

    /// <summary>
    /// DI容器使用的构造函数
    /// </summary>
    public PluginRegistry(ILogService logService)
    {
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        BuiltInPluginsDirectory = AppPaths.BuiltInPluginsDirectory;
    }

    /// <summary>
    /// 用于测试的构造函数
    /// </summary>
    /// <param name="builtInPluginsDirectory">内置插件目录路径</param>
    internal PluginRegistry(string builtInPluginsDirectory)
    {
        _logService = _logService;
        BuiltInPluginsDirectory = builtInPluginsDirectory;
    }

#endregion

#region Public Methods

    /// <summary>
    /// 获取所有内置插件信息
    /// </summary>
    /// <returns>插件信息列表</returns>
    public List<BuiltInPluginInfo> GetAllPlugins()
    {
        EnsureLoaded();
        return new List<BuiltInPluginInfo>(_plugins);
    }

    /// <summary>
    /// 根据 ID 获取插件信息
    /// </summary>
    /// <param name="pluginId">插件 ID</param>
    /// <returns>插件信息，不存在时返回 null</returns>
    public BuiltInPluginInfo? GetPlugin(string pluginId)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
            return null;

        EnsureLoaded();
        return _plugins.Find(p => p.Id.Equals(pluginId, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 获取插件源码目录
    /// </summary>
    /// <param name="pluginId">插件 ID</param>
    /// <returns>源码目录路径</returns>
    public string GetPluginSourceDirectory(string pluginId)
    {
        return Path.Combine(BuiltInPluginsDirectory, pluginId);
    }

    /// <summary>
    /// 检查插件是否存在于注册表中
    /// </summary>
    /// <param name="pluginId">插件 ID</param>
    /// <returns>是否存在</returns>
    public bool PluginExists(string pluginId)
    {
        return GetPlugin(pluginId) != null;
    }

    /// <summary>
    /// 重新加载索引
    /// </summary>
    public void Reload()
    {
        _isLoaded = false;
        _plugins.Clear();
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
        _plugins.Clear();

        if (!File.Exists(RegistryFilePath))
        {
            _logService.Warn("PluginRegistry", "索引文件不存在: {RegistryFilePath}", RegistryFilePath);
            return;
        }

        try
        {
            var data = JsonHelper.LoadFromFile<PluginRegistryData>(RegistryFilePath);
            if (data?.Plugins != null)
            {
                _plugins = data.Plugins;
                _logService.Debug("PluginRegistry", "已加载 {PluginCount} 个内置插件", _plugins.Count);
            }
        }
        catch (Exception ex)
        {
            _logService.Error("PluginRegistry", ex, "加载索引文件失败");
        }
    }

#endregion
}
}
