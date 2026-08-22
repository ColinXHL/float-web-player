using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AkashaNavigator.Core.Events;
using AkashaNavigator.Core.Events.Events;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Common;
using AkashaNavigator.Models.Config;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Models.PluginRepository;
using AkashaNavigator.Models.Update;

namespace AkashaNavigator.ViewModels.Pages
{
/// <summary>
/// 已安装插件页面的 ViewModel
/// 使用 CommunityToolkit.Mvvm 源生成器
/// </summary>
public partial class InstalledPluginsPageViewModel : ObservableObject, IDisposable
{
    private readonly IPluginLibrary _pluginLibrary;
    private readonly IPluginAssociationManager _pluginAssociationManager;
    private readonly INotificationService _notificationService;
    private readonly IEventBus _eventBus;
    private readonly IPluginSubscriptionService _pluginSubscriptionService;
    private readonly IPluginAcquisitionService _pluginAcquisitionService;

    /// <summary>
    /// 插件列表
    /// </summary>
    public ObservableCollection<InstalledPluginItemModel> Plugins { get; } = new();

    /// <summary>
    /// 搜索文本（自动生成 SearchText 属性和通知）
    /// </summary>
    [ObservableProperty]
    private string _searchText = string.Empty;

    /// <summary>
    /// 插件数量文本（自动生成属性和通知）
    /// </summary>
    [ObservableProperty]
    private string _pluginCountText = "共 0 个插件";

    /// <summary>
    /// 是否无插件（自动生成属性和通知）
    /// </summary>
    [ObservableProperty]
    private bool _isEmpty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UpdatePluginCommand))]
    private bool _isPluginUpdateBusy;

    [ObservableProperty]
    private double _pluginUpdateProgress;

    [ObservableProperty]
    private bool _isPluginUpdateProgressIndeterminate;

    [ObservableProperty]
    private string _pluginUpdateStatusText = string.Empty;

    /// <summary>
    /// 检查更新结果缓存
    /// </summary>
    private Dictionary<string, PluginSubscriptionUpdate> _updateCache = new();

    /// <summary>
    /// 构造函数
    /// </summary>
    public InstalledPluginsPageViewModel(IPluginLibrary pluginLibrary,
                                         IPluginAssociationManager pluginAssociationManager,
                                         INotificationService notificationService,
                                         IEventBus eventBus,
                                         IPluginSubscriptionService pluginSubscriptionService,
                                         IPluginAcquisitionService pluginAcquisitionService)
    {
        _pluginLibrary = pluginLibrary ?? throw new ArgumentNullException(nameof(pluginLibrary));
        _pluginAssociationManager =
            pluginAssociationManager ?? throw new ArgumentNullException(nameof(pluginAssociationManager));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _pluginSubscriptionService =
            pluginSubscriptionService ??
            throw new ArgumentNullException(nameof(pluginSubscriptionService));
        _pluginAcquisitionService =
            pluginAcquisitionService ??
            throw new ArgumentNullException(nameof(pluginAcquisitionService));

        // 订阅插件列表变化事件
        _eventBus.Subscribe<PluginListChangedEvent>(OnPluginListChanged);
        _eventBus.Subscribe<ProfileListChangedEvent>(OnProfileListChanged);
    }

    /// <summary>
    /// 插件列表变化事件处理
    /// </summary>
    private void OnPluginListChanged(PluginListChangedEvent e)
    {
        CheckAndRefreshPluginList();
    }

    /// <summary>
    /// Profile 列表变化事件处理
    /// </summary>
    private void OnProfileListChanged(ProfileListChangedEvent e)
    {
        RefreshPluginList();
    }

    /// <summary>
    /// 页面加载时检查更新并刷新插件列表
    /// </summary>
    [RelayCommand]
    public async Task OnLoadedAsync()
    {
        await CheckAndRefreshPluginListAsync(showResultNotification: false);
    }

    /// <summary>
    /// 搜索文本变化时重新加载（自动生成的方法）
    /// </summary>
    partial void OnSearchTextChanged(string value)
    {
        RefreshPluginList();
    }

    /// <summary>
    /// 检查更新并刷新插件列表
    /// </summary>
    public void CheckAndRefreshPluginList()
    {
        _updateCache = _pluginSubscriptionService
            .GetAvailableUpdates()
            .ToDictionary(update => update.PluginId, update => update);
        RefreshPluginList();
    }

    private async Task CheckAndRefreshPluginListAsync(
        bool showResultNotification)
    {
        var result =
            await _pluginSubscriptionService.CheckForUpdatesAsync();
        if (result.IsFailure)
        {
            RefreshPluginList();
            _notificationService.Show(
                $"检查插件更新失败: {result.Error?.Message}",
                NotificationType.Error);
            return;
        }

        var updates = result.Value!;
        _updateCache = updates
            .ToDictionary(update => update.PluginId, update => update);
        RefreshPluginList();
        if (!showResultNotification)
        {
            return;
        }

        _notificationService.Show(
            updates.Count == 0
                ? "所有已订阅插件都是最新版本"
                : $"发现 {updates.Count} 个插件有可用更新",
            updates.Count == 0
                ? NotificationType.Success
                : NotificationType.Info);
    }

    /// <summary>
    /// 刷新插件列表
    /// </summary>
    public void RefreshPluginList()
    {
        var plugins = _pluginLibrary.GetInstalledPlugins();
        var searchText = SearchText?.ToLower() ?? "";

        // 过滤搜索
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            plugins = plugins
                          .Where(p => p.Name.ToLower().Contains(searchText) ||
                                      (p.Description?.ToLower().Contains(searchText) ?? false))
                          .ToList();
        }

        // 转换为视图模型
        var viewModels = plugins
            .Select(CreatePluginItemModel)
            .OrderByDescending(plugin => plugin.HasUpdate)
            .ThenBy(
                plugin => plugin.Name,
                StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        Plugins.Clear();
        foreach (var vm in viewModels)
        {
            Plugins.Add(vm);
        }

        PluginCountText = $"共 {viewModels.Count} 个插件";
        IsEmpty = viewModels.Count == 0;
    }

    /// <summary>
    /// 创建插件项目模型
    /// </summary>
    private InstalledPluginItemModel CreatePluginItemModel(InstalledPluginInfo plugin)
    {
        var model = new InstalledPluginItemModel { Id = plugin.Id,
                                                   Name = plugin.Name,
                                                   Version = plugin.Version,
                                                   Description = plugin.Description,
                                                   Author = plugin.Author,
                                                   ReferenceCount =
                                                       _pluginAssociationManager.GetPluginReferenceCount(plugin.Id),
                                                   ProfilesText = GetProfilesText(plugin.Id),
                                                   HasDescription = !string.IsNullOrWhiteSpace(plugin.Description) };

        // 设置更新信息
        if (_updateCache.TryGetValue(plugin.Id, out var updateInfo))
        {
            model.HasUpdate = true;
            model.AvailableVersion = updateInfo.AvailableVersion;
        }

        return model;
    }

    /// <summary>
    /// 获取插件关联的 Profile 文本
    /// </summary>
    private string GetProfilesText(string pluginId)
    {
        var profiles = _pluginAssociationManager.GetProfilesUsingPlugin(pluginId);
        if (profiles.Count == 0)
            return "无";
        if (profiles.Count <= 3)
            return string.Join(", ", profiles);
        return $"{string.Join(", ", profiles.Take(3))} 等 {profiles.Count} 个";
    }

    /// <summary>
    /// 检查更新命令（自动生成 CheckUpdateCommand）
    /// </summary>
    [RelayCommand]
    private Task CheckUpdateAsync()
    {
        return CheckAndRefreshPluginListAsync(showResultNotification: true);
    }

    /// <summary>
    /// 更新插件命令（自动生成 UpdatePluginCommand）
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUpdatePlugin))]
    private async Task UpdatePluginAsync(string? pluginId)
    {
        if (string.IsNullOrWhiteSpace(pluginId) || IsPluginUpdateBusy)
            return;

        var pluginInfo = _pluginLibrary.GetInstalledPluginInfo(pluginId);
        var pluginName = pluginInfo?.Name ?? pluginId;

        IsPluginUpdateBusy = true;
        PluginUpdateProgress = 0;
        IsPluginUpdateProgressIndeterminate = true;
        PluginUpdateStatusText = $"正在更新 {pluginName}…";
        Result<InstalledPluginInfo> result;
        try
        {
            var progress = new Progress<PluginDownloadProgress>(
                value =>
                {
                    PluginUpdateProgress = Math.Clamp(value.Percentage, 0, 100);
                    IsPluginUpdateProgressIndeterminate =
                        value.TotalBytes <= 0 || value.Percentage >= 100;
                    PluginUpdateStatusText = value.TotalBytes <= 0
                        ? $"正在更新 {pluginName}，正在下载…"
                        : value.Percentage >= 100
                            ? $"正在更新 {pluginName}，下载完成，正在写入…"
                            : $"正在更新 {pluginName}，{FormatBytes(value.BytesReceived)} / {FormatBytes(value.TotalBytes)}";
                });
            result = await _pluginAcquisitionService.InstallOrUpdateAsync(
                pluginId,
                progress);
        }
        finally
        {
            IsPluginUpdateBusy = false;
            IsPluginUpdateProgressIndeterminate = false;
            PluginUpdateStatusText = string.Empty;
        }

        if (result.IsSuccess)
        {
            _notificationService.Show(
                $"{pluginName} 已更新到 v{result.Value!.Version}",
                NotificationType.Success);
            await CheckAndRefreshPluginListAsync(showResultNotification: false);
        }
        else
        {
            _notificationService.Show(
                $"更新 {pluginName} 失败: {result.Error?.Message}",
                NotificationType.Error);
        }
    }

    private bool CanUpdatePlugin(string? pluginId) =>
        !string.IsNullOrWhiteSpace(pluginId) && !IsPluginUpdateBusy;

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1024L * 1024 * 1024)
        {
            return $"{bytes / (1024d * 1024 * 1024):0.##} GB";
        }

        if (bytes >= 1024L * 1024)
        {
            return $"{bytes / (1024d * 1024):0.##} MB";
        }

        if (bytes >= 1024)
        {
            return $"{bytes / 1024d:0.##} KB";
        }

        return $"{bytes} B";
    }

    /// <summary>
    /// 添加到 Profile 命令（自动生成 AddToProfileCommand）
    /// 注意：此命令需要由 Code-behind 处理对话框显示
    /// </summary>
    [RelayCommand]
    private void AddToProfile(string? pluginId)
    {
        // 此命令由 Code-behind 的 AddToProfileRequested 事件处理
        AddToProfileRequested?.Invoke(this, pluginId);
    }

    /// <summary>
    /// 卸载插件命令（自动生成 UninstallCommand）
    /// 注意：此命令需要由 Code-behind 处理对话框显示
    /// </summary>
    [RelayCommand]
    private void Uninstall(string? pluginId)
    {
        // 此命令由 Code-behind 的 UninstallRequested 事件处理
        UninstallRequested?.Invoke(this, pluginId);
    }

    /// <summary>
    /// 添加到 Profile 请求事件（由 Code-behind 订阅以显示对话框）
    /// </summary>
    public event EventHandler<string?>? AddToProfileRequested;

    /// <summary>
    /// 卸载请求事件（由 Code-behind 订阅以显示对话框）
    /// </summary>
    public event EventHandler<string?>? UninstallRequested;

    public void Dispose()
    {
        _eventBus.Unsubscribe<PluginListChangedEvent>(OnPluginListChanged);
        _eventBus.Unsubscribe<ProfileListChangedEvent>(OnProfileListChanged);
    }
}
}
