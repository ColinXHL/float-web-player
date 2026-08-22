using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AkashaNavigator.Core.Events;
using AkashaNavigator.Core.Events.Events;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Config;
using AkashaNavigator.Models.Common;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Models.PluginRepository;
using AkashaNavigator.Models.Update;
using AkashaNavigator.Helpers;
using AkashaNavigator.Services;

namespace AkashaNavigator.ViewModels.Pages
{
/// <summary>
/// 可用插件页面的 ViewModel
/// 使用 CommunityToolkit.Mvvm 源生成器
/// </summary>
public partial class AvailablePluginsPageViewModel : ObservableObject
{
    private readonly IPluginLibrary _pluginLibrary;
    private readonly INotificationService _notificationService;
    private readonly IEventBus _eventBus;
    private readonly IPluginRepositoryService _pluginRepositoryService;
    private readonly IPluginSubscriptionService _pluginSubscriptionService;
    private readonly IPluginInstaller _pluginInstaller;
    private readonly IPluginAcquisitionService _pluginAcquisitionService;
    private readonly IConfigService _configService;
    private readonly Dictionary<string, CancellationTokenSource> _installations =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AvailablePluginItemModel> _installationItems =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 插件列表
    /// </summary>
    public ObservableCollection<AvailablePluginItemModel> Plugins { get; } = new();

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
    private PluginRepositoryChannel _selectedRepositoryChannel;

    [ObservableProperty]
    private string _customRepositoryUrl = string.Empty;

    [ObservableProperty]
    private bool _autoUpdateRepository;

    [ObservableProperty]
    private bool _autoUpdateSubscribedPlugins;

    [ObservableProperty]
    private bool _autoUpdatePluginResources;

    [ObservableProperty]
    private bool _isRepositoryBusy;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InstallCommand))]
    [NotifyCanExecuteChangedFor(nameof(UpdateAllSubscribedCommand))]
    private bool _isPluginInstallBusy;

    [ObservableProperty]
    private string _repositoryStatusText = "尚未加载插件仓库";

    public bool IsCustomRepositoryChannel =>
        SelectedRepositoryChannel == PluginRepositoryChannel.Custom;

    /// <summary>
    /// 构造函数
    /// </summary>
    public AvailablePluginsPageViewModel(
        IPluginLibrary pluginLibrary,
        INotificationService notificationService,
        IEventBus eventBus,
        IPluginRepositoryService pluginRepositoryService,
        IPluginSubscriptionService pluginSubscriptionService,
        IPluginInstaller pluginInstaller,
        IPluginAcquisitionService pluginAcquisitionService,
        IConfigService configService)
    {
        _pluginLibrary = pluginLibrary ?? throw new ArgumentNullException(nameof(pluginLibrary));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _pluginRepositoryService =
            pluginRepositoryService ?? throw new ArgumentNullException(nameof(pluginRepositoryService));
        _pluginSubscriptionService =
            pluginSubscriptionService ?? throw new ArgumentNullException(nameof(pluginSubscriptionService));
        _pluginInstaller =
            pluginInstaller ?? throw new ArgumentNullException(nameof(pluginInstaller));
        _pluginAcquisitionService =
            pluginAcquisitionService ?? throw new ArgumentNullException(nameof(pluginAcquisitionService));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));

        LoadRepositorySettings();
        UpdateRepositoryStatus(_pluginRepositoryService.Current);

        // 订阅插件列表变化事件
        _eventBus.Subscribe<PluginListChangedEvent>(OnPluginListChanged);
    }

    /// <summary>
    /// 插件列表变化事件处理
    /// </summary>
    private void OnPluginListChanged(PluginListChangedEvent e)
    {
        RefreshPluginList();
    }

    /// <summary>
    /// 页面加载时刷新插件列表
    /// </summary>
    [RelayCommand]
    public async Task OnLoadedAsync()
    {
        RefreshPluginList();

        var initializeResult = await _pluginRepositoryService.InitializeAsync();
        if (initializeResult.IsSuccess)
        {
            ReconcileSubscriptions(initializeResult.Value!);
            UpdateRepositoryStatus(initializeResult.Value);
            RefreshPluginList();
            return;
        }

        RepositoryStatusText = "插件仓库不可用";
        _notificationService.Show(
            $"加载插件仓库失败: {initializeResult.Error?.Message}",
            NotificationType.Error);
    }

    /// <summary>
    /// 搜索文本变化时重新加载（自动生成的方法）
    /// </summary>
    partial void OnSearchTextChanged(string value)
    {
        RefreshPluginList();
    }

    /// <summary>
    /// 刷新插件列表
    /// </summary>
    public void RefreshPluginList()
    {
        var allPlugins = GetAllCatalogPlugins();
        var searchText = SearchText?.ToLower() ?? "";

        // 过滤搜索
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            allPlugins = allPlugins
                             .Where(p => p.Name.ToLower().Contains(searchText) ||
                                         (p.Description?.ToLower().Contains(searchText) ?? false))
                             .ToList();
        }

        Plugins.Clear();
        foreach (var plugin in allPlugins)
        {
            Plugins.Add(
                _installationItems.TryGetValue(plugin.Id, out var activeInstallation)
                    ? activeInstallation
                    : plugin);
        }

        PluginCountText = $"共 {allPlugins.Count} 个插件";
        IsEmpty = allPlugins.Count == 0;
    }

    /// <summary>
    /// 获取所有内置插件列表（包括已安装和未安装）
    /// </summary>
    private List<AvailablePluginItemModel> GetAllCatalogPlugins()
    {
        var installed = _pluginLibrary.GetInstalledPlugins()
            .ToDictionary(plugin => plugin.Id, StringComparer.OrdinalIgnoreCase);
        var snapshot = _pluginRepositoryService.Current;
        if (snapshot == null)
        {
            return new List<AvailablePluginItemModel>();
        }

        var items = snapshot.Index.Plugins
            .Select(
                entry => CreateItem(
                    CreateCatalogEntry(entry),
                    installed.GetValueOrDefault(entry.Id)))
            .ToList();
        var catalogIds = snapshot.Index.Plugins
            .Select(entry => entry.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        items.AddRange(
            _pluginSubscriptionService
                .GetSubscriptions()
                .Where(
                    subscription =>
                        !subscription.IsAvailable &&
                        !catalogIds.Contains(subscription.PluginId))
                .Select(
                    subscription => CreateRemovedItem(
                        subscription,
                        installed.GetValueOrDefault(subscription.PluginId))));
        return items
            .OrderByDescending(item => item.HasUpdate)
            .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    [RelayCommand]
    private void SaveRepositorySettings()
    {
        var result = TrySaveRepositorySettings();
        _notificationService.Show(
            result.IsSuccess
                ? "插件仓库设置已保存"
                : $"保存插件仓库设置失败: {result.Error?.Message}",
            result.IsSuccess ? NotificationType.Success : NotificationType.Error);
    }

    [RelayCommand]
    private async Task UpdateRepositoryAsync()
    {
        if (!CanStartRepositoryOperation())
        {
            return;
        }

        IsRepositoryBusy = true;
        RepositoryStatusText = "正在更新插件仓库…";
        try
        {
            var result = await _pluginRepositoryService.RefreshAsync();
            HandleRepositoryUpdateResult(result, "插件仓库已更新");
        }
        finally
        {
            IsRepositoryBusy = false;
        }
    }

    [RelayCommand]
    private async Task ResetRepositoryAsync()
    {
        if (!CanStartRepositoryOperation())
        {
            return;
        }

        IsRepositoryBusy = true;
        RepositoryStatusText = "正在重置插件仓库…";
        try
        {
            var result = await _pluginRepositoryService.ResetAsync();
            HandleRepositoryUpdateResult(result, "插件仓库已重置");
        }
        finally
        {
            IsRepositoryBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanStartPluginInstall))]
    private async Task UpdateAllSubscribedAsync()
    {
        var updates = _pluginSubscriptionService.GetAvailableUpdates();
        if (updates.Count == 0)
        {
            _notificationService.Show(
                "已订阅插件均为最新版本",
                NotificationType.Info);
            return;
        }

        IsPluginInstallBusy = true;
        var succeeded = 0;
        var failures = new List<string>();
        try
        {
            for (var index = 0; index < updates.Count; index++)
            {
                var update = updates[index];
                var itemNumber = index + 1;
                var plugin = Plugins.FirstOrDefault(
                    item => string.Equals(
                        item.Id,
                        update.PluginId,
                        StringComparison.OrdinalIgnoreCase));
                using var cancellation = new CancellationTokenSource();
                if (plugin != null)
                {
                    _installations[plugin.Id] = cancellation;
                    _installationItems[plugin.Id] = plugin;
                    plugin.IsInstalling = true;
                    plugin.InstallProgress = 0;
                    plugin.IsInstallProgressIndeterminate = true;
                    plugin.InstallStatus =
                        $"正在更新订阅插件（{itemNumber}/{updates.Count}）…";
                }

                try
                {
                    var progress = plugin == null
                        ? null
                        : new Progress<PluginDownloadProgress>(
                            value =>
                            {
                                plugin.SelectedSourceText =
                                    $"下载源：{GetSourceDisplayName(value.SourceId)}";
                                plugin.InstallProgress =
                                    Math.Clamp(value.Percentage, 0, 100);
                                plugin.IsInstallProgressIndeterminate =
                                    value.TotalBytes <= 0 || value.Percentage >= 100;
                                plugin.InstallStatus = value.TotalBytes <= 0
                                    ? $"正在更新订阅插件（{itemNumber}/{updates.Count}），正在下载…"
                                    : value.Percentage >= 100
                                        ? $"正在更新订阅插件（{itemNumber}/{updates.Count}），下载完成，正在安装…"
                                        : $"正在更新订阅插件（{itemNumber}/{updates.Count}）：{FormatBytes(value.BytesReceived)} / {FormatBytes(value.TotalBytes)}";
                            });
                    var result =
                        await _pluginAcquisitionService.InstallOrUpdateAsync(
                            update.PluginId,
                            progress,
                            cancellation.Token);
                    if (result.IsSuccess)
                    {
                        succeeded++;
                    }
                    else
                    {
                        failures.Add(
                            $"{update.PluginId}: {result.Error?.Message ?? "未知错误"}");
                    }
                }
                finally
                {
                    if (plugin != null)
                    {
                        plugin.IsInstalling = false;
                        plugin.InstallStatus = string.Empty;
                        plugin.IsInstallProgressIndeterminate = false;
                        _installations.Remove(plugin.Id);
                        _installationItems.Remove(plugin.Id);
                    }
                }
            }
        }
        finally
        {
            IsPluginInstallBusy = false;
        }

        RefreshPluginList();
        RefreshRequested?.Invoke(this, EventArgs.Empty);
        if (failures.Count == 0)
        {
            _notificationService.Show(
                $"已更新 {succeeded} 个订阅插件",
                NotificationType.Success);
            return;
        }

        _notificationService.Show(
            $"已更新 {succeeded} 个插件，{failures.Count} 个失败：\n{string.Join("\n", failures)}",
            NotificationType.Warning);
    }

    /// <summary>
    /// 安装插件命令（自动生成 InstallCommand）
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanInstallPlugin))]
    private async Task InstallAsync(AvailablePluginItemModel? plugin)
    {
        if (plugin == null)
            return;

        if (FindRepositoryEntry(plugin.Id) == null)
        {
            _notificationService.Show(
                "该插件已从当前仓库中移除，无法安装或更新",
                NotificationType.Warning);
            return;
        }

        await InstallCatalogPluginAsync(plugin);
    }

    [RelayCommand]
    private void CancelInstall(AvailablePluginItemModel? plugin)
    {
        if (plugin != null && _installations.TryGetValue(plugin.Id, out var cancellation))
        {
            cancellation.Cancel();
        }
    }

    /// <summary>
    /// 从用户选择的 ZIP 插件包安装或更新插件
    /// </summary>
    public void InstallPackage(string archivePath)
    {
        var result = _pluginInstaller.InstallPackage(archivePath);
        if (result.IsSuccess)
        {
            _notificationService.Show(
                $"插件 \"{result.Value!.Name}\" 导入成功，可在“已安装插件”中管理。",
                NotificationType.Success);
            RefreshRequested?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            _notificationService.Show($"插件包导入失败: {result.Error?.Message}", NotificationType.Error);
        }
    }

    [RelayCommand]
    private void Subscribe(AvailablePluginItemModel? plugin)
    {
        if (plugin == null)
        {
            return;
        }

        var entry = FindRepositoryEntry(plugin.Id);
        if (entry == null)
        {
            _notificationService.Show(
                "插件已不在当前仓库中，无法订阅",
                NotificationType.Error);
            return;
        }

        var result = _pluginSubscriptionService.Subscribe(
            AppConstants.OfficialPluginRepositoryId,
            entry);
        if (result.IsSuccess)
        {
            plugin.IsSubscribed = true;
            _notificationService.Show(
                $"已订阅插件 \"{plugin.Name}\"",
                NotificationType.Success);
        }
        else
        {
            _notificationService.Show(
                $"订阅失败: {result.Error?.Message}",
                NotificationType.Error);
        }
    }

    [RelayCommand]
    private void Unsubscribe(AvailablePluginItemModel? plugin)
    {
        if (plugin == null)
        {
            return;
        }

        var result = _pluginSubscriptionService.Unsubscribe(plugin.Id);
        if (result.IsSuccess)
        {
            plugin.IsSubscribed = false;
            _notificationService.Show(
                $"已取消订阅插件 \"{plugin.Name}\"，已安装文件和配置均已保留",
                NotificationType.Success);
        }
        else
        {
            _notificationService.Show(
                $"取消订阅失败: {result.Error?.Message}",
                NotificationType.Error);
        }
    }

    /// <summary>
    /// 卸载请求命令（自动生成 UninstallCommand）
    /// 注意：此命令需要由 Code-behind 处理对话框显示
    /// </summary>
    [RelayCommand]
    private void Uninstall(AvailablePluginItemModel? plugin)
    {
        if (plugin == null)
            return;

        // 此命令由 Code-behind 的 UninstallRequested 事件处理
        UninstallRequested?.Invoke(this, plugin);
    }

    /// <summary>
    /// 刷新请求事件（由 Code-behind 订阅）
    /// </summary>
    public event EventHandler? RefreshRequested;

    /// <summary>
    /// 卸载请求事件（由 Code-behind 订阅以显示对话框）
    /// </summary>
    public event EventHandler<AvailablePluginItemModel>? UninstallRequested;

    public void Dispose()
    {
        foreach (var cancellation in _installations.Values)
        {
            cancellation.Cancel();
            cancellation.Dispose();
        }

        _installations.Clear();
        _installationItems.Clear();
        _eventBus.Unsubscribe<PluginListChangedEvent>(OnPluginListChanged);
    }

    private AvailablePluginItemModel CreateItem(
        PluginCatalogEntry entry,
        InstalledPluginInfo? installed)
    {
        var description = entry.Description ?? installed?.Description;
        var author = entry.Author ?? installed?.Author;
        return new AvailablePluginItemModel {
            Id = entry.Id,
            Name = entry.Name,
            Version = entry.Version,
            Description = description,
            Author = author,
            SourceDirectory = entry.LocalSourceDirectory ?? string.Empty,
            IsRemote = entry.IsRemote,
            DistributionType = entry.DistributionType,
            IsRepositoryAvailable = true,
            IsSubscribed = IsSubscribedToCurrentRepository(entry.Id),
            InstalledVersion = installed?.Version ?? string.Empty,
            InstalledVersionText = installed == null ? "未安装" : $"已安装: v{installed.Version}",
            PackageSizeText = entry.Package == null ? string.Empty : FormatBytes(entry.Package.Size),
            HasDescription = !string.IsNullOrWhiteSpace(description),
            HasAuthor = !string.IsNullOrWhiteSpace(author),
            IsInstalled = installed != null,
            HasUpdate = installed != null &&
                        PluginLibrary.CompareVersions(entry.Version, installed.Version) > 0,
            SelectedSourceText = entry.IsRemote
                ? $"下载源：{GetPreferenceDisplayName(_configService.Config.PluginDownloadSourcePreference)}"
                : string.Empty
        };
    }

    private static AvailablePluginItemModel CreateRemovedItem(
        PluginSubscriptionRecord subscription,
        InstalledPluginInfo? installed)
    {
        return new AvailablePluginItemModel {
            Id = subscription.PluginId,
            Name = installed?.Name ?? subscription.PluginId,
            Version = subscription.LastKnownVersion,
            Description = installed?.Description ?? "该插件已从仓库中移除",
            Author = installed?.Author,
            DistributionType = AppConstants.PluginDistributionRepository,
            IsRepositoryAvailable = false,
            IsSubscribed = true,
            InstalledVersion = installed?.Version ?? string.Empty,
            InstalledVersionText = installed == null
                ? "未安装"
                : $"已安装: v{installed.Version}",
            HasDescription = true,
            HasAuthor = !string.IsNullOrWhiteSpace(installed?.Author),
            IsInstalled = installed != null,
            HasUpdate = false
        };
    }

    partial void OnSelectedRepositoryChannelChanged(PluginRepositoryChannel value)
    {
        OnPropertyChanged(nameof(IsCustomRepositoryChannel));
    }

    private void LoadRepositorySettings()
    {
        var settings = _pluginRepositoryService.Settings;
        SelectedRepositoryChannel = settings.SelectedChannel;
        CustomRepositoryUrl = settings.CustomUrl;
        AutoUpdateRepository = settings.AutoUpdateRepository;
        AutoUpdateSubscribedPlugins = settings.AutoUpdateSubscribedPlugins;
        AutoUpdatePluginResources = settings.AutoUpdatePluginResources;
    }

    private Result TrySaveRepositorySettings()
    {
        return _pluginRepositoryService.SaveSettings(
            new PluginRepositorySettings {
                SelectedChannel = SelectedRepositoryChannel,
                CustomUrl = CustomRepositoryUrl.Trim(),
                Branch = AppConstants.OfficialPluginRepositoryBranch,
                AutoUpdateRepository = AutoUpdateRepository,
                AutoUpdateSubscribedPlugins = AutoUpdateSubscribedPlugins,
                AutoUpdatePluginResources = AutoUpdatePluginResources
            });
    }

    private bool CanStartRepositoryOperation()
    {
        if (IsRepositoryBusy)
        {
            return false;
        }

        var saveResult = TrySaveRepositorySettings();
        if (saveResult.IsSuccess)
        {
            return true;
        }

        _notificationService.Show(
            $"插件仓库设置无效: {saveResult.Error?.Message}",
            NotificationType.Error);
        return false;
    }

    private void HandleRepositoryUpdateResult(
        Result<PluginRepositorySnapshot> result,
        string successMessage)
    {
        if (result.IsFailure)
        {
            RepositoryStatusText = "插件仓库更新失败";
            _notificationService.Show(
                $"{RepositoryStatusText}: {result.Error?.Message}",
                NotificationType.Error);
            return;
        }

        UpdateRepositoryStatus(result.Value);
        ReconcileSubscriptions(result.Value!);
        RefreshPluginList();
        _notificationService.Show(
            result.Value!.UsedCache
                ? "仓库更新失败，已继续使用本地缓存"
                : successMessage,
            result.Value.UsedCache ? NotificationType.Warning : NotificationType.Success);
    }

    private void UpdateRepositoryStatus(PluginRepositorySnapshot? snapshot)
    {
        if (snapshot == null)
        {
            RepositoryStatusText = "尚未加载插件仓库";
            return;
        }

        var shortCommit = snapshot.CatalogCommit.Length > 8
            ? snapshot.CatalogCommit[..8]
            : snapshot.CatalogCommit;
        RepositoryStatusText =
            $"{(snapshot.UsedCache ? "本地缓存" : "最新仓库")} · {snapshot.Index.Plugins.Count} 个插件 · {shortCommit}";
    }

    private PluginCatalogEntry CreateCatalogEntry(PluginRepositoryEntry entry)
    {
        var isRelease =
            entry.DistributionType == AppConstants.PluginDistributionRelease;
        return new PluginCatalogEntry {
            Id = entry.Id,
            Name = entry.Name,
            Version = entry.Version,
            Description = entry.Description,
            MinHostVersion = entry.MinHostVersion,
            DistributionType = entry.DistributionType,
            LocalSourceDirectory = isRelease
                ? null
                : Path.Combine(_pluginRepositoryService.RepositoryDirectory, entry.Path),
            IsRemote = isRelease
        };
    }

    private async Task InstallCatalogPluginAsync(
        AvailablePluginItemModel plugin)
    {
        if (_installations.ContainsKey(plugin.Id) || IsPluginInstallBusy)
        {
            return;
        }

        var cancellation = new CancellationTokenSource();
        _installations[plugin.Id] = cancellation;
        _installationItems[plugin.Id] = plugin;
        IsPluginInstallBusy = true;
        var wasInstalled = plugin.IsInstalled;
        var isRelease =
            plugin.DistributionType == AppConstants.PluginDistributionRelease;
        plugin.IsInstalling = true;
        plugin.InstallProgress = 0;
        plugin.IsInstallProgressIndeterminate = true;
        plugin.InstallStatus = isRelease ? "正在准备下载…" : "正在安装插件，请稍候…";
        var progress = new Progress<PluginDownloadProgress>(
            value =>
            {
                plugin.SelectedSourceText =
                    $"下载源：{GetSourceDisplayName(value.SourceId)}";
                plugin.InstallProgress =
                    Math.Clamp(value.Percentage, 0, 100);
                plugin.IsInstallProgressIndeterminate =
                    value.TotalBytes <= 0 || value.Percentage >= 100;
                plugin.InstallStatus = value.TotalBytes <= 0
                    ? "正在下载插件…"
                    : value.Percentage >= 100
                        ? "下载完成，正在安装…"
                        : $"正在下载：{FormatBytes(value.BytesReceived)} / {FormatBytes(value.TotalBytes)}";
            });

        try
        {
            var result =
                await _pluginAcquisitionService.InstallOrUpdateAsync(
                    plugin.Id,
                    isRelease ? progress : null,
                    cancellation.Token);
            if (result.IsFailure)
            {
                if (result.Error?.Code ==
                    PluginErrorCodes.RemoteDownloadCanceled)
                {
                    _notificationService.Show(
                        "插件安装已取消",
                        NotificationType.Info);
                }
                else
                {
                    _notificationService.Show(
                        $"仓库插件安装失败: {result.Error?.Message}",
                        NotificationType.Error);
                }

                return;
            }

            plugin.IsInstalled = true;
            plugin.IsSubscribed = true;
            plugin.InstalledVersion = result.Value!.Version;
            plugin.InstalledVersionText =
                $"已安装: v{result.Value.Version}";
            plugin.HasUpdate = false;
            _notificationService.Show(
                $"插件 \"{plugin.Name}\" {(wasInstalled ? "更新" : "安装")}成功！",
                NotificationType.Success);
            RefreshRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException)
        {
            _notificationService.Show(
                "插件安装已取消",
                NotificationType.Info);
        }
        finally
        {
            plugin.IsInstalling = false;
            plugin.InstallStatus = string.Empty;
            plugin.IsInstallProgressIndeterminate = false;
            _installations.Remove(plugin.Id);
            _installationItems.Remove(plugin.Id);
            IsPluginInstallBusy = false;
            cancellation.Dispose();
        }
    }

    private bool CanInstallPlugin(AvailablePluginItemModel? plugin)
    {
        return plugin != null &&
               plugin.IsRepositoryAvailable &&
               !IsPluginInstallBusy;
    }

    private bool CanStartPluginInstall() => !IsPluginInstallBusy;

    private PluginRepositoryEntry? FindRepositoryEntry(string pluginId)
    {
        return _pluginRepositoryService.Current?.Index.Plugins.FirstOrDefault(
            entry => string.Equals(
                entry.Id,
                pluginId,
                StringComparison.OrdinalIgnoreCase));
    }

    private bool IsSubscribedToCurrentRepository(string pluginId)
    {
        var subscription = _pluginSubscriptionService.GetSubscription(pluginId);
        return subscription != null &&
               string.Equals(
                   subscription.RepositoryId,
                   AppConstants.OfficialPluginRepositoryId,
                   StringComparison.Ordinal);
    }

    private void ReconcileSubscriptions(PluginRepositorySnapshot snapshot)
    {
        var result = _pluginSubscriptionService.Reconcile(
            AppConstants.OfficialPluginRepositoryId,
            snapshot);
        if (result.IsFailure)
        {
            _notificationService.Show(
                $"同步插件订阅状态失败: {result.Error?.Message}",
                NotificationType.Warning);
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0)
        {
            return "0 B";
        }

        var mebibytes = bytes / 1024d / 1024d;
        return mebibytes >= 1
            ? $"{mebibytes:0.##} MiB"
            : $"{bytes / 1024d:0.##} KiB";
    }

    private static string GetPreferenceDisplayName(PluginDownloadSourcePreference preference)
    {
        return preference switch {
            PluginDownloadSourcePreference.GitHub => "GitHub",
            PluginDownloadSourcePreference.Cnb => "CNB",
            _ => "自动选择"
        };
    }

    private static string GetSourceDisplayName(string sourceId)
    {
        return string.Equals(sourceId, "github", StringComparison.OrdinalIgnoreCase)
            ? "GitHub"
            : string.Equals(sourceId, "cnb", StringComparison.OrdinalIgnoreCase)
                ? "CNB"
                : sourceId;
    }
}
}
