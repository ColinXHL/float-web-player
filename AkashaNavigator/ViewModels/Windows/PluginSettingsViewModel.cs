using System;
using System.IO;
using System.Threading.Tasks;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Helpers;
using AkashaNavigator.Models.Config;
using AkashaNavigator.Models.Plugin;

namespace AkashaNavigator.ViewModels.Windows;

public class PluginSettingsViewModel
{
    private readonly IProfileManager _profileManager;
    private readonly ILogService _logService;
    private readonly IPluginHost _pluginHost;
    private readonly INotificationService _notificationService;

    public string PluginId { get; }

    public string PluginName { get; }

    public string PluginDirectory { get; }

    public string ConfigDirectory { get; }

    public string? ProfileId { get; }

    public bool IsDirty { get; private set; }

    public PluginConfig Config { get; }

    public SettingsUiDefinition? SettingsDefinition { get; }

    public PluginSettingsViewModel(
        IProfileManager profileManager,
        ILogService logService,
        IPluginHost pluginHost,
        INotificationService notificationService,
        string pluginId,
        string pluginName,
        string pluginDirectory,
        string configDirectory,
        string? profileId = null)
    {
        _profileManager = profileManager ?? throw new ArgumentNullException(nameof(profileManager));
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        _pluginHost = pluginHost ?? throw new ArgumentNullException(nameof(pluginHost));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));

        PluginId = pluginId;
        PluginName = pluginName;
        PluginDirectory = pluginDirectory;
        ConfigDirectory = configDirectory;
        ProfileId = profileId;

        var configPath = Path.Combine(configDirectory, AppConstants.PluginConfigFileName);
        Config = PluginConfig.LoadFromFile(configPath, pluginId);

        var manifestPath = Path.Combine(pluginDirectory, AppConstants.PluginManifestFileName);
        var manifestResult = PluginManifest.LoadFromFile(manifestPath);
        var manifest = manifestResult.IsSuccess ? manifestResult.Manifest : null;
        if (manifest?.DefaultConfig != null)
        {
            Config.ApplyDefaults(manifest.DefaultConfig);
        }

        var settingsUiPath = PluginSettingsPathResolver.ResolveSettingsFile(
            pluginDirectory,
            manifest?.Settings);
        SettingsDefinition = settingsUiPath == null
            ? null
            : SettingsUiDefinition.LoadFromFile(settingsUiPath);
    }

    public void UpdateValue(string key, object? value)
    {
        Config.Set(key, value);
        IsDirty = true;
    }

    public void RemoveValue(string key)
    {
        Config.Remove(key);
        IsDirty = true;
    }

    public void MarkDirty()
    {
        IsDirty = true;
    }

    public async Task SaveAsync(bool notifyUser = true, bool reloadPlugin = true)
    {
        var configPath = Path.Combine(ConfigDirectory, AppConstants.PluginConfigFileName);
        var saveResult = Config.SaveToFile(configPath);
        if (!saveResult.IsSuccess)
        {
            _logService.Error(nameof(PluginSettingsViewModel), "保存插件配置失败: {ErrorCode}",
                              saveResult.Error?.Code ?? "UNKNOWN");
            if (notifyUser)
            {
                _notificationService.Error("保存设置失败，请查看日志获取详细信息");
            }

            return;
        }

        NotifyConfigChanged(null, null);

        var needsReload = reloadPlugin && IsCurrentProfileActive();
        if (needsReload)
        {
            _pluginHost.ReloadPlugin(PluginId);
        }

        if (notifyUser)
        {
            _notificationService.Success(needsReload ? "设置已保存，插件已重新加载" : "设置已保存");
        }

        IsDirty = false;
        await Task.CompletedTask;
    }

    public bool IsCurrentProfileActive()
    {
        var currentProfileId = _profileManager.CurrentProfile?.Id;
        return !string.IsNullOrEmpty(ProfileId) &&
               !string.IsNullOrEmpty(currentProfileId) &&
               string.Equals(ProfileId, currentProfileId, StringComparison.OrdinalIgnoreCase);
    }

    public void NotifyConfigChanged(string? key, object? value)
    {
        try
        {
            _pluginHost.BroadcastEvent("configChanged", new { pluginId = PluginId, key, value });
        }
        catch (Exception ex)
        {
            _logService.Error(nameof(PluginSettingsViewModel), ex, "通知插件配置变更失败");
        }
    }

    public void NotifyAction(string action)
    {
        try
        {
            _pluginHost.BroadcastEvent("settingsAction", new { pluginId = PluginId, action });
        }
        catch (Exception ex)
        {
            _logService.Error(nameof(PluginSettingsViewModel), ex, "通知插件动作失败");
        }
    }

    public void ShowWarning(string message)
    {
        _notificationService.Warning(message);
    }

    public void ShowSuccess(string message)
    {
        _notificationService.Success(message);
    }

    public void ShowError(string message)
    {
        _notificationService.Error(message);
    }
}
