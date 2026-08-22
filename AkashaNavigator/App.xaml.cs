using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using AkashaNavigator.Models.Config;
using AkashaNavigator.Services;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Core;
using AkashaNavigator.Helpers;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace AkashaNavigator
{
/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private const int CurrentFolderAclPolicyRevision = 1;

    #region Fields

    private Bootstrapper? _bootstrapper;
    private AppConfig _config = null!;
    private DataMigration? _dataMigration;
    private IConfigService? _configService;

    private static readonly LoggingLevelSwitch _logLevelSwitch = new(LogEventLevel.Information);

    private HotkeyManager? _hotkeyManager;
    private OsdManager? _osdManager;
    private ShutdownCoordinator? _shutdownCoordinator;
    private Views.Windows.PlayerWindow? _playerWindow;
    private Views.Windows.ControlBarWindow? _controlBarWindow;

    #endregion

    #region Properties

    /// <summary>
    /// 全局服务提供者，用于在需要时获取DI容器中的服务
    /// </summary>
    public static IServiceProvider Services { get; private set; } = null!;

    #endregion

    #region Event Handlers

    /// <summary>
    /// 应用启动事件
    /// </summary>
    private void Application_Startup(object sender, StartupEventArgs e)
    {
        ConfigureSerilog();

        InitializeServices();

        ModifyFolderSecurityIfVersionChanged();

        CheckAndHandleCrash();

        ExecuteDataMigration();

        StartBackgroundUpdates();

        InitializeApplication();
    }

    private void ModifyFolderSecurityIfVersionChanged()
    {
        try
        {
            var currentVersion = GetCurrentAppVersion();
            var appDirectory = AppContext.BaseDirectory;

            var isSameVersion = string.Equals(_config.RunForVersion, currentVersion, StringComparison.OrdinalIgnoreCase);
            var requiresPolicyMigration = _config.FolderAclPolicyRevision < CurrentFolderAclPolicyRevision;

            if (isSameVersion && !requiresPolicyMigration)
            {
                Log.Information("[{Source}] Skip folder ACL update: already applied for version {Version}", nameof(App),
                                currentVersion);
                return;
            }

            if (appDirectory.StartsWith(@"C:\", StringComparison.OrdinalIgnoreCase))
            {
                if (requiresPolicyMigration)
                {
                    var hasExpectedAcl = SecurityControlHelper.HasExpectedFolderSecurity(appDirectory);
                    if (hasExpectedAcl)
                    {
                        _config.RunForVersion = currentVersion;
                        _config.FolderAclPolicyRevision = CurrentFolderAclPolicyRevision;
                        _configService?.Save();
                        Log.Information("[{Source}] Folder ACL check passed during policy migration for version {Version}",
                                        nameof(App), currentVersion);
                        return;
                    }
                }

                var success = SecurityControlHelper.AllowFullFolderSecurity(appDirectory);
                if (!success)
                {
                    Log.Warning("[{Source}] Folder ACL update did not apply for {AppDirectory}", nameof(App),
                                appDirectory);
                    return;
                }

                _config.RunForVersion = currentVersion;
                _config.FolderAclPolicyRevision = CurrentFolderAclPolicyRevision;
                _configService?.Save();
                Log.Information("[{Source}] Folder ACL update applied for version {Version}", nameof(App),
                                currentVersion);
            }
            else
            {
                _config.RunForVersion = currentVersion;
                _config.FolderAclPolicyRevision = CurrentFolderAclPolicyRevision;
                _configService?.Save();
                Log.Information("[{Source}] Skip folder ACL update: app directory is not on C drive ({AppDirectory})",
                                nameof(App), appDirectory);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[{Source}] Unexpected exception while modifying folder ACL", nameof(App));
        }
    }

    private static string GetCurrentAppVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var infoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(infoVersion))
        {
            var plusIndex = infoVersion.IndexOf('+');
            return plusIndex > 0 ? infoVersion[..plusIndex] : infoVersion;
        }

        var version = assembly.GetName().Version;
        return version == null ? "0.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
    }

    /// <summary>
    /// 配置 Serilog 日志系统
    /// </summary>
    private void ConfigureSerilog()
    {
        var logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
        var logFile = Path.Combine(logDirectory, "akasha-navigator-.log");

        Log.Logger =
            new LoggerConfiguration()
                .MinimumLevel.ControlledBy(_logLevelSwitch)
                .WriteTo
                .File(logFile,
                      outputTemplate: ("[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level:u3}] " +
                                       "[{SourceContext}]{NewLine}{Message}{NewLine}{Exception}{NewLine}"),
                      rollingInterval: RollingInterval.Day, retainedFileCountLimit: 31,
                      retainedFileTimeLimit: TimeSpan.FromDays(21))
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

        Log.Information("Serilog 日志系统已初始化");
    }

    /// <summary>
    /// 初始化 DI 容器和服务
    /// </summary>
    private void InitializeServices()
    {
        _bootstrapper = new Bootstrapper();
        var serviceProvider = _bootstrapper.GetServiceProvider();

        Services = serviceProvider;

        _configService = serviceProvider.GetRequiredService<IConfigService>();
        _dataMigration = serviceProvider.GetRequiredService<DataMigration>();
        _ = serviceProvider.GetRequiredService<PluginStateCoordinator>();
        _shutdownCoordinator = serviceProvider.GetRequiredService<ShutdownCoordinator>();

        _config = _configService.Config;
    }

    private static void StartBackgroundUpdates()
    {
        var updateManifestService = Services.GetRequiredService<IUpdateManifestService>();
        var pluginRepositoryService =
            Services.GetRequiredService<IPluginRepositoryService>();
        var pluginSubscriptionService =
            Services.GetRequiredService<IPluginSubscriptionService>();
        var pluginInstaller =
            Services.GetRequiredService<IPluginInstaller>();
        var pluginResourceUpdateService =
            Services.GetRequiredService<IPluginResourceUpdateService>();
        var logService = Services.GetRequiredService<ILogService>();
        _ = RefreshUpdateManifestInBackgroundAsync(
            updateManifestService,
            logService);
        _ = RefreshPluginRepositoryInBackgroundAsync(
            pluginRepositoryService,
            pluginSubscriptionService,
            pluginInstaller,
            pluginResourceUpdateService,
            logService);
    }

    private static async Task RefreshPluginRepositoryInBackgroundAsync(
        IPluginRepositoryService pluginRepositoryService,
        IPluginSubscriptionService pluginSubscriptionService,
        IPluginInstaller pluginInstaller,
        IPluginResourceUpdateService pluginResourceUpdateService,
        ILogService logService)
    {
        var settings = pluginRepositoryService.Settings;
        if (!settings.AutoUpdateRepository &&
            !settings.AutoUpdateSubscribedPlugins &&
            !settings.AutoUpdatePluginResources)
        {
            return;
        }

        var result =
            await pluginSubscriptionService.CheckForUpdatesAsync(
                settings.AutoUpdateRepository || settings.AutoUpdatePluginResources);
        if (result.IsFailure)
        {
            logService.Warn(
                nameof(App),
                "后台更新插件仓库失败: {ErrorMessage}",
                result.Error?.Message ?? "未知错误");
        }
        if (result.IsSuccess && settings.AutoUpdateSubscribedPlugins)
        {
            foreach (var update in result.Value!
                         .Where(item => item.AutoUpdate))
            {
                var updateResult =
                    await pluginInstaller.InstallOrUpdateRepositoryPluginAsync(
                        update.PluginId);
                if (updateResult.IsFailure)
                {
                    logService.Warn(
                        nameof(App),
                        "自动更新插件 {PluginId} 失败: {ErrorMessage}",
                        update.PluginId,
                        updateResult.Error?.Message ?? "未知错误");
                }
            }
        }

        if (!settings.AutoUpdatePluginResources)
        {
            return;
        }

        var resourceResult =
            await pluginResourceUpdateService.UpdateSubscribedResourcesAsync(
                refreshRepository: false);
        if (resourceResult.IsFailure)
        {
            logService.Warn(
                nameof(App),
                "后台更新插件资源失败: {ErrorMessage}",
                resourceResult.Error?.Message ?? "未知错误");
            return;
        }

        foreach (var failed in resourceResult.Value!.Where(item => !item.Succeeded))
        {
            logService.Warn(
                nameof(App),
                "插件资源更新失败 ({PluginId}/{ResourceId}): {ErrorMessage}",
                failed.PluginId,
                failed.ResourceId,
                failed.ErrorMessage ?? "未知错误");
        }
    }

    private static async Task RefreshUpdateManifestInBackgroundAsync(
        IUpdateManifestService updateManifestService,
        ILogService logService)
    {
        var result = await updateManifestService.RefreshAsync();
        if (result.IsFailure)
        {
            logService.Warn(
                nameof(App),
                "后台刷新更新清单失败，远程更新功能暂不可用: {ErrorMessage}",
                result.Error?.Message ?? "未知错误");
        }
    }

    /// <summary>
    /// 执行数据迁移
    /// </summary>
    private void ExecuteDataMigration()
    {
        try
        {
            var logService = Services.GetRequiredService<ILogService>();

            if (_dataMigration == null || logService == null)
                return;

            if (!_dataMigration.NeedsMigration())
                return;

            logService.Info(nameof(App), "检测到需要数据迁移，开始执行...");

            var result = _dataMigration.Migrate();
            if (result.Status is MigrationResultStatus.Success or
                MigrationResultStatus.PartialSuccess)
            {
                Services.GetRequiredService<IPluginLibrary>().ReloadIndex();
            }

            switch (result.Status)
            {
                case MigrationResultStatus.Success:
                    logService.Info(
                        nameof(App), "数据迁移成功: {MigratedPluginCount} 个插件, {MigratedProfileCount} 个 Profile",
                        result.MigratedPluginCount, result.MigratedProfileCount);
                    break;

                case MigrationResultStatus.PartialSuccess:
                    logService.Warn(
                        nameof(App), "数据迁移部分成功: {MigratedPluginCount} 个插件, {MigratedProfileCount} 个 Profile",
                        result.MigratedPluginCount, result.MigratedProfileCount);
                    foreach (var warning in result.Warnings)
                    {
                        logService.Warn(nameof(App), "迁移警告: {Warning}", warning);
                    }
                    break;

                case MigrationResultStatus.Failed:
                    logService.Error(nameof(App), "数据迁移失败: {ErrorMessage}", result.ErrorMessage);
                    MessageBox.Show($"数据迁移失败：{result.ErrorMessage}\n\n应用将继续运行，但部分插件可能无法正常工作。",
                                    "迁移警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                    break;

                case MigrationResultStatus.NotNeeded:
                    break;
            }
        }
        catch (Exception ex)
        {
            var logService = Services.GetRequiredService<ILogService>();
            logService?.Error(nameof(App), ex, "数据迁移过程中发生异常");
        }
    }

    /// <summary>
    /// 检查并处理崩溃恢复
    /// </summary>
    private void CheckAndHandleCrash()
    {
        try
        {
            var crashRecoveryService = Services.GetRequiredService<ICrashRecoveryService>();
            var logService = Services.GetRequiredService<ILogService>();

            // 检测是否有崩溃
            if (crashRecoveryService.DetectCrash())
            {
                logService.Warn(nameof(App), "检测到上次程序异常退出");

                // 询问用户是否清理 WebView2 数据
                var result = MessageBox.Show(
                    "检测到程序上次异常退出。\n\n" +
                    "如果您遇到了程序卡死或无响应的问题，建议清理浏览器缓存数据。\n\n" +
                    "是否清理浏览器缓存？（不会影响您的配置和数据）",
                    "崩溃恢复",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    logService.Info(nameof(App), "用户选择清理 WebView2 数据");
                    var cleanResult = crashRecoveryService.CleanWebView2Data();

                    if (cleanResult.IsSuccess)
                    {
                        logService.Info(nameof(App), "WebView2 数据清理成功");
                        MessageBox.Show(
                            "浏览器缓存已清理完成。",
                            "清理成功",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    else
                    {
                        var errorMsg = cleanResult.Error?.Message ?? "未知错误";
                        logService.Error(nameof(App), "WebView2 数据清理失败: {ErrorMessage}", errorMsg);
                        MessageBox.Show(
                            $"清理失败：{errorMsg}\n\n您可以手动删除以下目录：\n{crashRecoveryService.WebView2DataPath}",
                            "清理失败",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
                else
                {
                    logService.Info(nameof(App), "用户选择不清理 WebView2 数据");
                }
            }

            // 标记程序正常启动
            crashRecoveryService.MarkStartup();
        }
        catch (Exception ex)
        {
            var logService = Services.GetRequiredService<ILogService>();
            logService?.Error(nameof(App), ex, "崩溃检测过程中发生异常");
        }
    }

    /// <summary>
    /// 初始化应用程序（窗口、快捷键、欢迎对话框等）
    /// </summary>
    private void InitializeApplication()
    {
        var serviceProvider = Services;

        UpdateLogLevel();

        ShowWelcomeDialogIfNeeded();

        SubscribeToConfigChanges();

        _bootstrapper!.Run();

        var playerWindow = serviceProvider.GetRequiredService<Views.Windows.PlayerWindow>();

        InitializeManagers(playerWindow);

        SetupPluginUpdateCheck(playerWindow);
    }

    /// <summary>
    /// 根据配置更新日志级别
    /// </summary>
    private void UpdateLogLevel()
    {
        var newLevel = _config.EnableDebugLog ? LogEventLevel.Debug : LogEventLevel.Information;
        if (_logLevelSwitch.MinimumLevel != newLevel)
        {
            _logLevelSwitch.MinimumLevel = newLevel;
            Log.Information("日志级别已切换为 {Level}", newLevel);
        }
    }

    /// <summary>
    /// 首次启动显示欢迎弹窗
    /// </summary>
    private void ShowWelcomeDialogIfNeeded()
    {
        if (_config.IsFirstLaunch)
        {
            var dialogFactory = Services.GetRequiredService<IDialogFactory>();
            var welcomeDialog = dialogFactory.CreateWelcomeDialog();
            welcomeDialog.ShowDialog();

            _config.IsFirstLaunch = false;
            _configService!.Save();
        }
    }

    /// <summary>
    /// 订阅配置变更事件
    /// </summary>
    private void SubscribeToConfigChanges()
    {
        _configService!.ConfigChanged += (s, config) =>
        {
            _config = config;
            ApplySettings();
        };
    }

    /// <summary>
    /// 初始化管理器（快捷键、OSD）
    /// </summary>
    private void InitializeManagers(Views.Windows.PlayerWindow playerWindow)
    {
        _playerWindow = playerWindow;
        _controlBarWindow = Services.GetRequiredService<Views.Windows.ControlBarWindow>();

        // 从 DI 容器获取 OsdManager
        _osdManager = Services.GetRequiredService<OsdManager>();

_hotkeyManager = Services.GetRequiredService<HotkeyManager>();
        _hotkeyManager.Initialize(playerWindow, _config,
            (message, icon) =>
            {
                if (_config.EnableOsd)
                {
                    _osdManager.ShowMessage(message, icon);
                }
            });

        ConfigureShutdownStages();
    }

    private void ConfigureShutdownStages()
    {
        var shutdownCoordinator = _shutdownCoordinator!;
        var pluginHost = Services.GetRequiredService<IPluginHost>();
        var companionProcessManager = Services.GetRequiredService<ICompanionProcessManager>();
        var overlayManager = Services.GetRequiredService<IOverlayManager>();
        var eventBus = Services.GetRequiredService<Core.Events.IEventBus>();
        var crashRecoveryService = Services.GetRequiredService<ICrashRecoveryService>();

        shutdownCoordinator.RegisterStage(
            nameof(HotkeyManager), 100, () => _hotkeyManager?.Dispose());
        shutdownCoordinator.RegisterStage(
            nameof(Views.Windows.ControlBarWindow), 200,
            () =>
            {
                _controlBarWindow?.StopAutoShowHide();
                if (_controlBarWindow?.IsLoaded == true)
                {
                    _controlBarWindow.Close();
                }
            });
        shutdownCoordinator.RegisterStage(
            nameof(IPluginHost.UnloadAllPlugins), 300, pluginHost.UnloadAllPlugins);
        shutdownCoordinator.RegisterStage(
            nameof(ICompanionProcessManager.StopAllAsync), 350,
            () => companionProcessManager.StopAllAsync().GetAwaiter().GetResult());
        shutdownCoordinator.RegisterStage(
            nameof(IOverlayManager.DestroyAllOverlays), 400, overlayManager.DestroyAllOverlays);
        shutdownCoordinator.RegisterStage(
            nameof(CloseAuxiliaryWindows), 500, CloseAuxiliaryWindows);
        shutdownCoordinator.RegisterStage(
            nameof(Core.Events.IEventBus.Clear), 700, eventBus.Clear);
        shutdownCoordinator.RegisterStage(
            nameof(ICrashRecoveryService.MarkShutdown), 800, crashRecoveryService.MarkShutdown);
    }

    private void CloseAuxiliaryWindows()
    {
        var windows = new List<Window>();
        foreach (Window window in Windows)
        {
            windows.Add(window);
        }

        foreach (var window in windows)
        {
            if (ReferenceEquals(window, _playerWindow) || ReferenceEquals(window, _controlBarWindow))
            {
                continue;
            }

            window.Close();
        }
    }

    /// <summary>
    /// 设置插件更新检查
    /// </summary>
    private void SetupPluginUpdateCheck(Views.Windows.PlayerWindow playerWindow)
    {
        var pluginSubscriptionService =
            Services.GetRequiredService<IPluginSubscriptionService>();
        var pluginInstaller = Services.GetRequiredService<IPluginInstaller>();
        var profileMarketplaceService = Services.GetRequiredService<ProfileMarketplaceService>();
        var notificationService = Services.GetRequiredService<INotificationService>();
        var eventBus = Services.GetRequiredService<Core.Events.IEventBus>();

        var checker = new PluginUpdateChecker(
            pluginSubscriptionService,
            pluginInstaller,
            profileMarketplaceService,
            notificationService,
            Services,
            eventBus,
            playerWindow,
            _config);
        checker.SetupUpdateCheck();
    }

    /// <summary>
    /// 应用设置变更
    /// </summary>
    private void ApplySettings()
    {
        UpdateLogLevel();

        var playerWindow = Services.GetRequiredService<Views.Windows.PlayerWindow>();
        playerWindow.UpdateConfig(_config);

        _hotkeyManager?.UpdateConfig(_config);
    }

    /// <summary>
    /// 应用退出事件
    /// </summary>
    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _shutdownCoordinator?.Shutdown();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "统一关停时发生异常");
        }

        try
        {
            _bootstrapper?.Dispose();
            Services = null!;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "释放根 ServiceProvider 时发生异常");
        }

        Log.CloseAndFlush();

        base.OnExit(e);
    }

    #endregion
}
}
